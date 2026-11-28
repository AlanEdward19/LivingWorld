using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Emergence;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Escolhe a ação do tick de cada NPC vivo: rotina diária por
/// padrão, utility AI só quando alguma necessidade supera o limiar de urgência do cenário, com
/// bônus de continuidade (histerese), conclusão de ação ao atingir a duração máxima declarada, e
/// deslocamento com custo real quando a ação vencedora exige um local diferente do atual.
/// <c>Sleep</c> usa um lugar de descanso alcançável (chão/moradia/cama); sem caminho o NPC
/// bloqueia no local atual em vez de teleportar.</summary>
[LivingCapability("BEHAVIOR", Events = [WorldEventKind.TheftDiscovered], Consumers = ["map.npc.action"])]
[RunsAfter(typeof(NeedsDecaySystem))]
public sealed partial class BehaviorDecisionSystem : IRunsBetweenHours
{
    public const string SystemName = "behavior-decision";

    private const double NonNeedBaselineUtility = 50.0;

    /// <summary>Déficit a partir do qual uma necessidade de sobrevivência (fome, sede, sono) deixa de
    /// competir como "mais uma preferência" e passa a escalar: ninguém trabalha até morrer de sede só
    /// porque a personalidade pesa muito em Work (peso até ~2,3 × 50 já passa de 100, o teto linear).</summary>
    private const int CriticalDeficit = 80;
    private const double CriticalEscalation = 3.0;

    /// <summary>Despensa que uma ida ao mercado tenta formar e abaixo da qual a casa pede reposição.</summary>
    private const int ProvisionTargetDays = 2;
    private const int PurchaseDaysOfSupply = 4;

    /// <summary>Quem mora longe do mercado repõe a despensa para a ida e a volta, não só para o dia: cada dia de caminho
    /// (ida e volta) soma um dia à meta, e a compra cobre a meta mais uma folga.</summary>
    private const int ProvisionSlackDays = 2;
    private const int MaxProvisionDays = 14;

    /// <summary>Fração da meta de dias abaixo da qual a casa sai para repor.</summary>
    private const double RestockShortfall = 0.5;
    private const long MinimumPurchaseQuantity = 10;
    private const int WellDaysOfSupply = 2;
    private const long WellMinimumDraw = 4;

    private static readonly ActionType[] AllActions = Enum.GetValues<ActionType>()
        .Where(a => a is not (ActionType.UsePower or ActionType.Steal))
        .ToArray();

    private readonly SkillsRules? _skillsRules;

    // Reaproveitado a cada tick (o tick é serial e o conjunto só vive dentro dele): criar e redimensionar um conjunto novo
    // com a posição de todos os NPCs vivos era 5% dos bytes alocados por tick a 10.000 NPCs.
    private readonly HashSet<CellCoord> _occupancy = [];

    // Mesmo reaproveitamento para os caches por cidade: valem só dentro do tick e são esvaziados no início dele.
    private readonly Dictionary<CityId, long> _cityPopulationCache = [];
    private readonly Dictionary<CityId, CityBounds> _cityBoundsCache = [];

    // AD-142: grupos da ação em conta e da vencedora, reaproveitados (o tick é serial)
    private readonly double[] _brainGroups = new double[2 * BrainState.Groups];

    // AD-197l: relações da decisão reaproveitadas de NPC em NPC (o tick é serial e o contexto sem rastro vive só na decisão)
    private readonly LazyRelationshipFacts _relations = new(null!, default);

    // AD-197m: o contexto da decisão sem rastro, reaproveitado de NPC em NPC (Reset reescreve todos os campos)
    private readonly DecisionContext _context = new(default, 0, default, default, null, [], [], [], [], default!, null);

    public BehaviorDecisionSystem(SkillsRules? skillsRules = null) => _skillsRules = skillsRules;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.NeedsRules;
        var catalog = world.ActionCatalog;
        long now = ctx.CurrentTick;
        long nowMs = world.CurrentDate.TotalMilliseconds; // AD-197c: no passo fino, ação começa e termina dentro da hora
        bool fine = world.FineTime;
        var marketIndex = MarketIndex.BuildForTick(world);
        var laborIndex = LaborValueIndex.BuildForTick(world);
        var targets = TargetsForTick(world);

