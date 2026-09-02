using System.Reflection;
using LivingWorld.Simulation.History;
using NetArchTest.Rules;

namespace LivingWorld.Tests.History;

/// <summary>Guarda estrutural da fronteira Verdade/Crença (Fase 10, T16, HIST-17/HIST-18).</summary>
internal static class HistoryQuerySeparationGuard
{
    internal static bool EnforceSeparation { get; set; } = true;

    private static readonly string[] GameAssemblies = ["LivingWorld.Api", "LivingWorld.Workers"];

    internal static IReadOnlyList<string> FindViolations()
    {
        var violations = new List<string>();
        foreach (var assemblyName in GameAssemblies)
        {
            var assembly = Assembly.Load(assemblyName);
            var result = Types.InAssembly(assembly)
                .Should()
                .NotHaveDependencyOn(typeof(HistoryTruthQuery).FullName!)
                .GetResult();

            if (!result.IsSuccessful)
                violations.AddRange(result.FailingTypeNames ?? []);
        }

        return violations;
    }

    internal static void AssertSeparationEnforced()
    {
        if (!EnforceSeparation)
            throw new InvalidOperationException("checagem de separação Verdade/Crença desligada — critério inválido");
    }

    internal static bool WouldPassWithoutEnforcement()
    {
        bool previous = EnforceSeparation;
        try
        {
            EnforceSeparation = false;
            try
            {
                AssertSeparationEnforced();
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
        finally
        {
            EnforceSeparation = previous;
        }
    }
}
