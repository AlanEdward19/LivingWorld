using System.Net.Http.Json;
using LivingWorld.Api.Observation;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Scheduling;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Api.Observation;

/// <summary>#58 (COG-11): o painel "Cérebro" do cliente só tem o que mostrar se o cliente publicar o escopo
/// que observa. Prova a pré-condição de ponta a ponta pela API: sem escopo o rastro fica vazio; com
/// <c>POST /observation/scope</c> de cidade, a mesma decisão urgente aparece em <c>GET /npcs/{id}</c>.</summary>
public sealed class ObservationToCognitionTraceTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public ObservationToCognitionTraceTests(LivingWorldApiFactory factory) => _factory = factory;

    private WorldState World => _factory.Services.GetRequiredService<WorldHost>().Current;

    /// <summary>Deixa o NPC com fome urgente, acorda-o e roda a decisão uma vez.</summary>
    private void DecideUrgently(long npcId)
    {
        var world = World;
        var npc = world.FindNpc(new LivingWorld.Domain.Shared.NpcId(npcId))!;
        npc.SetHunger(5, world.CurrentDate.TotalHours);
        SimulationWakeTestHelper.Wake(world, npc);
        new BehaviorDecisionSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));
    }

    private static async Task<int> TraceCount(HttpClient client, long npcId) =>
        (await client.GetFromJsonAsync<NpcInspectionDto>($"/npcs/{npcId}"))!.CognitionTrace.Count;

    [Fact]
    public async Task Trace_is_empty_without_a_published_scope_and_filled_once_the_city_scope_is_published()
    {
        _factory.ResetCanonicalWorld();
        var client = _factory.CreateClient();
        var npc = World.Npcs.First(n => n.IsAlive && n.City != default);

        DecideUrgently(npc.Id.Value);
        Assert.Equal(0, await TraceCount(client, npc.Id.Value));

        var scope = new ObservationScopeDto("City", npc.City.Value.ToString());
        Assert.True((await client.PostAsJsonAsync("/observation/scope", new ObservationScopeRequest("godot-test", scope))).IsSuccessStatusCode);
        DecideUrgently(npc.Id.Value);
        Assert.True(await TraceCount(client, npc.Id.Value) > 0);

        await client.PostAsJsonAsync("/observation/scope", new ObservationScopeRequest("godot-test", null));
        int recorded = await TraceCount(client, npc.Id.Value);
        DecideUrgently(npc.Id.Value);
        Assert.Equal(recorded, await TraceCount(client, npc.Id.Value));
    }
}
