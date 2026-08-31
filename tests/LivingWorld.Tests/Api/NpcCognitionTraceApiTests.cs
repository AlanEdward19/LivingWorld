using System.Net;
using System.Net.Http.Json;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Api;

/// <summary>Fase 28 T10 (COG-10, COG-12, COG-13): <c>GET /npcs/{id}</c> inclui
/// <see cref="NpcInspectionDto.CognitionTrace"/> a partir de <see cref="WorldState.CognitionLog"/>.</summary>
public sealed class NpcCognitionTraceApiTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public NpcCognitionTraceApiTests(LivingWorldApiFactory factory) => _factory = factory;

    private WorldState World =>
        _factory.Services.GetRequiredService<WorldHost>().Current;

    private static DecisionTrace SampleTrace(ActionType winner = ActionType.Work, WakeReason wake = WakeReason.Scheduled) =>
        new(
            wake,
            PreviousIntent: ActionType.Sleep,
            TopPressures: [new Pressure("AcquireFood", 80, ["Hunger"])],
            KnownOpportunities: [new Opportunity("FoodAtMarket", 60)],
            winner,
            WinningUtility: 42.5,
            TopPositiveFactors: ["Hunger"],
            TopNegativeFactors: ["Distance"],
            BlockingFactors: [],
            KnownAlternatives: [ActionType.Sleep, ActionType.Socialize]);

    [Fact]
    public async Task GetNpc_returns_empty_cognition_trace_when_npc_has_no_entries()
    {
        _factory.ResetCanonicalWorld();
        var client = _factory.CreateClient();
        long id = World.Npcs.First(n => n.IsAlive).Id.Value;

        var response = await client.GetAsync($"/npcs/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<NpcInspectionDto>();
        Assert.NotNull(dto);
        Assert.NotNull(dto!.CognitionTrace);
        Assert.Empty(dto.CognitionTrace);
    }

    [Fact]
    public async Task GetNpc_returns_cognition_trace_when_entries_exist()
    {
        _factory.ResetCanonicalWorld();
        var npc = World.Npcs.First(n => n.IsAlive);
        World.CognitionLog.Record(npc.Id, tick: 10, SampleTrace(ActionType.Eat, WakeReason.UrgentNeed));
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/npcs/{npc.Id.Value}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<NpcInspectionDto>();
        Assert.NotNull(dto);
        Assert.Single(dto!.CognitionTrace);
        Assert.Equal(10, dto.CognitionTrace[0].Tick);
        Assert.Equal(ActionType.Eat, dto.CognitionTrace[0].Trace.Winner);
    }

    [Fact]
    public async Task GetNpc_cognition_trace_matches_recent_entries_from_log()
    {
        _factory.ResetCanonicalWorld();
        var npc = World.Npcs.First(n => n.IsAlive);
        World.CognitionLog.Record(npc.Id, 1, SampleTrace(ActionType.Work));
        World.CognitionLog.Record(npc.Id, 2, SampleTrace(ActionType.Sleep));
        var expected = World.CognitionLog.RecentEntries(npc.Id, int.MaxValue);
        var client = _factory.CreateClient();

        var dto = await client.GetFromJsonAsync<NpcInspectionDto>($"/npcs/{npc.Id.Value}");

        Assert.NotNull(dto);
        Assert.Equal(expected.Count, dto!.CognitionTrace.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Tick, dto.CognitionTrace[i].Tick);
            Assert.Equal(expected[i].Trace.Winner, dto.CognitionTrace[i].Trace.Winner);
            Assert.Equal(expected[i].Trace.WinningUtility, dto.CognitionTrace[i].Trace.WinningUtility);
        }
    }

    [Fact]
    public async Task GetNpc_is_idempotent_for_cognition_trace_within_same_tick()
    {
        _factory.ResetCanonicalWorld();
        var npc = World.Npcs.First(n => n.IsAlive);
        World.CognitionLog.Record(npc.Id, 5, SampleTrace());
        var client = _factory.CreateClient();

        var first = await client.GetFromJsonAsync<NpcInspectionDto>($"/npcs/{npc.Id.Value}");
        var second = await client.GetFromJsonAsync<NpcInspectionDto>($"/npcs/{npc.Id.Value}");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.CognitionTrace.Count, second!.CognitionTrace.Count);
        for (int i = 0; i < first.CognitionTrace.Count; i++)
        {
            Assert.Equal(first.CognitionTrace[i].Tick, second.CognitionTrace[i].Tick);
            Assert.Equal(first.CognitionTrace[i].Trace.Winner, second.CognitionTrace[i].Trace.Winner);
            Assert.Equal(first.CognitionTrace[i].Trace.WakeReason, second.CognitionTrace[i].Trace.WakeReason);
        }
    }

    [Fact]
    public async Task GetNpc_does_not_mutate_cognition_log_on_read()
    {
        _factory.ResetCanonicalWorld();
        var npc = World.Npcs.First(n => n.IsAlive);
        World.CognitionLog.Record(npc.Id, 7, SampleTrace(ActionType.Socialize));
        var before = World.CognitionLog.RecentEntries(npc.Id, int.MaxValue);
        var client = _factory.CreateClient();

        _ = await client.GetAsync($"/npcs/{npc.Id.Value}");
        _ = await client.GetAsync($"/npcs/{npc.Id.Value}");

        var after = World.CognitionLog.RecentEntries(npc.Id, int.MaxValue);
        Assert.Equal(before.Count, after.Count);
        Assert.Equal(before[^1].Tick, after[^1].Tick);
        Assert.Equal(before[^1].Trace.Winner, after[^1].Trace.Winner);
    }

    [Fact]
    public async Task GetNpc_returns_cognition_trace_in_chronological_order()
    {
        _factory.ResetCanonicalWorld();
        var npc = World.Npcs.First(n => n.IsAlive);
        for (long tick = 1; tick <= 3; tick++)
            World.CognitionLog.Record(npc.Id, tick, SampleTrace((ActionType)tick));
        var client = _factory.CreateClient();

        var dto = await client.GetFromJsonAsync<NpcInspectionDto>($"/npcs/{npc.Id.Value}");

        Assert.NotNull(dto);
        Assert.Equal(3, dto!.CognitionTrace.Count);
        Assert.Equal([1L, 2L, 3L], dto.CognitionTrace.Select(e => e.Tick).ToArray());
    }

    [Fact]
    public async Task Inspect_query_returns_empty_cognition_trace_without_http()
    {
        _factory.ResetCanonicalWorld();
        var npc = World.Npcs.First(n => n.IsAlive);

        var result = NpcInspectionQuery.Inspect(World, npc.Id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.CognitionTrace);
        Assert.Empty(result.Value.CognitionTrace);
    }
}
