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
    [InlineData("dimension.pocket-store:abc")]
    [InlineData("matter.transmute:1:2")]
    [InlineData("foresight.preview:")]
    [InlineData("appearance.impersonate:0")]
    [InlineData("movement.flight")]
    [InlineData("gravity.self:abc")]
    [InlineData("attribute.strength:abc")]
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