        // Índice de ocupação construído uma vez por tick (O(população)), não por NPC — a versão
        // anterior escaneava todo mundo dentro de cada passeio ambiente e virava O(n²), inviável
        // acima de ~1000 NPCs (LIVE-POLISH: perf sensor). Mantido atualizado a cada `MoveTo` desta
        // função pra ordem dentro do próprio tick continuar correta.
        var occupancy = _occupancy;
        occupancy.Clear();
        foreach (var other in world.AliveNpcIndex.Alive)
            if (other.IsAlive)
                occupancy.Add(other.CurrentLocation);
        foreach (var constructCell in world.ExtraordinaryConstructs.SelectMany(construct => construct.Footprint))
            occupancy.Add(constructCell);

        // Cache de população por cidade, uma entrada calculada na primeira vez que a cidade é
        // consultada neste tick (não todas de uma vez — nem toda cidade tem NPC completando ação
        // ambiente na mesma hora). Sem isso, CityPopulationQuery.Population (O(população)) era
        // chamada a cada NPC dentro de MoveOneAmbientStep. A contagem não muda dentro do próprio
        // tick por causa de passeio ambiente (só afeta CurrentLocation, não City), então o
        // resultado é idêntico ao recalculado por chamada.
        var cityPopulationCache = _cityPopulationCache;
        var cityBoundsCache = _cityBoundsCache;
        cityPopulationCache.Clear();
        cityBoundsCache.Clear();

        // AD-142: cérebro (experimento atrás de flag); desligado, nada abaixo lê ou escreve estado novo
        bool brainOn = world.BrainOn;
        var brainGroups = _brainGroups;

