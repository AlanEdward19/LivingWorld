using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using LivingWorld.Tests.Shared;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>Contrato congelado entre o motor e qualquer cliente (Godot, ADR-0022): o documento OpenAPI e a
/// FORMA (nomes e tipos, sem valores) do que o escopo visual devolve. Mudar a API muda estes arquivos de
/// propósito: <c>LW_RECORD_CONTRACT=1 dotnet test --filter ApiContractTests</c> regrava, e o diff do commit
/// mostra exatamente o que o cliente precisa acompanhar.</summary>
[Collection(ApiEndpointCollection.Name)]
public class ApiContractTests
{
    private static readonly bool Record = Environment.GetEnvironmentVariable("LW_RECORD_CONTRACT") == "1";
    private readonly LivingWorldApiFactory _factory;

    public ApiContractTests(LivingWorldApiFactory factory) => _factory = factory;

    [Fact]
    public async Task OpenApi_document_matches_the_committed_contract()
    {
        var response = await _factory.CreateClient().GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AssertMatchesContract("openapi.v1.json", Normalize(JsonNode.Parse(await response.Content.ReadAsStringAsync())!));
    }

    [Theory]
    [InlineData("npc-directory", "/npcs?take=1")]
    [InlineData("npc-detail", "/npcs/0")]
    [InlineData("action-catalog", "/visual/catalog/actions")]
    [InlineData("world-subscribe", "/visual/subscribe?scope=World&mode=Spectator")]
    [InlineData("simulation-status", "/simulation/status")]
    public async Task Response_shape_matches_the_committed_contract(string name, string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var shape = ShapeOf(JsonNode.Parse(await response.Content.ReadAsStringAsync()));
        AssertMatchesContract($"shape.{name}.json", shape);
    }

    /// <summary>Escopos Cidade e Interior do primeiro prédio do mundo canônico: a forma inclui `floors`,
    /// `groundFootprint` e `layout` que o cliente 3D consome.</summary>
    [Fact]
    public async Task City_and_interior_shapes_match_the_committed_contract()
    {
        var client = _factory.CreateClient();
        var world = JsonNode.Parse(await client.GetStringAsync("/visual/subscribe?scope=World&mode=Spectator"))!;
        string cityId = world["payload"]!["cities"]![0]!["id"]!["value"]!.ToString();

        var cityText = await client.GetStringAsync($"/visual/subscribe?scope=City&refId={cityId}&mode=Spectator");
        var city = JsonNode.Parse(cityText)!;
        var buildings = city["payload"]!["buildings"]!.AsArray();
        Assert.NotEmpty(buildings);
        AssertMatchesContract("shape.city-subscribe.json", ShapeOf(city));
        foreach (var marker in buildings)
        {
            Assert.True(marker!["floors"]!.GetValue<int>() >= 1);
            Assert.Contains(marker["groundFootprint"]!.AsArray(), c => c!["material"]!.GetValue<int>() == 2); // Door
        }

        string buildingId = buildings[0]!["id"]!["value"]!.ToString();
        var interior = JsonNode.Parse(await client.GetStringAsync($"/visual/subscribe?scope=Interior&refId={buildingId}&mode=Spectator"))!;
        AssertMatchesContract("shape.interior-subscribe.json", ShapeOf(interior));
        var layout = interior["payload"]!["layout"]!;
        Assert.Equal(layout["floors"]!.GetValue<int>(), layout["floorLayouts"]!.AsArray().Count);
    }

