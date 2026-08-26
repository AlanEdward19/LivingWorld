using LivingWorld.Domain;
using LivingWorld.Simulation.Behavior;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation;

/// <summary>Escolhe a ação do tick de cada NPC vivo (Fase 4 — NEEDS-05..16): rotina diária por
/// padrão, utility AI só quando alguma necessidade supera o limiar de urgência do cenário, com
/// bônus de continuidade (histerese), conclusão de ação ao atingir a duração máxima declarada, e
/// deslocamento com custo real quando a ação vencedora exige um local diferente do atual.
/// <c>Sleep</c> usa um lugar de descanso alcançável (chão/moradia/cama, T12); sem caminho o NPC
/// bloqueia no local atual em vez de teleportar.</summary>
public sealed class BehaviorDecisionSystem : ISimulationSystem
{
    public const string SystemName = "behavior-decision";

    private const double NonNeedBaselineUtility = 50.0;

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
        var vacancyIndex = VacancyIndex.BuildForTick(world);
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
        // resultado é idêntico ao recalculado por chamada (PERF-06/07).
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
                        $"{npc.Id.Value}|possessed-action|{chosen}");
                }
            }
            else
            {
                var continuityAction = justCompleted ? null : npc.CurrentAction;

                var stage = world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate));
                var routineAction = catalog.RoutineOf(NoneIfSentinel(npc.Profession), stage, world.CurrentDate.Hour);

                PendingPowerInvocation? pendingPower = null;
                ActionType candidate;
                if (npc.HasUrgentNeed(rules, now))
                {
                    var decision = SelectByUtility(
                        DecisionContextBuilder.Build(world, npc, now),
                        rules,
                        world.EconomyRules,
                        continuityAction);
                    candidate = decision.Action;
                    pendingPower = decision.PendingPower;
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

        // LWV-02.3 (T9): sem workplace real (Employer nulo), Work nunca fabrica deslocamento —
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
    /// <see cref="Tick"/> (PERF-06/07).</summary>
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
        // dynamic-city-growth, T4b: ResolveGrownBounds realimenta os boxes de overflow das
        // próprias buildings da cidade, então o passeio ambiente já respeita os bounds crescidos
        // (CITYGROW-03/05) em vez do teto só-população de antes.
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

        var allNeighbors = Enumerable.Range(-1, 3)
            .SelectMany(dy => Enumerable.Range(-1, 3).Select(dx => new CellCoord(
                npc.CurrentLocation.X + dx,
                npc.CurrentLocation.Y + dy)))
            .Where(cell => cell != npc.CurrentLocation && world.Map.TryGetCell(cell, out _))
            .Where(cell => !world.IsExtraordinaryConstructCell(cell))
            .Where(cell => homeBounds is null || homeBounds.Value.Contains(cell))
            .OrderBy(cell => cell.Y)
            .ThenBy(cell => cell.X)
            .ToList();

        // Passeio ambiente é a única fonte de posições "livres" (sem destino declarado como
        // household/workplace, que legitimamente hospedam vários NPCs na mesma célula) — por
        // isso preferimos vizinhos desocupados aqui (LIVE-POLISH: NPCs se empilhando
        // visualmente). Em população densa isso pode esvaziar TODOS os 8 vizinhos de uma vez —
        // o NPC precisa continuar andando em vez de travar, então cai pra "qualquer vizinho
        // válido" quando não sobra nenhum livre.
        var candidates = allNeighbors.Where(cell => !occupancy.Contains(cell)).ToList();
        if (candidates.Count == 0) candidates = allNeighbors;
        if (candidates.Count == 0) return;

        var rng = ctx.Rng($"ambient-{action}-{npc.Id.Value}");
        int index = Math.Min((int)(rng.NextDouble() * candidates.Count), candidates.Count - 1);
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
        var market = marketIndex.NearestTo(npc.CurrentLocation);
        if (market is null) return false; // vendedor indisponível
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household)
            return false;

        var econ = world.EconomyRules;
        bool bought = false;
        foreach (var resourceId in new[] { econ.FoodResourceId, econ.WaterResourceId })
        {
            var resource = new ResourceType(resourceId);
            if (!market.Prices.TryGetValue(resource, out var unitPrice) || unitPrice <= 0) continue;

            const long quantity = 10;
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
        marketIndex.NearestTo(npc.CurrentLocation)?.Location;

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

    private readonly record struct UtilityDecision(ActionType Action, PendingPowerInvocation? PendingPower);

    private static UtilityDecision SelectByUtility(
        DecisionContext ctx, NeedsRules rules, EconomyRules economy, ActionType? continuityAction,
        PowerUtilityRules? powerRules = null)
    {
        var utilityRules = PowerUtilityRules.Resolve(powerRules);
        var best = ActionType.Eat;
        double bestScore = double.NegativeInfinity;
        PendingPowerInvocation? bestPending = null;

        foreach (var action in AllActions)
        {
            double score = UtilityBaseOf(ctx, action, economy) * PersonalityWeighting.WeightOf(ctx.Personality, action)
                + ContextFactorBonus(ctx, action);
            if (rules.HysteresisEnabled && continuityAction == action)
                score += rules.ContinuityBonus;

            if (score > bestScore)
            {
                bestScore = score;
                best = action;
                bestPending = null;
            }
        }

        foreach (var opp in ctx.PowerOpportunities)
        {
            double score = PowerOpportunityUtility(opp, ctx, utilityRules);
            if (score > bestScore)
            {
                bestScore = score;
                best = ActionType.UsePower;
                bestPending = new PendingPowerInvocation(
                    opp.PowerId, opp.MechanicToken, opp.SuggestedTarget);
            }
        }

        return new UtilityDecision(best, bestPending);
    }

    /// <summary>Utility de candidato dinâmico de poder (COH-33 / AD-012):
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
            ActionType.Socialize when ctx.KnownRelationships.Any(r => r.Trust >= 60 || r.Affection >= 60) => factorBonus,
            _ => 0.0,
        };
    }

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
    private static ActionType ApplyPerception(WorldState world, Npc npc, ActionType candidate)
    {
        int radius = AttributeMechanic.PerceptionRadius(world, npc);
        bool threat = false;
        bool otherNpc = false;
        foreach (var other in world.Npcs)
        {
            if (!other.IsAlive || other.Id == npc.Id) continue;
            if (Chebyshev(npc.CurrentLocation, other.CurrentLocation) > radius) continue;
            otherNpc = true;
            if (other.Health <= 1)
                threat = true;
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
        ActionType.Sleep => Deficit(ctx.Needs.Sleep),
        ActionType.Socialize => Deficit(ctx.Needs.Social),
        ActionType.Buy => BuyUtilityOf(ctx, economy),
        ActionType.Work or ActionType.Travel or ActionType.Idle => NonNeedBaselineUtility,
        ActionType.UsePower => 0.0, // só vence via candidatos PowerOpportunity (T22)
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "ActionType desconhecido"),
    };

    private static double EatUtilityOf(DecisionContext ctx, EconomyRules economy)
    {
        double deficit = Math.Max(Deficit(ctx.Needs.Hunger), Deficit(ctx.Needs.Thirst));
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

        bool needsFood = household.Stock.GetValueOrDefault(new ResourceType(economy.FoodResourceId)) < 1;
        bool needsWater = household.Stock.GetValueOrDefault(new ResourceType(economy.WaterResourceId)) < 1;
        if (!needsFood && !needsWater) return NonNeedBaselineUtility;

        return Math.Max(needsFood ? Deficit(ctx.Needs.Hunger) : 0, needsWater ? Deficit(ctx.Needs.Thirst) : 0);
    }

    private static int Deficit(int need) => 100 - need;

    private static ProfessionType? NoneIfSentinel(ProfessionType profession) =>
        profession == ProfessionType.None ? null : profession;

    private void EvaluateProfessionSwitch(WorldState world, Npc npc, VacancyIndex vacancyIndex)
    {
        if (_skillsRules is not { } rules) return;
        if (world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) != LifeStage.Adult) return;

        var catalog = world.EconomyCatalog;
        if (catalog.LocationTypeByProfession.Count == 0) return;

        ProfessionType? best = null;
        double bestScore = double.NegativeInfinity;
        foreach (var professionId in catalog.LocationTypeByProfession.Keys.OrderBy(id => id))
        {
            double score = ProfessionScoreOf(world, npc, rules, catalog, professionId, vacancyIndex);
            if (score > bestScore)
            {
                bestScore = score;
                best = new ProfessionType(professionId);
            }
        }

        if (best is { } candidate && candidate != npc.Profession)
            npc.SwitchProfession(candidate);
    }

    private static double ProfessionScoreOf(
        WorldState world, Npc npc, SkillsRules rules, EconomyCatalog catalog, int professionId, VacancyIndex vacancyIndex)
    {
        double skillWeight = rules.SkillByProfession.TryGetValue(professionId, out var skillType)
            ? 1.0 + npc.Skills.Get(skillType) / rules.Cap
            : 1.0;
        double personalityWeight = PersonalityWeighting.WeightOf(npc.Personality, ActionType.Work);
        double vacancyWeight = VacancyWeightOf(catalog, professionId, vacancyIndex);

        return skillWeight * personalityWeight * vacancyWeight;
    }

    private static double VacancyWeightOf(EconomyCatalog catalog, int professionId, VacancyIndex vacancyIndex)
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
