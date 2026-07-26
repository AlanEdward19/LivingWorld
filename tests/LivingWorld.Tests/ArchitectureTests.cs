using LivingWorld.Domain;
using NetArchTest.Rules;

namespace LivingWorld.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_has_zero_dependency_on_any_other_project_assembly()
    {
        var result = Types.InAssembly(typeof(Money).Assembly)
            .Should()
            .NotHaveDependencyOnAny(
                "LivingWorld.Simulation", "LivingWorld.Infrastructure",
                "LivingWorld.AI", "LivingWorld.Api", "LivingWorld.Workers")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Simulation_never_depends_on_AI()
    {
        var simulationAssembly = System.Reflection.Assembly.Load("LivingWorld.Simulation");

        var result = Types.InAssembly(simulationAssembly)
            .Should()
            .NotHaveDependencyOn("LivingWorld.AI")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Using_LivingWorld_AI_from_Simulation_fails_to_compile()
    {
        const string code = """
            using LivingWorld.AI;
            namespace Fixture { public class ForbiddenUsage { } }
            """;

        // Referencia só o que Simulation referencia de verdade: Domain — nunca AI.
        // "LivingWorld" já existe (via Domain), então o sub-namespace ausente dá CS0234,
        // não CS0246 (que é para uma raiz de namespace totalmente desconhecida).
        var diagnostics = InMemoryCompiler.Compile(code, typeof(Money));

        Assert.True(diagnostics.HasError("CS0234"), diagnostics.ToString());
    }
}
