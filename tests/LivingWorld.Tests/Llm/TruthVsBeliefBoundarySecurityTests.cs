using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LivingWorld.Tests.Llm;

/// <summary>Fase 11, T8 (LLM-15), story "Segurança de rede e injeção": prova estrutural (mesmo
/// padrão de <c>ArchitectureTests.No_src_symbol_is_named_fitness_...</c> — parse Roslyn do texto
/// fonte, nunca reflexão de tipos carregados, já que os "handlers" hoje são endpoints minimal-API
/// declarados como lambdas dentro de top-level statements) de que nenhum handler de jogo/API
/// acessa consulta de Verdade para montar contexto de LLM — só <c>NpcBeliefQuery</c>/Crença entra
/// no prompt (spec.md, AC "segredo... fica fora do prompt", "contexto usa só crença").</summary>
public class TruthVsBeliefBoundarySecurityTests
{
    [Fact]
    public void LlmContextAssembler_is_the_only_place_in_src_that_constructs_an_LlmContext()
    {
        var offenders = SourceFilesUnderSrc()
            .Where(f => Path.GetFileName(f) != "LlmContextAssembler.cs")
            .Where(ConstructsLlmContext)
            .ToList();

        Assert.True(offenders.Count == 0,
            "LlmContext construído fora do funil (LlmContextAssembler): " + string.Join(", ", offenders));
    }

    [Fact]
    public void LlmContextAssembler_never_references_HistoryTruthQuery()
    {
        var file = SourceFilesUnderSrc().Single(f => Path.GetFileName(f) == "LlmContextAssembler.cs");

        Assert.False(ReferencesIdentifier(file, "HistoryTruthQuery"),
            "LlmContextAssembler referencia HistoryTruthQuery — deveria usar só NpcBeliefQuery");
    }

    /// <summary>"Handlers de jogo/API que hoje existem" = todo o código dos dois hosts
    /// executáveis do repo (Api: endpoints HTTP; Workers: comandos de CLI). Nenhum dos dois pode
    /// referenciar consulta de Verdade — hoje nenhum constrói <c>LlmContext</c> fora do funil
    /// acima, então esta prova cobre também qualquer handler futuro que tente ler Verdade direto.</summary>
    [Theory]
    [InlineData("LivingWorld.Api")]
    [InlineData("LivingWorld.Workers")]
    public void No_handler_in_a_hosted_project_references_HistoryTruthQuery(string project)
    {
        var offenders = SourceFilesUnderSrc()
            .Where(f => f.Contains($"{Path.DirectorySeparatorChar}{project}{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => ReferencesIdentifier(f, "HistoryTruthQuery"))
            .ToList();

        Assert.True(offenders.Count == 0,
            $"handler em {project} referencia HistoryTruthQuery: " + string.Join(", ", offenders));
    }

    /// <summary>Prova que o scanner acima não é vácuo: uma fonte mutante que de fato referencia
    /// <c>HistoryTruthQuery</c> dentro de um handler precisa ser sinalizada (mesmo espírito do
    /// <c>Fitness_symbol_scanner_flags_a_banned_member_name_in_scratch_source</c> de
    /// <c>ArchitectureTests</c>).</summary>
    [Fact]
    public void Scanner_flags_a_mutated_handler_that_reads_truth_directly()
    {
        const string mutatedSource = """
            namespace Fixture;

            public class MutatedHandler
            {
                public string Handle() => LivingWorld.Simulation.History.HistoryTruthQuery.FactsOf(null!, default).ToString()!;
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(mutatedSource);
        Assert.True(ReferencesIdentifierInTree(tree.GetCompilationUnitRoot(), "HistoryTruthQuery"));
    }

    [Fact]
    public void Object_creation_scanner_flags_a_mutated_source_that_constructs_LlmContext_directly()
    {
        const string mutatedSource = """
            namespace Fixture;

            public class MutatedHandler
            {
                public object Handle() => new LivingWorld.Domain.Llm.LlmContext("s", "u", new string[0]);
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(mutatedSource);
        Assert.True(ConstructsLlmContextInTree(tree.GetCompilationUnitRoot()));
    }

    private static bool ConstructsLlmContext(string file) =>
        ConstructsLlmContextInTree(CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetCompilationUnitRoot());

    // ponytail: só cobre `new LlmContext(...)` com tipo explícito (é como o funil real
    // (LlmContextAssembler.cs) constrói hoje). `new(...)` implícito exigiria resolver o tipo alvo
    // via semantic model (Compilation completa) para não gerar falso positivo em qualquer outro
    // `new(...)` do repo — fora do escopo desta task; nenhum código de src/ usa `new(...)`
    // implícito para LlmContext hoje.
    private static bool ConstructsLlmContextInTree(CompilationUnitSyntax root) =>
        root.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Any(o => LastIdentifierOf(o.Type) == "LlmContext");

    private static string? LastIdentifierOf(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        GenericNameSyntax generic => generic.Identifier.Text,
        _ => null,
    };

    private static bool ReferencesIdentifier(string file, string identifier) =>
        ReferencesIdentifierInTree(CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetCompilationUnitRoot(), identifier);

    private static bool ReferencesIdentifierInTree(CompilationUnitSyntax root, string identifier) =>
        root.DescendantNodes().OfType<IdentifierNameSyntax>().Any(n => n.Identifier.Text == identifier);

    private static IEnumerable<string> SourceFilesUnderSrc()
    {
        string repoRoot = FindRepoRoot();
        var srcDir = Path.Combine(repoRoot, "src");
        foreach (var file in Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories))
            if (!file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                yield return file;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
