using System.Text.RegularExpressions;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.History;

namespace LivingWorld.Tests.Integration.Stage4;

/// <summary>T8 + T28 (LWV-07.5): completude cross-language do catálogo unificado
/// <c>web/src/map-engine/npcAnimationCatalog.ts</c> contra <see cref="ActionType"/> real.
/// Travel oculto é a única exceção documentada; o restante precisa de spec animada.</summary>
public sealed class ExistingActionVisualTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string CatalogPath = Path.Combine(RepoRoot, "web", "src", "map-engine", "npcAnimationCatalog.ts");
    private static readonly string GlobalCssPath = Path.Combine(RepoRoot, "web", "src", "styles", "global.css");

    [Fact]
    public void Every_action_type_has_exactly_one_visual_descriptor_in_the_catalog()
    {
        var expected = Enum.GetValues<ActionType>().Select(action => (int)action).Order();
        var fromIds = ParseIntArray("ACTION_TYPE_IDS").Order();
        var fromSpecs = ActionSpecKeys().Order();

        Assert.Equal(expected, fromIds);
        Assert.Equal(expected, fromSpecs);
    }

    [Fact]
    public void Unknown_action_ids_fall_back_to_a_readable_generic_descriptor_never_a_raw_enum()
    {
        string source = CatalogSource();

        Assert.Contains("spec(\"unknown\"", source, StringComparison.Ordinal);
        Assert.Contains("Atividade ${labelHint}", source, StringComparison.Ordinal);
        Assert.Contains("animationSpecForUnknown", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_mapped_action_except_hidden_travel_is_animated()
    {
        string source = CatalogSource();
        Assert.Contains("\"moon\"", ActionLine(1), StringComparison.Ordinal);

        foreach (var id in Enum.GetValues<ActionType>().Select(action => (int)action))
        {
            if (id == (int)ActionType.Travel)
            {
                Assert.Contains("hidden: true", ActionLine(id), StringComparison.Ordinal);
                Assert.DoesNotContain("animated: true", ActionLine(id), StringComparison.Ordinal);
                continue;
            }

            Assert.True(
                ActionLineIsAnimated(id),
                $"ActionType {(ActionType)id} ({id}) não tem spec animada no catálogo");
        }

        Assert.Contains("reducedMotionFallback: \"static-icon\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Travel_is_the_only_action_hidden_from_the_cue_walking_around_is_not_worth_a_badge()
    {
        string travelEntry = ActionLine((int)ActionType.Travel);
        Assert.Contains("hidden: true", travelEntry, StringComparison.Ordinal);

        int hiddenCount = Regex.Matches(ActionSpecsBlock(), "hidden: true").Count;
        Assert.Equal(1, hiddenCount);
    }

    [Fact]
    public void Animated_cue_declares_a_reduced_motion_fallback_that_stops_the_animation_not_the_cue()
    {
        string source = File.ReadAllText(GlobalCssPath);

        Assert.Contains("prefers-reduced-motion", source, StringComparison.Ordinal);
        Assert.Contains(".npc-action-badge-pulse { animation: none; }", source, StringComparison.Ordinal);
        Assert.Contains(".npc-anim-eat", source, StringComparison.Ordinal);
        Assert.Contains(".npc-anim-rest", source, StringComparison.Ordinal);

        foreach (Match block in Regex.Matches(source, @"@media \(prefers-reduced-motion: reduce\) \{([^}]+)\}"))
        {
            string body = block.Groups[1].Value;
            if (!body.Contains("npc-anim", StringComparison.Ordinal)
                && !body.Contains("npc-rest-cue", StringComparison.Ordinal)
                && !body.Contains("npc-action-badge", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.Contains("animation: none", body, StringComparison.Ordinal);
            Assert.DoesNotContain("display: none", body, StringComparison.Ordinal);
            Assert.DoesNotContain("visibility: hidden", body, StringComparison.Ordinal);
            Assert.DoesNotContain("opacity: 0", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Every_known_action_visual_declares_a_non_empty_label_and_icon()
    {
        var entries = Regex.Matches(ActionSpecsBlock(), @"(\d+):\s*spec\(""([^""]+)"",\s*""([^""]+)"",\s*""([^""]+)""");

        Assert.Equal(Enum.GetValues<ActionType>().Length, entries.Count);
        foreach (Match entry in entries)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Groups[2].Value));
            Assert.False(string.IsNullOrWhiteSpace(entry.Groups[3].Value));
            Assert.False(string.IsNullOrWhiteSpace(entry.Groups[4].Value));
        }
    }

    /// <summary>Motor/projector descriptor keys — not the TS <c>STAGE4_PROCESS_DESCRIPTORS</c>
    /// export. Completeness must fail if <c>PROCESS_SPECS</c> loses a key even when that list
    /// is also deleted.</summary>
    private static readonly string[] MotorStage4ProcessDescriptors =
    [
        "sleep-ground", "sleep-dwelling", "sleep-bed",
        "eat-raw", "eat-prepared",
        "cook-food", "collect-water", "carry-water", "deliver-water",
        "plant-crop", "water-crop", "harvest-crop",
        "construction",
    ];

    private static readonly WorldEventKind[] Lwv07EventKinds =
    [
        WorldEventKind.Birth, WorldEventKind.Death, WorldEventKind.Starvation,
        WorldEventKind.Marriage, WorldEventKind.CourtshipStarted,
        WorldEventKind.CourtshipRejected, WorldEventKind.CourtshipSucceeded,
        WorldEventKind.MaternalDeath, WorldEventKind.StillBirth,
    ];

    [Fact]
    public void Every_stage4_process_descriptor_has_an_animated_catalog_spec()
    {
        string block = ProcessSpecsBlock();
        var specKeys = ProcessSpecKeys();
        foreach (string descriptor in MotorStage4ProcessDescriptors)
        {
            Assert.Contains(descriptor, specKeys);
            Assert.True(
                ProcessLineIsAnimated(block, descriptor),
                $"process descriptor '{descriptor}' não está animado no catálogo");
        }
    }

    [Fact]
    public void Process_specs_map_contains_construction_independent_of_the_ts_list()
    {
        Assert.Contains("construction", ProcessSpecKeys());
        Assert.True(ProcessLineIsAnimated(ProcessSpecsBlock(), "construction"));
    }

    [Fact]
    public void Every_lwv07_event_kind_has_an_animated_catalog_spec()
    {
        string block = EventSpecsBlock();
        var specKeys = EventSpecKeys();
        foreach (var kind in Lwv07EventKinds)
        {
            int id = (int)kind;
            Assert.Contains(id, specKeys);
            var line = Regex.Match(block, $@"(?m)^\s*{id}:\s*spec\((.*)\),?\s*$");
            Assert.True(line.Success, $"WorldEventKind {kind} ({id}) sem spec no catálogo");
            Assert.Contains("animated: true", line.Groups[1].Value, StringComparison.Ordinal);
            Assert.DoesNotContain("keyframes: \"none\"", line.Groups[1].Value, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Travel_is_hidden_because_the_map_route_is_the_cue()
    {
        string travelEntry = ActionLine((int)ActionType.Travel);
        Assert.Contains("hidden: true", travelEntry, StringComparison.Ordinal);
        Assert.Contains("SPEC_DEVIATION", CatalogSource(), StringComparison.Ordinal);
        Assert.Contains("map walking/relocation route is the travel cue", CatalogSource(), StringComparison.Ordinal);
    }

    [Fact]
    public void Action_cue_has_a_text_equivalent_reaching_the_accessible_npc_token()
    {
        string tokenSource = File.ReadAllText(Path.Combine(RepoRoot, "web", "src", "components", "NpcTokenSvg.tsx"));

        Assert.Contains("actionVisualFor", tokenSource, StringComparison.Ordinal);
        Assert.Contains("alt=", tokenSource, StringComparison.Ordinal);
    }

    /// <summary>Feedback do usuário (2026-08-21, travada real): a versão original cacheava a
    /// imagem do pawn por `id:action` — toda troca de ação recriava/decodificava uma `Image`
    /// nova, sem nunca liberar a antiga. Prova estrutural (não só comportamental, coberta em
    /// `renderer.test.ts`) de que o cache voltou a ser só por identidade.</summary>
    [Fact]
    public void Pawn_image_cache_key_is_identity_only_never_the_action()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot, "web", "src", "map-engine", "renderer.ts"));

        Assert.Contains("npcPawnImages.get(entity.ref.id)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("npcPawnImages.get(`", source, StringComparison.Ordinal);
        Assert.Contains("npcPawnDataUrl({ id: entity.ref.id })", source, StringComparison.Ordinal);
    }

    private static string CatalogSource() => File.ReadAllText(CatalogPath);

    private static string ActionSpecsBlock()
    {
        var match = Regex.Match(CatalogSource(), @"const ACTION_SPECS:[\s\S]*?\n\};");
        Assert.True(match.Success, "ACTION_SPECS não encontrado");
        return match.Value;
    }

    private static string ProcessSpecsBlock()
    {
        var match = Regex.Match(CatalogSource(), @"const PROCESS_SPECS:[\s\S]*?\n\};");
        Assert.True(match.Success, "PROCESS_SPECS não encontrado");
        return match.Value;
    }

    private static string EventSpecsBlock()
    {
        var match = Regex.Match(CatalogSource(), @"const EVENT_SPECS:[\s\S]*?\n\};");
        Assert.True(match.Success, "EVENT_SPECS não encontrado");
        return match.Value;
    }

    private static IReadOnlyList<int> ActionSpecKeys()
    {
        return Regex.Matches(ActionSpecsBlock(), @"(?m)^\s*(\d+):\s*spec\(")
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToList();
    }

    private static IReadOnlyList<string> ProcessSpecKeys()
    {
        return Regex.Matches(ProcessSpecsBlock(), @"(?m)^\s*(?:""([^""]+)""|([A-Za-z0-9_-]+)):\s*spec\(")
            .Select(match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)
            .ToList();
    }

    private static IReadOnlyList<int> EventSpecKeys()
    {
        return Regex.Matches(EventSpecsBlock(), @"(?m)^\s*(\d+):\s*spec\(")
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToList();
    }

    private static string ActionLine(int id)
    {
        var match = Regex.Match(ActionSpecsBlock(), $@"(?m)^\s*{id}:\s*spec\((.*)\),?\s*$");
        Assert.True(match.Success, $"ActionType {id} sem linha spec() no catálogo");
        return match.Groups[1].Value;
    }

    private static bool ActionLineIsAnimated(int id)
    {
        string args = ActionLine(id);
        if (args.Contains("hidden: true", StringComparison.Ordinal)) return false;
        return args.Contains("animated: true", StringComparison.Ordinal)
            || args.Contains("EAT_BITE", StringComparison.Ordinal)
            || args.Contains("REST_ZZZ", StringComparison.Ordinal)
            || args.Contains("BUY_COIN", StringComparison.Ordinal);
    }

    private static bool ProcessLineIsAnimated(string block, string descriptor)
    {
        var quoted = Regex.Match(block, $@"(?m)^\s*""{Regex.Escape(descriptor)}"":\s*spec\((.*)\),?\s*$");
        var bare = Regex.Match(block, $@"(?m)^\s*{Regex.Escape(descriptor)}:\s*spec\((.*)\),?\s*$");
        var args = quoted.Success ? quoted.Groups[1].Value : bare.Success ? bare.Groups[1].Value : "";
        if (string.IsNullOrEmpty(args)) return false;
        return args.Contains("animated: true", StringComparison.Ordinal)
            || args.Contains("EAT_BITE", StringComparison.Ordinal)
            || args.Contains("REST_ZZZ", StringComparison.Ordinal);
    }

    private static List<int> ParseIntArray(string constName)
    {
        var match = Regex.Match(CatalogSource(), $@"export const {constName} = \[([^\]]+)\]");
        Assert.True(match.Success, $"{constName} não encontrado");
        return match.Groups[1].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
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
