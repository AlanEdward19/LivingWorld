using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.BannedApiAnalyzers;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace LivingWorld.Tests.Unit;

/// <summary>Para cada símbolo listado em BannedSymbols.txt, um fixture que o usa reprova com
/// o analyzer ligado. A lista de símbolos vem do próprio arquivo: um símbolo novo sem entrada
/// em <see cref="UsageBySymbol"/> reprova o teste em vez de passar silenciosamente.</summary>
public class BannedApiAnalyzerTests
{
    private static readonly string BannedSymbolsPath = Path.Combine(FindRepoRoot(), "BannedSymbols.txt");

    private static readonly Dictionary<string, string> UsageBySymbol = new()
    {
        ["T:System.Random"] = "var x = new System.Random();",
        ["P:System.Random.Shared"] = "var x = System.Random.Shared;",
        ["P:System.DateTime.Now"] = "var x = System.DateTime.Now;",
        ["P:System.DateTime.UtcNow"] = "var x = System.DateTime.UtcNow;",
        ["M:System.Guid.NewGuid"] = "var x = System.Guid.NewGuid();",
        ["P:System.Environment.TickCount"] = "var x = System.Environment.TickCount;",
    };

    public static IEnumerable<object[]> BannedSymbolIds() =>
        File.ReadAllLines(BannedSymbolsPath)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => new object[] { l.Split(';')[0] });

    [Theory]
    [MemberData(nameof(BannedSymbolIds))]
    public async Task Banned_symbol_usage_is_flagged_by_analyzer(string symbolId)
    {
        Assert.True(UsageBySymbol.TryGetValue(symbolId, out var usage),
            $"símbolo novo em BannedSymbols.txt sem fixture de cobertura: {symbolId}");

        var diagnostics = await RunAnalyzer(usage!, useBannedFile: true);

        Assert.Contains(diagnostics, d => d.Id == "RS0030");
    }

    [Fact]
    public async Task Without_BannedSymbols_file_the_same_usage_is_not_flagged()
    {
        // Par de mutação (R5): se este teste não falhar sem o arquivo, o de cima não mede nada.
        var diagnostics = await RunAnalyzer(UsageBySymbol["T:System.Random"], useBannedFile: false);

        Assert.DoesNotContain(diagnostics, d => d.Id == "RS0030");
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzer(string statement, bool useBannedFile)
    {
        var code = $$"""
            namespace Fixture { public class C { public void M() { {{statement}} } } }
            """;

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "BannedApiFixture",
            [CSharpSyntaxTree.ParseText(code)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new CSharpSymbolIsBannedAnalyzer());
        var additionalFiles = useBannedFile
            ? ImmutableArray.Create<AdditionalText>(new BannedSymbolsAdditionalText(BannedSymbolsPath))
            : ImmutableArray<AdditionalText>.Empty;

        var withAnalyzers = compilation.WithAnalyzers(analyzers, new AnalyzerOptions(additionalFiles));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private sealed class BannedSymbolsAdditionalText(string path) : AdditionalText
    {
        public override string Path { get; } = path;
        public override SourceText GetText(CancellationToken cancellationToken = default)
            => SourceText.From(File.ReadAllText(Path));
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "BannedSymbols.txt")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("BannedSymbols.txt não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
