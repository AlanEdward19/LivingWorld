using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Ontogeny;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Ontogeny;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Ontogeny;

/// <summary>Fase 21a (ontogeny-model): marcos como dado, filtro de elegibilidade antes da utility, choro pela fome, estado por NPC.</summary>
public class OntogenyModelTests
{
    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
    private static readonly ActionType[] InfantActions = [ActionType.Cry, ActionType.Eat, ActionType.Sleep, ActionType.Idle];
    private const long TenYearsInHours = 10L * 360 * 24;

    private static NeedsRules UrgentRules() => NeedsRules.Create(
        hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
        urgencyThreshold: 70, maxActionSelectionSteps: 10, hysteresisEnabled: false,
        continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;

    private static EconomyRules EnabledEconomy() => EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(), spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(), priceFloor: new Dictionary<int, long>(), priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static DecisionContext Ctx(HouseholdSnapshot? household, int hunger = 5) => new(
        new NpcId(1), Tick: 4, new NeedsSnapshot(Hunger: hunger, Thirst: 90, Sleep: 80, Social: 5),
        new BodySnapshot(1.0, 10, 5, 1.0, 1.0), household, RelevantMemories: [], RelevantBeliefs: [], KnownRelationships: [],
        PowerOpportunities: [], Neutral, CurrentAction: null);

    private static LivingWorld.Simulation.Behavior.Decision.ActionEligibility For(DevelopmentState state) =>
        OntogenyMechanic.EligibilityFor(OntogenyDefaults.Human, state);

    private static HouseholdSnapshot Household(long food, long water) => new(
        new HouseholdId(1),
        new Dictionary<ResourceType, long> { [new ResourceType(1)] = food, [new ResourceType(2)] = water }, []);

    // --- Critério 1: toda ação declara o marco que exige (reflexão) ---

    [Fact]
    public void Create_fails_naming_each_action_that_lacks_a_declared_milestone()
    {
        foreach (var action in Enum.GetValues<ActionType>())
        {
            var requirements = OntogenyDefaults.HumanRequirements.Where(kv => kv.Key != action).ToDictionary(kv => kv.Key, kv => kv.Value);
            var result = OntogenyRules.Create(true, OntogenyDefaults.HumanMilestones, requirements);

            Assert.False(result.IsSuccess, $"{action} sem requisito deveria reprovar");
            Assert.Contains(action.ToString(), result.Error);
        }
    }

    [Fact]
    public void Create_rejects_unknown_prerequisite_cycle_bad_window_and_empty_axis()
    {
        var req = OntogenyDefaults.HumanRequirements;
        var ms = OntogenyDefaults.HumanMilestones.ToList();

        var unknown = ms.Select(m => m.Id == "sit" ? m with { Requires = ["nope"] } : m).ToList();
        Assert.Contains("nope", OntogenyRules.Create(true, unknown, req).Error);

        var cycle = ms.Select(m => m.Id == "hold-head" ? m with { Requires = ["sit"] } : m).ToList();
        Assert.Contains("ciclo", OntogenyRules.Create(true, cycle, req).Error);

        var window = ms.Select(m => m.Id == "walk" ? m with { WindowMedianYears = 0.1 } : m).ToList();
        Assert.Contains("walk", OntogenyRules.Create(true, window, req).Error);

        var noAxis = ms.Where(m => m.Axis != DevelopmentAxis.Language).ToList();
        Assert.Contains("Language", OntogenyRules.Create(true, noAxis, req).Error);
    }

    // --- Critério 2: recém-nascido, Cry pela fome ---

    [Fact]
    public void Newborn_has_zero_progress_and_only_cry_eat_sleep_idle_are_eligible()
    {
        var rules = OntogenyDefaults.Human;
        foreach (var axis in Enum.GetValues<DevelopmentAxis>())
            Assert.Equal(0, DevelopmentState.Newborn.Get(axis));

        foreach (var action in Enum.GetValues<ActionType>())
            Assert.Equal(InfantActions.Contains(action), rules.IsEligible(DevelopmentState.Newborn, action));
    }

    [Fact]
    public void A_hungry_newborn_with_an_empty_pantry_cries_and_one_with_food_eats_without_any_script()
    {
        var empty = BehaviorDecisionSystem.SelectByUtility(
            Ctx(Household(0, 0)), UrgentRules(), EnabledEconomy(), continuityAction: null, captureTrace: false, eligibility: For(DevelopmentState.Newborn));
        var fed = BehaviorDecisionSystem.SelectByUtility(
            Ctx(Household(5, 5)), UrgentRules(), EnabledEconomy(), continuityAction: null, captureTrace: false, eligibility: For(DevelopmentState.Newborn));

        Assert.Equal(ActionType.Cry, empty.Action);
        Assert.Equal(ActionType.Eat, fed.Action);
    }

    [Fact]
    public void A_newborn_never_selects_an_action_above_its_development_even_when_the_adult_would()
    {
        var adult = BehaviorDecisionSystem.SelectByUtility(
            Ctx(Household(0, 0), hunger: 90), UrgentRules(), EnabledEconomy(), continuityAction: null, captureTrace: false, eligibility: default);
        var baby = BehaviorDecisionSystem.SelectByUtility(
            Ctx(Household(0, 0), hunger: 90), UrgentRules(), EnabledEconomy(), continuityAction: null, captureTrace: false, eligibility: For(DevelopmentState.Newborn));

        Assert.DoesNotContain(adult.Action, new[] { ActionType.Cry }); // adulto não chora
        Assert.Contains(baby.Action, InfantActions);
    }

    [Fact]
    public void The_cry_fades_as_language_appears()
    {
        var speaking = DevelopmentState.Newborn with { Language = 1.0, GrossMotor = 1.0, SocialCognition = 1.0 };
        var decision = BehaviorDecisionSystem.SelectByUtility(
            Ctx(Household(0, 0)), UrgentRules(), EnabledEconomy(), continuityAction: null, captureTrace: false, eligibility: For(speaking));

        Assert.NotEqual(ActionType.Cry, decision.Action);
    }

    // --- Estado por idade ---

    [Fact]
    public void Progress_follows_effective_age_and_completes_to_null()
    {
        var world = ScenarioRunner.Create(seed: 7, initialPopulation: 0).World;
        var rules = world.OntogenyRules;
        Assert.True(rules.Enabled);

        var child = Birth(world, ageYears: 0.5);
        var older = Birth(world, ageYears: 6.0);
        var adult = Birth(world, ageYears: rules.CompleteAtYears + 1);

        Assert.True(OntogenyMechanic.StateFor(world, child)!.GrossMotor < OntogenyMechanic.StateFor(world, older)!.GrossMotor);
        Assert.Null(OntogenyMechanic.StateFor(world, adult));
    }

    [Fact]
    public void A_faster_rate_gene_develops_earlier_at_the_same_age()
    {
        var world = ScenarioRunner.Create(seed: 7, initialPopulation: 0).World;
        var slow = Birth(world, 2.0, rate: 0.7);
        var fast = Birth(world, 2.0, rate: 1.3);

        Assert.True(OntogenyMechanic.StateFor(world, fast)!.Language > OntogenyMechanic.StateFor(world, slow)!.Language);
    }

    private static long _seq;

    private static Npc Birth(WorldState world, double ageYears, double rate = 1.0)
    {
        var birth = world.CurrentDate.AddHours(-(long)(ageYears * world.CurrentDate.Calendar.HoursPerYear));
        long id = 9000 + (++_seq);
        return new Npc(
            new NpcId(id), $"t{id}", Sex.Female, birth, default, default, null, null, null, 100, Neutral, default, default,
            rateGene: new LivingWorld.Domain.Population.Body.RateGene(rate));
    }

    // --- Critério 1b: ninguém age acima do próprio desenvolvimento em 10 anos; desligar a checagem reprova ---

    private static int CountViolations(WorldState world, OntogenyRules judge)
    {
        int violations = 0;
        foreach (var npc in world.Npcs)
            // O marco vale para a intenção: a viagem que atende uma intenção elegível (ir à cama) é assistida, quem não anda é levado.
            if (npc.IsAlive && npc.CurrentAction is { } action
                && !judge.IsEligible(npc.Development, action == ActionType.Travel ? npc.TravelPurpose ?? action : action))
                violations++;
        return violations;
    }

    private static int RunAndCountViolations(OntogenyRules decisionRules, bool seedStatesFromHuman)
    {
        var (world, clock) = ScenarioRunner.Create(seed: 42, initialPopulation: 40, ontogenyRules: decisionRules);
        int violations = 0;
        for (long t = 0; t < TenYearsInHours; t += 24)
        {
            clock.Run(world, 24);
            if (seedStatesFromHuman)
                foreach (var npc in world.Npcs.Where(n => n.IsAlive && n.Development is null && OntogenyMechanic.EffectiveAgeYears(world, n) < OntogenyDefaults.Human.CompleteAtYears))
                    npc.SetDevelopment(OntogenyDefaults.Human.StateAt(OntogenyMechanic.EffectiveAgeYears(world, npc)));
            violations += CountViolations(world, OntogenyDefaults.Human);
        }
        return violations;
    }

    [Fact]
    public void No_npc_acts_above_its_development_across_ten_years()
    {
        Assert.Equal(0, RunAndCountViolations(OntogenyDefaults.Human, seedStatesFromHuman: false));
    }

    [Fact]
    public void Mutation_with_the_eligibility_check_off_the_same_invariant_fails()
    {
        // Mesmo mundo e mesmo juiz, mas a decisão roda com a ontogenia desligada (cada filho recebe o estado por idade só para o juiz medir).
        Assert.True(RunAndCountViolations(OntogenyRules.Disabled, seedStatesFromHuman: true) > 0);
    }

    // --- Critério 3: entrou na conta ---

    [Fact]
    public void Turning_the_subsystem_off_changes_the_canonical_hash_in_ten_years()
    {
        string Hash(OntogenyRules rules)
        {
            var (world, clock) = ScenarioRunner.Create(seed: 42, initialPopulation: 40, ontogenyRules: rules);
            clock.Run(world, TenYearsInHours);
            return WorldSnapshot.CanonicalHash(world);
        }

        Assert.NotEqual(Hash(OntogenyDefaults.Human), Hash(OntogenyRules.Disabled));
    }

    [Fact]
    public void Initial_adults_have_no_development_state_and_cost_nothing()
    {
        var world = ScenarioRunner.Create(seed: 3, initialPopulation: 40).World;
        foreach (var npc in world.Npcs.Where(n => n.AgeYears(world.CurrentDate) >= 21))
            Assert.Null(npc.Development);
    }

    [Fact]
    public void Born_children_start_as_newborns_and_the_state_survives_a_snapshot_round_trip()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 42, initialPopulation: 40);
        clock.Run(world, 3L * 360 * 24);

        var children = world.Npcs.Where(n => n.IsAlive && n.Development is not null).ToList();
        Assert.NotEmpty(children);

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        foreach (var child in children)
            Assert.Equal(child.Development, restored.FindNpc(child.Id)!.Development);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
    }

    // --- Dado de cenário ---

    [Fact]
    public void Scenario_without_the_block_gets_the_human_milestones_and_enabled_false_turns_it_off()
    {
        Assert.Same(OntogenyDefaults.Human, OntogenyScenarioLoader.Load("{}").Value);
        Assert.False(OntogenyScenarioLoader.Load("""{"Ontogeny":{"Enabled":false}}""").Value!.Enabled);
        Assert.Contains("Ontogeny.Requirements", OntogenyScenarioLoader.Load("""{"Ontogeny":{"Enabled":true,"Requirements":{"Work":{"Axis":"Language","Threshold":0.5}}}}""").Error);
        Assert.Contains("Ontogeny.Milestones[x].Axis", OntogenyScenarioLoader.Load("""{"Ontogeny":{"Enabled":true,"Milestones":[{"Id":"x","Axis":"Nope"}]}}""").Error);
    }
}
