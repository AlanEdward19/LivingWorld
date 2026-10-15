using System.Net;
using System.Text.Json.Nodes;
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
    private static void Check(JsonNode? value, JsonNode shape, string path, List<string> errors)
    {
        switch (shape)
        {
            case JsonObject expected:
                if (value is not JsonObject actual) { errors.Add($"{path}: esperado objeto"); return; }
                foreach (var (key, child) in expected)
                {
                    if (!actual.ContainsKey(key)) errors.Add($"{path}.{key}: campo ausente");
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

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }
}
