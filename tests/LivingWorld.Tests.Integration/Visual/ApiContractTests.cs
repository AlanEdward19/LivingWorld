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
    [InlineData("world-subscribe", "/visual/subscribe?scope=World&mode=Spectator")]
    [InlineData("simulation-status", "/simulation/status")]
    public async Task Response_shape_matches_the_committed_contract(string name, string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var shape = ShapeOf(JsonNode.Parse(await response.Content.ReadAsStringAsync()));
        AssertMatchesContract($"shape.{name}.json", shape);
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
