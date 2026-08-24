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
            EvaluateProfessionSwitch(world, npc, vacancyIndex);

            bool justCompleted = TryCompleteAction(
                world, npc, rules, catalog, now, marketIndex, ctx, occupancy,
                cityPopulationCache, cityBoundsCache);
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

            long ticksNeeded = TravelResolution.TicksBetween(world.Map, npc.CurrentLocation, destination);
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

        ApplyActionEffect(world, npc, action, marketIndex, now);
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

    private static void ApplyActionEffect(WorldState world, Npc npc, ActionType action, MarketIndex marketIndex, long tick)
    {
        switch (action)
        {
            case ActionType.Eat:
                ApplyEat(world, npc, tick);
                break;
            case ActionType.Sleep:
                ApplySleep(world, npc, tick);
                break;
            case ActionType.Socialize:
                npc.SetSocial(100, tick);
                break;
            case ActionType.Buy:
                ApplyBuy(world, npc, marketIndex);
                break;
        }
    }

    private static void ApplySleep(WorldState world, Npc npc, long tick)
    {
        var rest = RestPlaceResolver.Resolve(world, npc);
        if (rest.Location != npc.CurrentLocation) return;

        npc.SetSleep((int)(100 * rest.RecoveryEfficiency), tick);
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

        var meal = FoodResolver.ResolveMeal(world, household);
        if (meal.Id > 0 && household.Withdraw(meal, 1).IsSuccess)
        {
            npc.SetHunger(100, tick);
            world.RecordResourceConsumed(meal, 1);
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
