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
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Escolhe a ação do tick de cada NPC vivo: rotina diária por
/// padrão, utility AI só quando alguma necessidade supera o limiar de urgência do cenário, com
/// bônus de continuidade (histerese), conclusão de ação ao atingir a duração máxima declarada, e
/// deslocamento com custo real quando a ação vencedora exige um local diferente do atual.
/// <c>Sleep</c> usa um lugar de descanso alcançável (chão/moradia/cama); sem caminho o NPC
/// bloqueia no local atual em vez de teleportar.</summary>
[LivingCapability("BEHAVIOR", Consumers = ["map.npc.action"])]
[RunsAfter(typeof(NeedsDecaySystem))]
public sealed partial class BehaviorDecisionSystem : ISimulationSystem
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
        .Where(a => a != ActionType.UsePower)
        .ToArray();

    private readonly SkillsRules? _skillsRules;

    // Reaproveitado a cada tick (o tick é serial e o conjunto só vive dentro dele): criar e redimensionar um conjunto novo
    // com a posição de todos os NPCs vivos era 5% dos bytes alocados por tick a 10.000 NPCs.
    private readonly HashSet<CellCoord> _occupancy = [];

    public BehaviorDecisionSystem(SkillsRules? skillsRules = null) => _skillsRules = skillsRules;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.NeedsRules;
        var catalog = world.ActionCatalog;
        long now = ctx.CurrentTick;
        var marketIndex = MarketIndex.BuildForTick(world);
        var laborIndex = LaborValueIndex.BuildForTick(world);
        var targets = TargetsForTick(world);

        // Índice de ocupação construído uma vez por tick (O(população)), não por NPC — a versão
        // anterior escaneava todo mundo dentro de cada passeio ambiente e virava O(n²), inviável
        // acima de ~1000 NPCs (LIVE-POLISH: perf sensor). Mantido atualizado a cada `MoveTo` desta
        // função pra ordem dentro do próprio tick continuar correta.
        var occupancy = _occupancy;
        occupancy.Clear();
        foreach (var other in world.Npcs)
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
        var cityPopulationCache = new Dictionary<CityId, long>();
        var cityBoundsCache = new Dictionary<CityId, CityBounds>();

        foreach (var npc in targets)
        {
            var stage = world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate));
            bool possessed = ControlMechanic.IsPossessed(world, npc);
            if (!possessed)
                EvaluateProfessionSwitch(world, ctx, npc, laborIndex, stage);

            bool justCompleted = TryCompleteAction(
                world, npc, rules, catalog, now, marketIndex, ctx, occupancy,
                cityPopulationCache, cityBoundsCache);

            ActionType chosen;
            ActionType? travelPurpose = null;
            if (possessed && ControlMechanic.TryDelegatedAction(world, npc, justCompleted, out var delegated))
            {
                chosen = delegated;
                if (justCompleted || chosen != npc.CurrentAction)
                {
                    npc.SetCurrentAction(chosen, now);
                    ctx.LogEvent(
                        WorldEventKind.ExtraordinaryEffectApplied,
                        $"{npc.Id.Value}|possessed-action|{chosen}", sourceSystem: SystemName);
                }
            }
            else
            {
                var continuityAction = justCompleted ? null : npc.CurrentAction;

                var routineAction = RoutineWithCommuteLead(world, npc, catalog, stage);

                PendingPowerInvocation? pendingPower = null;
                ActionType candidate;
                if (HasUrgentNeedForDecision(npc, rules, now))
                {
                    bool recordTrace = ShouldRecordCognitionTrace(world, npc);
                    var decision = SelectByUtility(
                        DecisionContextBuilder.Build(world, npc, now, provisionTargetDays: ProvisionTargetFor(world, npc, marketIndex)),
                        rules,
                        world.EconomyRules,
                        continuityAction,
                        wakeReason: justCompleted ? WakeReason.ActionCompleted : WakeReason.UrgentNeed,
                        previousIntent: npc.CurrentIntent,
                        captureTrace: recordTrace);
                    candidate = decision.Action;
                    pendingPower = decision.PendingPower;

                    if (recordTrace)
                        world.CognitionLog.Record(npc.Id, now, decision.Trace);
                }
                else
                {
                    // despensa baixa para a distância do mercado: quem tem dinheiro vai repor sem esperar a sede (à noite dorme)
                    candidate = routineAction != ActionType.Sleep && NeedsRestocking(world, npc, marketIndex)
                        ? ActionType.Buy
                        : routineAction;
                }

                candidate = ApplyPerception(world, npc, candidate);

                chosen = ResolveWithStepCap(npc.Id.Value, candidate, world, npc, marketIndex, rules.MaxActionSelectionSteps);
                travelPurpose = chosen == ActionType.Travel && candidate is ActionType.Work or ActionType.Sleep or ActionType.Buy
                    ? candidate
                    : null;

                if (justCompleted || chosen != npc.CurrentAction)
                {
                    npc.SetCurrentAction(chosen, now);
                    if (chosen == ActionType.UsePower && pendingPower is not null)
                        npc.PendingPowerInvocation = pendingPower;
                    if (chosen is ActionType.Buy or ActionType.Eat)
                        EnsureFoodIntent(npc, chosen, now);
                }
            }

            if (npc.CurrentAction == ActionType.Travel)
                npc.SetTravelPurpose(travelPurpose);
            NpcWakeScheduler.RescheduleAfterHour(
                world, ctx, npc, rules, catalog, now, TravelArrivalTick(world, npc, marketIndex), stage);
        }
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
        WorldState world, Npc npc, NeedsRules rules, ActionCatalog catalog, long now,
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

            long ticksNeeded = TravelResolution.TicksBetween(
                world.Map, npc.CurrentLocation, destination,
                BodyMechanic.MovementCostMultiplier(world, npc));
            if (now - npc.ActionStartedAtTick < ticksNeeded) return false;
            if (world.IsExtraordinaryConstructCell(destination)) return false;

            MoveTracked(npc, destination, now, occupancy);
            return true;
        }

        if (now - npc.ActionStartedAtTick < catalog.MaxDurationHours[action]) return false;

        // Sem workplace real (Employer nulo), Work nunca fabrica deslocamento —
        // NPC bloqueado fica onde está em vez de "trabalhar" andando à toa sem destino nenhum.
        if (action is ActionType.Idle or ActionType.Socialize || (action == ActionType.Work && npc.Employer is not null))
            MoveOneAmbientStep(world, npc, ctx, now, action, occupancy, cityPopulationCache, cityBoundsCache);

        ApplyActionEffect(world, npc, action, marketIndex, now, ctx);
        return true;
    }

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
