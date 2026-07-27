using LivingWorld.Domain;

namespace LivingWorld.Tests.Behavior;

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
    public void Create_fails_naming_the_action_missing_a_declared_duration(ActionType missing)
    {
        var result = ActionCatalog.Create(AllDurations(omit: missing), routineSlots: [], defaultAction: ActionType.Idle);

        Assert.False(result.IsSuccess);
        Assert.Contains(missing.ToString(), result.Error);
    }

    [Fact]
    public void Create_succeeds_when_all_six_actions_declare_a_duration()
    {
        var result = ActionCatalog.Create(AllDurations(), routineSlots: [], defaultAction: ActionType.Idle);

        Assert.True(result.IsSuccess);
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
