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