    /// <summary>Forma dos deltas empurrados por WebSocket/SSE: montados à mão com um elemento em cada lista para
    /// a gravação mostrar o formato dos itens (o envelope é o mesmo que o gateway serializa).</summary>
    [Fact]
    public void Delta_shapes_match_the_committed_contract()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var scope = new LivingWorld.Api.Visual.Scope.VisualScope(LivingWorld.Api.Visual.Scope.VisualScopeKind.World, "");
        var from = new LivingWorld.Api.Visual.Scope.VisualCursor(3, "world", 4);
        var to = new LivingWorld.Api.Visual.Scope.VisualCursor(4, "world", 5);
        var npc = new LivingWorld.Domain.Shared.NpcId(1);
        var cell = new LivingWorld.Domain.Geography.CellCoord(2, 3);
        var city = new LivingWorld.Domain.Shared.CityId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var delta = new LivingWorld.Api.Visual.Scope.ScopeTickDelta(
            4,
            [new LivingWorld.Api.Visual.Scope.NpcVisual(npc, cell, null, null)],
            [npc],
            [new LivingWorld.Api.Visual.Scope.CityVisual(city, "Alfa", cell, 10, new LivingWorld.Api.Visual.Projection.CellBounds(0, 0, 2, 2))],
            [city],
            [new LivingWorld.Api.Visual.Scope.BuildingVisual(new LivingWorld.Domain.Shared.BuildingId(7), city, -1, cell, 90)],
            [new LivingWorld.Domain.Shared.BuildingId(7)],
            [new LivingWorld.Api.Visual.Scope.ProcessVisual(1, "rest", 1, 0.5, "sleep-bed")],
            [1L],
            [new LivingWorld.Api.Visual.Scope.IndicatorUpdate("population", 10)],
            [new LivingWorld.Api.Visual.Scope.NotableVisualEvent(4, LivingWorld.Domain.History.WorldEventKind.Marriage, "Casamento", null)]);
        var world = new LivingWorld.Api.Visual.Scope.VisualDeltaEnvelope<object?>(scope, from, to, delta);
        var interior = new LivingWorld.Api.Visual.Scope.VisualDeltaEnvelope<object?>(
            new LivingWorld.Api.Visual.Scope.VisualScope(LivingWorld.Api.Visual.Scope.VisualScopeKind.Interior, "7"), from, to,
            new LivingWorld.Api.Visual.Projection.InteriorTickDelta(4,
                [new LivingWorld.Api.Visual.Projection.InteriorOccupant(npc, new LivingWorld.Domain.Geography.Spatial.FloorLevel(1), cell, null)],
                [new LivingWorld.Domain.Shared.NpcId(2)]));

        AssertMatchesContract("shape.visual-delta.json", ShapeOf(JsonNode.Parse(JsonSerializer.Serialize(world, options))));
        AssertMatchesContract("shape.interior-delta.json", ShapeOf(JsonNode.Parse(JsonSerializer.Serialize(interior, options))));
    }

    /// <summary>Estrutura sem valores: objeto → propriedades ordenadas; lista → forma do primeiro elemento
    /// ("[]" quando vazia); folha → tipo JSON. Dois mundos diferentes têm a mesma forma.</summary>
    internal static JsonNode ShapeOf(JsonNode? node) => node switch
    {
        JsonObject obj => Sorted(obj, ShapeOf),
        JsonArray { Count: 0 } => JsonValue.Create("[]"),
        JsonArray array => new JsonArray(ShapeOf(array[0])),
        JsonValue value => JsonValue.Create(value.GetValueKind() is JsonValueKind.True or JsonValueKind.False
            ? "boolean"
            : value.GetValueKind().ToString().ToLowerInvariant()),
        _ => JsonValue.Create("null"),
    };

    /// <summary>Ordena propriedades de objetos para o arquivo não depender da ordem de serialização.</summary>
    private static JsonNode Normalize(JsonNode node) => node switch
    {
        JsonObject obj => Sorted(obj, value => value is null ? null : Normalize(value)),
        JsonArray array => new JsonArray(array.Select(item => item is null ? null : Normalize(item)).ToArray()),
        _ => node.DeepClone(),
    };

    private static JsonObject Sorted(JsonObject source, Func<JsonNode?, JsonNode?> map)
    {
        var result = new JsonObject();
        foreach (var property in source.OrderBy(p => p.Key, StringComparer.Ordinal))
            result[property.Key] = map(property.Value);
        return result;
    }

    private static void AssertMatchesContract(string file, JsonNode actual)
    {
        string path = Path.Combine(FindRepoRoot(), "tests", "contracts", file);
        string text = actual.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
        if (Record)
        {
            File.WriteAllText(path, text);
            return;
        }

        Assert.True(File.Exists(path), $"{path} não existe — rode com LW_RECORD_CONTRACT=1");
        Assert.True(
            File.ReadAllText(path).ReplaceLineEndings("\n") == text.ReplaceLineEndings("\n"),
            $"contrato {file} mudou — se foi de propósito, regrave com LW_RECORD_CONTRACT=1 e avise o cliente");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }
}
