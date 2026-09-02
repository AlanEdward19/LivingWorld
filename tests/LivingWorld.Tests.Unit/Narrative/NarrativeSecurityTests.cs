using LivingWorld.Domain;
using LivingWorld.Domain.Narrative;
using LivingWorld.Simulation.Narrative;
using LivingWorld.Tests.History;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetArchTest.Rules;

namespace LivingWorld.Tests.Narrative;

/// <summary>Fase 12, T9 (NARR-13..15, story "Crença separada de verdade"): blindagem estrutural
/// Verdade vs Crença sobre a superfície narrativa desta fase (endpoints + <see
/// cref="BeliefAssimilationService"/>) e par de mutação obrigatório do <see
/// cref="ClaimAnchorValidator"/> (Fase 12, T3, NARR-01..04). Mesmo padrão de <see
/// cref="HistoryQuerySeparationTests"/> (Fase 10) e <c>TruthVsBeliefBoundarySecurityTests</c>
/// (Fase 11): reflexão sobre o assembly compilado dos hosts + parse Roslyn do código-fonte
/// específico desta fase.</summary>
public class NarrativeSecurityTests
{
    // --- Verdade vs Crença: reflexão sobre os hosts (NARR-13..15) ---

    [Fact]
    public void No_hosted_project_references_HistoryTruthQuery_including_the_new_narrative_endpoints()
    {
        // Reusa o guarda estrutural já existente (Fase 10, T16): cobre os dois hosts inteiros —
        // incluindo NarrativeEndpoints.cs, introduzido nesta fase (T7) — sem duplicar a
        // implementação da checagem, só a asserção do requisito desta fase.
        var violations = HistoryQuerySeparationGuard.FindViolations();

        Assert.True(violations.Count == 0,
            "handler de jogo (incluindo endpoints narrativos) não pode referenciar HistoryTruthQuery: "
            + string.Join(", ", violations));
    }

    [Fact]
    public void Narrative_endpoints_class_is_covered_by_the_hosted_assembly_scan()
    {
        var types = Types.InAssembly(System.Reflection.Assembly.Load("LivingWorld.Api")).GetTypes();

        Assert.Contains(types, t => t.Name == "NarrativeEndpoints");
    }

    // --- Verdade vs Crença: parse do código-fonte da superfície narrativa (NARR-13..15) ---

    [Theory]
    [InlineData("WindowedHistoryAggregator.cs")]
    [InlineData("ClaimAnchorValidator.cs")]
    [InlineData("NarrativeRenderer.cs")]
    [InlineData("NpcBiographyQuery.cs")]
    [InlineData("ChronicleGenerationSystem.cs")]
    [InlineData("BeliefAssimilationService.cs")]
    public void Narrative_simulation_file_never_references_HistoryTruthQuery(string fileName)
    {
        string file = Path.Combine(FindRepoRoot(), "src", "LivingWorld.Simulation", "Narrative", fileName);
        Assert.False(ReferencesIdentifier(file, "HistoryTruthQuery"),
            $"{fileName} referencia HistoryTruthQuery — pipeline narrativo só pode ler crença (HistoryBeliefQuery)");
    }

    [Fact]
    public void NarrativeEndpoints_source_never_references_HistoryTruthQuery()
    {
        string file = Path.Combine(FindRepoRoot(), "src", "LivingWorld.Api", "NarrativeEndpoints.cs");
        Assert.False(ReferencesIdentifier(file, "HistoryTruthQuery"),
            "NarrativeEndpoints.cs referencia HistoryTruthQuery — endpoints de jogo só podem ler crença");
    }

    [Fact]
    public void Scanner_flags_a_mutated_narrative_source_that_reads_truth_directly()
    {
        // Prova que o scanner acima não é vácuo (mesmo espírito de
        // ArchitectureTests.Fitness_symbol_scanner_flags_a_banned_member_name_in_scratch_source):
        // uma fonte mutante que de fato referencia HistoryTruthQuery dentro do pipeline narrativo
        // precisa ser sinalizada.
        const string mutatedSource = """
            namespace Fixture;

            public static class MutatedNarrativeHandler
            {
                public static object Leak() =>
                    LivingWorld.Simulation.History.HistoryTruthQuery.GetFact(null!, default);
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(mutatedSource);
        bool referencesTruthQuery = tree.GetCompilationUnitRoot().DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(id => id.Identifier.Text == "HistoryTruthQuery");

        Assert.True(referencesTruthQuery);
    }

    // --- Par de mutação obrigatório: ClaimAnchorValidator (Fase 12, T3, NARR-01..04) ---

    [Fact]
    public void ValidateClaims_mutation_pair_kills_an_off_by_one_mutant_on_event_ids_count()
    {
        // Mutação candidata: trocar `claim.EventIds.Count > 0` (produção) por `>= 0` — aceitaria
        // todo claim, inclusive sem ancoragem. O par (claim ancorado vs órfão) força resultados
        // opostos no validador real; o mutante trataria os dois como aprovados, divergindo do
        // real e sendo pego por este teste.
        var anchored = new NarrativeClaim("ancorado", [1]);
        var orphan = new NarrativeClaim("sem ancoragem", []);

        var outcome = ClaimAnchorValidator.ValidateClaims([anchored, orphan]);

        Assert.Contains(anchored, outcome.Approved);
        Assert.DoesNotContain(orphan, outcome.Approved);

        bool MutatedAlwaysAccepts(NarrativeClaim c) => c.EventIds.Count >= 0;
        Assert.True(MutatedAlwaysAccepts(orphan));
        Assert.NotEqual(MutatedAlwaysAccepts(orphan), outcome.Approved.Contains(orphan));
    }

    [Fact]
    public void ValidateProse_mutation_pair_kills_a_mutant_that_skips_orphan_detection()
    {
        // Mutação candidata: ValidateProse sempre devolve Ok, pulando a checagem de nome/número
        // órfão (NARR-03/04). O par (prosa com órfão vs sem órfão) força resultados opostos no
        // validador real; o mutante trataria os dois como sucesso, divergindo do real.
        var claims = new List<NarrativeClaim> { new("colheita normal", [1]) };
        const string proseWithOrphanNumber = "a colheita rendeu 500 sacos.";
        const string proseWithoutOrphan = "a colheita foi normal.";

        var withOrphan = ClaimAnchorValidator.ValidateProse(proseWithOrphanNumber, claims);
        var withoutOrphan = ClaimAnchorValidator.ValidateProse(proseWithoutOrphan, claims);

        Assert.False(withOrphan.IsSuccess);
        Assert.True(withoutOrphan.IsSuccess);

        static Result<Unit> MutatedAlwaysOk() => Result<Unit>.Ok(Unit.Value);
        Assert.True(MutatedAlwaysOk().IsSuccess);
        Assert.NotEqual(MutatedAlwaysOk().IsSuccess, withOrphan.IsSuccess);
    }

    private static bool ReferencesIdentifier(string file, string identifier) =>
        CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetCompilationUnitRoot()
            .DescendantNodes().OfType<IdentifierNameSyntax>().Any(n => n.Identifier.Text == identifier);

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
