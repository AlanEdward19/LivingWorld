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

    [Theory]
    [InlineData("hunger")]
    [InlineData("thirst")]
    [InlineData("sleep")]
    [InlineData("social")]
    public async Task Each_need_override_changes_the_trace_when_it_alone_differs(string need)
    {
        long id = AliveNpc();
        var client = _factory.CreateClient();
        var satisfied = new[] { "hunger", "thirst", "sleep", "social" }.ToDictionary(n => n, _ => 100);
        var lacking = new Dictionary<string, int>(satisfied) { [need] = 2 };

        var fed = await PostAsync(client, id, new { needs = satisfied });
        var short_ = await PostAsync(client, id, new { needs = lacking });

        Assert.NotEqual(fed.GetProperty("trace").GetRawText(), short_.GetProperty("trace").GetRawText());
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
    public void Sandbox_context_uses_the_same_provision_target_as_the_engine()
    {
        AliveNpc();
        var world = World;
        var index = LivingWorld.Simulation.Economy.Market.MarketIndex.BuildForTick(world);
        var living = world.Npcs.Where(n => n.IsAlive).ToList();

        Assert.All(living, npc => Assert.Equal(
            LivingWorld.Simulation.Behavior.Decision.BehaviorDecisionSystem.ProvisionTargetFor(world, npc, index),
            LivingWorld.Simulation.Behavior.Decision.NpcDecisionSandbox.BuildContext(world, npc).ProvisionTargetDays));
        Assert.Contains(living, npc => LivingWorld.Simulation.Behavior.Decision.BehaviorDecisionSystem.ProvisionTargetFor(world, npc, index)
            != 2.0); // padrão do contexto: sem mercado a meta é a mesma, com mercado muda
    }

    [Fact]
    public void The_real_sandbox_path_decides_with_the_engines_provision_target()
    {
        AliveNpc();
        var world = World;
        string Json(object o) => System.Text.Json.JsonSerializer.Serialize(o);
        var differing = 0;

        foreach (var npc in world.Npcs.Where(n => n.IsAlive && n.Household is not null).ToList())
        {
            // Despensa vazia e dinheiro na mão: é aí que a meta de provisão pesa na decisão (ProvisionPressureOf).
            var household = world.FindHousehold(npc.Household!.Value)!;
            foreach (var (resource, amount) in household.Stock.ToList())
                household.Withdraw(resource, amount);
            // 1 dia de estoque por membro: abaixo da meta (~2 dias) por uma fração que depende da meta de cada lado.
            household.Deposit(new LivingWorld.Domain.Geography.ResourceType(world.EconomyRules.FoodResourceId), household.Members.Count);
            household.Deposit(new LivingWorld.Domain.Geography.ResourceType(world.EconomyRules.WaterResourceId), household.Members.Count);
            npc.CreditWallet(new LivingWorld.Domain.Shared.Money(1_000));

            var overrides = new LivingWorld.Simulation.Behavior.Decision.NpcSandboxOverrides(Hunger: 80, Thirst: 80, Sleep: 80, Social: 80);
            var viaRoute = LivingWorld.Simulation.Behavior.Decision.NpcDecisionSandbox.Run(world, npc.Id, overrides).Value!;
            var context = LivingWorld.Simulation.Behavior.Decision.NpcDecisionSandbox.BuildContext(world, npc);
            var set = context with { Needs = new LivingWorld.Simulation.Behavior.Decision.NeedsSnapshot(80, 80, 80, 80) };
            var request = new LivingWorld.Simulation.Behavior.Decision.DecisionSandboxRequest(ContinuityAction: npc.CurrentAction, PreviousIntent: npc.CurrentIntent);
            var engine = LivingWorld.Simulation.Behavior.Decision.DecisionSandbox.Decide(set, world.NeedsRules, world.EconomyRules, request);
            var withDefault = LivingWorld.Simulation.Behavior.Decision.DecisionSandbox.Decide(set with { ProvisionTargetDays = 2.0 }, world.NeedsRules, world.EconomyRules, request);

            Assert.Equal(Json(engine.Trace), Json(viaRoute.Trace));
            if (Json(engine.Trace) != Json(withDefault.Trace)) differing++;
        }

        Assert.True(differing > 0, "premissa: com a despensa vazia e dinheiro, a meta de provisão do motor muda o rastro de ao menos um NPC");
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
