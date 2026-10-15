using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using LivingWorld.Api.Admin;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.Configuration;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>Formas congeladas de <c>/admin/*</c> que o console do cliente Godot interpreta (#59 fatia E). Factory própria com
/// <c>ADMIN_TOKEN</c>: pausar e retomar mexem no relógio do host.</summary>
public class AdminContractTests
{
    private const string Token = "contrato-admin";

    [Fact]
    public async Task Pause_resume_search_and_audit_shapes_match_the_committed_contracts()
    {
        using var factory = new LivingWorldApiFactory().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["ADMIN_TOKEN"] = Token })));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(AdminEndpoints.TokenHeader, Token);
        client.DefaultRequestHeaders.Add(AdminEndpoints.ActorHeader, "contrato");

        var pause = await client.PostAsJsonAsync("/admin/world/pause", new { reason = "contrato", maxTicks = 48 });
        Assert.Equal(HttpStatusCode.OK, pause.StatusCode);
        AssertState("shape.admin-pause.json", JsonNode.Parse(await pause.Content.ReadAsStringAsync()));

        var resume = await client.PostAsync("/admin/world/resume", null);
        Assert.Equal(HttpStatusCode.OK, resume.StatusCode);
        // Retomado, ator, motivo, teto e tick da pausa vêm nulos: o mesmo contrato os aceita.
        AssertState("shape.admin-pause.json", JsonNode.Parse(await resume.Content.ReadAsStringAsync()));

        var search = await client.GetAsync("/admin/search?q=npc&kind=name");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        ApiContractTests.AssertMatchesContract("shape.admin-search.json", ApiContractTests.ShapeOf(JsonNode.Parse(await search.Content.ReadAsStringAsync())));

        var audit = await client.GetAsync("/admin/audit?fromTick=0");
        Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
        ApiContractTests.AssertMatchesContract("shape.admin-audit.json", ApiContractTests.ShapeOf(JsonNode.Parse(await audit.Content.ReadAsStringAsync())));
    }

    /// <summary>Ator, motivo, teto e tick só existem pausado: no contrato valem "qualquer valor".</summary>
    private static void AssertState(string file, JsonNode? body)
    {
        var shape = ApiContractTests.ShapeOf(body)!;
        foreach (var optional in new[] { "actor", "reason", "maxTicks", "pausedAtTick" })
            shape[optional] = "null";
        ApiContractTests.AssertMatchesContract(file, shape);
    }
}
