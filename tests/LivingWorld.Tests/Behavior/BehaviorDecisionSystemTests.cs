using LivingWorld.Domain;
using LivingWorld.Domain.Llm;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Economy;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 4, task 12: <see cref="BehaviorDecisionSystem"/> — rotina diária por padrão,
/// utility AI só quando alguma necessidade supera o limiar de urgência (NEEDS-05/06/10/11),
/// desempate por menor <c>ActionId</c> (NEEDS-06), fome vence trabalho com controle (NEEDS-07)
/// e tabela dos 10 traços de personalidade (NEEDS-08).</summary>
public class BehaviorDecisionSystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly LifeStageRules Stages = LifeStageRules.Create(childMaxAge: 14, adultMaxAge: 64).Value!;

    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    /// <summary>Traços cuja ação modulada é <see cref="ActionType.Socialize"/> (tabela de
    /// influência de <see cref="PersonalityWeighting"/>) — usados para decidir qual necessidade
    /// serve de gatilho de urgência sem interferir na comparação sob teste.</summary>
    private static readonly HashSet<string> SocializeTraits =
    [
        nameof(Personality.Extroversion), nameof(Personality.Openness),
        nameof(Personality.Altruism), nameof(Personality.Agreeableness),
    ];

    /// <summary>[Traço, ação prevista em 20, ação prevista em 80] — par de ações com a mesma
    /// utilidade base sob personalidade neutra (Work/Travel/Idle = 50 fixo; Socialize = déficit
    /// de Social ajustado para empatar em 50), de forma que só a personalidade decide o vencedor
    /// (NEEDS-08). Uma linha por trait de <see cref="Personality"/> — os dois papéis de
    /// Impulsivity (Idle positivo, Work negativo) produzem o mesmo par Work×Idle, cobertos numa
    /// linha só. Quando o traço sob teste empurra sua ação abaixo do empate de 50, o vencedor é
    /// o candidato baseline restante de menor <c>ActionId</c> (Work=2 &lt; Socialize=3 &lt;
    /// Travel=4 &lt; Idle=5) — não necessariamente "Idle": o desempate por menor id (NEEDS-06)
    /// vale aqui também.</summary>
    public static readonly TheoryData<string, ActionType, ActionType> TraitPredictedActionCases = new()
    {
        { nameof(Personality.Conscientiousness), ActionType.Travel, ActionType.Work },
        { nameof(Personality.Ambition), ActionType.Travel, ActionType.Work },
        { nameof(Personality.Loyalty), ActionType.Travel, ActionType.Work },
        { nameof(Personality.Impulsivity), ActionType.Work, ActionType.Idle },
        { nameof(Personality.RiskAversion), ActionType.Travel, ActionType.Work },
        { nameof(Personality.EmotionalStability), ActionType.Idle, ActionType.Work },
        { nameof(Personality.Extroversion), ActionType.Work, ActionType.Socialize },
        { nameof(Personality.Openness), ActionType.Work, ActionType.Socialize },
        { nameof(Personality.Altruism), ActionType.Work, ActionType.Socialize },
        { nameof(Personality.Agreeableness), ActionType.Work, ActionType.Socialize },
    };

    private static NeedsRules MakeRules(int urgencyThreshold) => NeedsRules.Create(
        hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
        urgencyThreshold, maxActionSelectionSteps: 10, hysteresisEnabled: false,
        continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;

    private static ActionCatalog MakeCatalogWithOpenWorkShift() => ActionCatalog.Create(
        maxDurationHours: new Dictionary<ActionType, int>
        {
            [ActionType.Eat] = 2,
            [ActionType.Sleep] = 8,
            [ActionType.Work] = 8,
            [ActionType.Socialize] = 3,
            [ActionType.Travel] = 4,
            [ActionType.Idle] = 2,
            [ActionType.Buy] = 2,
            [ActionType.UsePower] = 1,
        },
        routineSlots: [new RoutineSlot(ProfessionId: null, Stage: LifeStage.Adult, HourStart: 0, HourEnd: 23, Action: ActionType.Work)],
        defaultAction: ActionType.Idle).Value!;

    private static Personality WithTrait(string traitName, int value)
    {
        var values = new Dictionary<string, int>
        {
            [nameof(Personality.Extroversion)] = 50,
            [nameof(Personality.Agreeableness)] = 50,
            [nameof(Personality.Conscientiousness)] = 50,
            [nameof(Personality.EmotionalStability)] = 50,
            [nameof(Personality.Openness)] = 50,
            [nameof(Personality.Ambition)] = 50,
            [nameof(Personality.Loyalty)] = 50,
            [nameof(Personality.Altruism)] = 50,
            [nameof(Personality.Impulsivity)] = 50,
            [nameof(Personality.RiskAversion)] = 50,
        };
        values[traitName] = value;

        return Personality.Create(
            values[nameof(Personality.Extroversion)], values[nameof(Personality.Agreeableness)],
            values[nameof(Personality.Conscientiousness)], values[nameof(Personality.EmotionalStability)],
            values[nameof(Personality.Openness)], values[nameof(Personality.Ambition)],
            values[nameof(Personality.Loyalty)], values[nameof(Personality.Altruism)],
            values[nameof(Personality.Impulsivity)], values[nameof(Personality.RiskAversion)]).Value!;
    }

    /// <summary>T9 (LWV-02.3): passeio ambiente de Work só é legítimo com workplace real —
    /// dá ao NPC um employer no próprio local corrente, sem exigir viagem, pra testes que só
    /// querem exercitar o passeio ambiente em si (não o deslocamento até o trabalho).</summary>
    private static void Employ(WorldState world, Npc npc)
    {
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), npc.CurrentLocation, maxVacancies: 1,
            employees: [npc.Id], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        npc.Hire(workplace.Id);
    }

    private static (WorldState World, TickContext Ctx, Npc Npc) BuildWorld(
        ulong seed, NeedsRules rules, ActionCatalog catalog, Personality personality,
        int hunger = 100, int thirst = 100, int sleep = 100, int social = 100)
    {
        var map = ScenarioRunner.DefaultMap(seed);
        var world = new WorldState(
            Calendar, seed, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            rules, catalog, Stages);
        var location = new CellCoord(1, 1);

        var npc = new Npc(
            new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: personality, profession: ProfessionType.None, currentLocation: location,
            hunger: hunger, thirst: thirst, sleep: sleep, social: social);

        world.AddNpc(npc);
        world.AdvanceNpcIdTo(2);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        SimulationWakeTestHelper.Wake(world, npc);
        return (world, ctx, npc);
    }

    [Fact]
    public void No_need_above_the_urgency_threshold_follows_the_declared_daily_routine()
    {
        var rules = MakeRules(urgencyThreshold: 70);
        var catalog = MakeCatalogWithOpenWorkShift();
        var (world, ctx, npc) = BuildWorld(seed: 1, rules, catalog, Neutral);

        new BehaviorDecisionSystem().Tick(world, ctx);

        Assert.Equal(ActionType.Work, npc.CurrentAction);
    }

    [Fact]
    public void Completing_idle_moves_one_valid_step_and_same_seed_reproduces_the_destination()
    {
        static CellCoord RunOnce()
        {
            var rules = MakeRules(urgencyThreshold: 70);
            var catalog = ActionCatalog.Create(
                new Dictionary<ActionType, int>
                {
                    [ActionType.Eat] = 2,
                    [ActionType.Sleep] = 8,
                    [ActionType.Work] = 8,
                    [ActionType.Socialize] = 3,
                    [ActionType.Travel] = 4,
                    [ActionType.Idle] = 2,
                    [ActionType.Buy] = 2,
                    [ActionType.UsePower] = 1,
                }, [], ActionType.Idle).Value!;
            var (world, ctx, npc) = BuildWorld(seed: 44, rules, catalog, Neutral);
            var before = npc.CurrentLocation;
            npc.SetCurrentAction(ActionType.Idle, tick: -2);

            new BehaviorDecisionSystem().Tick(world, ctx);

            Assert.NotEqual(before, npc.CurrentLocation);
            Assert.True(world.Map.TryGetCell(npc.CurrentLocation, out _));
            return npc.CurrentLocation;
        }

        Assert.Equal(RunOnce(), RunOnce());
    }

    [Fact]
    public void Completing_work_moves_one_valid_step_and_same_seed_reproduces_the_destination()
    {
        static CellCoord RunOnce()
        {
            var rules = MakeRules(urgencyThreshold: 70);
            var catalog = MakeCatalogWithOpenWorkShift();
            var (world, ctx, npc) = BuildWorld(seed: 81, rules, catalog, Neutral);
            Employ(world, npc); // T9 (LWV-02.3): sem employer, Work nunca fabrica passeio ambiente
            var before = npc.CurrentLocation;
            npc.SetCurrentAction(ActionType.Work, tick: -8);

            new BehaviorDecisionSystem().Tick(world, ctx);

            Assert.NotEqual(before, npc.CurrentLocation);
            Assert.True(world.Map.TryGetCell(npc.CurrentLocation, out _));
            return npc.CurrentLocation;
        }

        Assert.Equal(RunOnce(), RunOnce());
    }

    [Theory]
    [InlineData(ActionType.Idle, 2)]
    [InlineData(ActionType.Work, 8)]
    [InlineData(ActionType.Socialize, 3)]
    public void Completing_an_ambient_action_stays_inside_the_home_city_footprint(
        ActionType action, int duration)
    {
        static CellCoord RunOnce(ActionType action, int duration)
        {
            var rules = MakeRules(urgencyThreshold: 70);
            var catalog = MakeCatalogWithOpenWorkShift();
            var (world, ctx, npc) = BuildWorld(seed: 91, rules, catalog, Neutral);
            var city = new City(
                world.NextCityId(), npc.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
            world.AddCity(city);
            npc.JoinCity(city.Id);
            if (action == ActionType.Work) Employ(world, npc); // T9 (LWV-02.3): idem, Work exige employer real
            npc.SetCurrentAction(action, tick: -duration);
            var before = npc.CurrentLocation;

            new BehaviorDecisionSystem().Tick(world, ctx);

            var bounds = SpatialBoundsResolver.ResolveCity(
                city, CityPopulationQuery.Population(world, city.Id), world.Map.Width, world.Map.Height).Bounds;
            Assert.NotEqual(before, npc.CurrentLocation);
            Assert.True(bounds.Contains(npc.CurrentLocation));
            return npc.CurrentLocation;
        }

        Assert.Equal(RunOnce(action, duration), RunOnce(action, duration));
    }

    [Fact]
    public void Ambient_step_never_lands_on_a_cell_already_occupied_by_another_living_npc()
    {
        var rules = MakeRules(urgencyThreshold: 70);
        var catalog = MakeCatalogWithOpenWorkShift();
        var (world, ctx, npc) = BuildWorld(seed: 91, rules, catalog, Neutral);
        var freeCell = new CellCoord(2, 2);

        var occupiedCells = new HashSet<CellCoord>();
        var blockerId = 2L;
        foreach (var dy in new[] { -1, 0, 1 })
            foreach (var dx in new[] { -1, 0, 1 })
            {
                var cell = new CellCoord(npc.CurrentLocation.X + dx, npc.CurrentLocation.Y + dy);
                if (cell == npc.CurrentLocation || cell == freeCell) continue;

                var blocker = new Npc(
                    new NpcId(blockerId++), "blocker", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30),
                    new CultureId(1), cell, motherId: null, fatherId: null, household: null, health: 100,
                    personality: Neutral, profession: ProfessionType.None, currentLocation: cell,
                    hunger: 100, thirst: 100, sleep: 100, social: 100);
                world.AddNpc(blocker);
                occupiedCells.Add(cell);
            }
        world.AdvanceNpcIdTo(blockerId);

        npc.SetCurrentAction(ActionType.Idle, tick: -2);

        new BehaviorDecisionSystem().Tick(world, ctx);

        Assert.Equal(freeCell, npc.CurrentLocation);
        Assert.DoesNotContain(npc.CurrentLocation, occupiedCells);
    }

    [Fact]
    public void A_need_above_the_urgency_threshold_overrides_the_routine()
    {
        var rules = MakeRules(urgencyThreshold: 70);
        var catalog = MakeCatalogWithOpenWorkShift();
        var (world, ctx, npc) = BuildWorld(seed: 1, rules, catalog, Neutral, hunger: 0);

        new BehaviorDecisionSystem().Tick(world, ctx);

        Assert.Equal(ActionType.Eat, npc.CurrentAction);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    [InlineData(4u)]
    [InlineData(5u)]
    [InlineData(6u)]
    [InlineData(7u)]
    [InlineData(8u)]
    [InlineData(9u)]
    [InlineData(10u)]
    public void Hunger_beats_the_open_work_shift_with_a_control_arm_in_10_of_10_seeds(ulong seed)
    {
        var rules = MakeRules(urgencyThreshold: 70);
        var catalog = MakeCatalogWithOpenWorkShift();
        var system = new BehaviorDecisionSystem();

        // Braço "fome=90" (bem faminto): satisfação de fome baixa (déficit 90, urgente).
        var (worldHungry, ctxHungry, npcHungry) = BuildWorld(seed, rules, catalog, Neutral, hunger: 10);
        system.Tick(worldHungry, ctxHungry);
        Assert.Equal(ActionType.Eat, npcHungry.CurrentAction);

        // Braço de controle "fome=10" (quase saciado): déficit 10, abaixo do limiar — segue rotina.
        var (worldFed, ctxFed, npcFed) = BuildWorld(seed, rules, catalog, Neutral, hunger: 90);
        system.Tick(worldFed, ctxFed);
        Assert.Equal(ActionType.Work, npcFed.CurrentAction);
    }

    [Fact]
    public void Exact_utility_tie_breaks_by_the_smaller_ActionId()
    {
        var rules = MakeRules(urgencyThreshold: 40);
        var catalog = MakeCatalogWithOpenWorkShift();
        // Déficit de Social = 50 (> limiar 40, urgente) empata com a utilidade base fixa de
        // Work/Travel/Idle (50) sob personalidade neutra — Work (ActionId=2) é o menor id entre
        // os quatro empatados (Work, Socialize, Travel, Idle).
        var (world, ctx, npc) = BuildWorld(seed: 1, rules, catalog, Neutral, social: 50);

        new BehaviorDecisionSystem().Tick(world, ctx);

        Assert.Equal(ActionType.Work, npc.CurrentAction);
    }

    [Fact]
    public void Every_personality_trait_has_a_predicted_action_case()
    {
        var covered = TraitPredictedActionCases.Select(row => (string)row[0]!).ToHashSet();
        Assert.Equal(10, PersonalityWeighting.AllTraitNames.Count);
        foreach (var trait in PersonalityWeighting.AllTraitNames)
            Assert.Contains(trait, covered);
    }

    [Theory]
    [MemberData(nameof(TraitPredictedActionCases))]
    public void Trait_at_20_vs_80_flips_the_predicted_action_in_10_of_10_seeds(
        string trait, ActionType lowAction, ActionType highAction)
    {
        bool isSocializeTrait = SocializeTraits.Contains(trait);
        var rules = MakeRules(urgencyThreshold: 40);
        var catalog = MakeCatalogWithOpenWorkShift();
        int social = isSocializeTrait ? 50 : 100; // déficit 50 (gatilho + concorrente de Socialize)
        int sleep = isSocializeTrait ? 100 : 55; // déficit 45 (gatilho que não interfere no par sob teste)

        for (ulong seed = 1; seed <= 10; seed++)
        {
            var low = WithTrait(trait, 20);
            var (worldLow, ctxLow, npcLow) = BuildWorld(seed, rules, catalog, low, social: social, sleep: sleep);
            new BehaviorDecisionSystem().Tick(worldLow, ctxLow);
            Assert.Equal(lowAction, npcLow.CurrentAction);

            var high = WithTrait(trait, 80);
            var (worldHigh, ctxHigh, npcHigh) = BuildWorld(seed, rules, catalog, high, social: social, sleep: sleep);
            new BehaviorDecisionSystem().Tick(worldHigh, ctxHigh);
            Assert.Equal(highAction, npcHigh.CurrentAction);
        }
    }

    [Fact]
    public void Teleport_power_with_reach_urgency_chooses_UsePower_and_sets_Pending()
    {
        var rules = MakeRules(urgencyThreshold: 70);
        var catalog = MakeCatalogWithOpenWorkShift();
        var descriptor = new PowerDescriptor(
            "teleport-power", "test", ["npc.teleport:elsewhere"], "Active", [], "Guaranteed",
            [], [], [], []);
        var carrier = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);

        var map = ScenarioRunner.DefaultMap(11);
        var world = new WorldState(
            Calendar, 11, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            rules, catalog, Stages,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]),
            extraordinaryCarriers: [carrier]);
        var location = new CellCoord(1, 1);
        var npc = new Npc(
            new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: ProfessionType.None, currentLocation: location,
            hunger: 20, thirst: 100, sleep: 100, social: 100);
        world.AddNpc(npc);
        world.AdvanceNpcIdTo(2);
        world.AddNpcMemory(
            npc.Id, MemoryCategory.Social, "foi traído por X na colheita", importance: 90, originTick: 1,
            participants: [npc.Id], location: npc.CurrentLocation,
            canonicalImportanceThreshold: LlmRules.Default.CanonicalMemoryImportanceThreshold);

        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        SimulationWakeTestHelper.Wake(world, npc);
        new BehaviorDecisionSystem().Tick(world, ctx);

        Assert.Equal(ActionType.UsePower, npc.CurrentAction);
        Assert.NotNull(npc.PendingPowerInvocation);
        Assert.Equal("teleport-power", npc.PendingPowerInvocation!.PowerId);
        Assert.Equal("npc.teleport:elsewhere", npc.PendingPowerInvocation.MechanicToken);
    }

    [Fact]
    public void Without_power_capability_never_chooses_UsePower_under_same_pressure()
    {
        var rules = MakeRules(urgencyThreshold: 70);
        var catalog = MakeCatalogWithOpenWorkShift();
        var (world, ctx, npc) = BuildWorld(seed: 11, rules, catalog, Neutral, hunger: 20);
        world.AddNpcMemory(
            npc.Id, MemoryCategory.Social, "foi traído por X na colheita", importance: 90, originTick: 1,
            participants: [npc.Id], location: npc.CurrentLocation,
            canonicalImportanceThreshold: LlmRules.Default.CanonicalMemoryImportanceThreshold);

        new BehaviorDecisionSystem().Tick(world, ctx);

        Assert.NotEqual(ActionType.UsePower, npc.CurrentAction);
        Assert.Null(npc.PendingPowerInvocation);
        Assert.Equal(ActionType.Travel, npc.CurrentAction);
    }

    [Fact]
    public void PowerOpportunityUtility_is_deterministic_for_same_inputs()
    {
        var opp = new PowerOpportunity("p", "npc.teleport:x", null, 0m, 0.1, "Guaranteed");
        var ctx = new DecisionContext(
            new NpcId(1), 0,
            new NeedsSnapshot(20, 100, 100, 100),
            new BodySnapshot(1.7, 68, 28, 1, 1),
            null,
            [new NpcMemory(1, new NpcId(1), MemoryCategory.Social, "threat nearby", 90, 1, Array.Empty<NpcId>(), new CellCoord(0, 0))],
            Array.Empty<string>(),
            Array.Empty<RelationshipFact>(),
            [opp],
            Neutral,
            null);

        double a = BehaviorDecisionSystem.PowerOpportunityUtility(opp, ctx, PowerUtilityRules.Default);
        double b = BehaviorDecisionSystem.PowerOpportunityUtility(opp, ctx, PowerUtilityRules.Default);
        Assert.Equal(a, b);
        Assert.True(a > 50);
    }

    [Fact]
    public void Completing_UsePower_invokes_engine_logs_PowerInvoked_with_CauseEventId_and_clears_Pending()
    {
        var rules = MakeRules(urgencyThreshold: 70);
        var catalog = MakeCatalogWithOpenWorkShift();
        var descriptor = new PowerDescriptor(
            "curse-power", "test", ["luck.curse:1:10"], "Active", [], "Guaranteed",
            [], [], [], []);
        var carrier = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);

        var map = ScenarioRunner.DefaultMap(33);
        var world = new WorldState(
            Calendar, 33, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            rules, catalog, Stages,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]),
            extraordinaryCarriers: [carrier]);
        var location = new CellCoord(1, 1);
        var npc = new Npc(
            new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: ProfessionType.None, currentLocation: location,
            hunger: 100, thirst: 100, sleep: 100, social: 100);
        world.AddNpc(npc);
        world.AdvanceNpcIdTo(2);

        npc.SetCurrentAction(ActionType.UsePower, tick: 0);
        npc.PendingPowerInvocation = new PendingPowerInvocation(
            "curse-power", "luck.curse:1:10", SuggestedTarget: null);

        var sink = new ListWorldEventSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        // Avança além da duração (UsePower=1) para completar a ação.
        world.CurrentDate = WorldDate.Epoch(Calendar).AddHours(catalog.MaxDurationHours[ActionType.UsePower]);
        SimulationWakeTestHelper.Wake(world, npc);
        new BehaviorDecisionSystem().Tick(world, ctx);

        Assert.Null(npc.PendingPowerInvocation);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.PowerInvoked && e.CauseEventId is not null);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.ExtraordinaryUseAttempted);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.ExtraordinaryEffectApplied);

        var powerInvoked = sink.Events.First(e =>
            e.Kind == WorldEventKind.PowerInvoked && e.CauseEventId is not null);
        Assert.Contains(sink.Events, e =>
            e.Kind == WorldEventKind.ExtraordinaryUseAttempted
            && e.CauseEventId == powerInvoked.EventId);
    }

    // --- Fase 16.3 T26 (COH-42): plan alternatives before Invalidated ---

    private static EconomyRules EnabledFoodEconomy() => EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long>(),
        priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0,
        demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    [Fact]
    public void Buy_unavailable_uses_household_stock_before_invalidating_Intent()
    {
        var economy = EnabledFoodEconomy();
        var map = ScenarioRunner.DefaultMap(7);
        var world = new WorldState(
            Calendar, 7, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            MakeRules(urgencyThreshold: 70), MakeCatalogWithOpenWorkShift(), Stages,
            economyRules: economy);
        var loc = new CellCoord(1, 1);
        var food = new ResourceType(1);
        var npc = new Npc(
            new NpcId(1), "buyer", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), loc,
            null, null, household: new HouseholdId(1), health: 100, Neutral, ProfessionType.None, loc,
            hunger: 0, thirst: 100);
        var household = new Household(
            new HouseholdId(1), loc, npc.Id, [npc.Id],
            stock: new Dictionary<ResourceType, long> { [food] = 3 });
        world.AddHousehold(household);
        world.AddNpc(npc);
        npc.SetIntent(ActionType.Buy, tick: 0);
        var markets = MarketIndex.BuildForTick(world);

        var status = BehaviorDecisionSystem.ResolveFoodPlan(world, npc, markets, tick: 1, ActionType.Buy);

        Assert.Equal(100, npc.Hunger);
        Assert.Equal(2, household.Stock[food]);
        Assert.Equal(IntentStatus.Completed, status);
        Assert.NotEqual(IntentStatus.Invalidated, npc.IntentStatus);
    }

    [Fact]
    public void Buy_and_household_alternatives_fail_invalidates_Intent()
    {
        var economy = EnabledFoodEconomy();
        var map = ScenarioRunner.DefaultMap(8);
        var world = new WorldState(
            Calendar, 8, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            MakeRules(urgencyThreshold: 70), MakeCatalogWithOpenWorkShift(), Stages,
            economyRules: economy);
        var loc = new CellCoord(1, 1);
        var npc = new Npc(
            new NpcId(1), "buyer", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), loc,
            null, null, household: new HouseholdId(1), health: 100, Neutral, ProfessionType.None, loc,
            hunger: 0, thirst: 100);
        var household = new Household(
            new HouseholdId(1), loc, npc.Id, [npc.Id],
            stock: new Dictionary<ResourceType, long>());
        world.AddHousehold(household);
        world.AddNpc(npc);
        npc.SetIntent(ActionType.Buy, tick: 0);
        var markets = MarketIndex.BuildForTick(world);

        var status = BehaviorDecisionSystem.ResolveFoodPlan(world, npc, markets, tick: 1, ActionType.Buy);

        Assert.Equal(0, npc.Hunger);
        Assert.Equal(IntentStatus.Invalidated, status);
        Assert.Equal(IntentStatus.Invalidated, npc.IntentStatus);
    }

    [Fact]
    public void Successful_Buy_completes_Intent()
    {
        var economy = EnabledFoodEconomy();
        var catalog = new EconomyCatalog(new Dictionary<int, ProductionRecipe>(), [1], new Dictionary<int, int>());
        var map = ScenarioRunner.DefaultMap(9);
        var world = new WorldState(
            Calendar, 9, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            MakeRules(urgencyThreshold: 70), MakeCatalogWithOpenWorkShift(), Stages,
            economyRules: economy, economyCatalog: catalog);
        var loc = new CellCoord(1, 1);
        var food = new ResourceType(1);
        var market = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), loc, maxVacancies: 0,
            employees: [], stock: new Dictionary<ResourceType, long> { [food] = 50 },
            treasury: Money.Zero, prices: new Dictionary<ResourceType, long> { [food] = 5 });
        world.AddWorkplace(market);
        var npc = new Npc(
            new NpcId(1), "buyer", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), loc,
            null, null, household: new HouseholdId(1), health: 100, Neutral, ProfessionType.None, loc,
            hunger: 0, thirst: 100);
        npc.CreditWallet(new Money(100));
        var household = new Household(new HouseholdId(1), loc, npc.Id, [npc.Id]);
        world.AddHousehold(household);
        world.AddNpc(npc);
        npc.SetIntent(ActionType.Buy, tick: 0);
        var markets = MarketIndex.BuildForTick(world);

        var status = BehaviorDecisionSystem.ResolveFoodPlan(world, npc, markets, tick: 1, ActionType.Buy);

        Assert.Equal(10, household.Stock.GetValueOrDefault(food));
        Assert.Equal(IntentStatus.Completed, status);
        Assert.Equal(IntentStatus.Completed, npc.IntentStatus);
    }

    private sealed class ListWorldEventSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
