using LivingWorld.Simulation.History.Queries;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LivingWorld.Tests.Unit.History;

/// <summary>Fase 10, T16: par de mutação da fronteira Verdade/Crença (HIST-18).</summary>
public class HistoryQuerySeparationMutationTests
{
    [Fact]
    public void Disabling_separation_check_invalidates_the_criterion()
    {
        Assert.False(HistoryQuerySeparationGuard.WouldPassWithoutEnforcement());
    }

    [Fact]
    public void Scratch_handler_referencing_truth_query_is_detected_when_enforcement_is_on()
    {
        const string mutatedSource = """
            using LivingWorld.Domain;
            using LivingWorld.Simulation;
            using LivingWorld.Simulation.History;

            namespace LivingWorld.Api;

            public static class ForbiddenTruthHandler
            {
                public static Result<Fact> Leak(WorldState world, FactId id) =>
                    HistoryTruthQuery.GetFact(world, id);
            }
            """;

        var root = CSharpSyntaxTree.ParseText(mutatedSource).GetCompilationUnitRoot();
        bool referencesTruthQuery = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(id => id.Identifier.Text == nameof(HistoryTruthQuery));

        Assert.True(referencesTruthQuery);
    }
}
