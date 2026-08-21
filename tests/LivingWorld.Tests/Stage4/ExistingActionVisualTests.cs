using System.Text.RegularExpressions;
using LivingWorld.Domain;

namespace LivingWorld.Tests.Stage4;

/// <summary>Fase 15.1, T8 (LWV-02, "Data-driven visual cues for existing actions"): mesma
/// disciplina de completude cross-language de <see cref="FrontendCapabilityContractTests"/> —
/// como o catálogo vive em TS (`web/src/map-engine/actionVisuals.ts`, nenhuma lógica de decisão
/// nova, só apresentação), a prova de completude sobre o enum real (<see cref="ActionType"/>)
/// tem que ler o texto-fonte, não pode chamar TS de C#.</summary>
public sealed class ExistingActionVisualTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string CatalogPath = Path.Combine(RepoRoot, "web", "src", "map-engine", "actionVisuals.ts");
    private static readonly string AppearancePath = Path.Combine(RepoRoot, "web", "src", "npcAppearance.ts");

    [Fact]
    public void Every_action_type_has_exactly_one_visual_descriptor_in_the_catalog()
    {
        var expected = Enum.GetValues<ActionType>().Select(action => (int)action).Order();
        var actual = CatalogKeys().Order();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Unknown_action_ids_fall_back_to_a_readable_generic_descriptor_never_a_raw_enum()
    {
        string source = File.ReadAllText(CatalogPath);

        Assert.Contains("key: \"unknown\"", source, StringComparison.Ordinal);
        Assert.Contains("Atividade ${actionId}", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Sleep_is_the_only_animated_action_and_renders_the_accessible_zzz_glyph()
    {
        string source = File.ReadAllText(CatalogPath);
        var sleepEntry = Regex.Match(source, @"1:\s*\{[^}]*\}");
        Assert.True(sleepEntry.Success, "entrada da ação Sleep (ActionType = 1) não encontrada no catálogo");
        Assert.Contains("\"Zzz\"", sleepEntry.Value, StringComparison.Ordinal);
        Assert.Contains("animated: true", sleepEntry.Value, StringComparison.Ordinal);

        int animatedCount = Regex.Matches(source, "animated: true").Count;
        Assert.Equal(1, animatedCount);
    }

    [Fact]
    public void Animated_cue_declares_a_reduced_motion_fallback_that_stops_the_animation_not_the_cue()
    {
        string source = File.ReadAllText(AppearancePath);

        Assert.Contains("prefers-reduced-motion", source, StringComparison.Ordinal);
        Assert.Contains(".action-glyph-pulse{animation:none}", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_known_action_visual_declares_a_non_empty_label_and_glyph()
    {
        string source = File.ReadAllText(CatalogPath);
        var entries = Regex.Matches(source, @"(\d+):\s*\{([^}]*)\}");

        Assert.NotEmpty(entries);
        foreach (Match entry in entries)
        {
            Assert.Matches("label:\\s*\"[^\"]+\"", entry.Groups[2].Value);
            Assert.Matches("glyph:\\s*\"[^\"]+\"", entry.Groups[2].Value);
        }
    }

    [Fact]
    public void Action_cue_has_a_text_equivalent_reaching_the_accessible_npc_token()
    {
        string tokenSource = File.ReadAllText(Path.Combine(RepoRoot, "web", "src", "components", "NpcTokenSvg.tsx"));

        Assert.Contains("actionVisualFor", tokenSource, StringComparison.Ordinal);
        Assert.Contains("alt=", tokenSource, StringComparison.Ordinal);
    }

    private static IReadOnlyList<int> CatalogKeys()
    {
        string source = File.ReadAllText(CatalogPath);
        return Regex.Matches(source, @"(?m)^\s*(\d+):\s*\{")
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToList();
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
