using System.Text.RegularExpressions;
using LivingWorld.Api.Visual;

namespace LivingWorld.Tests.Stage4;

public sealed class FrontendCapabilityContractTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string RegistryPath = Path.Combine(RepoRoot, "web", "src", "state", "frontendCapabilityConsumers.ts");
    private static readonly string StorePath = Path.Combine(RepoRoot, "web", "src", "state", "simulationStore.ts");
    private static readonly string StreamPath = Path.Combine(RepoRoot, "web", "src", "data", "real", "tickStreamSource.ts");
    private static readonly string ContractsPath = Path.Combine(RepoRoot, "web", "src", "data", "contracts.ts");

    [Fact]
    public void Every_catalog_consumer_key_is_registered_exactly_once()
    {
        var expected = LivingWorldCapabilityCatalog.All.SelectMany(capability => capability.ConsumerKeys).Order();
        var actual = RegistryKeys().Order();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Registry_applies_deltas_instead_of_only_listing_keys()
    {
        string source = File.ReadAllText(RegistryPath);

        Assert.Contains("export function applyLivingDelta", source, StringComparison.Ordinal);
        Assert.Contains("npcUpserts", source, StringComparison.Ordinal);
        Assert.Contains("cityUpserts", source, StringComparison.Ordinal);
        Assert.Contains("buildingUpserts", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Store_uses_the_shared_normalized_delta_reducer()
    {
        string source = File.ReadAllText(StorePath);

        Assert.Contains("applyLivingDelta", source, StringComparison.Ordinal);
        Assert.Contains("livingStateOf", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Store_detects_duplicates_and_sequence_gaps()
    {
        string source = File.ReadAllText(StorePath);

        Assert.Contains("delta.sequence <= this.lastSequence", source, StringComparison.Ordinal);
        Assert.Contains("delta.fromSequence !== this.lastSequence", source, StringComparison.Ordinal);
        Assert.Contains("loadSnapshot", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Real_stream_preserves_envelope_sequence_in_the_typed_delta()
    {
        string source = File.ReadAllText(StreamPath);
        string contracts = File.ReadAllText(ContractsPath);

        Assert.Contains("fromSequence", source, StringComparison.Ordinal);
        Assert.Contains("toCursor.sequence", source, StringComparison.Ordinal);
        Assert.Contains("sequence?: number", contracts, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> RegistryKeys()
    {
        string source = File.ReadAllText(RegistryPath);
        return Regex.Matches(source, "^\\s*\\\"([^\\\"]+)\\\"\\s*:", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToList();
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "LivingWorld.sln")))
            current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("raiz do repositório não encontrada");
    }
}
