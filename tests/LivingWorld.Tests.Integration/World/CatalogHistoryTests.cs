using System.Net.Http.Json;
using System.Text.Json.Nodes;
using LivingWorld.Api.Narrative;
using LivingWorld.Api.World;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.World;

public class CatalogHistoryTests(LivingWorldApiFactory factory) : IClassFixture<LivingWorldApiFactory>
{
    [Theory]
    [InlineData("medieval")]
    [InlineData("prehistoric")]
    [InlineData("modern")]
    [InlineData("futuristic")]
    [InlineData("creatures")]
    public async Task Menu_created_catalog_world_accumulates_history_and_exposes_narratives(string period)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LivingWorld.sln"))) dir = dir.Parent;
        string json = File.ReadAllText(Path.Combine(dir!.FullName, "scenarios", "periods", $"{period}.json"));
        var root = JsonNode.Parse(json)!.AsObject();
        root["Seed"] = period switch { "medieval" => 91, "prehistoric" => 92, "modern" => 93, "futuristic" => 94, _ => 95 };
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/worlds/create", new CreateWorldRequest(root.ToJsonString(), period));
        response.EnsureSuccessStatusCode();
        var host = factory.Services.GetRequiredService<WorldHost>();
        Assert.True(host.Current.HistoryRules.Enabled);
        host.Clock.Run(host.Current, 3 * 360 * 24);
        Assert.NotEmpty(host.Current.Facts);
        var reports = await client.GetFromJsonAsync<List<ReportResponse>>("/narratives/reports");
        Assert.NotEmpty(reports!);
        var report = host.Current.Reports.First();
        var beliefs = await client.GetFromJsonAsync<List<BeliefResponse>>($"/history/beliefs?communityId={report.CommunityId.Value}");
        Assert.Contains(beliefs!, belief => belief.ReportId == report.Id.Value);
        var fact = host.Current.Facts.First(f => f.Participants.Count > 0);
        var biography = await client.GetFromJsonAsync<BiographyResponse>($"/narratives/biographies/{fact.Participants[0].Value}");
        Assert.NotEmpty(biography!.Claims);
        var chronicle = await client.GetFromJsonAsync<ChronicleResponse>($"/narratives/chronicles?periodStart=0&periodEnd={host.Current.CurrentDate.TotalHours + 1}");
        Assert.NotEmpty(chronicle!.Claims);
    }
}
