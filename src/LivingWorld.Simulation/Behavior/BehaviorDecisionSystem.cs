using LivingWorld.Domain;
using LivingWorld.Simulation.Behavior;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Simulation;

/// <summary>Escolhe a ação do tick de cada NPC vivo (Fase 4 — NEEDS-05..16): rotina diária por
/// padrão, utility AI só quando alguma necessidade supera o limiar de urgência do cenário, com
/// bônus de continuidade (histerese), conclusão de ação ao atingir a duração máxima declarada, e
/// deslocamento com custo real quando a ação vencedora exige um local diferente do atual (hoje,
/// só <c>Sleep</c> tem exigência de local — dormir em <see cref="Household"/>.Location; sem-teto
/// dorme onde está, NEEDS-15).</summary>
public sealed class BehaviorDecisionSystem : ISimulationSystem
{
    public const string SystemName = "behavior-decision";

    private const double NonNeedBaselineUtility = 50.0;

    private static readonly ActionType[] AllActions = Enum.GetValues<ActionType>();

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

        foreach (var npc in targets)
        {
            EvaluateProfessionSwitch(world, npc, vacancyIndex);

            bool justCompleted = TryCompleteAction(world, npc, rules, catalog, now, marketIndex, ctx);
            var continuityAction = justCompleted ? null : npc.CurrentAction;

            var stage = world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate));
            var routineAction = catalog.RoutineOf(NoneIfSentinel(npc.Profession), stage, world.CurrentDate.Hour);

            var candidate = npc.HasUrgentNeed(rules, now)
                ? SelectByUtility(world, npc, rules, continuityAction, now)
                : routineAction;

            var chosen = ResolveWithStepCap(npc.Id.Value, candidate, world, npc, marketIndex, rules.MaxActionSelectionSteps);

            if (justCompleted || chosen != npc.CurrentAction)
                npc.SetCurrentAction(chosen, now);

            NpcWakeScheduler.RescheduleAfterHour(world, ctx, npc, rules, catalog, now);
        }
    }

    internal static IEnumerable<Npc> TargetsForTick(WorldState world) => world.NpcWakeBatch;

    private static bool TryCompleteAction(
        WorldState world, Npc npc, NeedsRules rules, ActionCatalog catalog, long now,
        MarketIndex marketIndex, TickContext ctx)
    {
        if (npc.CurrentAction is not { } action) return false;

        if (action == ActionType.Travel && TravelDestinationOf(world, npc, marketIndex) is { } destination && destination != npc.CurrentLocation)
        {
            long ticksNeeded = TravelResolution.TicksBetween(world.Map, npc.CurrentLocation, destination);
            if (now - npc.ActionStartedAtTick < ticksNeeded) return false;

            npc.MoveTo(destination, now);
            return true;
        }

        if (now - npc.ActionStartedAtTick < catalog.MaxDurationHours[action]) return false;

        if (action is ActionType.Idle or ActionType.Work or ActionType.Socialize)
            MoveOneAmbientStep(world, npc, ctx, now, action);

        ApplyActionEffect(world, npc, rules, action, marketIndex, now);
        return true;
    }

    private static void MoveOneAmbientStep(
        WorldState world, Npc npc, TickContext ctx, long tick, ActionType action)
    {
        CityBounds? homeBounds = world.FindCity(npc.City) is { } city
            ? SpatialBoundsResolver.ResolveCity(
                city, CityPopulationQuery.Population(world, city.Id), world.Map.Width, world.Map.Height).Bounds
            : null;
        var candidates = Enumerable.Range(-1, 3)
            .SelectMany(dy => Enumerable.Range(-1, 3).Select(dx => new CellCoord(
                npc.CurrentLocation.X + dx,
                npc.CurrentLocation.Y + dy)))
            .Where(cell => cell != npc.CurrentLocation && world.Map.TryGetCell(cell, out _))
            .Where(cell => homeBounds is null || homeBounds.Value.Contains(cell))
            .OrderBy(cell => cell.Y)
            .ThenBy(cell => cell.X)
            .ToList();
        if (candidates.Count == 0) return;

        var rng = ctx.Rng($"ambient-{action}-{npc.Id.Value}");
        int index = Math.Min((int)(rng.NextDouble() * candidates.Count), candidates.Count - 1);
        npc.MoveTo(candidates[index], tick);
    }

    private static void ApplyActionEffect(WorldState world, Npc npc, NeedsRules rules, ActionType action, MarketIndex marketIndex, long tick)
    {
        switch (action)
        {
            case ActionType.Eat:
                ApplyEat(world, npc, tick);
                break;
            case ActionType.Sleep:
                npc.SetSleep(npc.HomelessSince is null ? 100 : (int)(100 * rules.HomelessSleepEfficiency), tick);
                break;
            case ActionType.Socialize:
                npc.SetSocial(100, tick);
                break;
            case ActionType.Buy:
                ApplyBuy(world, npc, marketIndex);
                break;
        }
    }

    private static void ApplyEat(WorldState world, Npc npc, long tick)
    {
        var econ = world.EconomyRules;
        if (!econ.Enabled)
        {
            npc.SetHunger(100, tick);
            npc.SetThirst(100, tick);
            return;
        }

        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household) return;

        var food = new ResourceType(econ.FoodResourceId);
        if (household.Withdraw(food, 1).IsSuccess)
        {
            npc.SetHunger(100, tick);
            world.RecordResourceConsumed(food, 1);
        }

        var water = new ResourceType(econ.WaterResourceId);
        if (household.Withdraw(water, 1).IsSuccess)
        {
            npc.SetThirst(100, tick);
            world.RecordResourceConsumed(water, 1);
        }
    }

    private static void ApplyBuy(WorldState world, Npc npc, MarketIndex marketIndex)
    {
        if (!world.EconomyRules.Enabled) return;
        var market = marketIndex.NearestTo(npc.CurrentLocation);
        if (market is null) return;
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household) return;

        var econ = world.EconomyRules;
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
        }
    }

    private static ActionType RefineForLocation(WorldState world, Npc npc, ActionType candidate, MarketIndex marketIndex) => candidate switch
    {
        ActionType.Sleep when SleepDestinationOf(world, npc) is { } dest && dest != npc.CurrentLocation => ActionType.Travel,
        ActionType.Buy when BuyDestinationOf(world, npc, marketIndex) is { } dest && dest != npc.CurrentLocation => ActionType.Travel,
        _ => candidate,
    };

    private static CellCoord? SleepDestinationOf(WorldState world, Npc npc) =>
        npc.Household is { } householdId ? world.FindHousehold(householdId)?.Location : null;

    private static CellCoord? BuyDestinationOf(WorldState world, Npc npc, MarketIndex marketIndex) =>
        marketIndex.NearestTo(npc.CurrentLocation)?.Location;

    private static CellCoord? TravelDestinationOf(WorldState world, Npc npc, MarketIndex marketIndex) =>
        SleepDestinationOf(world, npc) is { } sleepDest && sleepDest != npc.CurrentLocation
            ? sleepDest
            : BuyDestinationOf(world, npc, marketIndex);

    private static ActionType SelectByUtility(WorldState world, Npc npc, NeedsRules rules, ActionType? continuityAction, long tick)
    {
        var best = ActionType.Eat;
        double bestScore = double.NegativeInfinity;

        foreach (var action in AllActions)
        {
            double score = UtilityBaseOf(world, action, npc, tick) * PersonalityWeighting.WeightOf(npc.Personality, action);
            if (rules.HysteresisEnabled && continuityAction == action)
                score += rules.ContinuityBonus;

            if (score > bestScore)
            {
                bestScore = score;
                best = action;
            }
        }

        return best;
    }

    private static double UtilityBaseOf(WorldState world, ActionType action, Npc npc, long tick) => action switch
    {
        ActionType.Eat => EatUtilityOf(world, npc, tick),
        ActionType.Sleep => Deficit(npc.SleepAt(tick)),
        ActionType.Socialize => Deficit(npc.SocialAt(tick)),
        ActionType.Buy => BuyUtilityOf(world, npc, tick),
        ActionType.Work or ActionType.Travel or ActionType.Idle => NonNeedBaselineUtility,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "ActionType desconhecido"),
    };

    private static double EatUtilityOf(WorldState world, Npc npc, long tick)
    {
        double deficit = Math.Max(Deficit(npc.HungerAt(tick)), Deficit(npc.ThirstAt(tick)));
        if (!world.EconomyRules.Enabled) return deficit;
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household) return deficit;

        var econ = world.EconomyRules;
        bool hasFood = household.Stock.GetValueOrDefault(new ResourceType(econ.FoodResourceId)) >= 1;
        bool hasWater = household.Stock.GetValueOrDefault(new ResourceType(econ.WaterResourceId)) >= 1;
        return hasFood || hasWater ? deficit : NonNeedBaselineUtility / 2;
    }

    private static double BuyUtilityOf(WorldState world, Npc npc, long tick)
    {
        if (!world.EconomyRules.Enabled) return NonNeedBaselineUtility;
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household)
            return NonNeedBaselineUtility;

        var econ = world.EconomyRules;
        bool needsFood = household.Stock.GetValueOrDefault(new ResourceType(econ.FoodResourceId)) < 1;
        bool needsWater = household.Stock.GetValueOrDefault(new ResourceType(econ.WaterResourceId)) < 1;
        if (!needsFood && !needsWater) return NonNeedBaselineUtility;

        return Math.Max(needsFood ? Deficit(npc.HungerAt(tick)) : 0, needsWater ? Deficit(npc.ThirstAt(tick)) : 0);
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
