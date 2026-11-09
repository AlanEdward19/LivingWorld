using System.Text.RegularExpressions;
using LivingWorld.Simulation.Extraordinary.Mechanics;

namespace LivingWorld.Tests.Unit.Extraordinary;

/// <summary>Issue #64, critério 3: o vocabulário de atributo do NPC vive só em
/// <c>NpcStatAccessors</c>; nenhum switch/literal de atributo nas demais mecânicas.</summary>
public sealed class NpcStatTableArchitectureTests
{
    private const string TableFile = "NpcStatAccessors.cs";

    // Literal exato ("health", "carrier.hunger", "bond.share:sleep") ou com argumentos
    // ("carrier.health:", "npc.thirst:-5"). Prosa com a palavra solta não casa: exige aspas coladas.
    private static Regex AttributeLiteral() => new(
        "\"(?:(?:npc|carrier|transfer)\\.|bond\\.share:)?(?:"
        + string.Join('|', NpcStatAccessors.All.Select(stat => Regex.Escape(stat.Key)))
        + ")(?::[^\"]*)?\"",
        RegexOptions.CultureInvariant);

    [Fact]
    public void No_attribute_name_literal_exists_in_Simulation_Extraordinary_outside_the_stat_table()
    {
        var regex = AttributeLiteral();
        var offenders = Scan(regex).ToList();

        Assert.True(offenders.Count == 0, "literal/switch de atributo fora da tabela: " + string.Join("; ", offenders));
    }

    [Fact]
    public void Scanner_flags_a_reintroduced_attribute_switch()
    {
        const string mutant = """
            switch (key) { case "health": npc.SetHealth(1); break; case "carrier.hunger": break; }
            """;

        Assert.Matches(AttributeLiteral(), mutant);
    }

    [Theory]
    [InlineData("var key = \"carrier.health:\";")]
    [InlineData("var key = \"npc.thirst:-5\";")]
    [InlineData("var key = \"bond.share:sleep\";")]
    [InlineData("var key = \"transfer.social\";")]
    public void Scanner_flags_literals_with_colon_arguments(string source) =>
        Assert.Matches(AttributeLiteral(), source);

    [Theory]
    [InlineData("var text = $\"hunger={value}\";")]
    [InlineData("// a saúde (health) do alvo")]
    [InlineData("var key = \"household.resource.9\";")]
    public void Scanner_ignores_prose_and_unrelated_literals(string source) =>
        Assert.DoesNotMatch(AttributeLiteral(), source);

    private static IEnumerable<string> Scan(Regex regex)
    {
        string root = Path.Combine(FindRepoRoot(), "src", "LivingWorld.Simulation", "Extraordinary");
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) == TableFile) continue;
            foreach (var (line, index) in File.ReadAllLines(file).Select((line, index) => (line, index)))
                if (regex.IsMatch(line))
                    yield return $"{Path.GetRelativePath(root, file)}:{index + 1}";
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }
}
