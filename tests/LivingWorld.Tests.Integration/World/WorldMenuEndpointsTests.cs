using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using LivingWorld.Api.Periods;
using LivingWorld.Api.World;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Integration.Visual;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.World;

/// <summary>AD-175: o que o menu do jogo pede à API — cartão do período, lista completa de mundos, renomear, salvar agora,
/// criação com etapas e mundo que já abre rodando. Só acréscimos: sem os parâmetros novos tudo responde como antes.</summary>
public class WorldMenuEndpointsTests : IClassFixture<LivingWorldApiFactory>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly LivingWorldApiFactory _factory;
    public WorldMenuEndpointsTests(LivingWorldApiFactory factory) => _factory = factory;

    private static CreateWorldRequest Request(ulong seed, string name, string? period = null) =>
        new(WorldCreateEndpointsTests.FullValidScenario(seed).ToJsonString(), name, period);

    private async Task<SavedWorldSummary[]> Worlds(HttpClient client) =>
        (await client.GetFromJsonAsync<SavedWorldSummary[]>("/worlds", Web))!;

    [Fact]
    public async Task Periods_bring_a_card_with_name_era_description_and_numbers_of_each_preset()
    {
        var periods = (await _factory.CreateClient().GetFromJsonAsync<PeriodSummaryResponse[]>("/periods", Web))!;

        var village = Assert.Single(periods, p => p.PeriodId == "vila-pequena").Card;
        Assert.Equal("Vila pequena", village.Name);
        Assert.False(string.IsNullOrWhiteSpace(village.Era));
        Assert.False(string.IsNullOrWhiteSpace(village.Description));
        Assert.False(string.IsNullOrWhiteSpace(village.StartingTechnology));
        Assert.Equal(40, village.InitialPopulation);
        Assert.Equal(1, village.SettlementCount);
        Assert.Equal(["Lavrador", "Artesão"], village.Professions);
        Assert.Equal(2, village.ProfessionCount);
        Assert.True(village.InventionEnabled);
        Assert.Equal(400, Assert.Single(periods, p => p.PeriodId == "grande-metropole").Card.InitialPopulation);
    }

    [Fact]
    public async Task The_three_presets_are_the_same_era_so_the_menu_does_not_promise_other_eras()
    {
        var periods = (await _factory.CreateClient().GetFromJsonAsync<PeriodSummaryResponse[]>("/periods", Web))!;

        string[] presets = ["vila-pequena", "cidade-media", "grande-metropole"];
        Assert.Single(periods.Where(p => presets.Contains(p.PeriodId)).Select(p => p.Card.Era).Distinct());
    }

    [Fact]
    public async Task Period_detail_carries_the_same_card_as_the_list()
    {
        var client = _factory.CreateClient();
        var list = (await client.GetFromJsonAsync<PeriodSummaryResponse[]>("/periods", Web))!;
        var detail = (await client.GetFromJsonAsync<PeriodDetailResponse>("/periods/cidade-media", Web))!;

        Assert.Equal(JsonSerializer.Serialize(list.Single(p => p.PeriodId == "cidade-media").Card), JsonSerializer.Serialize(detail.Card));
    }

    [Fact]
    public async Task A_period_without_presentation_is_named_by_its_source_and_has_no_era()
    {
        var client = _factory.CreateClient();
        var definition = WorldCreateEndpointsTests.FullValidScenario(5);
        var created = await client.PostAsJsonAsync("/periods", new { periodId = "sem-cartao", version = 1, periodDefinition = definition, source = "Operador" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var card = (await client.GetFromJsonAsync<PeriodDetailResponse>("/periods/sem-cartao", Web))!.Card;

        Assert.Equal(("Operador", ""), (card.Name, card.Era));
    }

    [Fact]
    public async Task Worlds_list_says_period_year_when_played_which_is_open_and_who_created_it()
    {
        var client = _factory.CreateClient();
        var host = _factory.Services.GetRequiredService<WorldHost>();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/worlds/create", Request(75001, "Primeiro", "vila-pequena"))).StatusCode);
        host.Clock.Run(host.Current, 3);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/worlds/create", Request(75002, "Segundo"))).StatusCode);

        var worlds = await Worlds(client);
        var first = worlds.Single(w => w.Seed == 75001);
        var second = worlds.Single(w => w.Seed == 75002);

        Assert.Equal(("vila-pequena", "Vila pequena", false, "player"), (first.PeriodId, first.PeriodName, first.IsCurrent, first.Origin));
        Assert.Equal((3L, 3L / host.Current.Calendar.HoursPerYear), (first.CurrentDate, first.Year));
        Assert.Equal(("", true, "player"), (second.PeriodId, second.IsCurrent, second.Origin));
        Assert.Equal("automatic", worlds.Single(w => w.Seed == SavedWorldLibrary.BootstrapSeed).Origin);
        Assert.Single(worlds, w => w.IsCurrent);
        Assert.True(first.LastPlayedAtUtc > DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public async Task Rename_changes_the_open_world_name_without_touching_the_simulated_state()
    {
        var client = _factory.CreateClient();
        var host = _factory.Services.GetRequiredService<WorldHost>();
        await client.PostAsJsonAsync("/worlds/create", Request(75101, "Antes"));
        string hash = WorldSnapshot.CanonicalHash(host.Current);

        var response = await client.PostAsJsonAsync($"/worlds/{WorldIdentity.WorldIdFor(75101)}/rename", new RenameWorldRequest("  Vale Novo "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Vale Novo", (await response.Content.ReadFromJsonAsync<SavedWorldSummary>(Web))!.Name);
        Assert.Equal(("Vale Novo", hash), (host.Current.Name, WorldSnapshot.CanonicalHash(host.Current)));
    }

    [Fact]
    public async Task A_closed_world_renamed_opens_with_the_new_name()
    {
        var client = _factory.CreateClient();
        var host = _factory.Services.GetRequiredService<WorldHost>();
        await client.PostAsJsonAsync("/worlds/create", Request(75201, "Fechado"));
        await client.PostAsJsonAsync("/worlds/create", Request(75202, "Aberto"));

        await client.PostAsJsonAsync($"/worlds/{WorldIdentity.WorldIdFor(75201)}/rename", new RenameWorldRequest("Reaberto"));
        await client.PostAsync($"/worlds/{WorldIdentity.WorldIdFor(75201)}/load", null);

        Assert.Equal("Reaberto", host.Current.Name);
    }

    [Theory]
    [InlineData("   ", HttpStatusCode.BadRequest, "vazio")]
    [InlineData("12345678901234567890123456789012345678901", HttpStatusCode.BadRequest, "40")]
    [InlineData("linha\nquebrada", HttpStatusCode.BadRequest, "controle")]
    [InlineData("DUPLICADO", HttpStatusCode.Conflict, "Já existe")]
    public async Task Rename_rejects_an_invalid_name_with_a_clear_error(string name, HttpStatusCode status, string error)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/worlds/create", Request(75301, "Duplicado"));
        await client.PostAsJsonAsync("/worlds/create", Request(75302, "Outro nome"));

        var response = await client.PostAsJsonAsync($"/worlds/{WorldIdentity.WorldIdFor(75302)}/rename", new RenameWorldRequest(name));

        Assert.Equal(status, response.StatusCode);
        Assert.Contains(error, (await response.Content.ReadFromJsonAsync<JsonObject>())!["error"]!.GetValue<string>());
    }

    [Fact]
    public async Task Rename_of_an_unknown_world_is_404()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync($"/worlds/{Guid.Empty}/rename", new RenameWorldRequest("Qualquer"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Save_now_writes_the_open_world_and_says_when_without_changing_it()
    {
        var client = _factory.CreateClient();
        var host = _factory.Services.GetRequiredService<WorldHost>();
        await client.PostAsJsonAsync("/worlds/create", Request(75401, "Salvar"));
        host.Clock.Run(host.Current, 5); // fora da fronteira de 24 ticks do laço
        string hash = WorldSnapshot.CanonicalHash(host.Current);
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        var saved = (await (await client.PostAsync("/world/save", null)).Content.ReadFromJsonAsync<SaveWorldResponse>(Web))!;

        Assert.Equal((WorldIdentity.WorldIdFor(75401), 5L, hash), (saved.WorldId, saved.Tick, WorldSnapshot.CanonicalHash(host.Current)));
        Assert.True(saved.SavedAtUtc >= before);
        Assert.Equal(5, (await Worlds(client)).Single(w => w.Seed == 75401).CurrentDate);
    }

    [Fact]
    public async Task Without_startRunning_a_created_or_loaded_world_stays_paused_as_before()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/worlds/create", Request(75501, "Pausado"));

        Assert.True(_factory.Services.GetRequiredService<SimulationHost>().IsPaused);
    }

    [Fact]
    public async Task StartRunning_without_the_admin_token_is_403_and_creates_nothing()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/worlds/create?startRunning=true", Request(75601, "Sem token"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(await Worlds(client), w => w.Seed == 75601);
    }

    [Fact]
    public async Task StartRunning_with_the_admin_token_creates_and_loads_a_running_world()
    {
        var admin = _factory.CreateAdminClient();
        var simulation = _factory.Services.GetRequiredService<SimulationHost>();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/worlds/create?startRunning=true", Request(75701, "Rodando"))).StatusCode);
        Assert.False(simulation.IsPaused);
        await admin.PostAsJsonAsync("/worlds/create", Request(75702, "Outro"));

        Assert.Equal(HttpStatusCode.Forbidden, (await _factory.CreateClient().PostAsync($"/worlds/{WorldIdentity.WorldIdFor(75701)}/load?startRunning=true", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/worlds/{WorldIdentity.WorldIdFor(75701)}/load?startRunning=true", null)).StatusCode);
        Assert.False(simulation.IsPaused);
    }

    [Fact]
    public async Task Async_creation_reports_real_stages_and_ends_with_the_same_answer_as_create()
    {
        var client = _factory.CreateClient();
        var accepted = await client.PostAsJsonAsync("/worlds/create/async", Request(75801, "Aos poucos", "vila-pequena"));
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var body = (await accepted.Content.ReadFromJsonAsync<WorldCreationAccepted>(Web))!;
        Assert.Equal(["terrain", "settlements", "families", "economy", "saving", "ready"], body.Stages.Select(s => s.Id));

        var job = await WaitFor(client, body.StatusUrl);

        Assert.Equal((false, 200, "ready", "Aos poucos"), (job.Failed, job.Status, job.Stage, job.Result!.Name));
        Assert.Equal(WorldIdentity.WorldIdFor(75801), _factory.Services.GetRequiredService<SavedWorldLibrary>().CurrentId);
    }

    [Fact]
    public async Task Async_creation_failures_come_back_in_the_job_with_the_create_status()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/worlds/create", Request(75901, "Original"));
        var duplicate = (await (await client.PostAsJsonAsync("/worlds/create/async", Request(75901, "Repetido"))).Content.ReadFromJsonAsync<WorldCreationAccepted>(Web))!;
        var broken = (await (await client.PostAsJsonAsync("/worlds/create/async", new CreateWorldRequest("{}", "Quebrado"))).Content.ReadFromJsonAsync<WorldCreationAccepted>(Web))!;

        var conflict = await WaitFor(client, duplicate.StatusUrl);
        var invalid = await WaitFor(client, broken.StatusUrl);

        Assert.Equal((true, 409), (conflict.Failed, conflict.Status));
        Assert.Equal((true, 400), (invalid.Failed, invalid.Status));
        Assert.NotEqual("", invalid.Error);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/worlds/create/jobs/{Guid.Empty}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/worlds/create/async", new CreateWorldRequest("{}", " "))).StatusCode);
    }

    [Fact]
    public async Task New_menu_responses_match_the_committed_contract()
    {
        var client = _factory.CreateClient();
        var accepted = (await (await client.PostAsJsonAsync("/worlds/create/async", Request(76001, "Contrato"))).Content.ReadFromJsonAsync<WorldCreationAccepted>(Web))!;
        await WaitFor(client, accepted.StatusUrl);
        var job = JsonNode.Parse(await client.GetStringAsync(accepted.StatusUrl))!;
        var save = JsonNode.Parse(await (await client.PostAsync("/world/save", null)).Content.ReadAsStringAsync())!;

        var jobShape = ApiContractTests.ShapeOf(job);
        ApiContractTests.Optional(jobShape, "result", ApiContractTests.ShapeOf(job["result"]));
        ApiContractTests.AssertMatchesContract("shape.world-create-job.json", jobShape);
        ApiContractTests.AssertMatchesContract("shape.world-save.json", ApiContractTests.ShapeOf(save));
    }

    [Fact]
    public async Task A_world_saved_before_these_fields_is_listed_with_honest_defaults()
    {
        string root = Path.Combine(Path.GetTempPath(), "lw-menu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using (var factory = DiskFactory(root))
            {
                var client = factory.CreateClient();
                await client.PostAsJsonAsync("/worlds/create", Request(76101, "Velho", "vila-pequena"));
                await client.PostAsJsonAsync("/worlds/create", Request(76102, "Atual"));
            }
            string file = Path.Combine(root, "worlds", $"{WorldIdentity.WorldIdFor(76101):D}.json");
            var stored = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            foreach (var key in new[] { "PeriodId", "Year", "Origin", "LastPlayedAtUtc" }) stored.Remove(key);
            File.WriteAllText(file, stored.ToJsonString()); // forma gravada antes do AD-175

            using (var factory = DiskFactory(root))
            {
                var old = (await Worlds(factory.CreateClient())).Single(w => w.Seed == 76101);
                Assert.Equal(("", 0L, "player", false), (old.PeriodId, old.Year, old.Origin, old.IsCurrent));
                Assert.Equal(old.LastSavedAtUtc, old.LastPlayedAtUtc);
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<global::Program> DiskFactory(string root) =>
        new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<global::Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:World", $"Data Source={Path.Combine(root, "legacy.db")};Pooling=False");
            b.UseSetting("WorldsDirectory", Path.Combine(root, "worlds"));
            b.UseSetting("TICK_LOOP_ENABLED", "false");
        });

    private static async Task<WorldCreationJobResponse> WaitFor(HttpClient client, string statusUrl)
    {
        for (int i = 0; i < 600; i++)
        {
            var job = (await client.GetFromJsonAsync<WorldCreationJobResponse>(statusUrl, Web))!;
            if (job.Done) return job;
            await Task.Delay(50);
        }
        throw new TimeoutException("criação assíncrona não terminou em 30 s");
    }
}