        foreach (var npc in targets)
        {
            var stage = world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate));
            // #255: quem usa um objeto da casa (banho, vaso, TV...) fica nisso até acabar; o fim do uso o acorda, a decisão da hora não o tira dali
            LivingWorld.Simulation.Objects.ObjectUsage.SettleDue(world, ctx, npc);
            if (npc.BodyTask is { Action: LivingWorld.Simulation.Objects.SocialInteractions.Action }) LivingWorld.Simulation.Objects.SocialInteractions.SettleDue(world, ctx, npc); // #259
            if (npc.BodyTask is { Action: LivingWorld.Simulation.Objects.ObjectUsage.Action or LivingWorld.Simulation.Objects.SocialInteractions.Action } && LivingWorld.Simulation.Players.PlayerTasks.Of(world, npc) is not null)
            {
                if (fine) NpcWakeScheduler.RescheduleAtMs(world, npc, rules, catalog, null, stage);
                else NpcWakeScheduler.RescheduleAfterHour(world, ctx, npc, rules, catalog, now, null, stage);
                continue;
            }
            // AD-154: quem está preso num sequestro fica fora da rotina, no cativeiro (come do que o sequestrador tem)
            if (world.Crime?.Captives is not null && LivingWorld.Simulation.Crime.Kidnapping.Hold(world, npc, now))
            {
                NpcWakeScheduler.RescheduleAfterHour(world, ctx, npc, rules, catalog, now, null, stage);
                continue;
            }
            // #234: quem apanhou do jogador e marcou o revide (ou acode a quem pediu ajuda) golpeia na hora marcada
            if (npc.Combat is { RetaliateAtMs: > 0 }) LivingWorld.Simulation.Players.PlayerCombat.OnWake(world, ctx, npc, nowMs);
            // AD-197f: a disputa de vontade da posse comum é checada quando o possuído desperta (sem evento agendado)
            if (npc.Will is { Controlled: true }) LivingWorld.Simulation.Players.PossessionContest.OnWake(world, ctx, npc, now);
            bool possessed = ControlMechanic.IsPossessed(world, npc);
            if (!possessed)
                EvaluateProfessionSwitch(world, ctx, npc, laborIndex, stage);

            long startedBefore = npc.ActionStartedAtTick;
            bool justCompleted = TryCompleteAction(
                world, npc, rules, catalog, now, nowMs, marketIndex, ctx, occupancy,
                cityPopulationCache, cityBoundsCache);
            SettleCarried(world, npc, now); // AD-147: devolve em casa; longe, bebe do que leva se a sede é crítica

            ActionType chosen;
            ActionType? travelPurpose = null;
            bool departing = false;
            if (possessed && ControlMechanic.TryDelegatedAction(world, npc, justCompleted, out var delegated))
            {
                chosen = delegated;
                if (justCompleted || chosen != npc.CurrentAction)
                {
                    StartAction(npc, chosen, now, nowMs, fine);
                    ctx.LogEvent(
                        WorldEventKind.ExtraordinaryEffectApplied,
                        $"{npc.Id.Value}|possessed-action|{chosen}", sourceSystem: SystemName);
                }
            }
            else
            {
                var continuityAction = justCompleted ? null : npc.CurrentAction;

                PendingPowerInvocation? pendingPower = null;
                ActionType candidate;
                int buySource = 0; // AD-148, só contagem: 1 utilidade, 2 rotina (despensa baixa ou choro)
                bool urgent = HasUrgentNeedForDecision(npc, rules, now);
                bool workHour = LivingWorld.Simulation.Observation.PurchaseTally.Diagnose && npc.Employer is not null
                    && RoutineWithCommuteLead(world, npc, catalog, stage) == ActionType.Work;
                var diag = world.PurchaseTally;
                if (npc.ControlledAction is { } playerAction)
                {
                    // Controlador externo: troca só a escolha. A resolução abaixo (percepção, passos, viagem) é a de qualquer NPC.
                    candidate = playerAction;
                    if (playerAction == ActionType.UsePower) // AD-196: a invocação sai da mesma lista de oportunidades do motor
                        (candidate, pendingPower) = ControlledPower(world, npc, now);
                }
                else if (urgent)
                {
                    bool recordTrace = ShouldRecordCognitionTrace(world, npc);
                    var provision = ProvisionViewFor(world, npc, marketIndex); // AD-148
                    var decision = SelectByUtility(
                        DecisionContextBuilder.Build(world, npc, now, recordTrace ? null : _relations, // AD-197l: sem rastro, não sai da decisão
                            provisionTargetDays: provision.TargetDays * PsycheMechanic.HoardFactor(world, npc, now),
                            foodUnitPrice: provision.FoodPrice, shopperOut: provision.ShopperOut, foodStallKnownEmpty: provision.FoodStallEmpty,
                            reuseContext: recordTrace ? null : _context), // AD-197m: idem o contexto inteiro
                        rules,
                        world.EconomyRules,
                        continuityAction,
                        wakeReason: justCompleted ? WakeReason.ActionCompleted : WakeReason.UrgentNeed,
                        previousIntent: npc.CurrentIntent,
                        captureTrace: recordTrace,
                        eligibility: LivingWorld.Simulation.Ontogeny.OntogenyMechanic.EligibilityFor(world.OntogenyRules, npc.Development),
                        brainGains: brainOn ? BrainMechanic.GainsFor(world.BrainRules!, npc, now) : null,
                        recountWinner: brainOn);
                    candidate = decision.Action;
                    pendingPower = decision.PendingPower;
                    buySource = 1;
                    // AD-185: a sede sem água em casa nem no que leva vai à fonte quando ela fica mais perto que o mercado (ou sem com que
                    // pagar), qualquer que fosse a escolha (sem água nem moeda a utilidade não via saída e a pessoa morria de sede em casa);
                    // e quem está em casa com a água acabada vai buscá-la para a casa (a criança que chora, o resto da família)
                    LivingWorld.Simulation.Economy.Supply.WaterFetch.SickGetsWaterBrought(world, npc, now);
                    if (LivingWorld.Simulation.Economy.Supply.WaterFetch.OnTheWay(world, npc)
                        && (100 - npc.HungerAt(now) < LivingWorld.Simulation.Economy.Supply.WaterFetch.StarvingDeficit || candidate == ActionType.Travel
                            || LivingWorld.Simulation.Economy.Supply.WaterFetch.ThirstFirst(world, npc, now))) // a caminho da água: segue, salvo fome à beira da morte (com a sede também, segue)
                        candidate = ActionType.Travel;
                    else if (ThirstGoesToSource(world, npc, now, marketIndex, candidate))
                        candidate = ActionType.Travel;
                    else if (LivingWorld.Simulation.Economy.Supply.WaterFetch.NeighbourWatersChild(world, npc, now))
                        candidate = ActionType.Eat; // o vizinho trouxe água: a criança bebe
                    else if (candidate is not (ActionType.Eat or ActionType.Buy or ActionType.Travel)
                             && LivingWorld.Simulation.Economy.Supply.WaterFetch.ThirstyAwayWithWaterHome(world, npc, now))
                        candidate = ActionType.Eat; // volta para beber da despensa
                    else if (candidate is not (ActionType.Travel or ActionType.Eat or ActionType.Buy)
                             && LivingWorld.Simulation.Economy.Supply.WaterFetch.HomeDry(world, npc)
                             && LivingWorld.Simulation.Economy.Supply.WaterFetch.WantsToFetch(world, npc, now))
                        candidate = ActionType.Travel;
                    if (workHour) diag.UrgentAtWork[Math.Min(15, (int)candidate)]++;
                    if (brainOn)
                    {
                        // AD-167: os grupos da vencedora só são recontados quando a escolha vai ser guardada (quem segue no que faz não guarda)
                        if (BrainRememberStores(npc, candidate, justCompleted))
                            FillWinnerGroups(decision, brainGroups);
                        BrainRemember(world, npc, candidate, brainGroups.AsSpan(BrainState.Groups, BrainState.Groups), now, justCompleted);
                        if (LivingWorld.Simulation.Observation.BrainDecisionTally.Diagnose) // AD-160: sonda, só o brain-trace liga
                            ProbeBrain(npc, DecisionContextBuilder.Build(world, npc, now, provisionTargetDays: provision.TargetDays * PsycheMechanic.HoardFactor(world, npc, now),
                                foodUnitPrice: provision.FoodPrice, shopperOut: provision.ShopperOut, foodStallKnownEmpty: provision.FoodStallEmpty),
                                rules, world.EconomyRules, continuityAction,
                                LivingWorld.Simulation.Ontogeny.OntogenyMechanic.EligibilityFor(world.OntogenyRules, npc.Development), candidate, now);
                    }

                    if (recordTrace)
                        world.CognitionLog.Record(npc.Id, now, decision.Trace);
                }
                else
                {
                    // despensa baixa para a distância do mercado: quem tem dinheiro vai repor sem esperar a sede (à noite dorme)
                    // a rotina só é consultada aqui: quem decide por urgência ou por controlador não a lê
                    candidate = NonUrgentCandidate(
                        world, npc, RoutineWithCommuteLead(world, npc, catalog, stage), stage, marketIndex);
                    buySource = 2;
                    if (workHour) { diag.NonUrgentAtWork++; if (candidate == ActionType.Buy) diag.RestockAtWork++; }
                    var beforeMood = candidate;
                    candidate = MoodShapesRoutine(world, ctx, npc, candidate, now); // AD-138: luto e vergonha recolhem
                    if (candidate is ActionType.Sleep && SupperBeforeSleep(world, npc, catalog, now)) candidate = ActionType.Eat; // AD-157b
                    if (workHour && candidate != beforeMood) diag.MoodAtWork++;
                    if (brainOn) candidate = BrainWeighsRoutine(world, npc, candidate, now, justCompleted, catalog, stage); // AD-160b: tempo livre
                }

                if (MealTimeAtHome(world, npc, candidate, now)) candidate = ActionType.Eat; // AD-157b
                if (npc.ControlledAction is null && ChildHungerSendsShopper(world, npc, candidate, now)) candidate = ActionType.Buy; // AD-184
                var beforeSteps = candidate;
                candidate = ApplyPerception(world, npc, candidate);
                if (workHour && !urgent && candidate != beforeSteps) diag.PerceptionAtWork++;
                beforeSteps = candidate;
                candidate = ConstrainByDevelopment(world, npc, candidate);
                if (workHour && !urgent && candidate != beforeSteps) diag.DevelopmentAtWork++;
                // só quem já decidiu buscar comida ou água (comer, comprar) pensa em furtar: o resto das decisões urgentes não paga a conta
                // AD-166: o furto também passa pelo marco (Steal pede cognição social); antes entrava depois dele e a criança furtava
                if (urgent && candidate is ActionType.Eat or ActionType.Buy && npc.ControlledAction is null && world.TheftRules.Enabled
                    && ConstrainByDevelopment(world, npc, ActionType.Steal) == ActionType.Steal && TheftResolver.Wants(world, npc, now))
                    candidate = ActionType.Steal; // fome sem comida nem como comprar + temperamento + algo a levar (AD-150)
                var sickAware = LivingWorld.Simulation.Health.SickBehavior.Constrain(world, npc, candidate, now); // de cama ou cuidando (AD-137)
                if (workHour && !urgent && sickAware != candidate) { if (sickAware == ActionType.Idle) diag.CareAtWork++; else diag.SickAtWork++; }
                // a cama troca a ação (comer, comprar) depois do marco: a troca também passa pelo desenvolvimento (AD-140). A
                // criança de cama que não pode ir comprar come (o plano de comida usa a despensa e a compra assistida), em vez
                // de ficar parada e morrer de sede com água em casa
                candidate = sickAware == candidate ? candidate
                    : sickAware == ActionType.Buy && ConstrainByDevelopment(world, npc, ActionType.Buy) != ActionType.Buy
                        ? ConstrainByDevelopment(world, npc, ActionType.Eat)
                        : ConstrainByDevelopment(world, npc, sickAware);

                // #242: quem está de folga sai; quem deixou o turno por humor, corpo ou cuidado de doente (a rotina diz trabalho) fica onde está
                _onDuty = candidate is ActionType.Idle or ActionType.Socialize && world.StreetRules.AfterWorkOutings
                    && RoutineWithCommuteLead(world, npc, catalog, stage) == ActionType.Work;
                // #255: sem nada urgente, quem ninguém conduz e está à toa em casa cuida das necessidades do catálogo (banho, vaso, TV) pelos usos dos objetos,
                // antes de sair a passeio ou de se deitar (quem vai dormir passa no vaso)
                // #258: o parceiro fora de casa com uma necessidade da casa urgente (bexiga, higiene) volta para ela: o destino é a cama da casa, e lá o cuidado acontece
                if (npc.ControllerId is null && npc.ExtraNeeds is not null && candidate is ActionType.Idle or ActionType.Socialize && CareCallsHome(world, npc, nowMs))
                    candidate = ActionType.Sleep;
                // #258: a casa que a pessoa mantém (luz ao escurecer, apagar ao dormir ou sair, preparar a refeição) pelos usos do catálogo
                bool tended = !urgent && npc.ControllerId is null && LivingWorld.Simulation.Objects.HomeTending.Applies(world)
                    && LivingWorld.Simulation.Objects.HomeTending.TryTend(world, ctx, npc, candidate,
                        world.Lights is not null && candidate != ActionType.Sleep && RefineForLocation(world, npc, candidate, marketIndex) == ActionType.Travel);
                bool cared = tended || npc.ExtraNeeds is not null && npc.ControllerId is null && (candidate == ActionType.Sleep ? npc.SleepAt(now) > 15 : !urgent || LivingWorld.Simulation.Objects.ObjectUsage.HasCriticalNeed(world, npc, nowMs))
                    && candidate is ActionType.Idle or ActionType.Socialize or ActionType.Sleep
                    && LivingWorld.Simulation.Objects.ObjectUsage.TryCare(world, ctx, npc, candidate == ActionType.Sleep ? 25 : 0);
                // #259: sem nada a cuidar, quem está só e à toa procura companhia (interação social do catálogo, o mesmo caminho do jogador)
                if (!cared && !urgent && npc.ControllerId is null && candidate is ActionType.Idle or ActionType.Socialize && world.ObjectCatalog?.Social is not null)
                    cared = LivingWorld.Simulation.Objects.SocialInteractions.TryInitiate(world, ctx, npc);
                chosen = cared ? ActionType.Idle
                    : npc.ControllerId is not null && npc.ControlledAction == ActionType.Idle
                    ? ActionType.Idle // #222: o corpo do jogador "à toa" fica onde está (não vai à toa em casa); o jogador o conduz
                    : ResolveWithStepCap(npc.Id.Value, candidate, world, npc, marketIndex, rules.MaxActionSelectionSteps);
                _onDuty = false;
                // AD-184: a hora de beber (em casa, acordado ou indo deitar, a sede na linha da refeição): um copo do pote, sem trocar a ação
                if (chosen is not (ActionType.Eat or ActionType.Travel))
                    LivingWorld.Simulation.Economy.Supply.WaterFetch.SipAtHome(world, npc, chosen,
                        chosen == ActionType.Sleep ? Objects.ObjectUse.DurationHours(world, npc, ActionType.Sleep, catalog) : 0, now);
                // AD-185: em casa, com sede urgente e água na despensa, bebe antes de sair (a mudança de cidade levava a criança com
                // sede para uma viagem de um dia, ao lado da água de casa)
                if (chosen == ActionType.Travel && urgent && LivingWorld.Simulation.Economy.Supply.WaterFetch.ThirstyAtHomeWithWater(world, npc, now))
                    LivingWorld.Simulation.Economy.Supply.WaterFetch.DrinkFromPantry(world, npc, now); // só a água: a comida fica
                travelPurpose = chosen == ActionType.Travel
                    && (candidate is ActionType.Work or ActionType.Sleep or ActionType.Buy or ActionType.Eat or ActionType.Socialize
                        || (candidate == ActionType.Idle && world.StreetRules.AfterWorkOutings)) // #242: o passeio de quem está à toa
                    ? candidate
                    : null;

                if (justCompleted || chosen != npc.CurrentAction)
                {
                    departing = chosen == ActionType.Travel;
                    if (departing && candidate == ActionType.Eat) FeltHomeForMeal(world, npc); // AD-147: longe da despensa
                    if (departing && candidate == ActionType.Socialize && travelPurpose == ActionType.Socialize) world.InventionTally.CompanyTrips++;
                    // AD-179: o diarista que larga a jornada no meio (fome, sede, fim da rotina) conta as horas que fez no trecho
                    if (!justCompleted && npc.CurrentAction == ActionType.Work
                        && (npc.Employer is null || LivingWorld.Simulation.Cities.Streets.SiteWork.IsHelper(world, npc)))
                        LivingWorld.Simulation.Cities.Streets.SiteWork.Clock(world, ctx, npc, npc.ActionStartedAtTick, now);
                    if (candidate == ActionType.Buy && (departing || npc.CurrentAction != ActionType.Travel))
                    {
                        if (buySource == 1) world.PurchaseTally.BuyByUtility++;
                        else world.PurchaseTally.BuyByRestock++;
                    }
                    StartAction(npc, chosen, now, nowMs, fine);
                    if (chosen == ActionType.UsePower && pendingPower is not null)
                        npc.PendingPowerInvocation = pendingPower;
                    if (chosen is ActionType.Buy or ActionType.Eat)
                        EnsureFoodIntent(npc, chosen, now);
                }
            }

            if (npc.CurrentAction == ActionType.Travel)
                npc.SetTravelPurpose(travelPurpose);
            if (departing && IsOutingPurpose(world, travelPurpose)) PinCompanyTarget(world, npc, now); // AD-186: o destino vale a ida toda
            if (departing) PackOnDeparture(world, npc, marketIndex, now); // AD-147: ao sair de casa, leva o que a viagem vai pedir
            world.WakeCounters.RecordWake(withoutEffect: !justCompleted && npc.ActionStartedAtTick == startedBefore);
            if (fine) // AD-197h: a chegada no instante do caminho em ms; a perna fica no viajante para a pose de cada tick
            {
                var leg = TravelLegNow(world, npc, marketIndex);
                if (leg is { } planned) npc.SetTravelLeg(planned.To, planned.EndMs);
                NpcWakeScheduler.RescheduleAtMs(world, npc, rules, catalog, leg?.EndMs, stage);
            }
            else
                NpcWakeScheduler.RescheduleAfterHour(
                    world, ctx, npc, rules, catalog, now, TravelArrivalTick(world, npc, marketIndex), stage);
        }
    }

    /// <summary>Fase 21: rotina, percepção e controlador externo passam pelo mesmo marco que a utility. O marco vale para a <b>intenção</b>: a
    /// viagem ou compra que o refino de local acrescenta para atender uma intenção elegível (ir à cama, à comida) é assistida — quem não anda é levado. Ação cujo
    /// marco o NPC não alcançou vira Idle (sempre elegível). O choro por fome sai da utility, que é quem decide quando o bebê tem fome.</summary>
    internal static ActionType ConstrainByDevelopment(WorldState world, Npc npc, ActionType action)
    {
        var ontogeny = world.OntogenyRules;
        if (!ontogeny.Enabled || ontogeny.IsEligible(npc.Development, action)) return action;
        return ActionType.Idle;
    }

    internal static IEnumerable<Npc> TargetsForTick(WorldState world) => world.NpcWakeBatch;

    /// <summary>Só grava rastro quando o NPC está materializado (linha em
    /// <see cref="WorldState.Npcs"/>) e dentro do escopo observacional com detalhe cosmético
    /// pleno — ver <see cref="MaterializationSystem.HasFullCosmeticDetail"/>; nunca para pool
    /// agregado nem NPC aproximado (ex.: interior sem escopo de prédio).</summary>
    private static bool ShouldRecordCognitionTrace(WorldState world, Npc npc) =>
        world.FindNpc(npc.Id) is not null
        && MaterializationSystem.HasFullCosmeticDetail(world, npc);

    private static bool TryCompleteAction(
        WorldState world, Npc npc, NeedsRules rules, ActionCatalog catalog, long now, long nowMs,
        MarketIndex marketIndex, TickContext ctx, HashSet<CellCoord> occupancy,
        Dictionary<CityId, long> cityPopulationCache, Dictionary<CityId, CityBounds> cityBoundsCache)
    {
        if (npc.CurrentAction is not { } action) return false;

        if (action == ActionType.Travel && TravelDestinationOf(world, npc, marketIndex) is { } destination && destination != npc.CurrentLocation)
        {
            var extraordinary = ExtraordinaryLocomotion.Resolve(world, npc);
            if (extraordinary.HasModifier)
            {
                var advance = ExtraordinaryLocomotion.Advance(
                    world, npc, destination, now, occupancy, extraordinary, ctx);
                return advance.Reached;
            }

            if (world.FineTime)
            {
                // AD-197h: no passo fino a viagem dura o caminho em ms (um trajeto curto leva minutos)
                if (nowMs - npc.ActionStartedAtMs < Mobility.TravelMs(world, npc, npc.CurrentLocation, destination,
                        BodyMechanic.MovementCostMultiplier(world, npc))) return false;
            }
            else if (Underway(nowMs, npc.ActionStartedAtMs, Mobility.TravelTicks(
                world, npc, npc.CurrentLocation, destination, BodyMechanic.MovementCostMultiplier(world, npc)))) return false; // AD-197c: em ms
            if (world.IsExtraordinaryConstructCell(destination)) return false;

            // AD-141: quem atravessou água a pé sente o tempo perdido; quem foi na embarcação da casa, não
            if (world.InventionOn) LivingWorld.Simulation.Invention.InventionIntake.OnTravelArrived(world, npc, npc.CurrentLocation, destination, now);
            if (world.StreetsOn) world.Ground!.RecordTrip(npc.CurrentLocation, destination); // AD-176: o pisoteio desta viagem
            MoveTracked(npc, destination, now, occupancy);
            LivingWorld.Simulation.Invention.Leavings.OnArrived(world, ctx, npc, destination); // AD-168: o espólio no caminho
            LivingWorld.Simulation.Economy.Supply.WaterFetch.OnArrived(world, ctx, npc, destination); // AD-185: fonte ou casa da ida de água
            return true;
        }

        if (world.FineTime
                ? nowMs - npc.ActionStartedAtMs < LivingWorld.Simulation.Objects.ObjectUse.DurationMs(world, npc, action, catalog) // AD-197h
                : Underway(nowMs, npc.ActionStartedAtMs, LivingWorld.Simulation.Objects.ObjectUse.DurationHours(world, npc, action, catalog)))
            return false;

        // #222: o corpo do jogador não passeia nem anda sozinho no trabalho: quem o move é o jogador (walk)
        if (npc.ControllerId is not null && npc.ControlledAction == ActionType.Idle) return false;

        // Sem workplace real (Employer nulo), Work nunca fabrica deslocamento —
        // NPC bloqueado fica onde está em vez de "trabalhar" andando à toa sem destino nenhum.
        if (action is ActionType.Idle or ActionType.Socialize && world.StreetRules.AmbientStepsByHour)
        {
            // AD-192 (08c 146): o passeio e a conversa andam pelo relógio (os passos das horas que a ação durou), não um por ação
            int steps = AmbientSteps(npc.ActionStartedAtTick, now, world.StreetRules.AmbientStepsPerHour);
            for (int step = 0; step < steps; step++)
                MoveOneAmbientStep(world, npc, ctx, now, action, occupancy, cityPopulationCache, cityBoundsCache, step);
        }
        else if (action is ActionType.Idle or ActionType.Socialize
            || (action == ActionType.Work && npc.Employer is not null && !LivingWorld.Simulation.Cities.Streets.SiteWork.HelpingNow(world, npc)))
            MoveOneAmbientStep(world, npc, ctx, now, action, occupancy, cityPopulationCache, cityBoundsCache);

        // AD-142: a ação que a utilidade escolheu e o cérebro guardou ensina pelo alívio que deu
        bool learn = world.BrainOn && npc.Brain?.PendingAction == action;
        var felt = learn && !world.BrainRules!.ValuesSustenance ? FeltNow(world, npc, now) : default; // AD-160b: mede só no fim
        long startedAt = npc.ActionStartedAtTick;
        ApplyActionEffect(world, npc, action, marketIndex, now, ctx);
        if (action == ActionType.Work && (npc.Employer is null || LivingWorld.Simulation.Cities.Streets.SiteWork.IsHelper(world, npc)))
            LivingWorld.Simulation.Cities.Streets.SiteWork.Clock(world, ctx, npc, startedAt, now); // AD-179, AD-184 (ajudante)
        if (learn) BrainLearnOnCompletion(world, npc, action, felt, startedAt, now, ctx);
        return true;
    }

    /// <summary>AD-197c: a ação começada em <paramref name="startedMs"/> ainda não cumpriu <paramref name="hours"/> horas. Na hora cheia
    /// é a conta de antes em horas (<c>now - início &lt; duração</c>).</summary>
    internal static bool Underway(long nowMs, long startedMs, long hours) =>
        hours >= long.MaxValue / WorldMillis.PerHour || nowMs - startedMs < hours * WorldMillis.PerHour;

    /// <summary>AD-197c: no passo fino a ação começa no instante do tick (dentro da hora); de hora em hora, na hora cheia, como antes.</summary>
    private static void StartAction(Npc npc, ActionType action, long now, long nowMs, bool fine)
    {
        if (fine) npc.SetCurrentActionAtMs(action, nowMs);
        else npc.SetCurrentAction(action, now);
    }

    private static BrainMechanic.Felt FeltNow(WorldState world, Npc npc, long now) =>
        BrainMechanic.Feel(world, npc, now, world.EconomyRules.FoodResourceId, world.EconomyRules.WaterResourceId);

    /// <summary>Move e mantém `occupancy` coerente com a posição real dentro do mesmo tick — sem
    /// isso, um NPC que já andou nesta rodada continuaria "fantasma" na célula antiga pros
    /// próximos NPCs avaliados no mesmo `Tick` (ou "preso" na nova, bloqueando a si mesmo).</summary>
    /// <summary>População da cidade memoizada por tick — ver comentário no início de
    /// <see cref="Tick"/>.</summary>
    private static long PopulationOf(WorldState world, CityId city, Dictionary<CityId, long> cache)
    {
        if (!cache.TryGetValue(city, out var population))
            cache[city] = population = CityPopulationQuery.Population(world, city);
        return population;
    }
}
