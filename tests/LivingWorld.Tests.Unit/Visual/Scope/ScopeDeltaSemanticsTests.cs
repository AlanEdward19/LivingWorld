using System.Text.Json;
using System.Text.Json.Nodes;
using LivingWorld.Api.Visual.Observable;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Queries;

namespace LivingWorld.Tests.Unit.Visual.Scope;

/// <summary>AD-172: o que o delta deixa de reenviar e como o redutor (e o cliente) reconstrói. Processo de descanso ou refeição que
/// só avançou no relógio não vai; NPC já conhecido que só andou vai sem cidade, observável, aparência e grupo da ação.</summary>
public class ScopeDeltaSemanticsTests
{
    private static readonly CityId City = new(Guid.Parse("00000000-0000-0000-0000-000000000172"));
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    private static ProcessVisual Rest(long remaining, double progress, int x = 1) =>
        new(1_000_001, "rest", 1, progress, "sleep-dwelling", 1, remaining, new CellCoord(x, 0));

    private static LivingScopeState State(long tick, params ProcessVisual[] processes) =>
        LivingScopeState.Empty with { Processes = processes, Tick = tick };

    private static NpcVisual Walker(int x, long startedAt = 10, ActionType action = ActionType.Work) =>
        new(new NpcId(7), new CellCoord(x, 0), action, City, null, null, new Appearance(1, 2, 3, 4))
        {
            Observable = new NpcObservable("Ana", "Female", 30, "Adult", 2, "ferreira", 165, 1, null, null,
                new EmotionsVisual(0, 0, 0, 0.3, 0, 0), "joy", false, ValueList<CarriedVisual>.Empty, null),
            ActionStartedAtTick = startedAt,
            ActionTarget = new NpcActionTargetDto("workplace", "3"),
            Intent = action,
            IntentTarget = "workplace:3",
        };

    [Fact]
    public void Rest_that_only_advanced_with_the_clock_is_left_out_and_replays_to_the_same_process()
    {
        // duração 8: em 10 faltam 6 (progresso 0,25); em 13 faltam 3 (0,625)
        var before = State(10, Rest(6, 1.0 - 6 / 8.0));
        var after = State(13, Rest(3, 1.0 - 3 / 8.0));

        var delta = ScopeDeltaBuilder.Diff(13, before, after);

        Assert.Empty(delta.ProcessUpserts);
        Assert.Equal(after.Processes, LivingDeltaReducer.Apply(before, delta).Processes);
    }

    [Fact]
    public void Rest_that_moved_or_did_not_advance_as_predicted_is_upserted()
    {
        var before = State(10, Rest(6, 0.25));

        var moved = ScopeDeltaBuilder.Diff(11, before, State(11, Rest(5, 1.0 - 5 / 8.0, x: 2)));
        var stalled = ScopeDeltaBuilder.Diff(11, before, State(11, Rest(6, 0.25)));

        Assert.Equal((1, 1), (moved.ProcessUpserts.Count, stalled.ProcessUpserts.Count));
    }

    [Fact]
    public void Process_advance_covers_a_gap_of_many_ticks_and_stops_at_zero()
    {
        var advanced = ProcessAdvance.Advance(Rest(6, 0.25), 24);
        var crop = new ProcessVisual(9, "crop", 9, 0.5, "crop", RemainingHours: 10);

        Assert.Equal((0L, 1.0), (advanced.RemainingHours!.Value, advanced.Progress));
        Assert.Same(crop, ProcessAdvance.Advance(crop, 5)); // só descanso e refeição avançam sozinhos
    }

    [Fact]
    public void Known_npc_that_only_walked_goes_without_city_observable_appearance_and_action_group()
    {
        var before = LivingScopeState.Empty with { Npcs = [Walker(1)], Tick = 10 };
        var after = LivingScopeState.Empty with { Npcs = [Walker(2)], Tick = 11 };

        var delta = ScopeDeltaBuilder.Diff(11, before, after);
        var json = JsonNode.Parse(JsonSerializer.Serialize(delta, Wire))!["npcUpserts"]![0]!.AsObject();

        Assert.Equal(
            (false, false, false, false, false, true),
            (json.ContainsKey("observable"), json.ContainsKey("appearance"), json.ContainsKey("actionStartedAtTick"),
                json.ContainsKey("actionTarget"), json.ContainsKey("intent"), json["city"] is null && json.ContainsKey("city")));
        Assert.Equal(after.Npcs, LivingDeltaReducer.Apply(before, delta).Npcs);
    }

    [Fact]
    public void Known_npc_that_changed_action_carries_the_whole_action_group()
    {
        var before = LivingScopeState.Empty with { Npcs = [Walker(1)], Tick = 10 };
        var after = LivingScopeState.Empty with { Npcs = [Walker(1, startedAt: 11, action: ActionType.Eat)], Tick = 11 };

        var upsert = Assert.Single(ScopeDeltaBuilder.Diff(11, before, after).NpcUpserts);

        Assert.Equal((11L, ActionType.Eat, "workplace:3"), (upsert.ActionStartedAtTick!.Value, upsert.Intent!.Value, upsert.IntentTarget));
        Assert.Null(upsert.Observable);
    }

    [Fact]
    public void Npc_new_to_the_scope_carries_everything()
    {
        var after = LivingScopeState.Empty with { Npcs = [Walker(2)], Tick = 11 };

        Assert.Equal(Walker(2), Assert.Single(ScopeDeltaBuilder.Diff(11, LivingScopeState.Empty, after).NpcUpserts));
    }
}
