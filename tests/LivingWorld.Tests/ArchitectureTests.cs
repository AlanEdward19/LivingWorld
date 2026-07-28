using LivingWorld.Domain;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

    /// <summary>Task 11: prova estrutural de "zero round-trips durante o tick" — o tick não pode
    /// nem *conseguir* chamar EF Core, porque Simulation não referencia Infrastructure.</summary>
    [Fact]
    public void Simulation_never_depends_on_Infrastructure()
    {
        var simulationAssembly = System.Reflection.Assembly.Load("LivingWorld.Simulation");

        var result = Types.InAssembly(simulationAssembly)
            .Should()
            .NotHaveDependencyOn("LivingWorld.Infrastructure")
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

    /// <summary>Fase 7, T20 / FAM-22: nenhum campo, método, tipo ou parâmetro em
    /// <c>src/</c> pode se chamar "fitness", "aptidão" ou "score global" — critério estático da
    /// spec (sem função de aptidão artificial).</summary>
    [Fact]
    public void No_src_symbol_is_named_fitness_aptidao_or_score_global()
    {
        var offenders = new List<string>();
        foreach (var file in SourceFilesUnderSrc())
        {
            string text = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(text, path: file);
            var root = tree.GetCompilationUnitRoot();

            foreach (var name in CollectDeclaredSymbolNames(root))
            {
                if (IsBannedFitnessSymbol(name))
                    offenders.Add($"{file}: {name}");
            }
        }

        Assert.True(offenders.Count == 0,
            "símbolos banidos (FAM-22) encontrados: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Fitness_symbol_scanner_flags_a_banned_member_name_in_scratch_source()
    {
        const string mutatedSource = """
            namespace LivingWorld.Domain;
            public static class Mutant { public static double ComputeFitness() => 1; }
            """;

        var tree = CSharpSyntaxTree.ParseText(mutatedSource);
        Assert.Contains(
            CollectDeclaredSymbolNames(tree.GetCompilationUnitRoot()),
            name => IsBannedFitnessSymbol(name));
    }

    private static bool IsBannedFitnessSymbol(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        var normalized = name.ToLowerInvariant().Replace("_", "", StringComparison.Ordinal);
        if (normalized.Contains("fitness", StringComparison.Ordinal))
            return true;
        if (normalized.Contains("aptidao", StringComparison.Ordinal)
            || normalized.Contains("aptidão", StringComparison.Ordinal))
            return true;
        if (normalized.Contains("scoreglobal", StringComparison.Ordinal))
            return true;

        return false;
    }

    private static IEnumerable<string> CollectDeclaredSymbolNames(CompilationUnitSyntax root)
    {
        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case TypeDeclarationSyntax type:
                    yield return type.Identifier.Text;
                    break;
                case MethodDeclarationSyntax method:
                    yield return method.Identifier.Text;
                    break;
                case PropertyDeclarationSyntax property:
                    yield return property.Identifier.Text;
                    break;
                case FieldDeclarationSyntax field:
                    foreach (var variable in field.Declaration.Variables)
                        yield return variable.Identifier.Text;
                    break;
                case EventDeclarationSyntax eventDecl:
                    yield return eventDecl.Identifier.Text;
                    break;
                case ParameterSyntax parameter:
                    yield return parameter.Identifier.Text;
                    break;
            }
        }
    }

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
