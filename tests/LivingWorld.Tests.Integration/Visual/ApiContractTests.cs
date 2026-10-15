using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

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
    [InlineData("action-catalog", "/visual/catalog/actions")]
    [InlineData("world-subscribe", "/visual/subscribe?scope=World&mode=Spectator")]
    [InlineData("simulation-status", "/simulation/status")]
    [InlineData("world-calendar", "/world/calendar")]
    [InlineData("world-info", "/world/info")]
    public async Task Response_shape_matches_the_committed_contract(string name, string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var shape = ShapeOf(JsonNode.Parse(await response.Content.ReadAsStringAsync()));
        if (name == "simulation-status")
        {
            // Quem pausou, por quê, desde quando e até quando só existem com o mundo pausado por admin (ADR-0027).
            Optional(shape!, "pausedBy", "string");
            Optional(shape!, "pauseReason", "string");
            Optional(shape!, "pausedAtTick", "number");
            Optional(shape!, "pauseMaxTicks", "number");
        }
        AssertMatchesContract($"shape.{name}.json", shape!);
    }

    /// <summary>`GET /npcs/{id}` é a ficha que o cliente abre ao clicar num NPC: forma congelada, 404 para id inexistente.</summary>
    [Fact]
    public async Task Npc_inspection_shape_matches_the_committed_contract_and_an_unknown_id_is_404()
    {
        var client = _factory.CreateClient();
        var directory = JsonNode.Parse(await client.GetStringAsync("/npcs?take=1"))!;
        long id = directory["items"]![0]!["id"]!["value"]!.GetValue<long>();

        // O rastro de cognição só existe para NPC observado; grava um para a forma do item ficar congelada.
        // `previousIntent` e `detail` são opcionais no servidor: nulos aqui, o contrato os trata como "qualquer valor".
        _factory.Services.GetRequiredService<WorldHost>().Current.CognitionLog.Record(new NpcId(id), tick: 1, new DecisionTrace(
            WakeReason.UrgentNeed, null, [new Pressure("AcquireFood", 60, ["Hunger"])], [new Opportunity("FoodAtMarket", 40)],
            ActionType.Eat, 120, ["Hunger"], ["Distance"], ["Buy:outscored"], [ActionType.Work]));

        var response = await client.GetAsync($"/npcs/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertMatchesContract("shape.npc-inspect.json", InspectShape(JsonNode.Parse(await response.Content.ReadAsStringAsync())));

        // `POST /npcs/{id}/materialize` (comando explícito do cliente, #59 fatia H) devolve a mesma ficha.
        var materialized = await client.PostAsync($"/npcs/{id}/materialize", null);
        Assert.Equal(HttpStatusCode.OK, materialized.StatusCode);
        AssertMatchesContract("shape.npc-inspect.json", InspectShape(JsonNode.Parse(await materialized.Content.ReadAsStringAsync())));

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/npcs/987654321")).StatusCode);
    }

    /// <summary>Campo opcional tipado: a chave vira <c>"campo?"</c> com a forma que ele tem quando vem preenchido. Pode faltar ou
    /// vir nulo; se vier com valor tem de cumprir a forma (antes era <c>"null"</c>, que aceitava qualquer coisa).</summary>
    internal static void Optional(JsonNode shape, string key, JsonNode typed)
    {
        var obj = shape.AsObject();
        obj.Remove(key);
        obj[key + "?"] = typed;
        var sorted = Sorted(obj, v => v?.DeepClone());
        obj.Clear();
        foreach (var (k, v) in sorted.ToList()) { sorted.Remove(k); obj[k] = v; }
    }

    internal static JsonNode Id() => new JsonObject { ["value"] = "number" };

    /// <summary>Forma da ficha de NPC. Opcionais no servidor (HouseholdId?, NpcId? do cônjuge, CityId? do escopo, empregador, ação
    /// corrente...): quem não os tem não pode ser reprovado pelo contrato, senão a ficha de um NPC solteiro nunca abre.</summary>
    internal static JsonNode InspectShape(JsonNode? body)
    {
        var shape = ShapeOf(body)!;
        foreach (var key in new[] { "household", "spouse", "motherId", "fatherId", "employer" })
            Optional(shape, key, Id());
        Optional(shape, "currentAction", "number");
        Optional(shape, "actionTarget", new JsonObject { ["id"] = "string", ["kind"] = "string" });
        Optional(shape, "rest", new JsonObject { ["blocked"] = "boolean", ["kind"] = "number", ["location"] = new JsonObject { ["x"] = "number", ["y"] = "number" }, ["quality"] = "number", ["remainingHours"] = "number" });
        Optional(shape, "food", new JsonObject { ["blocked"] = "boolean", ["preparation"] = "number", ["remainingHours"] = "number", ["resourceId"] = "number" });
        var scope = shape["currentScope"]!;
        Optional(scope, "cityId", new JsonObject { ["value"] = "string" }); // CityId serializa o Guid como texto
        return shape;
    }

    /// <summary>Conversa com NPC (#59 fatia A): forma de `start` e `send` que a janela do cliente interpreta.</summary>
    [Fact]
    public async Task Conversation_start_and_send_shapes_match_the_committed_contract()
    {
        var client = _factory.CreateClient();
        var directory = JsonNode.Parse(await client.GetStringAsync("/npcs?take=1"))!;
        long id = directory["items"]![0]!["id"]!["value"]!.GetValue<long>();

        var start = await client.PostAsJsonAsync("/conversations/start", new { npcId = id });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var started = ShapeOf(JsonNode.Parse(await start.Content.ReadAsStringAsync()))!;
        long sessionId = JsonNode.Parse(await start.Content.ReadAsStringAsync())!["sessionId"]!.GetValue<long>();
        Optional(started, "sessionId", "number"); // nulo quando o NPC recusa
        AssertMatchesContract("shape.conversation-start.json", started);

        var send = await client.PostAsJsonAsync("/conversations/send", new { sessionId, message = "olá" });
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);
        AssertMatchesContract("shape.conversation-send.json", ShapeOf(JsonNode.Parse(await send.Content.ReadAsStringAsync())));

        await client.PostAsJsonAsync("/conversations/end", new { sessionId });
    }

    /// <summary>"E se…?" do painel Cérebro (#58 fatia 2, ADR-0026): resposta com a mesma forma do rastro.</summary>
    [Fact]
    public async Task Decision_sandbox_shape_matches_the_committed_contract()
    {
        var client = _factory.CreateClient();
        var directory = JsonNode.Parse(await client.GetStringAsync("/npcs?take=1"))!;
        long id = directory["items"]![0]!["id"]!["value"]!.GetValue<long>();

        var response = await client.PostAsJsonAsync($"/npcs/{id}/decision-sandbox", new { needs = new { hunger = 2 } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shape = ShapeOf(JsonNode.Parse(await response.Content.ReadAsStringAsync()))!;
        // Opcionais/variáveis: nulos ou listas que podem vir vazias valem "qualquer valor/lista".
        Optional(shape["trace"]!, "previousIntent", "number");
        shape["trace"]!["knownOpportunities"] = "[]";
        AssertMatchesContract("shape.decision-sandbox.json", shape);
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
        var occupant = new LivingWorld.Api.Visual.Projection.InteriorOccupant(npc, new LivingWorld.Domain.Geography.Spatial.FloorLevel(1), cell, null);
        var world = new LivingWorld.Api.Visual.Scope.VisualDeltaEnvelope<object?>(scope, from, to, delta);
        var interior = new LivingWorld.Api.Visual.Scope.VisualDeltaEnvelope<object?>(
            new LivingWorld.Api.Visual.Scope.VisualScope(LivingWorld.Api.Visual.Scope.VisualScopeKind.Interior, "7"), from, to,
            new LivingWorld.Api.Visual.Scope.InteriorTickDelta(4, new LivingWorld.Domain.Shared.BuildingId(7),
                [occupant], [occupant], [occupant], [new LivingWorld.Domain.Shared.NpcId(2)]));

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

    /// <summary>Campos que a gravação viu nulos mas que têm forma conhecida quando preenchidos: viram opcionais tipados em qualquer
    /// profundidade (ex.: `action` do diretório e dos deltas de interior, `lodging` dos prédios).</summary>
    private static readonly Dictionary<string, (string Key, Func<JsonNode> Typed)[]> TypedOptionals = new()
    {
        ["shape.npc-directory.json"] = [("action", () => "number")],
        ["shape.interior-delta.json"] = [("action", () => "number")],
        ["shape.visual-delta.json"] = [("currentAction", () => "number")],
        ["shape.city-subscribe.json"] = [("lodging", () => new JsonObject { ["nightlyRate"] = "number", ["occupied"] = "number", ["rentPerMonth"] = "number", ["units"] = "number" })],
    };

    private static void ApplyTypedOptionals(string file, JsonNode node)
    {
        if (!TypedOptionals.TryGetValue(file, out var rules)) return;
        void Walk(JsonNode? n)
        {
            if (n is JsonObject obj)
            {
                foreach (var (key, typed) in rules)
                    if (obj.TryGetPropertyValue(key, out var current) && current is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.String && v.GetValue<string>() == "null")
                        Optional(obj, key, typed());
                foreach (var child in obj.Select(p => p.Value).ToList()) Walk(child);
            }
            else if (n is JsonArray arr)
                foreach (var child in arr) Walk(child);
        }
        Walk(node);
    }

    internal static void AssertMatchesContract(string file, JsonNode actual)
    {
        ApplyTypedOptionals(file, actual);
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
