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
    /// <summary>Um exemplo preenchido de cada registro do observável (AD-170), para a forma gravada mostrar todo campo.</summary>
    private static class ObservableSample
    {
        private static readonly LivingWorld.Domain.Geography.CellCoord Cell = new(2, 3);
        public static readonly LivingWorld.Api.Visual.Observable.NpcObservable Npc = new(
            "Ana", "Female",
            30, "Adult", 1, "lavrador", 165, 1, new(0.5, 1), new(3, true, true), new(0.6, 0, 0, 0, 0, 0.2), "grief", true,
            [new LivingWorld.Api.Visual.Observable.CarriedVisual(1, 3, "trigo")], new(9, 77));
        public static readonly LivingWorld.Api.Visual.Observable.CityDensityVisual Density = new(12, 30, [new(ActionType.Work, 5)], 14.5);
        public static readonly LivingWorld.Api.Visual.Observable.EventInvolved Involved = new(
            1, 2, 3, 4, [5L], 6, 7, 8, 9, 10, 11, "city", 12, "levou", true);
        public static readonly LivingWorld.Api.Visual.Observable.BuildingStateVisual BuildingState = new(
            7, -1, "casa", "dwelling,workplace", 1, 2, "fazenda", 3, [4L], [5L], [6L], [new(1, 20, "trigo")], 40, false);
        public static readonly LivingWorld.Api.Visual.Observable.CropVisual Crop = new(
            3, Cell, 1, "Growing", "growing", 0.4, false, 1.2, 2.5, 0, "trigo", 1, 2, 300);
        public static readonly LivingWorld.Api.Visual.Observable.AnimalVisual Animal = new(5, "wolf", Cell, 80, false, "deer");
        public static readonly LivingWorld.Api.Visual.Observable.FaunaGroupVisual FaunaGroup = new("wolf", 1, 4, Cell);
        public static readonly LivingWorld.Api.Visual.Observable.LeavingVisual Leaving = new(6, Cell, 1, 2, 3, 4, 5, 9, 77, 7, 8);
        public static readonly LivingWorld.Api.Visual.Observable.ObjectVisual Object = new(9, 77, 2, 1, Cell, true, 3, "cylinder", 4, 0.9);
        public static readonly LivingWorld.Api.Visual.Observable.ScopeClimateVisual Climate = new(6, 2, "summer", 21.5, 1, 900, false);
        public static readonly LivingWorld.Api.Visual.Catalogs.ExtraordinaryNpcVisual Extraordinary = new(
            ["voo"], true, "manifest", 1.2, "#ffffff", "faisca", new("hunger", 1, 2), 0.5, true, 1.5);
    }

    /// <summary>AD-172: cada registro do escopo com todo campo anulável nulo — a forma marca como opcional tudo o que pode faltar,
    /// não só o que o mundo de exemplo deixou nulo.</summary>
    private static class MinimalSample
    {
        private static readonly LivingWorld.Domain.Geography.CellCoord Cell = new(1, 1);
        private static readonly LivingWorld.Domain.Shared.CityId City = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        private static readonly LivingWorld.Api.Visual.Observable.NpcObservable Observable = new(
            "Bia", "Male", 3, "Child", 0, null, 90, 0.5, null, null, new(0, 0, 0, 0, 0, 0), null, false,
            [new LivingWorld.Api.Visual.Observable.CarriedVisual(1, 1)], null);
        private static readonly LivingWorld.Api.Visual.Scope.NpcVisual Npc =
            new(new LivingWorld.Domain.Shared.NpcId(2), Cell, null, null, null,
                ObservableSample.Extraordinary with { NeedSubstitution = null }, null)
            { Observable = Observable };
        private static readonly LivingWorld.Api.Visual.Scope.CityVisual CityItem =
            new(City, "B", Cell, 0, new LivingWorld.Api.Visual.Projection.CellBounds(0, 0, 1, 1))
            { Density = new(0, 0, [new(null, 1)], 0) };
        private static readonly LivingWorld.Api.Visual.Scope.BuildingVisual Building =
            new(new LivingWorld.Domain.Shared.BuildingId(8), City, -1, Cell);

        // AD-173: lote sem rua à frente, sem quintal e sem caminho (prédio de degrau sem rua): a rua é opcional na forma.
        private static readonly LivingWorld.Api.Visual.Scope.BuildingVisual BuildingWithoutStreet =
            new(new LivingWorld.Domain.Shared.BuildingId(9), City, -1, Cell) { Lot = new([], [], null) };
        private static readonly LivingWorld.Api.Visual.Scope.ProcessVisual Process = new(2, "construction", 1, 0, "construction");
        private static readonly LivingWorld.Api.Visual.Scope.NotableVisualEvent Event =
            new(4, LivingWorld.Domain.History.WorldEventKind.Death, "Morte") { Involved = new() };
        private static readonly LivingWorld.Api.Visual.Observable.BuildingStateVisual BuildingState =
            new(8, -1, null, "structure", null, null, null, null, [], [], [], [new(1, 2)], 0, false);
        private static readonly LivingWorld.Api.Visual.Observable.CropVisual Crop =
            new(4, Cell, 1, "Fallow", "fallow", 0, false, 0, 0, 0, null, null, null, 0);
        private static readonly LivingWorld.Api.Visual.Observable.AnimalVisual Animal = new(6, "deer", Cell, 10, false, null);
        private static readonly LivingWorld.Api.Visual.Observable.LeavingVisual Leaving = new(7, Cell, 1, null, null, 1, 0, null, null, null, null);
        private static readonly LivingWorld.Api.Visual.Observable.ObjectVisual Object = new(10, 1, 1, null, null, false, null, null, null, 0);

        public static LivingWorld.Api.Visual.Scope.LivingScopeState State => LivingWorld.Api.Visual.Scope.LivingScopeState.Empty with
        {
            Npcs = [Npc],
            Cities = [CityItem],
            Buildings = [Building, BuildingWithoutStreet],
            Processes = [Process],
            Events = [Event],
            BuildingStates = [BuildingState],
            Crops = [Crop],
            Animals = [Animal],
            Leavings = [Leaving],
            Objects = [Object],
        };

        public static LivingWorld.Api.Visual.Scope.ScopeTickDelta Delta(long tick) =>
            new(tick, [Npc], [], [CityItem], [], [Building, BuildingWithoutStreet], [], [Process], [], [], [Event])
            {
                BuildingStateUpserts = [BuildingState],
                CropUpserts = [Crop],
                AnimalUpserts = [Animal],
                LeavingUpserts = [Leaving],
                ObjectUpserts = [Object],
            };
    }

    /// <summary>AD-172: forma de um snapshot = a resposta canônica + cada snapshot de um mundo rodando + um <c>livingState</c> com
    /// todo registro preenchido (lista que o mundo não encheu ainda fica com a forma do elemento).</summary>
    private static JsonNode RunningShape(string canonical, IReadOnlyList<JsonNode> running)
    {
        var shape = ContractShapes.Merge(ContractShapes.Of(JsonNode.Parse(canonical)), RunningWorldSamples.ShapeOf(running));
        var city = new LivingWorld.Domain.Shared.CityId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var cell = new LivingWorld.Domain.Geography.CellCoord(2, 3);
        var filled = LivingWorld.Api.Visual.Scope.LivingScopeState.Empty with
        {
            Npcs = [new LivingWorld.Api.Visual.Scope.NpcVisual(new LivingWorld.Domain.Shared.NpcId(1), cell, ActionType.Work, city, cell,
                ObservableSample.Extraordinary, new LivingWorld.Domain.Population.Appearance(1, 2, 3, 4))
            {
                Observable = ObservableSample.Npc, ActionStartedAtTick = 3,
                ActionTarget = new LivingWorld.Simulation.Cities.Queries.NpcActionTargetDto("workplace", "1"), Intent = ActionType.Work,
                IntentTarget = "workplace:1",
            }],
            Processes = [new LivingWorld.Api.Visual.Scope.ProcessVisual(1, "rest", 1, 0.5, "sleep-bed", 1, 3, cell, [cell], "brilho")
                { ResourceId = 1, ResourceName = "trigo" }],
            Events = [new LivingWorld.Api.Visual.Scope.NotableVisualEvent(4, LivingWorld.Domain.History.WorldEventKind.Marriage, "Casamento", cell)
                { Involved = ObservableSample.Involved, EventId = 12 }],
            BuildingStates = [ObservableSample.BuildingState],
            Crops = [ObservableSample.Crop],
            Animals = [ObservableSample.Animal],
            FaunaGroups = [ObservableSample.FaunaGroup],
            Leavings = [ObservableSample.Leaving],
            Objects = [ObservableSample.Object],
            Climate = ObservableSample.Climate,
            // AD-173: um lote completo (quintal, caminho e rua), para a forma conhecer o elemento das listas em todo escopo
            Buildings = [new LivingWorld.Api.Visual.Scope.BuildingVisual(new LivingWorld.Domain.Shared.BuildingId(3), city, -1, cell)
                { Lot = new([cell], [cell], cell) }],
        };
        var living = ContractShapes.Merge(
            ContractShapes.Of(JsonNode.Parse(LivingWorld.Api.Realtime.VisualWire.Serialize(filled))),
            ContractShapes.Of(JsonNode.Parse(LivingWorld.Api.Realtime.VisualWire.Serialize(MinimalSample.State))));
        var payload = shape["payload"]!.AsObject();
        payload["livingState"] = ContractShapes.Merge(payload["livingState"]!, living);
        return shape;
    }

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
    [InlineData("saved-worlds", "/worlds")]
    [InlineData("periods", "/periods")] // AD-175: cartão do período no menu
    public async Task Response_shape_matches_the_committed_contract(string name, string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var shape = ShapeOf(JsonNode.Parse(await response.Content.ReadAsStringAsync()));
        if (name == "world-subscribe")
            shape = RunningShape(await response.Content.ReadAsStringAsync(), RunningWorldSamples.ForRecording.WorldSnapshots);
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
        // Se a resposta gravada trouxe o campo preenchido, a forma observada tem de ser exatamente a declarada: o opcional
        // tipado não pode esconder um tipo errado do servidor.
        if (obj.TryGetPropertyValue(key, out var observed) && observed is not null
            && !(observed is JsonValue observedValue && observedValue.GetValueKind() == System.Text.Json.JsonValueKind.String && observedValue.GetValue<string>() == "null"))
            Assert.Equal(Normalize(typed).ToJsonString(), Normalize(observed).ToJsonString());
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
        // AD-170: prédio da casa e emprego legível só existem para quem tem casa com prédio e emprego
        Optional(shape, "householdBuildingId", Id());
        Optional(shape, "employment", new JsonObject { ["buildingId?"] = Id(), ["id"] = Id(), ["locationTypeId"] = "number", ["name?"] = "string", ["ownerId?"] = Id() });
        Optional(shape, "actionTarget", new JsonObject { ["id"] = "string", ["kind"] = "string" });
        Optional(shape, "rest", new JsonObject { ["blocked"] = "boolean", ["kind"] = "number", ["location"] = new JsonObject { ["x"] = "number", ["y"] = "number" }, ["quality"] = "number", ["remainingHours"] = "number" });
        Optional(shape, "food", new JsonObject { ["blocked"] = "boolean", ["preparation"] = "number", ["remainingHours"] = "number", ["resourceId"] = "number" });
        // `skills.values` é um mapa id → nível: as chaves são dado, não forma (AD-132: quem existe no início já tem ofício).
        if (shape["skills"] is JsonObject skills) skills["values"] = new JsonObject();
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
        AssertMatchesContract("shape.city-subscribe.json", RunningShape(cityText, RunningWorldSamples.ForRecording.CitySnapshots));
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
            [new LivingWorld.Api.Visual.Scope.NpcVisual(npc, cell, ActionType.Work, city, cell, ObservableSample.Extraordinary,
                new LivingWorld.Domain.Population.Appearance(1, 2, 3, 4))
            {
                Observable = ObservableSample.Npc, ActionStartedAtTick = 3,
                ActionTarget = new LivingWorld.Simulation.Cities.Queries.NpcActionTargetDto("workplace", "1"), Intent = ActionType.Work,
                IntentTarget = "workplace:1",
            }],
            [npc],
            [new LivingWorld.Api.Visual.Scope.CityVisual(city, "Alfa", cell, 10, new LivingWorld.Api.Visual.Projection.CellBounds(0, 0, 2, 2), city)
                { Density = ObservableSample.Density }],
            [city],
            [new LivingWorld.Api.Visual.Scope.BuildingVisual(new LivingWorld.Domain.Shared.BuildingId(7), city, -1, cell, 90)
                { Floors = 2, GroundFootprint = [new LivingWorld.Domain.Cities.Buildings.FootprintCell(cell, LivingWorld.Domain.Cities.Buildings.BuildingMaterial.Door)],
                  Value = 100, Lodging = new LivingWorld.Api.Visual.Projection.LodgingMarker(2, 1, 30, 2) }],
            [new LivingWorld.Domain.Shared.BuildingId(7)],
            [new LivingWorld.Api.Visual.Scope.ProcessVisual(1, "rest", 1, 0.5, "sleep-bed", 1, 3, cell, [cell], "brilho") { ResourceId = 1, ResourceName = "trigo" }],
            [1L],
            [new LivingWorld.Api.Visual.Scope.IndicatorUpdate("population", 10)],
            [new LivingWorld.Api.Visual.Scope.NotableVisualEvent(4, LivingWorld.Domain.History.WorldEventKind.Marriage, "Casamento", cell)
                { Involved = ObservableSample.Involved, EventId = 12 }])
        {
            BuildingStateUpserts = [ObservableSample.BuildingState],
            BuildingStateRemoved = [7L],
            CropUpserts = [ObservableSample.Crop],
            CropRemoved = [3L],
            AnimalUpserts = [ObservableSample.Animal],
            AnimalRemoved = [5L],
            FaunaGroupUpserts = [ObservableSample.FaunaGroup],
            FaunaGroupRemoved = ["wolf:1"],
            LeavingUpserts = [ObservableSample.Leaving],
            LeavingRemoved = [6L],
            ObjectUpserts = [ObservableSample.Object],
            ObjectRemoved = [9L],
            Climate = ObservableSample.Climate,
        };
        var occupant = new LivingWorld.Api.Visual.Projection.InteriorOccupant(npc, new LivingWorld.Domain.Geography.Spatial.FloorLevel(1), cell, null);
        var world = new LivingWorld.Api.Visual.Scope.VisualDeltaEnvelope<object?>(scope, from, to, delta);
        var interior = new LivingWorld.Api.Visual.Scope.VisualDeltaEnvelope<object?>(
            new LivingWorld.Api.Visual.Scope.VisualScope(LivingWorld.Api.Visual.Scope.VisualScopeKind.Interior, "7"), from, to,
            new LivingWorld.Api.Visual.Scope.InteriorTickDelta(4, new LivingWorld.Domain.Shared.BuildingId(7),
                [occupant], [occupant], [occupant], [new LivingWorld.Domain.Shared.NpcId(2)]));

        // AD-172: o delta vai pelo fio sem as chaves nulas; a forma junta o exemplo preenchido com os deltas de um mundo rodando
        var sample = ContractShapes.Of(JsonNode.Parse(LivingWorld.Api.Realtime.VisualWire.Serialize(world)));
        var minimal = ContractShapes.Of(JsonNode.Parse(LivingWorld.Api.Realtime.VisualWire.Serialize(
            new LivingWorld.Api.Visual.Scope.VisualDeltaEnvelope<object?>(scope, from, to, MinimalSample.Delta(4)))));
        AssertMatchesContract("shape.visual-delta.json", ContractShapes.Merge(ContractShapes.Merge(sample, minimal),
            RunningWorldSamples.ShapeOf(RunningWorldSamples.ForRecording.Deltas)));
        AssertMatchesContract("shape.interior-delta.json", ShapeOf(JsonNode.Parse(JsonSerializer.Serialize(interior, options))));
    }

    /// <summary>AD-170: ficha genérica <c>GET /entities/{kind}/{id}</c> por tipo, no mundo canônico (o estado tem forma por tipo).</summary>
    [Theory]
    [InlineData("npc")]
    [InlineData("building")]
    [InlineData("city")]
    [InlineData("household")]
    public async Task Entity_inspection_shapes_match_the_committed_contract(string kind)
    {
        var world = _factory.Services.GetRequiredService<WorldHost>().Current;
        var home = world.Households.First(h => h.Dwelling is not null && !h.IsEmpty);
        string id = kind switch
        {
            "npc" => home.Members[0].Value.ToString(),
            "building" => home.Dwelling!.Value.Value.ToString(),
            "city" => world.ActiveCities().First().Id.ToString(),
            _ => home.Id.Value.ToString(),
        };

        var response = await _factory.CreateClient().GetAsync($"/entities/{kind}/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shape = ShapeOf(JsonNode.Parse(await response.Content.ReadAsStringAsync()))!;
        shape["recentEvents"] = new JsonArray(EventViewShape());
        AssertMatchesContract($"shape.entity-{kind}.json", shape);
    }

    private static JsonNode EventViewShape() => ShapeOf(JsonNode.Parse(JsonSerializer.Serialize(
        new LivingWorld.Api.Visual.Observable.EntityEventView(4, 12, LivingWorld.Domain.History.WorldEventKind.Marriage, "Casamento",
            new LivingWorld.Domain.Geography.CellCoord(1, 2), ObservableSample.Involved),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))))!;

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

    /// <summary>AD-170: campos do observável que só vêm quando existem (a gravação do mundo canônico os vê nulos).</summary>
    private static readonly (string Key, Func<JsonNode> Typed)[] ObservableOptionals =
    [
        ("intent", () => "number"), ("intentTarget", () => "string"), ("professionName", () => "string"),
        ("actionTarget", () => new JsonObject { ["id"] = "string", ["kind"] = "string" }),
        ("pregnancy", () => new JsonObject { ["babies"] = "number", ["fraction"] = "number" }),
        ("illness", () => new JsonObject { ["bedridden"] = "boolean", ["diseaseId"] = "number", ["symptomatic"] = "boolean" }),
        ("held", () => new JsonObject { ["designId"] = "number", ["itemId"] = "number" }),
        ("dominantEmotion", () => "string"), ("typeName", () => "string"), ("locationTypeName", () => "string"),
        ("workplaceId", () => "number"), ("locationTypeId", () => "number"), ("ownerId", () => "number"), ("treasuryCents", () => "number"),
        ("resourceName", () => "string"), ("farmId", () => "number"), ("resourceId", () => "number"),
    ];

    /// <summary>Campos que a gravação viu nulos mas que têm forma conhecida quando preenchidos: viram opcionais tipados em qualquer
    /// profundidade (ex.: `action` do diretório e dos deltas de interior, `lodging` dos prédios).</summary>
    private static readonly Dictionary<string, (string Key, Func<JsonNode> Typed)[]> TypedOptionals = new()
    {
        ["shape.npc-directory.json"] = [("action", () => "number")],
        ["shape.interior-delta.json"] = [("action", () => "number")],
        ["shape.visual-delta.json"] = [("currentAction", () => "number")],
        ["shape.world-subscribe.json"] = ObservableOptionals,
        ["shape.entity-npc.json"] = ObservableOptionals,
        ["shape.entity-building.json"] = ObservableOptionals,
        ["shape.city-subscribe.json"] = [("lodging", () => new JsonObject { ["nightlyRate"] = "number", ["occupied"] = "number", ["rentPerMonth"] = "number", ["units"] = "number" }),
            // Indicadores sem fonte (AD-123): sempre nulos hoje; quando #118/#122/#124 derem fonte viram número.
            ("security", () => "number"), ("education", () => "number"), ("infrastructure", () => "number"),
            .. ObservableOptionals],
    };


    private static void ApplyTypedOptionals(string file, JsonNode node)
    {
        if (!TypedOptionals.TryGetValue(file, out var rules)) return;
        void Walk(JsonNode? n)
        {
            if (n is JsonObject obj)
            {
                foreach (var (key, typed) in rules)
                {
                    if (obj.TryGetPropertyValue(key, out var current) && current is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.String && v.GetValue<string>() == "null")
                        Optional(obj, key, typed());
                    else if (obj.TryGetPropertyValue(key + "?", out var optional) && optional is JsonValue o && o.GetValueKind() == System.Text.Json.JsonValueKind.String && o.GetValue<string>() == "null")
                        obj[key + "?"] = typed(); // AD-172: forma juntada de amostras já marca o opcional
                }
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
