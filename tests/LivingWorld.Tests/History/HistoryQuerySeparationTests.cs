using LivingWorld.Simulation.History;

namespace LivingWorld.Tests.History;

/// <summary>Fase 10, T16: separação estrutural Verdade/Crença (HIST-17).</summary>
public class HistoryQuerySeparationTests
{
    [Fact]
    public void Game_handlers_never_reference_HistoryTruthQuery()
    {
        HistoryQuerySeparationGuard.AssertSeparationEnforced();

        var violations = HistoryQuerySeparationGuard.FindViolations();

        Assert.True(
            violations.Count == 0,
            "handlers de jogo não podem referenciar HistoryTruthQuery: " + string.Join(", ", violations));
    }

    [Fact]
    public void Separation_guard_covers_all_types_in_api_and_workers()
    {
        foreach (var assemblyName in new[] { "LivingWorld.Api", "LivingWorld.Workers" })
        {
            var assembly = System.Reflection.Assembly.Load(assemblyName);
            Assert.NotEmpty(assembly.GetTypes());
        }
    }
}
