using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Sandbox;

/// <summary>#58 fatia 2 (ADR-0026): <c>POST /npcs/{id}/decision-sandbox</c> roda o motor de decisão do NPC real com
/// estímulos sobrepostos e nunca escreve no mundo.</summary>
public sealed class DecisionSandboxEndpointTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public DecisionSandboxEndpointTests(LivingWorldApiFactory factory) => _factory = factory;

    private WorldState World => _factory.Services.GetRequiredService<WorldHost>().Current;

    private long AliveNpc()
    {
        _factory.ResetCanonicalWorld();
        return World.Npcs.First(n => n.IsAlive).Id.Value;
    }

    private static async Task<JsonElement> PostAsync(HttpClient client, long id, object body)
    {
        var response = await client.PostAsJsonAsync($"/npcs/{id}/decision-sandbox", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string[] PressureKinds(JsonElement result) =>
        result.GetProperty("trace").GetProperty("topPressures").EnumerateArray().Select(p => p.GetProperty("kind").GetString()!).ToArray();

    [Fact]
    public async Task A_hundred_calls_leave_the_canonical_hash_untouched()
    {
        long id = AliveNpc();
        var client = _factory.CreateClient();
        var before = WorldSnapshot.CanonicalHash(World);

        for (int i = 0; i < 100; i++)
            await PostAsync(client, id, new { needs = new { hunger = i % 100 }, personality = new { ambition = (i * 7) % 100 } });

        Assert.Equal(before, WorldSnapshot.CanonicalHash(World));
    }

    [Fact]
    public async Task Changing_only_hunger_changes_the_top_pressures()
    {
        long id = AliveNpc();
        var client = _factory.CreateClient();

        var starving = await PostAsync(client, id, new { needs = new { hunger = 2, thirst = 100, sleep = 100, social = 100 } });
        var fed = await PostAsync(client, id, new { needs = new { hunger = 100, thirst = 100, sleep = 100, social = 100 } });

        Assert.NotEqual(PressureKinds(starving), PressureKinds(fed));
        Assert.Contains("AcquireFood", PressureKinds(starving));
    }

    [Fact]
    public async Task Personality_overrides_change_the_decision_trace()
    {
        long id = AliveNpc();
        var client = _factory.CreateClient();
        var needs = new { hunger = 40, thirst = 40, sleep = 40, social = 40 };
        var all = new[] { "extroversion", "agreeableness", "conscientiousness", "emotionalStability", "openness", "ambition", "loyalty", "altruism", "impulsivity", "riskAversion" };

        var low = await PostAsync(client, id, new { needs, personality = all.ToDictionary(t => t, _ => 0) });
        var high = await PostAsync(client, id, new { needs, personality = all.ToDictionary(t => t, _ => 100) });

        Assert.NotEqual(low.GetProperty("trace").GetRawText(), high.GetProperty("trace").GetRawText());
    }

    [Fact]
    public async Task Continuity_action_override_changes_the_trace()
    {
        long id = AliveNpc();
        var client = _factory.CreateClient();
        var needs = new { hunger = 60, thirst = 60, sleep = 60, social = 60 };

        var traces = new HashSet<string>();
        foreach (var action in Enum.GetValues<LivingWorld.Domain.Behavior.ActionType>())
            traces.Add((await PostAsync(client, id, new { needs, continuityAction = (int)action })).GetProperty("trace").GetRawText());

        Assert.True(traces.Count > 1, "a ação de continuidade tem que influenciar a decisão");
    }

    [Fact]
    public async Task Same_request_gives_the_same_answer_and_an_empty_body_uses_the_real_npc()
    {
        long id = AliveNpc();
        var client = _factory.CreateClient();

        var a = await PostAsync(client, id, new { needs = new { hunger = 10 } });
        var b = await PostAsync(client, id, new { needs = new { hunger = 10 } });
        Assert.Equal(a.GetRawText(), b.GetRawText());

        var empty = await client.PostAsync($"/npcs/{id}/decision-sandbox", null);
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
    }

    [Fact]
    public async Task Unknown_npc_is_404_and_invalid_bodies_are_400()
    {
        long id = AliveNpc();
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/npcs/987654321/decision-sandbox", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/npcs/{id}/decision-sandbox", new { needs = new { hunger = 101 } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/npcs/{id}/decision-sandbox", new { needs = new { sleep = -1 } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/npcs/{id}/decision-sandbox", new { personality = new { ambition = 200 } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/npcs/{id}/decision-sandbox", new { personality = new { charme = 50 } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/npcs/{id}/decision-sandbox", new { continuityAction = 9999 })).StatusCode);
    }

    [Fact]
    public async Task Dead_npc_is_404()
    {
        long id = AliveNpc();
        var npc = World.FindNpc(new LivingWorld.Domain.Shared.NpcId(id))!;
        npc.Die(World.CurrentDate);
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/npcs/{id}/decision-sandbox", new { })).StatusCode);
    }
}
