using System.Text.RegularExpressions;

namespace LivingWorld.Tests.Unit.Invention;

/// <summary>ADR-0045, teste 4 (varredura de fonte, como <c>DeterministicMathTests</c>): leis são código, artefatos nunca. Em
/// <c>Domain</c> e <c>Simulation</c> (fora de comentário) não há nome de artefato nem parte de design com medida literal; toda
/// <c>DesignPart</c> nasce de um material ou de outra parte.</summary>
public class NoWrittenArtifactTests
{
    private static readonly Regex ArtifactWord = new(
        @"\b(boat|canoe|raft|kayak|dugout|ship|vessel|barco|canoa|jangada|bote|navio|coracle|paddle|remo|oar|sail|vela)s?\b",
        RegexOptions.IgnoreCase);

    private static readonly Regex LiteralPart = new(@"new\s+DesignPart\s*\(\s*PartShape\.\w+\s*,\s*(\(short\)\s*)?\d");

    private static IEnumerable<(string File, int Line, string Code)> CodeLines()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "LivingWorld.sln"))) root = Directory.GetParent(root)!.FullName;
        var sep = Path.DirectorySeparatorChar;
        foreach (var project in new[] { "LivingWorld.Domain", "LivingWorld.Simulation" })
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src", project), "*.cs", SearchOption.AllDirectories)
                         .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}")).Order())
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = lines[i];
                    int comment = code.IndexOf("//", StringComparison.Ordinal);
                    if (comment >= 0) code = code[..comment];
                    if (code.TrimStart().StartsWith('*')) continue;
                    yield return (file, i + 1, code);
                }
            }
    }

    [Fact]
    public void No_artifact_name_appears_in_engine_code()
    {
        var hits = CodeLines().Where(l => ArtifactWord.IsMatch(l.Code)).Select(l => $"{l.File}:{l.Line}: {l.Code.Trim()}").ToList();

        Assert.True(hits.Count == 0, string.Join('\n', hits));
    }

    [Fact]
    public void No_design_part_is_written_with_literal_measures()
    {
        var hits = CodeLines().Where(l => LiteralPart.IsMatch(l.Code)).Select(l => $"{l.File}:{l.Line}: {l.Code.Trim()}").ToList();

        Assert.True(hits.Count == 0, string.Join('\n', hits));
    }

    [Fact]
    public void The_scan_catches_a_written_artifact()
    {
        Assert.Matches(LiteralPart, "new DesignPart(PartShape.Cylinder, 6000, 500, 500, 0, 1, 0, JointKind.Fixed, -1, 0, 0, 0)");
        Assert.Matches(ArtifactWord, "var canoe = Find();");
    }
}
