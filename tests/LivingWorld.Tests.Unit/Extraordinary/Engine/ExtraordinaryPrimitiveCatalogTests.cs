using LivingWorld.Simulation.Extraordinary.Culture;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Inheritance;
using LivingWorld.Simulation.Extraordinary.Mechanics;

namespace LivingWorld.Tests.Unit.Extraordinary.Engine;

/// <summary>Issue #64, critérios 4 e 5: catálogo por reflexão e validação na carga.</summary>
public sealed class ExtraordinaryPrimitiveCatalogTests
{
    private static IReadOnlyList<Type> MechanicTypesByReflection() =>
        typeof(IExtraordinaryMechanic).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && typeof(IExtraordinaryMechanic).IsAssignableFrom(type))
            .ToList();

    private static IReadOnlySet<string> CoveredMechanics(ExtraordinaryMechanicRegistry registry) =>
        ExtraordinaryPrimitiveCatalog.Describe(registry).Select(info => info.Mechanic).ToHashSet();

    [Fact]
    public void Every_mechanic_type_in_the_assembly_is_registered_and_present_in_the_catalog()
    {
        var covered = CoveredMechanics(ExtraordinaryMechanicRegistry.Default);

        var missing = MechanicTypesByReflection()
            .Where(type => !covered.Contains(type.Name))
            .Select(type => type.Name)
            .ToList();

        Assert.True(missing.Count == 0, "mecânicas fora do catálogo: " + string.Join(", ", missing));
        Assert.True(MechanicTypesByReflection().Count >= 28);
    }

    [Fact]
    public void Coverage_check_detects_a_mechanic_left_out_of_the_registry()
    {
        // Mutação: registro sem NpcCloneMechanic — a verificação por reflexão precisa reprovar.
        var partial = new ExtraordinaryMechanicRegistry(
            ExtraordinaryMechanicRegistry.Default.All.Where(item => item is not NpcCloneMechanic).ToList());

        var covered = CoveredMechanics(partial);

        Assert.Contains(MechanicTypesByReflection(), type => !covered.Contains(type.Name));
    }

    [Fact]
    public void Npc_stat_table_feeds_the_catalog_for_every_stat_without_a_parallel_list()
    {
        var heads = ExtraordinaryPrimitiveCatalog.Describe(ExtraordinaryMechanicRegistry.Default)
            .Select(info => info.Head).ToHashSet();

        foreach (var stat in NpcStatAccessors.All)
        {
            Assert.Contains($"npc.{stat.Key}", heads);
            Assert.Contains($"carrier.{stat.Key}", heads);
            Assert.Contains($"transfer.{stat.Key}", heads);
            Assert.Contains($"bond.share:{stat.Key}", heads);
        }
    }

    [Fact]
    public void Unknown_effect_token_is_refused_on_load_naming_descriptor_and_token()
    {
        var result = ExtraordinaryScenarioLoader.Load(Scenario("""["npc.mana:+5"]""", "[]"));

        Assert.False(result.IsSuccess);
        Assert.Contains("mana-bolt", result.Error, StringComparison.Ordinal);
        Assert.Contains("npc.mana:+5", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""["npc.health:+5"]""", """["carrier.mana:3"]""")]
    [InlineData("""["npc.health:+5"]""", """["npc.health:3"]""")]
    [InlineData("""["carrier.health:3"]""", "[]")]
    [InlineData("""["transfer.mana:3"]""", "[]")]
    [InlineData("""["mind.reader"]""", "[]")]
    public void Tokens_no_mechanic_accepts_are_refused_per_kind(string effects, string costs)
    {
        var result = ExtraordinaryScenarioLoader.Load(Scenario(effects, costs));

        Assert.False(result.IsSuccess);
        Assert.Contains("mana-bolt", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Tokens_declared_by_mechanics_are_accepted_on_load()
    {
        var result = ExtraordinaryScenarioLoader.Load(Scenario(
            """["npc.health:+5","npc.thirst:sunlight:-3","mind.read","area:radius:2","bond.share:hunger:50"]""",
            """["carrier.hunger:2","household.resource.9:1"]"""));

        Assert.True(result.IsSuccess, result.Error);
    }

    // Sintaxe sem contexto de mundo: a carga recusa o que toda invocação recusaria.
    [Theory]
    [InlineData("npc.health")]
    [InlineData("npc.health:0")]
    [InlineData("npc.health:abc")]
    [InlineData("transfer.health:-1")]
    [InlineData("transfer.health:5:from-target:x")]
    [InlineData("household.resource.abc:1")]
    [InlineData("area:radius:abc")]
    [InlineData("area:radius:-1")]
    [InlineData("bond.share:health:abc")]
    [InlineData("bond.share:health:0")]
    [InlineData("bond.oath:")]
    [InlineData("bond.oath:unknown.token:1")]
    [InlineData("construct.create:bad")]
    [InlineData("mind.read:x")]
    [InlineData("mind.commune:1")]
    [InlineData("soul.persist-as-ghost:1")]
    [InlineData("control.body-swap:1")]
    [InlineData("control.possess:NotAnAction")]
    [InlineData("npc.force-action:99")]
    [InlineData("npc.reincarnate:101")]
    [InlineData("luck.curse:0")]
    [InlineData("skill.copy:x")]
    [InlineData("environment.temperature:1")]
    [InlineData("dimension.portal:1:1:1:1")]
    [InlineData("matter.transmute:1:2")]
    [InlineData("foresight.preview:")]
    [InlineData("appearance.impersonate:0")]
    [InlineData("gravity.self:abc")]
    [InlineData("fauna.dominate:-2")]
    [InlineData("flora.growth-rate:-1")]
    public void Malformed_effect_arguments_are_refused_on_load(string effect)
    {
        var result = ExtraordinaryScenarioLoader.Load(Scenario($"[\"{effect}\"]", "[]"));

        Assert.False(result.IsSuccess);
        Assert.Contains(effect, result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("carrier.hunger")]
    [InlineData("carrier.hunger:abc")]
    [InlineData("carrier.hunger:-2")]
    [InlineData("carrier.hunger:0")]
    [InlineData("household.resource.abc:1")]
    [InlineData("household.resource.9")]
    public void Malformed_cost_arguments_are_refused_on_load(string cost)
    {
        var result = ExtraordinaryScenarioLoader.Load(Scenario("""["npc.health:5"]""", $"[\"{cost}\"]"));

        Assert.False(result.IsSuccess);
        Assert.Contains(cost, result.Error, StringComparison.Ordinal);
    }

    // Um token válido de cada mecânica: a validação de sintaxe nunca recusa o que o motor aceita.
    private static readonly string[] ValidEffects =
    [
        "npc.health:15", "npc.hunger:-10", "npc.health:sunlight:-10", "npc.teleport:5",
        "npc.force-action:1", "construct.create:1x1:10:5:stone", "movement.flight:1",
        "movement.speed-multiplier:3", "gravity.self:0.5", "gravity.target:2", "attribute.fertility:2.5",
        "attribute.strength:2", "attribute.perception:3", "attribute.reaction-speed:2", "area:radius:3",
        "area:region:2", "transfer.health:20", "transfer.thirst:5:from-target", "transfer.lifespan-years:2",
        "mind.read", "mind.read-memory", "mind.commune", "mind.erase-memory:5", "mind.implant-memory:5",
        "mind.alter-trait:agreeableness:-5", "luck.capacity-bonus:10", "luck.curse:10", "luck.curse:10:100",
        "matter.transmute:1:2:3", "skill.copy:1", "skill.learn-rate:2", "environment.temperature:0:-5:10",
        "dimension.pocket-store", "dimension.pocket-store:2", "dimension.portal:0:0:3:3",
        "fauna.dominate:3", "fauna.dominate:animal-4", "fauna.infect-vector:plague", "flora.growth-rate:5",
        "combat.strike:8", "combat.engage:3", "npc.clone:1", "npc.split-on-death:2", "npc.reincarnate:50",
        "bond.share:health", "bond.share:social:50", "bond.oath:npc.health:-5", "soul.persist-as-ghost",
        "control.possess", "control.possess:Sleep", "control.body-swap", "appearance.impersonate:5",
        "foresight.preview:death",
        // Forma tipada: o motor reduz `chave:tipo:n` para toda mecânica antes de resolver.
        "npc.force-action:fire:1", "gravity.target:fire:2", "luck.curse:fire:10",
        "mind.alter-trait:agreeableness:fire:-5", "bond.share:health:fire:1", "npc.teleport:fire:5",
        "combat.strike:fire:8", "transfer.health:fire:20",
        // A invocação move estoque mesmo com argumento inválido (quantidade 1).
        "dimension.pocket-store:abc", "dimension.pocket-store:0", "dimension.pocket-store:-1",
        // Mecânicas que só leem a magnitude e não conferem a chave.
        "npc.teleportx:5", "construct.create :1x1:10:5:stone", "combat.strike:8:1",
        "combat.engage:8:1", "npc.force-actionx:1",
    ];

    private static readonly string[] ValidCosts =
        ["carrier.hunger:2", "carrier.health:1", "carrier.sleep:sunlight:3", "household.resource.9:2"];

    [Fact]
    public void A_valid_token_of_every_mechanic_is_accepted_by_the_load_validation()
    {
        var registry = ExtraordinaryMechanicRegistry.Default;
        foreach (var effect in ValidEffects)
            Assert.True(ExtraordinaryPrimitiveCatalog.Validate(registry, effect, ExtraordinaryMechanicKind.Effect).IsSuccess, effect);
        foreach (var cost in ValidCosts)
            Assert.True(ExtraordinaryPrimitiveCatalog.Validate(registry, cost, ExtraordinaryMechanicKind.Cost).IsSuccess, cost);

        var covered = ValidEffects.Concat(ValidCosts).Select(registry.Resolve).Select(item => item!.GetType()).ToHashSet();
        var uncovered = MechanicTypesByReflection().Where(type => !covered.Contains(type)).Select(type => type.Name);
        Assert.Empty(uncovered);
    }

    // Um caso válido e um malformado por Primitive; falta de caso para algum Head do catálogo reprova.
    private static readonly Dictionary<string, (string Valid, string Invalid)> Cases = new()
    {
        ["npc.teleport"] = ("npc.teleport:5", "npc.teleport:abc"),
        ["npc.force-action"] = ("npc.force-action:1", "npc.force-action:99"),
        ["construct.create"] = ("construct.create:1x1:10:5:stone", "construct.create:1x1"),
        // A invocação aceita qualquer `movement.*`/valor de `attribute.*` sem efeito (ADR-0040): o malformado
        // que a carga recusa é o que nenhuma mecânica resolve.
        ["movement.flight"] = ("movement.flight:1", "movement"),
        ["movement.speed-multiplier"] = ("movement.speed-multiplier:3", "movement"),
        ["gravity.self"] = ("gravity.self:0.5", "gravity.self:abc"),
        ["gravity.target"] = ("gravity.target:2", "gravity.target:-1"),
        ["attribute.fertility"] = ("attribute.fertility:2.5", "attribute.fertilityx:2"),
        ["attribute.strength"] = ("attribute.strength:2", "attribute.strengthx:2"),
        ["attribute.perception"] = ("attribute.perception:3", "attribute.perceptionx:3"),
        ["attribute.reaction-speed"] = ("attribute.reaction-speed:2", "attribute.reaction-speedx:2"),
        ["area:radius:"] = ("area:radius:3", "area:radius:abc"),
        ["area:region:"] = ("area:region:2", "area:region:x"),
        ["transfer.lifespan-years"] = ("transfer.lifespan-years:2", "transfer.lifespan-years:abc"),
        ["mind.read"] = ("mind.read", "mind.read:x"),
        ["mind.read-memory"] = ("mind.read-memory", "mind.read-memory:1"),
        ["mind.commune"] = ("mind.commune", "mind.commune:1"),
        ["mind.erase-memory:"] = ("mind.erase-memory:5", "mind.erase-memory:abc"),
        ["mind.implant-memory:"] = ("mind.implant-memory:5", "mind.implant-memory:abc"),
        ["mind.alter-trait:"] = ("mind.alter-trait:agreeableness:-5", "mind.alter-trait:mana:-5"),
        ["luck.capacity-bonus:"] = ("luck.capacity-bonus:10", "luck.capacity-bonus:0"),
        ["luck.curse:"] = ("luck.curse:10", "luck.curse:0"),
        ["household.resource."] = ("household.resource.9:2", "household.resource.abc:2"),
        ["matter.transmute"] = ("matter.transmute:1:2:3", "matter.transmute:1:2"),
        ["skill.copy:"] = ("skill.copy:1", "skill.copy:x"),
        ["skill.learn-rate:"] = ("skill.learn-rate:2", "skill.learn-rate:-1"),
        ["environment.temperature:"] = ("environment.temperature:0:-5:10", "environment.temperature:1"),
        ["dimension.pocket-store"] = ("dimension.pocket-store", "dimension.pocket-store.x"),
        ["dimension.portal:"] = ("dimension.portal:0:0:3:3", "dimension.portal:1:1:1:1"),
        ["fauna.dominate:"] = ("fauna.dominate:3", "fauna.dominate:-2"),
        ["fauna.infect-vector:"] = ("fauna.infect-vector:plague", "fauna.infect-vector:"),
        ["flora.growth-rate:"] = ("flora.growth-rate:5", "flora.growth-rate:-1"),
        ["combat.strike:"] = ("combat.strike:8", "combat.strike:abc"),
        ["combat.engage:"] = ("combat.engage:3", "combat.engage:abc"),
        ["npc.clone"] = ("npc.clone:1", "npc.clone:abc"),
        ["npc.split-on-death"] = ("npc.split-on-death:2", "npc.split-on-death:abc"),
        ["npc.reincarnate"] = ("npc.reincarnate:50", "npc.reincarnate:101"),
        ["soul.persist-as-ghost"] = ("soul.persist-as-ghost", "soul.persist-as-ghost:1"),
        ["control.possess"] = ("control.possess:Sleep", "control.possess:NotAnAction"),
        ["control.body-swap"] = ("control.body-swap", "control.body-swap:1"),
        ["appearance.impersonate:"] = ("appearance.impersonate:5", "appearance.impersonate:0"),
        ["foresight.preview:"] = ("foresight.preview:death", "foresight.preview:"),
        ["bond.oath:"] = ("bond.oath:npc.health:-5", "bond.oath:"),
    };

    private static (string Valid, string Invalid) CaseFor(string head)
    {
        if (Cases.TryGetValue(head, out var explicitCase)) return explicitCase;
        foreach (var stat in NpcStatAccessors.All)
        {
            string k = stat.Key;
            if (head == $"npc.{k}") return ($"npc.{k}:5", $"npc.{k}:0");
            if (head == $"carrier.{k}") return ($"carrier.{k}:2", $"carrier.{k}:-2");
            if (head == $"transfer.{k}") return ($"transfer.{k}:5", $"transfer.{k}:-1");
            if (head == $"bond.share:{k}") return ($"bond.share:{k}:50", $"bond.share:{k}:abc");
        }
        throw new Xunit.Sdk.XunitException($"Head sem caso de teste: {head}");
    }

    [Fact]
    public void Every_catalog_head_has_a_valid_and_a_malformed_case_that_the_validation_tells_apart()
    {
        var registry = ExtraordinaryMechanicRegistry.Default;
        var catalog = ExtraordinaryPrimitiveCatalog.Describe(registry);
        foreach (var info in catalog)
        {
            var (valid, invalid) = CaseFor(info.Head);
            Assert.True(ExtraordinaryPrimitiveCatalog.Validate(registry, valid, info.Kind).IsSuccess, valid);
            Assert.False(ExtraordinaryPrimitiveCatalog.Validate(registry, invalid, info.Kind).IsSuccess, invalid);
        }

        var heads = catalog.Select(info => info.Head).ToHashSet();
        Assert.All(Cases.Keys, head => Assert.Contains(head, heads));
    }

    [Theory]
    [InlineData("npc.healthy:5")]
    [InlineData("transfer.health:fire:5:from-target")]
    [InlineData("bond.share:healthy")]
    [InlineData("carrier.hunger:abc:5x")]
    public void Boundary_and_exact_key_rules_still_refuse_look_alike_tokens(string effect)
    {
        var registry = ExtraordinaryMechanicRegistry.Default;
        var kind = effect.StartsWith("carrier.", StringComparison.Ordinal)
            ? ExtraordinaryMechanicKind.Cost : ExtraordinaryMechanicKind.Effect;

        Assert.False(ExtraordinaryPrimitiveCatalog.Validate(registry, effect, kind).IsSuccess, effect);
    }

    [Fact]
    public void Transfer_with_from_target_suffix_is_accepted()
    {
        Assert.True(ExtraordinaryPrimitiveCatalog.Validate(
            ExtraordinaryMechanicRegistry.Default, "transfer.health:5:from-target",
            ExtraordinaryMechanicKind.Effect).IsSuccess);
    }

    [Fact]
    public void Every_scenario_file_in_the_repository_still_loads()
    {
        string root = FindRepoRoot();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "scenarios"), "*.json", SearchOption.AllDirectories))
        {
            var result = ExtraordinaryScenarioLoader.Load(File.ReadAllText(file));
            Assert.True(result.IsSuccess, $"{file}: {result.Error}");
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }

    private static string Scenario(string effects, string costs) => $$"""
    {
      "Extraordinary": {
        "Enabled": true,
        "Descriptors": [{
          "Id": "mana-bolt", "Source": "s", "Effects": {{effects}}, "Mode": "Active",
          "Costs": {{costs}}, "Reliability": "Guaranteed"
        }]
      }
    }
    """;
}
