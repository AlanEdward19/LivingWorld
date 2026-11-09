using System.Reflection;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Climate;
using LivingWorld.Domain.Crime;
using LivingWorld.Domain.Health;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Unit.Observation;

namespace LivingWorld.Tests.Unit.Periods;

/// <summary>#112 fatia D: o mundo carregado por JSON recebe as mesmas regras de <c>ScenarioRunner.Create</c> (antes caía em
/// <c>FamilyRules.Disabled</c> e sem ecologia), e os blocos <c>Disease</c>/<c>Climate</c>/<c>Theft</c> (ADR-0043) carregam.</summary>
public class WorldRulesWiringTests
{
    private static JsonObject Default() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(ObservatoryFixture.RepoRoot(), "scenarios", "default.json")))!.AsObject();

    private static WorldState Load(JsonObject root) => ScenarioLoaderV2.LoadWorld(root.ToJsonString()).Value.World;

    /// <summary>Propriedades de regra que o <c>Create</c> deixa no padrão e o JSON precisa igualar. As que o JSON declara
    /// (população, economia, necessidades, cidades, etc.) ou que são estado de execução ficam fora, por nome.</summary>
    private static readonly HashSet<string> DeclaredByScenarioOrState =
    [
        "PopulationRules", "NeedsRules", "ActionCatalog", "LifeStageRules", "EconomyRules", "EconomyCatalog", "CityRules",
        "CityCatalog", "RestPlaceCatalog", "HistoryRules", "Extraordinary", "EmergenceRules", "OntogenyRules", "AppearanceRules",
        "ObjectCatalog", "PopulationCatalog", "Map", "Calendar", "ResourceCatalog", "ProcessRecipes", "DiseaseRules",
        "ClimateRules", "TheftRules",
    ];

    [Fact]
    public void Json_world_gets_the_same_default_rules_as_ScenarioRunner_Create_rule_by_rule()
    {
        var json = Load(Default());
        var created = ScenarioRunner.Create(1).World;

        var rules = typeof(WorldState).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => (p.Name.EndsWith("Rules") || p.Name.EndsWith("Catalog")) && !DeclaredByScenarioOrState.Contains(p.Name))
            .ToList();
        Assert.Contains(rules, p => p.Name == "FamilyRules");
        Assert.Contains(rules, p => p.Name == "PerfRules");
        Assert.Contains(rules, p => p.Name == "AnimalSpeciesRules");

        foreach (var property in rules)
            Assert.True(RuleEquals(property.GetValue(json), property.GetValue(created)), $"{property.Name} difere entre LoadWorld e Create");
    }

    private static bool RuleEquals(object? a, object? b)
    {
        if (a is System.Collections.IEnumerable ea && b is System.Collections.IEnumerable eb && a is not string)
            return ea.Cast<object?>().SequenceEqual(eb.Cast<object?>());
        return Equals(a, b);
    }

    [Fact]
    public void Json_world_has_real_family_rules_not_the_disabled_placeholder()
    {
        var family = Load(Default()).FamilyRules;
        Assert.NotEqual(0.0, family.CourtshipThreshold);
        Assert.NotEmpty(family.ConceptionResourceFloor);
        Assert.True(family.MaternalDeathRisk > 0);
    }

    [Fact]
    public void Absent_blocks_leave_the_rules_disabled()
    {
        var world = Load(Default());
        Assert.False(world.DiseaseRules.Enabled);
        Assert.False(world.ClimateRules.Enabled);
        Assert.False(world.TheftRules.Enabled);
        Assert.Equal(DiseaseRules.Disabled, world.DiseaseRules);
        Assert.Equal(ClimateRules.Disabled, world.ClimateRules);
        Assert.Equal(TheftRules.Disabled, world.TheftRules);
    }

    [Fact]
    public void Blocks_load_with_their_values()
    {
        var root = Default();
        root["Disease"] = JsonNode.Parse("""{"Enabled":true,"OutbreakPopulationThreshold":20,"OutbreakDailyChance":0.02,"Diseases":[{"Id":3,"ContagionPerContactDay":0.08,"Lethality":0.15,"DurationDays":6,"ImmunityDays":180}]}""");
        root["Climate"] = JsonNode.Parse("""{"Enabled":true,"MinMilli":300,"MaxMilli":1500}""");
        root["Theft"] = JsonNode.Parse("""{"Enabled":true,"DesperateBelow":90,"BaseSuccess":0.7,"GuardProfessionId":4,"GuardEffect":0.3}""");

        var world = Load(root);

        Assert.True(world.DiseaseRules.Enabled);
        Assert.Equal(20, world.DiseaseRules.OutbreakPopulationThreshold);
        Assert.Equal(0.02, world.DiseaseRules.OutbreakDailyChance);
        Assert.Equal(new DiseaseProfile(3, 0.08, 0.15, 6, 180), Assert.Single(world.DiseaseRules.Diseases));
        Assert.Equal(new ClimateRules(true, 300, 1500), world.ClimateRules);
        Assert.True(world.TheftRules.Enabled);
        Assert.Equal(90, world.TheftRules.DesperateBelow);
        Assert.Equal(0.7, world.TheftRules.BaseSuccess);
        Assert.Equal(4, world.TheftRules.GuardProfessionId);
        Assert.Equal(0.3, world.TheftRules.GuardEffect);
        Assert.Equal(TheftRules.Disabled.DiscoveryChance, world.TheftRules.DiscoveryChance); // campo ausente usa o padrão
    }

    [Theory]
    [InlineData("Disease", """{"Diseases":[]}""", "Disease.Enabled")]
    [InlineData("Disease", """{"Enabled":true,"Diseases":[]}""", "ao menos uma doença")]
    [InlineData("Disease", """{"Enabled":false,"OutbreakDailyChance":2}""", "OutbreakDailyChance")]
    [InlineData("Disease", """{"Enabled":true,"Diseases":[{"Id":1,"ContagionPerContactDay":0.1,"Lethality":0.1,"DurationDays":0,"ImmunityDays":1}]}""", "DurationDays")]
    [InlineData("Disease", """{"Enabled":true,"Diseases":[{"Id":1,"ContagionPerContactDay":0.1,"Lethality":0.1,"DurationDays":2,"ImmunityDays":1},{"Id":1,"ContagionPerContactDay":0.1,"Lethality":0.1,"DurationDays":2,"ImmunityDays":1}]}""", "duplicado")]
    [InlineData("Climate", """{"MinMilli":1}""", "Climate.Enabled")]
    [InlineData("Climate", """{"Enabled":true,"MinMilli":900,"MaxMilli":500}""", "MaxMilli")]
    [InlineData("Theft", """{"Enabled":true,"BaseSuccess":1.5}""", "BaseSuccess")]
    [InlineData("Theft", """{"Enabled":true,"DesperateBelow":101}""", "DesperateBelow")]
    [InlineData("Theft", """{"Enabled":"sim"}""", "Theft.Enabled")]
    public void Invalid_blocks_fail_pointing_at_the_field(string block, string body, string expected)
    {
        var root = Default();
        root[block] = JsonNode.Parse(body);
        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.False(result.IsSuccess);
        Assert.Contains(expected, result.Error);
    }

    [Fact]
    public void Disease_off_block_matches_no_block_hash()
    {
        var plain = Default();
        var off = Default();
        off["Disease"] = JsonNode.Parse("""{"Enabled":false}""");
        off["Climate"] = JsonNode.Parse("""{"Enabled":false}""");
        off["Theft"] = JsonNode.Parse("""{"Enabled":false}""");
        var (a, clockA) = ScenarioLoaderV2.LoadWorld(plain.ToJsonString()).Value;
        var (b, clockB) = ScenarioLoaderV2.LoadWorld(off.ToJsonString()).Value;
        clockA.Run(a, 24 * 30);
        clockB.Run(b, 24 * 30);
        Assert.Equal(WorldSnapshot.CanonicalHash(a), WorldSnapshot.CanonicalHash(b));
    }
}
