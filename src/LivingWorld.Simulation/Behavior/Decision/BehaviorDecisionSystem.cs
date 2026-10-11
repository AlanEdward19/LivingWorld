using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Behavior.Resolvers;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Extraordinary.Opportunity;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Escolhe a ação do tick de cada NPC vivo: rotina diária por
/// padrão, utility AI só quando alguma necessidade supera o limiar de urgência do cenário, com
/// bônus de continuidade (histerese), conclusão de ação ao atingir a duração máxima declarada, e
/// deslocamento com custo real quando a ação vencedora exige um local diferente do atual.
/// <c>Sleep</c> usa um lugar de descanso alcançável (chão/moradia/cama); sem caminho o NPC
/// bloqueia no local atual em vez de teleportar.</summary>
public sealed class BehaviorDecisionSystem : ISimulationSystem
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
    private const long MinimumPurchaseQuantity = 10;

    private static readonly ActionType[] AllActions = Enum.GetValues<ActionType>()
        .Where(a => a != ActionType.UsePower)
        .ToArray();

    private readonly SkillsRules? _skillsRules;

    public BehaviorDecisionSystem(SkillsRules? skillsRules = null) => _skillsRules = skillsRules;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.NeedsRules;
        var catalog = world.ActionCatalog;
        long now = ctx.CurrentTick;
        var marketIndex = MarketIndex.BuildForTick(world);
        var vacancyIndex = VacancyWeights.BuildForTick(world);
        var targets = TargetsForTick(world);

        // Índice de ocupação construído uma vez por tick (O(população)), não por NPC — a versão
        // anterior escaneava todo mundo dentro de cada passeio ambiente e virava O(n²), inviável
        // acima de ~1000 NPCs (LIVE-POLISH: perf sensor). Mantido atualizado a cada `MoveTo` desta
        // função pra ordem dentro do próprio tick continuar correta.
        var occupancy = new HashSet<CellCoord>();
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
            bool possessed = ControlMechanic.IsPossessed(world, npc);
            if (!possessed)
                EvaluateProfessionSwitch(world, npc, vacancyIndex);

            bool justCompleted = TryCompleteAction(
                world, npc, rules, catalog, now, marketIndex, ctx, occupancy,
                cityPopulationCache, cityBoundsCache);

            ActionType chosen;
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

                var stage = world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate));
                var routineAction = RoutineWithCommuteLead(world, npc, catalog, stage);

                PendingPowerInvocation? pendingPower = null;
                ActionType candidate;
                if (HasUrgentNeedForDecision(npc, rules, now))
                {
                    var decision = SelectByUtility(
                        DecisionContextBuilder.Build(world, npc, now),
                        rules,
                        world.EconomyRules,
                        continuityAction,
                        wakeReason: justCompleted ? WakeReason.ActionCompleted : WakeReason.UrgentNeed,
                        previousIntent: npc.CurrentIntent);
                    candidate = decision.Action;
                    pendingPower = decision.PendingPower;

                    if (ShouldRecordCognitionTrace(world, npc))
                        world.CognitionLog.Record(npc.Id, now, decision.Trace);
                }
                else
                {
                    candidate = routineAction;
                }

                candidate = ApplyPerception(world, npc, candidate);

                chosen = ResolveWithStepCap(npc.Id.Value, candidate, world, npc, marketIndex, rules.MaxActionSelectionSteps);

                if (justCompleted || chosen != npc.CurrentAction)
                {
                    npc.SetCurrentAction(chosen, now);
                    if (chosen == ActionType.UsePower && pendingPower is not null)
                        npc.PendingPowerInvocation = pendingPower;
                    if (chosen is ActionType.Buy or ActionType.Eat)
                        EnsureFoodIntent(npc, chosen, now);
                }
            }

            NpcWakeScheduler.RescheduleAfterHour(world, ctx, npc, rules, catalog, now);
        }
    }

    internal static IEnumerable<Npc> TargetsForTick(WorldState world) => world.NpcWakeBatch;

    /// <summary>Necessidade urgente que faz o NPC largar a rotina: fome, sede e sono sempre; solidão só
    /// quando crítica — quem se sente um pouco sozinho no meio do turno continua a rotina do dia
    /// (e socializa na janela dela), como qualquer pessoa.</summary>
    private static bool HasUrgentNeedForDecision(Npc npc, NeedsRules rules, long now)
    {
        if (!npc.HasUrgentNeed(rules, now)) return false;
        return IsUrgent(npc.HungerAt(now), rules) || IsUrgent(npc.ThirstAt(now), rules)
            || IsUrgent(npc.SleepAt(now), rules) || npc.SocialAt(now) <= SocialCriticalNeed;
    }

    private static bool IsUrgent(int need, NeedsRules rules) => need == 0 || 100 - need > rules.UrgencyThreshold;

    /// <summary>Necessidade social abaixo da qual a solidão deixa de ser preferência e força a decisão.</summary>
    internal const int SocialCriticalNeed = 20;

    /// <summary>Rotina do horário atual, mas saindo cedo: quem tem de estar no trabalho (ou em casa para
    /// dormir) às X horas parte já na hora X menos o tempo de deslocamento, e não só quando o turno
    /// começa — senão cada trajeto empurra o dia para a frente e o ciclo deixa de ser de 24 h.</summary>
    private static ActionType RoutineWithCommuteLead(WorldState world, Npc npc, ActionCatalog catalog, LifeStage stage)
    {
        var profession = NoneIfSentinel(npc.Profession);
        var date = world.CurrentDate;
        var routine = catalog.RoutineOf(profession, stage, date.Hour);

        if (routine != ActionType.Work
            && WorkDestinationOf(world, npc) is { } workplace
            && LeadHoursTo(world, npc, workplace) is > 0 and var workLead
            && catalog.RoutineOf(profession, stage, HourAfter(date, workLead)) == ActionType.Work)
            return ActionType.Work;

        if (routine != ActionType.Sleep && routine != ActionType.Work
            && SleepDestinationOf(world, npc) is { } bed
            && LeadHoursTo(world, npc, bed) is > 0 and var bedLead
            && catalog.RoutineOf(profession, stage, HourAfter(date, bedLead)) == ActionType.Sleep)
            return ActionType.Sleep;

        return routine;
    }

    /// <summary>Horas de deslocamento até onde <paramref name="action"/> acontece (trabalho ou cama);
    /// 0 para as demais ações ou quando o NPC já está lá.</summary>
    internal static long CommuteLeadHours(WorldState world, Npc npc, ActionType action) => action switch
    {
        ActionType.Work when WorkDestinationOf(world, npc) is { } workplace => LeadHoursTo(world, npc, workplace),
        ActionType.Sleep when SleepDestinationOf(world, npc) is { } bed => LeadHoursTo(world, npc, bed),
        _ => 0,
    };

    private static long LeadHoursTo(WorldState world, Npc npc, CellCoord destination) =>
        destination == npc.CurrentLocation
            ? 0
            : TravelResolution.TicksBetween(world.Map, npc.CurrentLocation, destination,
                BodyMechanic.MovementCostMultiplier(world, npc));

    private static int HourAfter(WorldDate date, long hours) =>
        (int)((date.TotalHours + hours) % date.Calendar.HoursPerDay);

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

    private static void MoveTracked(Npc npc, CellCoord destination, long tick, HashSet<CellCoord> occupancy)
    {
        occupancy.Remove(npc.CurrentLocation);
        npc.MoveTo(destination, tick);
        occupancy.Add(destination);
    }

    private static void MoveOneAmbientStep(
        WorldState world, Npc npc, TickContext ctx, long tick, ActionType action, HashSet<CellCoord> occupancy,
        Dictionary<CityId, long> cityPopulationCache, Dictionary<CityId, CityBounds> cityBoundsCache)
    {
        // ResolveGrownBounds realimenta os boxes de overflow das
        // próprias buildings da cidade, então o passeio ambiente já respeita os bounds crescidos
        // em vez do teto só-população de antes.
        CityBounds? homeBounds = null;
        if (world.FindCity(npc.City) is { } city)
        {
            if (!cityBoundsCache.TryGetValue(city.Id, out var cached))
            {
                cached = CityOccupancy.ResolveGrownBounds(
                    world, city, PopulationOf(world, city.Id, cityPopulationCache)).Bounds;
                cityBoundsCache[city.Id] = cached;
            }
            homeBounds = cached;
        }

        // Vizinhos válidos em ordem (Y, X) crescente — o próprio laço dy→dx já gera essa ordem.
        Span<CellCoord> neighbors = stackalloc CellCoord[8];
        int count = 0;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var cell = new CellCoord(npc.CurrentLocation.X + dx, npc.CurrentLocation.Y + dy);
                if (!world.Map.TryGetCell(cell, out _)) continue;
                if (world.IsExtraordinaryConstructCell(cell)) continue;
                if (homeBounds is { } bounds && !bounds.Contains(cell)) continue;
                neighbors[count++] = cell;
            }

        // Passeio ambiente é a única fonte de posições "livres" (sem destino declarado como
        // household/workplace, que legitimamente hospedam vários NPCs na mesma célula) — por
        // isso preferimos vizinhos desocupados aqui (LIVE-POLISH: NPCs se empilhando
        // visualmente). Em população densa isso pode esvaziar TODOS os 8 vizinhos de uma vez —
        // o NPC precisa continuar andando em vez de travar, então cai pra "qualquer vizinho
        // válido" quando não sobra nenhum livre.
        Span<CellCoord> free = stackalloc CellCoord[8];
        int freeCount = 0;
        for (int i = 0; i < count; i++)
            if (!occupancy.Contains(neighbors[i]))
                free[freeCount++] = neighbors[i];

        var candidates = freeCount > 0 ? free[..freeCount] : neighbors[..count];
        if (candidates.Length == 0) return;

        var rng = ctx.Rng($"ambient-{action}-{npc.Id.Value}");
        int index = Math.Min((int)(rng.NextDouble() * candidates.Length), candidates.Length - 1);
        MoveTracked(npc, candidates[index], tick, occupancy);
    }

    private static void ApplyActionEffect(
        WorldState world, Npc npc, ActionType action, MarketIndex marketIndex, long tick, TickContext ctx)
    {
        switch (action)
        {
            case ActionType.Eat:
                ApplyFoodPlan(world, npc, marketIndex, tick, primary: ActionType.Eat);
                break;
            case ActionType.Sleep:
                ApplySleep(world, npc, tick);
                break;
            case ActionType.Socialize:
                npc.SetSocial(100, tick);
                break;
            case ActionType.Buy:
                ApplyFoodPlan(world, npc, marketIndex, tick, primary: ActionType.Buy);
                break;
            case ActionType.UsePower:
                ApplyUsePower(world, npc, tick, ctx);
                break;
        }
    }

    /// <summary>Executa o PendingPowerInvocation via motor extraordinário e registra
    /// <see cref="WorldEventKind.PowerInvoked"/> com proveniência (COH-33).</summary>
    private static void ApplyUsePower(WorldState world, Npc npc, long tick, TickContext ctx)
    {
        var pending = npc.PendingPowerInvocation;
        npc.PendingPowerInvocation = null;
        if (pending is null) return;

        // Decisão (raiz) + PowerInvoked com CauseEventId apontando pra ela (COH-33 AC).
        long decisionEventId = ctx.LogEvent(
            WorldEventKind.PowerInvoked,
            $"{npc.Id.Value}|{pending.PowerId}|decision",
            SystemName,
            causeEventId: null);

        long powerInvokedId = ctx.LogEvent(
            WorldEventKind.PowerInvoked,
            $"{npc.Id.Value}|{pending.PowerId}|{pending.MechanicToken}",
            SystemName,
            causeEventId: decisionEventId);

        NpcId targetId = pending.SuggestedTarget ?? npc.Id;
        CellCoord? targetCell = null;
        if (pending.MechanicToken.StartsWith("npc.teleport", StringComparison.Ordinal))
            targetCell = ResolveTeleportTargetCell(world, npc);

        var invocation = new ExtraordinaryInvocation(
            world.NextEventId,
            npc.Id,
            pending.PowerId,
            targetId,
            Resolution: null,
            TargetCell: targetCell,
            Origin: ExtraordinaryInvocationOrigin.Authored);

        ExtraordinaryInvocationEngine.Invoke(world, ctx, invocation, causeEventId: powerInvokedId);
        _ = tick;
    }

    private static CellCoord? ResolveTeleportTargetCell(WorldState world, Npc npc)
    {
        // Destino adjacente vazio preferido; senão a própria célula (Invoke pode falhar — ok).
        foreach (var delta in new (int X, int Y)[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1) })
        {
            var candidate = new CellCoord(npc.CurrentLocation.X + delta.X, npc.CurrentLocation.Y + delta.Y);
            if (!world.Map.TryGetCell(candidate, out _)) continue;
            if (world.IsExtraordinaryConstructCell(candidate)) continue;
            if (world.Npcs.Any(o => o.IsAlive && o.CurrentLocation == candidate)) continue;
            return candidate;
        }

        return npc.CurrentLocation;
    }

    private static void ApplySleep(WorldState world, Npc npc, long tick)
    {
        var rest = RestPlaceResolver.Resolve(world, npc);
        if (rest.Location != npc.CurrentLocation) return;

        npc.SetSleep((int)(100 * rest.RecoveryEfficiency), tick);
    }

    /// <summary>COH-42: falha local tenta alternativa (Buy↔Eat/estoque) antes de
    /// <see cref="IntentStatus.Invalidated"/>.</summary>
    private static void ApplyFoodPlan(
        WorldState world, Npc npc, MarketIndex marketIndex, long tick, ActionType primary) =>
        ResolveFoodPlan(world, npc, marketIndex, tick, primary);

    /// <summary>Resolve Buy/Eat com alternativas; retorna o status final do Intent quando
    /// um plano alimentar estava em curso (para testes T26).</summary>
    internal static IntentStatus? ResolveFoodPlan(
        WorldState world, Npc npc, MarketIndex marketIndex, long tick, ActionType primary)
    {
        EnsureFoodIntent(npc, primary, tick);

        bool primaryOk = primary == ActionType.Buy
            ? TryApplyBuy(world, npc, marketIndex)
            : TryApplyEat(world, npc, tick);

        if (primaryOk)
        {
            if (npc.IntentStatus == IntentStatus.Active)
                npc.CompleteIntent();
            return npc.IntentStatus;
        }

        bool alternativeOk = primary == ActionType.Buy
            ? TryApplyEat(world, npc, tick)
            : TryApplyBuy(world, npc, marketIndex);

        if (alternativeOk)
        {
            if (npc.IntentStatus == IntentStatus.Active)
                npc.CompleteIntent();
            return npc.IntentStatus;
        }

        if (npc.IntentStatus == IntentStatus.Active)
            npc.InvalidateIntent();
        return npc.IntentStatus;
    }

    private static void EnsureFoodIntent(Npc npc, ActionType foodAction, long tick)
    {
        if (npc.IntentStatus == IntentStatus.Active
            && npc.CurrentIntent is ActionType.Buy or ActionType.Eat)
            return;

        npc.SetIntent(foodAction, tick);
    }

    private static bool TryApplyEat(WorldState world, Npc npc, long tick)
    {
        var econ = world.EconomyRules;
        if (!econ.Enabled)
        {
            npc.SetHunger(100, tick);
            npc.SetThirst(100, tick);
            return true;
        }

        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household)
            return false;

        bool fed = false;
        var meal = FoodResolver.ResolveMeal(world, household);
        if (meal.Id > 0 && household.Withdraw(meal, 1).IsSuccess)
        {
            npc.SetHunger(100, tick);
            world.RecordResourceConsumed(meal, 1);
            fed = true;
        }

        var water = new ResourceType(econ.WaterResourceId);
        if (household.Withdraw(water, 1).IsSuccess)
        {
            npc.SetThirst(100, tick);
            world.RecordResourceConsumed(water, 1);
            fed = true;
        }

        return fed;
    }

    private static bool TryApplyBuy(WorldState world, Npc npc, MarketIndex marketIndex)
    {
        if (!world.EconomyRules.Enabled) return false;
        var market = NearestEssentialsMarket(world, npc, marketIndex);
        if (market is null) return false; // vendedor indisponível
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household)
            return false;

        var econ = world.EconomyRules;
        bool bought = false;
        foreach (var resourceId in new[] { econ.FoodResourceId, econ.WaterResourceId })
        {
            var resource = new ResourceType(resourceId);
            if (!market.Prices.TryGetValue(resource, out var unitPrice) || unitPrice <= 0) continue;

            // Compra de domicílio, como uma ida real ao mercado: cobre ~4 dias de cada membro
            // (mínimo 10), limitada ao que a carteira paga.
            long quantity = Math.Max(MinimumPurchaseQuantity, household.Members.Count * PurchaseDaysOfSupply);
            quantity = Math.Max(1, Math.Min(quantity, npc.Wallet.Amount / unitPrice));
            var ctx = new TransactionContext(
                npc.Wallet, market.Treasury, market.Stock.GetValueOrDefault(resource),
                household.Stock.GetValueOrDefault(resource), resource, unitPrice, quantity);

            if (!MarketTransaction.Execute(ctx).IsSuccess) continue;

            var price = new Money(unitPrice * quantity);
            npc.TryDebitWallet(price);
            market.Withdraw(resource, quantity);
            market.CreditTreasury(price);
            household.Deposit(resource, quantity);
            bought = true;
        }

        return bought;
    }

    private static ActionType RefineForLocation(WorldState world, Npc npc, ActionType candidate, MarketIndex marketIndex) => candidate switch
    {
        ActionType.UsePower => ActionType.UsePower,
        ActionType.Sleep when SleepDestinationOf(world, npc) is { } dest && dest != npc.CurrentLocation => ActionType.Travel,
        ActionType.Work when WorkDestinationOf(world, npc) is { } dest && dest != npc.CurrentLocation => ActionType.Travel,
        ActionType.Buy when BuyDestinationOf(world, npc, marketIndex) is { } dest && dest != npc.CurrentLocation => ActionType.Travel,
        _ when RelocationDestinationOf(world, npc) is { } relocation && relocation != npc.CurrentLocation => ActionType.Travel,
        _ => candidate,
    };

    private static CellCoord? WorkDestinationOf(WorldState world, Npc npc) =>
        npc.Employer is { } workplaceId ? world.FindWorkplace(workplaceId)?.Location : null;

    private static CellCoord? SleepDestinationOf(WorldState world, Npc npc)
    {
        var rest = RestPlaceResolver.Resolve(world, npc);
        if (rest.Location == npc.CurrentLocation) return null;
        return RestPlaceResolver.IsReachable(world.Map, npc.CurrentLocation, rest.Location) ? rest.Location : null;
    }

    private static CellCoord? BuyDestinationOf(WorldState world, Npc npc, MarketIndex marketIndex) =>
        NearestEssentialsMarket(world, npc, marketIndex)?.Location;

    /// <summary>Mercado mais próximo com comida ou água em estoque — quem vai comprar não vai à
    /// ferraria só porque ela fica mais perto.</summary>
    private static Workplace? NearestEssentialsMarket(WorldState world, Npc npc, MarketIndex marketIndex) =>
        marketIndex.NearestSelling(
            npc.CurrentLocation,
            new ResourceType(world.EconomyRules.FoodResourceId), new ResourceType(world.EconomyRules.WaterResourceId));

    private static CellCoord? TravelDestinationOf(WorldState world, Npc npc, MarketIndex marketIndex) =>
        RelocationDestinationOf(world, npc) is { } relocationDest
            ? relocationDest
            : SleepDestinationOf(world, npc) is { } sleepDest && sleepDest != npc.CurrentLocation
                ? sleepDest
                : WorkDestinationOf(world, npc) is { } workDest && workDest != npc.CurrentLocation
                    ? workDest
                    : BuyDestinationOf(world, npc, marketIndex);

    private static CellCoord? RelocationDestinationOf(WorldState world, Npc npc) =>
        npc.Household is { } householdId
        && world.FindHousehold(householdId) is { PendingRelocationCity: { } cityId }
        && world.FindCity(cityId) is { } city
            ? city.Location
            : null;

    /// <summary>O rastro é montado sob demanda: quase nenhuma decisão é gravada no
    /// <see cref="NpcCognitionLog"/>, e derivar pressões/oportunidades custava mais que a
    /// própria escolha da ação.</summary>
    internal readonly struct UtilityDecision(
        ActionType action, PendingPowerInvocation? pendingPower, Func<DecisionTrace> trace)
    {
        public ActionType Action { get; } = action;
        public PendingPowerInvocation? PendingPower { get; } = pendingPower;
        public DecisionTrace Trace => trace();
    }

    /// <summary>Escolhe ação por utility e expõe <see cref="DecisionTrace"/> volátil.
    /// Foresight entra via <see cref="DecisionContext.ForesightPreviews"/>
    /// — dicionário vazio compartilhado no caminho comum, sem parâmetro paralelo omnisciente.</summary>
    internal static UtilityDecision SelectByUtility(
        DecisionContext ctx, NeedsRules rules, EconomyRules economy, ActionType? continuityAction,
        PowerUtilityRules? powerRules = null,
        WakeReason wakeReason = WakeReason.UrgentNeed,
        ActionType? previousIntent = null)
    {
        var utilityRules = PowerUtilityRules.Resolve(powerRules);
        var foresight = ctx.ForesightPreviews ?? ForesightMechanic.EmptyPreviews;
        var best = ActionType.Eat;
        double bestScore = double.NegativeInfinity;
        PendingPowerInvocation? bestPending = null;
        var scored = new List<(ActionType Action, double Score)>(AllActions.Length + ctx.PowerOpportunities.Count);

        foreach (var action in AllActions)
        {
            double score = UtilityBaseOf(ctx, action, economy) * PersonalityWeighting.WeightOf(ctx.Personality, action)
                + ContextFactorBonus(ctx, action);
            if (foresight.TryGetValue(action, out var preview))
                score *= ForesightUtilityFactor(preview);
            if (rules.HysteresisEnabled && continuityAction == action)
                score += rules.ContinuityBonus;

            scored.Add((action, score));
            if (score > bestScore)
            {
                bestScore = score;
                best = action;
                bestPending = null;
            }
        }

        foreach (var opp in ctx.PowerOpportunities)
        {
            // Spec edge: mechanic/candidate scoring failure is isolated — exclude this
            // opportunity from comparison; other candidates and fixed ActionTypes continue.
            double score;
            try
            {
                score = PowerOpportunityUtility(opp, ctx, utilityRules);
            }
            catch
            {
                continue;
            }

            scored.Add((ActionType.UsePower, score));
            if (score > bestScore)
            {
                bestScore = score;
                best = ActionType.UsePower;
                bestPending = new PendingPowerInvocation(
                    opp.PowerId, opp.MechanicToken, opp.SuggestedTarget);
            }
        }

        return new UtilityDecision(
            best, bestPending,
            () => BuildDecisionTrace(ctx, wakeReason, previousIntent, best, bestScore, scored));
    }

    private static DecisionTrace BuildDecisionTrace(
        DecisionContext ctx,
        WakeReason wakeReason,
        ActionType? previousIntent,
        ActionType winner,
        double winningUtility,
        List<(ActionType Action, double Score)> scored)
    {
        var pressures = PressureModel.DerivePressures(ctx);
        var opportunities = OpportunityModel.DeriveOpportunities(ctx);

        var topPressures = pressures
            .OrderByDescending(p => p.Intensity)
            .ThenBy(p => p.Kind, StringComparer.Ordinal)
            .Take(3)
            .ToList();

        var ordered = scored
            .GroupBy(s => s.Action)
            .Select(g => (Action: g.Key, Score: g.Max(x => x.Score)))
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Action)
            .ToList();

        var alternatives = ordered
            .Where(s => s.Action != winner)
            .Take(3)
            .Select(s => s.Action)
            .ToList();

        var positives = new List<string>();
        foreach (var pressure in topPressures)
        {
            positives.Add($"{pressure.Kind}:{pressure.Intensity:0.#}");
            foreach (var factor in pressure.Factors.Take(2))
                positives.Add(factor);
        }

        foreach (var opp in opportunities.Take(2))
            positives.Add(opp.Kind);

        var negatives = new List<string>();
        var blocking = new List<string>();
        foreach (var alt in ordered.Where(s => s.Action != winner).Take(3))
        {
            negatives.Add($"{alt.Action}:{alt.Score:0.#}");
            if (alt.Score < 0 || alt.Score + 25 < winningUtility)
                blocking.Add($"{alt.Action}:outscored");
        }

        if (ctx.Household is null && winner is ActionType.Eat or ActionType.Buy)
            blocking.Add("NoHousehold");

        return new DecisionTrace(
            WakeReason: wakeReason,
            PreviousIntent: previousIntent,
            TopPressures: topPressures,
            KnownOpportunities: opportunities,
            Winner: winner,
            WinningUtility: winningUtility,
            TopPositiveFactors: positives,
            TopNegativeFactors: negatives,
            BlockingFactors: blocking,
            KnownAlternatives: alternatives);
    }

    /// <summary>Utility de candidato dinâmico de poder:
    /// <c>Urgency×w − Cost×w − Risk×w + Reliability×w</c>.</summary>
    internal static double PowerOpportunityUtility(
        PowerOpportunity opp, DecisionContext ctx, PowerUtilityRules rules)
    {
        double urgency = PowerUrgencyOf(ctx, opp);
        double reliabilityBonus = string.Equals(opp.Reliability, "Guaranteed", StringComparison.Ordinal)
            ? 1.0
            : string.Equals(opp.Reliability, "ResolutionCheck", StringComparison.Ordinal)
                ? 0.35
                : 0.1;

        return rules.UrgencyWeight * urgency
            - rules.CostWeight * (double)opp.EstimatedCost * 10.0
            - rules.RiskWeight * opp.EstimatedRisk * 100.0
            + rules.ReliabilityWeight * reliabilityBonus * 25.0;
    }

    /// <summary>Urgência contextual: déficit máximo de needs + boost ReachDestinationUrgently
    /// para teleport/movimento quando memória de ameaça ou necessidade de deslocamento.</summary>
    internal static double PowerUrgencyOf(DecisionContext ctx, PowerOpportunity opp)
    {
        double urgency = Math.Max(
            Math.Max(Deficit(ctx.Needs.Hunger), Deficit(ctx.Needs.Thirst)),
            Math.Max(Deficit(ctx.Needs.Sleep), Deficit(ctx.Needs.Social)));

        bool reachUrgent = ctx.RelevantMemories.Any(m =>
            ContainsAny(m.Content, "traído", "traido", "betray", "threat", "perigo", "danger"));
        bool locomotion = opp.MechanicToken.StartsWith("npc.teleport", StringComparison.Ordinal)
            || opp.MechanicToken.StartsWith("movement.", StringComparison.Ordinal);

        if (reachUrgent && locomotion)
            urgency = Math.Max(urgency, 95.0);

        return urgency;
    }

    /// <summary>Fator de utility a partir do preview de foresight:
    /// desfecho ruim → fator &lt; 1; sucesso → fator ≥ 1; PartialSuccess neutro.</summary>
    internal static double ForesightUtilityFactor(ResolutionResult preview) => preview switch
    {
        ResolutionResult.CriticalSuccess => 1.5,
        ResolutionResult.Success => 1.25,
        ResolutionResult.PartialSuccess => 1.0,
        ResolutionResult.Failure => 0.35,
        ResolutionResult.CriticalFailure => 0.1,
        _ => 1.0,
    };

    /// <summary>Bônus só quando memória/crença/relação estão presentes (COH-13/14) —
    /// listas vazias → 0, comportamento idêntico ao pré-DecisionContext (golden preservado).</summary>
    private static double ContextFactorBonus(DecisionContext ctx, ActionType action)
    {
        const double factorBonus = 40.0;
        return action switch
        {
            ActionType.Travel when ctx.RelevantMemories.Any(m =>
                ContainsAny(m.Content, "traído", "traido", "betray", "threat", "perigo", "danger")) => factorBonus,
            ActionType.Buy when ctx.RelevantBeliefs.Any(b =>
                ContainsAny(b, "scarcity", "escassez", "fome", "hunger", "food")) => factorBonus,
            ActionType.Socialize when HasStrongBond(ctx.KnownRelationships) => factorBonus,
            _ => 0.0,
        };
    }

    private static bool HasStrongBond(IReadOnlyList<RelationshipFact> known) =>
        known is LazyRelationshipFacts lazy
            ? lazy.AnyBondAtLeast(60)
            : known.Any(r => r.Trust >= 60 || r.Affection >= 60);

    private static bool ContainsAny(string text, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>PWR-56..58: considera NPCs/perigo no raio Chebyshev do portador (1 = adjacência
    /// sem poder). Perigo observável = saúde ≤ 1; percepção &gt; 1 aproxima socialmente.</summary>
    private static ActionType ApplyPerception(WorldState world, Npc npc, ActionType candidate) =>
        ApplyPerceptionWithin(world, npc, candidate, AttributeMechanic.PerceptionRadius(world, npc));

    internal static ActionType ApplyPerceptionWithin(WorldState world, Npc npc, ActionType candidate, int radius)
    {
        if (radius < 0) return candidate;

        var index = world.AliveNpcIndex;
        // Sem ninguém em perigo no mundo não há ameaça a achar; com raio ≤ 1 a mera presença de outros
        // também não muda nada (só raio > 1 aproxima socialmente): o resultado é o candidato.
        bool anyEndangered = index.EndangeredCount > 0;
        if (!anyEndangered && radius <= 1) return candidate;

        bool threat = false;
        bool otherNpc = false;
        var origin = npc.CurrentLocation;
        long window = (2L * radius + 1) * (2L * radius + 1);
        if (window >= index.Alive.Count)
        {
            // Raio grande: varrer os vivos custa menos que visitar a janela inteira de células.
            foreach (var other in index.Alive)
                if (Observe(other)) break;
        }
        else
        {
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                    foreach (var other in index.At(new CellCoord(origin.X + dx, origin.Y + dy)))
                        if (Observe(other)) goto scanned;
        }
    scanned:

        // Devolve true quando o resultado já está decidido e o restante da varredura não pode mudá-lo:
        // ameaça → Travel; sem ninguém em perigo, um vizinho basta para Socializar (raio > 1).
        bool Observe(Npc other)
        {
            if (other.Id == npc.Id) return false;
            if (Chebyshev(origin, other.CurrentLocation) > radius) return false;
            otherNpc = true;
            if (other.Health <= AliveNpcIndex.EndangeredHealth)
                threat = true;
            return threat || (!anyEndangered && radius > 1);
        }

        if (threat) return ActionType.Travel;
        if (otherNpc && radius > 1) return ActionType.Socialize;
        return candidate;
    }

    private static int Chebyshev(CellCoord a, CellCoord b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private static double UtilityBaseOf(DecisionContext ctx, ActionType action, EconomyRules economy) => action switch
    {
        ActionType.Eat => EatUtilityOf(ctx, economy),
        ActionType.Sleep => SurvivalDeficit(ctx.Needs.Sleep),
        ActionType.Socialize => Deficit(ctx.Needs.Social),
        ActionType.Buy => BuyUtilityOf(ctx, economy),
        ActionType.Work or ActionType.Travel or ActionType.Idle => NonNeedBaselineUtility,
        ActionType.UsePower => 0.0, // só vence via candidatos PowerOpportunity (T22)
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "ActionType desconhecido"),
    };

    private static double EatUtilityOf(DecisionContext ctx, EconomyRules economy)
    {
        double deficit = Math.Max(SurvivalDeficit(ctx.Needs.Hunger), SurvivalDeficit(ctx.Needs.Thirst));
        if (!economy.Enabled) return deficit;
        if (ctx.Household is not { } household) return deficit;

        bool hasFood = household.Stock.GetValueOrDefault(new ResourceType(economy.FoodResourceId)) >= 1;
        bool hasWater = household.Stock.GetValueOrDefault(new ResourceType(economy.WaterResourceId)) >= 1;
        return hasFood || hasWater ? deficit : NonNeedBaselineUtility / 2;
    }

    private static double BuyUtilityOf(DecisionContext ctx, EconomyRules economy)
    {
        if (!economy.Enabled) return NonNeedBaselineUtility;
        if (ctx.Household is not { } household)
            return NonNeedBaselineUtility;

        long food = household.Stock.GetValueOrDefault(new ResourceType(economy.FoodResourceId));
        long water = household.Stock.GetValueOrDefault(new ResourceType(economy.WaterResourceId));
        bool needsFood = food < 1;
        bool needsWater = water < 1;
        double provision = ProvisionPressureOf(ctx, household, Math.Min(food, water));
        if (!needsFood && !needsWater) return Math.Max(NonNeedBaselineUtility, provision);

        double own = Math.Max(needsFood ? SurvivalDeficit(ctx.Needs.Hunger) : 0, needsWater ? SurvivalDeficit(ctx.Needs.Thirst) : 0);
        return Math.Max(own, provision);
    }

    /// <summary>Quem tem dinheiro abastece a casa antes de ela esvaziar, e não só quando sente fome
    /// (crianças sem renda dependem disso): sobe de ~55 com 2 dias de estoque por membro até 130 com
    /// a despensa vazia — acima do que o peso de personalidade dá a Work.</summary>
    private static double ProvisionPressureOf(DecisionContext ctx, HouseholdSnapshot household, long scarcestStock)
    {
        if (ctx.WalletAmount < 1) return 0;
        int members = Math.Max(1, household.Members.Count);
        double daysPerMember = (double)scarcestStock / members;
        double shortfall = 1.0 - daysPerMember / ProvisionTargetDays;
        return shortfall <= 0 ? 0 : 55 + 75 * Math.Min(1.0, shortfall);
    }

    private static int Deficit(int need) => 100 - need;

    /// <summary>Déficit linear até <see cref="CriticalDeficit"/>; acima dele cada ponto vale
    /// <c>1 + CriticalEscalation</c> — a urgência dispara quando a necessidade vira crítica.</summary>
    private static double SurvivalDeficit(int need)
    {
        int deficit = Deficit(need);
        return deficit <= CriticalDeficit ? deficit : deficit + (deficit - CriticalDeficit) * CriticalEscalation;
    }

    private static ProfessionType? NoneIfSentinel(ProfessionType profession) =>
        profession == ProfessionType.None ? null : profession;

    private void EvaluateProfessionSwitch(WorldState world, Npc npc, VacancyWeights vacancyIndex)
    {
        if (_skillsRules is not { } rules) return;
        if (world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) != LifeStage.Adult) return;

        var catalog = world.EconomyCatalog;
        if (catalog.LocationTypeByProfession.Count == 0) return;

        ProfessionType? best = null;
        double bestScore = double.NegativeInfinity;
        // Igual para todas as profissões: calculado uma vez por avaliação, não por profissão.
        double personalityWeight = PersonalityWeighting.WeightOf(npc.Personality, ActionType.Work);
        foreach (var professionId in SortedProfessionIds(catalog))
        {
            double score = ProfessionScoreOf(npc, rules, catalog, professionId, vacancyIndex, personalityWeight);
            if (score > bestScore)
            {
                bestScore = score;
                best = new ProfessionType(professionId);
            }
        }

        if (best is { } candidate && candidate != npc.Profession)
            npc.SwitchProfession(candidate);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EconomyCatalog, int[]> SortedProfessions = new();

    /// <summary>Ids de profissão do catálogo em ordem crescente (o catálogo é imutável: ordena uma vez).</summary>
    private static int[] SortedProfessionIds(EconomyCatalog catalog) =>
        SortedProfessions.GetValue(catalog, c => c.LocationTypeByProfession.Keys.OrderBy(id => id).ToArray());

    private static double ProfessionScoreOf(
        Npc npc, SkillsRules rules, EconomyCatalog catalog, int professionId, VacancyWeights vacancyIndex,
        double personalityWeight)
    {
        double skillWeight = rules.SkillByProfession.TryGetValue(professionId, out var skillType)
            ? 1.0 + npc.Skills.Get(skillType) / rules.Cap
            : 1.0;
        double vacancyWeight = VacancyWeightOf(catalog, professionId, vacancyIndex);

        return skillWeight * personalityWeight * vacancyWeight;
    }

    private static double VacancyWeightOf(EconomyCatalog catalog, int professionId, VacancyWeights vacancyIndex)
    {
        if (!catalog.LocationTypeByProfession.TryGetValue(professionId, out var locationTypeId)) return 1.0;
        return vacancyIndex.VacancyWeightForLocationType(locationTypeId);
    }

    internal static ActionType ResolveWithStepCap(
        long npcId, ActionType initial, WorldState world, Npc npc, MarketIndex marketIndex, int maxSteps)
    {
        var current = initial;
        for (int step = 0; step < maxSteps; step++)
        {
            var next = RefineForLocation(world, npc, current, marketIndex);
            if (next == current) return current;
            current = next;
        }

        var final = RefineForLocation(world, npc, current, marketIndex);
        throw new TickBudgetExceededException(
            $"npc {npcId}: seleção de ação não convergiu em {maxSteps} passos — ações empatadas em ciclo ({current} <-> {final})",
            maxSteps);
    }

    /// <summary>Overload for tests that supply a custom refine step without capturing world state.</summary>
    internal static ActionType ResolveWithStepCap(long npcId, ActionType initial, Func<ActionType, ActionType> refine, int maxSteps)
    {
        var current = initial;
        for (int step = 0; step < maxSteps; step++)
        {
            var next = refine(current);
            if (next == current) return current;
            current = next;
        }

        var final = refine(current);
        throw new TickBudgetExceededException(
            $"npc {npcId}: seleção de ação não convergiu em {maxSteps} passos — ações empatadas em ciclo ({current} <-> {final})",
            maxSteps);
    }
}
