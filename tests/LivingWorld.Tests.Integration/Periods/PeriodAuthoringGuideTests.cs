using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using LivingWorld.Tests.Shared;

namespace LivingWorld.Tests.Integration.Periods;

/// <summary>Fase 13, PERIOD-12: o exemplo de <c>periodDefinition</c> publicado no guia para IA
/// externa (<c>docs/domain/period-authoring-example.md</c>) é aceito por <c>POST /periods</c>
/// sem transformação. Lê o <c>.md</c> do disco: guia inválido reprova o gate.</summary>
public class PeriodAuthoringGuideTests : IClassFixture<LivingWorldApiFactory>
{
    private readonly LivingWorldApiFactory _factory;

    public PeriodAuthoringGuideTests(LivingWorldApiFactory factory) => _factory = factory;

    private static string GuideExampleJson(string? corruptField = null)
    {
        string doc = File.ReadAllText(Path.Combine(FindRepoRoot(), "docs", "domain", "period-authoring-example.md"));
        const string open = "```json\n";
        int start = doc.IndexOf(open, StringComparison.Ordinal);
        Assert.True(start >= 0, "o guia não tem bloco ```json");
        start += open.Length;
        int end = doc.IndexOf("```", start, StringComparison.Ordinal);
        string json = doc[start..end];
        if (corruptField is null) return json;
        var node = JsonNode.Parse(json)!.AsObject();
        node.Remove(corruptField);
        return node.ToJsonString();
    }

    private static StringContent Envelope(string periodId, string definitionJson) =>
        new($$"""{"PeriodId":"{{periodId}}","Version":1,"PeriodDefinition":{{definitionJson}},"Source":"guide"}""",
            Encoding.UTF8, "application/json");

    [Fact]
    public async Task Guide_example_is_accepted_by_post_periods_and_readable_back()
    {
        var client = _factory.CreateClient();
        string periodId = $"guide-{Guid.NewGuid():N}";

        var post = await client.PostAsync("/periods", Envelope(periodId, GuideExampleJson()));
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        var get = await client.GetAsync($"/periods/{periodId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Contains(periodId, await get.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Guide_example_with_a_required_field_removed_is_rejected()
    {
        var client = _factory.CreateClient();

        var post = await client.PostAsync("/periods", Envelope($"guide-bad-{Guid.NewGuid():N}", GuideExampleJson("Width")));

        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
