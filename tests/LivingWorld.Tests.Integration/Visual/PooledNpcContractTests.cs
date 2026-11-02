using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using LivingWorld.Api.World;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>#59 fatia H contra dados reais do servidor: um morador do pool agregado (<c>lod</c> 2) tem de passar no mesmo contrato
/// congelado da ficha que o cliente valida (senão o painel nunca mostraria o aviso "anônimo"), e o comando explícito o
/// materializa numa ficha completa. Factory própria: materializar muta o mundo.</summary>
public class PooledNpcContractTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public PooledNpcContractTests(LivingWorldApiFactory factory) => _factory = factory;

    /// <summary>Mesmas regras do <c>shape_validator.gd</c>: objeto exige as chaves, lista confere o 1º item, folha confere o tipo,
    /// <c>"null"</c> e <c>"[]"</c> aceitam o que o contrato gravou como opcional/vazio.</summary>
    internal static void Check(JsonNode? value, JsonNode shape, string path, List<string> errors)
    {
        switch (shape)
        {
            case JsonObject expected:
                if (value is not JsonObject actual) { errors.Add($"{path}: esperado objeto"); return; }
                foreach (var (key, child) in expected)
                {
                    if (key.EndsWith('?'))
                    {
                        // opcional tipado: pode faltar ou vir nulo; se vier com valor, cumpre a forma
                        string real = key[..^1];
                        if (actual.ContainsKey(real) && actual[real] is not null) Check(actual[real], child!, $"{path}.{real}", errors);
                    }
                    else if (!actual.ContainsKey(key)) errors.Add($"{path}.{key}: campo ausente");
                    else Check(actual[key], child!, $"{path}.{key}", errors);
                }
                break;
            case JsonArray { Count: 1 } list:
                if (value is not JsonArray items) { errors.Add($"{path}: esperado lista"); return; }
                for (int i = 0; i < items.Count; i++) Check(items[i], list[0]!, $"{path}[{i}]", errors);
                break;
            case JsonValue leaf when leaf.GetValue<string>() is "string" or "number" or "boolean" or "[]":
                string kind = leaf.GetValue<string>();
                bool ok = kind switch
                {
                    "string" => value is JsonValue s && s.GetValueKind() == System.Text.Json.JsonValueKind.String,
                    "number" => value is JsonValue n && n.GetValueKind() == System.Text.Json.JsonValueKind.Number,
                    "boolean" => value is JsonValue b && b.GetValueKind() is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False,
                    _ => value is JsonArray,
                };
                if (!ok) errors.Add($"{path}: esperado {kind}");
                break;
        }
    }

    [Fact]
    public async Task A_pooled_member_satisfies_the_frozen_inspect_shape_and_materializes_on_the_explicit_command()
    {
        var world = _factory.Services.GetRequiredService<WorldHost>().Current;
        var pool = new AggregatePopulationPool(3, 300, 240);
        var poolIds = world.ReserveNpcIdBlock(pool.Count);
        var city = new City(world.NextCityId(), new CellCoord(9, 9), foundedAtTick: 0, foundedFromCityId: null, aggregatePool: pool, poolNpcIds: poolIds);
        world.AddCity(city);
        long id = poolIds[0].Value;
        var client = _factory.CreateClient();
        var shape = JsonNode.Parse(File.ReadAllText(Path.Combine(FindRepoRoot(), "tests", "contracts", "shape.npc-inspect.json")))!;

        var read = await client.GetAsync($"/npcs/{id}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var pooled = JsonNode.Parse(await read.Content.ReadAsStringAsync())!;
        Assert.Equal(2, pooled["lod"]!.GetValue<int>());
        var errors = new List<string>();
        Check(pooled, shape, "$", errors);
        Assert.True(errors.Count == 0, "o morador anônimo reprova o contrato que o cliente valida: " + string.Join("; ", errors));

        // O GET não materializa: continua anônimo depois de ler.
        var again = JsonNode.Parse(await client.GetStringAsync($"/npcs/{id}"))!;
        Assert.Equal(2, again["lod"]!.GetValue<int>());

        var materialized = await client.PostAsync($"/npcs/{id}/materialize", null);
        Assert.Equal(HttpStatusCode.OK, materialized.StatusCode);
        var full = JsonNode.Parse(await materialized.Content.ReadAsStringAsync())!;
        Assert.Equal(0, full["lod"]!.GetValue<int>());
        errors.Clear();
        Check(full, shape, "$", errors);
        Assert.True(errors.Count == 0, "a ficha materializada reprova o contrato: " + string.Join("; ", errors));

        var after = JsonNode.Parse(await client.GetStringAsync($"/npcs/{id}"))!;
        Assert.Equal(0, after["lod"]!.GetValue<int>());
    }

    /// <summary>Opcionais tipados com valor de verdade: depois de algumas horas simuladas há NPC com empregador, ação corrente e
    /// descanso/comida preenchidos, e o servidor tem de devolvê-los na forma declarada em <c>shape.npc-inspect.json</c>.</summary>
    [Fact]
    public async Task Filled_optional_fields_of_living_npcs_match_the_typed_contract()
    {
        var host = _factory.Services.GetRequiredService<WorldHost>();
        for (int i = 0; i < 72; i++) host.Clock.Tick(host.Current);
        var client = _factory.CreateClient();
        var shape = JsonNode.Parse(File.ReadAllText(Path.Combine(FindRepoRoot(), "tests", "contracts", "shape.npc-inspect.json")))!;
        var seen = new HashSet<string>();

        foreach (var npc in host.Current.Npcs.Where(n => n.IsAlive).Take(40))
        {
            var body = JsonNode.Parse(await client.GetStringAsync($"/npcs/{npc.Id.Value}"))!;
            var errors = new List<string>();
            Check(body, shape, "$", errors);
            Assert.True(errors.Count == 0, $"NPC {npc.Id.Value}: " + string.Join("; ", errors));
            foreach (var key in new[] { "employer", "currentAction", "household", "spouse", "motherId", "fatherId", "rest", "food", "actionTarget" })
                if (body[key] is not null) seen.Add(key);
        }

        // Premissa: o teste só vale se ao menos os opcionais mais comuns vieram preenchidos em algum NPC.
        Assert.Contains("currentAction", seen);
        Assert.Contains("household", seen);
    }

    /// <summary>#92: observa refeições reais hora a hora, sem depender de um único instante da simulação.</summary>
    [Fact]
    public async Task Eating_npc_exposes_non_null_food_matching_the_typed_contract()
    {
        _factory.ResetCanonicalWorld();
        var host = _factory.Services.GetRequiredService<WorldHost>();
        var client = _factory.CreateClient();
        var shape = JsonNode.Parse(File.ReadAllText(Path.Combine(FindRepoRoot(), "tests", "contracts", "shape.npc-inspect.json")))!;

        for (int hour = 0; hour < 72; hour++)
        {
            host.Clock.Tick(host.Current);
            foreach (var npc in host.Current.Npcs.Where(n => n.IsAlive && n.CurrentAction == ActionType.Eat))
            {
                var body = JsonNode.Parse(await client.GetStringAsync($"/npcs/{npc.Id.Value}"))!;
                Assert.True(body["food"] is not null, $"NPC {npc.Id.Value} comendo na hora {hour + 1}: food ausente");
                var errors = new List<string>();
                Check(body, shape, "$", errors);
                Assert.True(errors.Count == 0, string.Join("; ", errors));
                return;
            }
        }

        Assert.Fail("Nenhuma refeição observada em 72 horas simuladas (seed 1).");
    }

    /// <summary>#92: família autorada atravessa criação, consulta HTTP e contrato tipado com ambos os pais presentes.</summary>
    [Fact]
    public async Task Authored_child_exposes_both_parent_ids_matching_the_typed_contract()
    {
        var scenario = JsonNode.Parse(File.ReadAllText(Path.Combine(FindRepoRoot(), "scenarios", "default.json")))!.AsObject();
        scenario["Seed"] = 92;
        scenario["InitialPopulation"] = 0;
        scenario["AuthoredHouseholds"] = JsonNode.Parse("""
            [{"Members":[
              {"Name":"Ana","Sex":"Female","AgeYears":34,"ProfessionId":1,"Spouse":1},
              {"Name":"Beto","Sex":"Male","AgeYears":36,"ProfessionId":2,"Spouse":0},
              {"Name":"Cai","Sex":"Male","AgeYears":6,"ProfessionId":1,"Mother":0,"Father":1}]}]
            """);
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/worlds/create", new CreateWorldRequest(scenario.ToJsonString(), "Contrato de pais"));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var world = _factory.Services.GetRequiredService<WorldHost>().Current;
        var child = world.Npcs.Single(n => n.Name == "Cai");
        var body = JsonNode.Parse(await client.GetStringAsync($"/npcs/{child.Id.Value}"))!;
        var shape = JsonNode.Parse(File.ReadAllText(Path.Combine(FindRepoRoot(), "tests", "contracts", "shape.npc-inspect.json")))!;

        Assert.True(body["motherId"] is not null, "motherId ausente na ficha do filho autorado");
        Assert.True(body["fatherId"] is not null, "fatherId ausente na ficha do filho autorado");
        var errors = new List<string>();
        Check(body, shape, "$", errors);
        Assert.True(errors.Count == 0, string.Join("; ", errors));
        Assert.Equal(world.Npcs.Single(n => n.Name == "Ana").Id.Value, body["motherId"]!["value"]!.GetValue<long>());
        Assert.Equal(world.Npcs.Single(n => n.Name == "Beto").Id.Value, body["fatherId"]!["value"]!.GetValue<long>());
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }
}
