using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LivingWorld.Tests.Shared;

/// <summary>Compila um snippet C# em memória contra um conjunto declarado de assemblies
/// (R5: fronteira provada por compilação Roslyn, não por leitura humana de código).</summary>
public static class InMemoryCompiler
{
    public static ImmutableArrayDiagnostics Compile(string code, params Type[] referenceAssembliesFrom)
    {
        var references = TrustedPlatformAssemblies()
            .Concat(referenceAssembliesFrom.Select(t => MetadataReference.CreateFromFile(t.Assembly.Location)))
            .ToArray();

        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            assemblyName: "InMemoryCompilerFixture",
            syntaxTrees: [tree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return new ImmutableArrayDiagnostics(compilation.GetDiagnostics());
    }

    private static IEnumerable<MetadataReference> TrustedPlatformAssemblies()
    {
        // TRUSTED_PLATFORM_ASSEMBLIES do host de teste inclui TODAS as ProjectReference do
        // próprio LivingWorld.Tests (AI, Api, Workers...) — exatamente as fronteiras que este
        // fixture existe para provar que Simulation NÃO tem. Só o runtime compartilhado entra
        // daqui; qualquer assembly LivingWorld.* vem só do parâmetro explícito do chamador.
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return paths
            .Where(p => !Path.GetFileName(p).StartsWith("LivingWorld.", StringComparison.Ordinal))
            .Select(p => MetadataReference.CreateFromFile(p));
    }
}

public readonly struct ImmutableArrayDiagnostics(IEnumerable<Diagnostic> diagnostics)
{
    public bool HasError(string diagnosticId) => diagnostics.Any(d => d.Id == diagnosticId);
    public override string ToString() => string.Join("\n", diagnostics.Select(d => d.ToString()));
}
