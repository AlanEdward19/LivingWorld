using LivingWorld.Domain.Extraordinary;
using LivingWorld.Simulation.Extraordinary.Culture;

namespace LivingWorld.Tests.Unit.Extraordinary.Culture;

/// <summary>AD-156: a genética do poder no cenário JSON é opcional; quem não declara fica com a regra nula (padrão documentado).</summary>
public sealed class PowerGeneticsLoaderTests
{
    private static string Json(string scenarioGenetics, string descriptorGenetics) => $$"""
    {
      "Extraordinary": {
        "Enabled": true,
        "Prevalence": 0.02,
        {{scenarioGenetics}}
        "Descriptors": [{
          "Id": "x", "Source": "genetica", "Effects": ["npc.health:1"], "Mode": "Active", "Reliability": "Guaranteed"
          {{descriptorGenetics}}
        }]
      }
    }
    """;

    [Fact]
    public void Scenario_without_genetics_keeps_the_null_rule()
    {
        var loaded = ExtraordinaryScenarioLoader.Load(Json("", ""));

        Assert.True(loaded.IsSuccess, loaded.Error);
        Assert.Null(loaded.Value!.InheritanceRules);
    }

    [Fact]
    public void Declared_genetics_fills_omitted_fields_from_the_default()
    {
        var loaded = ExtraordinaryScenarioLoader.Load(Json(
            "\"Genetics\": { \"Penetrance\": 0.5 },", ", \"Genetics\": { \"Mode\": \"XLinkedRecessive\", \"MutationRate\": 0.0001 }"));

        Assert.True(loaded.IsSuccess, loaded.Error);
        var rules = loaded.Value!.InheritanceRules!;
        Assert.Equal((PowerInheritanceMode.XLinkedRecessive, 0.0001, 0.9, 0.5),
            (rules.For("x").Mode, rules.For("x").MutationRate, rules.For("x").Penetrance, rules.For("outro").Penetrance));
    }

    [Theory]
    [InlineData("\"Genetics\": { \"Penetrance\": 1.5 },", "")]
    [InlineData("", ", \"Genetics\": { \"Modo\": \"Dominant\" }")]
    [InlineData("\"Genetics\": 3,", "")]
    public void Invalid_genetics_fails_at_load(string scenarioGenetics, string descriptorGenetics)
    {
        Assert.False(ExtraordinaryScenarioLoader.Load(Json(scenarioGenetics, descriptorGenetics)).IsSuccess);
    }
}
