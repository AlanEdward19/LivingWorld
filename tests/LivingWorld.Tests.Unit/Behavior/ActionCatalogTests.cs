using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Behavior;

/// <summary>Fase 4, task 2: <see cref="ActionType"/> como catálogo fechado com id estável
/// (desempate de utility AI, NEEDS-06) e <see cref="ActionCatalog"/> — duração máxima
/// obrigatória por ação e resolução de rotina (NEEDS-10, NEEDS-13).</summary>
public class ActionCatalogTests
{
    private static readonly ProfessionType Farmer = new(1);

    private static Dictionary<ActionType, int> AllDurations(ActionType? omit = null) =>
        Enum.GetValues<ActionType>()
            .Where(a => a != omit)
            .ToDictionary(a => a, _ => 4);

    [Theory]
    [InlineData(ActionType.Eat, 0)]
    [InlineData(ActionType.Sleep, 1)]
    [InlineData(ActionType.Work, 2)]
    [InlineData(ActionType.Socialize, 3)]
    [InlineData(ActionType.Travel, 4)]
    [InlineData(ActionType.Idle, 5)]
    [InlineData(ActionType.Buy, 6)]
    [InlineData(ActionType.UsePower, 7)]
    public void ActionType_values_are_stable_and_used_as_the_tie_break_id(ActionType action, int expectedId)
    {
        Assert.Equal(expectedId, (int)action);
    }

    [Theory]
    [InlineData(ActionType.Eat)]
    [InlineData(ActionType.Sleep)]
    [InlineData(ActionType.Work)]
    [InlineData(ActionType.Socialize)]
    [InlineData(ActionType.Travel)]
    [InlineData(ActionType.Idle)]
    [InlineData(ActionType.Buy)]
    [InlineData(ActionType.UsePower)]
    public void Create_fails_naming_the_action_missing_a_declared_duration(ActionType missing)
    {
        var result = ActionCatalog.Create(AllDurations(omit: missing), routineSlots: [], defaultAction: ActionType.Idle);

        Assert.False(result.IsSuccess);
        Assert.Contains(missing.ToString(), result.Error);
    }

    [Fact]
    public void Create_succeeds_when_all_actions_declare_a_duration()
    {
        var result = ActionCatalog.Create(AllDurations(), routineSlots: [], defaultAction: ActionType.Idle);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.MaxDurationHours.ContainsKey(ActionType.UsePower));
    }

    [Fact]
    public void PersonalityWeighting_WeightOf_UsePower_does_not_throw()
    {
        var personality = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

        double weight = PersonalityWeighting.WeightOf(personality, ActionType.UsePower);

        Assert.Equal(1.0, weight);
    }

    [Fact]
    public void NpcWakeScheduler_ComputeNextWakeTick_handles_UsePower_duration()
    {
        var catalog = ActionCatalog.Create(AllDurations(), routineSlots: [], defaultAction: ActionType.Idle).Value!;
        var needs = NeedsRules.Create(
            hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
            urgencyThreshold: 70, maxActionSelectionSteps: 10, hysteresisEnabled: false,
            continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 1, ScenarioRunner.DefaultMap(1),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            needs, catalog, ScenarioRunner.DefaultLifeStageRules);
        var npc = new Npc(
            new NpcId(1), "n", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar).AddYears(-20),
            ScenarioRunner.DefaultCulture, new CellCoord(0, 0), null, null, null, 100,
            Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            ProfessionType.None, new CellCoord(0, 0),
            currentAction: ActionType.UsePower, actionStartedAtTick: 10);
        npc.PendingPowerInvocation = new PendingPowerInvocation("p", "npc.teleport:1", null);

        long wake = NpcWakeScheduler.ComputeNextWakeTick(
            npc, needs, catalog, now: 10, world);

        Assert.Equal(10 + catalog.MaxDurationHours[ActionType.UsePower], wake);
    }

    [Fact]
    public void RoutineOf_resolves_profession_specific_slot_first()
    {
        var slots = new List<RoutineSlot>
        {
            new(ProfessionId: Farmer.Id, LifeStage.Adult, HourStart: 6, HourEnd: 14, ActionType.Work),
            new(ProfessionId: null, LifeStage.Adult, HourStart: 6, HourEnd: 14, ActionType.Idle),
        };
        var catalog = ActionCatalog.Create(AllDurations(), slots, ActionType.Idle).Value!;

        var action = catalog.RoutineOf(Farmer, LifeStage.Adult, hour: 10);

        Assert.Equal(ActionType.Work, action);
    }

    [Fact]
    public void RoutineOf_falls_back_to_any_slot_when_no_profession_specific_slot_matches()
    {
        var slots = new List<RoutineSlot>
        {
            new(ProfessionId: null, LifeStage.Adult, HourStart: 22, HourEnd: 23, ActionType.Sleep),
        };
        var catalog = ActionCatalog.Create(AllDurations(), slots, ActionType.Idle).Value!;

        var action = catalog.RoutineOf(Farmer, LifeStage.Adult, hour: 22);

        Assert.Equal(ActionType.Sleep, action);
    }

    [Fact]
    public void RoutineOf_falls_back_to_default_action_without_throwing_when_no_slot_matches()
    {
        var catalog = ActionCatalog.Create(AllDurations(), routineSlots: [], ActionType.Idle).Value!;

        var action = catalog.RoutineOf(Farmer, LifeStage.Adult, hour: 10);

        Assert.Equal(ActionType.Idle, action);
    }
}
