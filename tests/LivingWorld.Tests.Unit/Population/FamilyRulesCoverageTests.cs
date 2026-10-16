using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Population;

/// <summary>Fase 7, T20: rede de segurança — todo <c>ResourceId</c> em
/// <see cref="FamilyRules.MarriageInitialStock"/>/<see cref="FamilyRules.ConceptionResourceFloor"/>
/// do cenário default precisa existir no <see cref="EconomyRules"/>/<see cref="EconomyCatalog"/>
/// default (mesmo padrão de <see cref="SkillsRulesCoverageTests"/>).</summary>
public class FamilyRulesCoverageTests
{
    [Fact]
    public void Every_default_family_rules_resource_id_exists_in_default_economy()
    {
        var known = CollectResourceIds(ScenarioRunner.DefaultEconomyRules, ScenarioRunner.DefaultEconomyCatalog);
        Assert.NotEmpty(known);

        foreach (var resourceId in ScenarioRunner.DefaultFamilyRules.MarriageInitialStock.Keys)
            Assert.True(
                known.Contains(resourceId),
                $"MarriageInitialStock[{resourceId}] não existe no EconomyRules/EconomyCatalog default");

        foreach (var resourceId in ScenarioRunner.DefaultFamilyRules.ConceptionResourceFloor.Keys)
            Assert.True(
                known.Contains(resourceId),
                $"ConceptionResourceFloor[{resourceId}] não existe no EconomyRules/EconomyCatalog default");
    }

    internal static HashSet<int> CollectResourceIds(EconomyRules rules, EconomyCatalog catalog)
    {
        var ids = new HashSet<int>();
        if (rules.FoodResourceId > 0)
            ids.Add(rules.FoodResourceId);
        if (rules.WaterResourceId > 0)
            ids.Add(rules.WaterResourceId);

        foreach (var key in rules.CapacityByResourceLocation.Keys)
            ids.Add(key.ResourceId);
        foreach (var resourceId in rules.SpoilagePerDayByResource.Keys)
            ids.Add(resourceId);
        foreach (var resourceId in rules.PriceFloor.Keys)
            ids.Add(resourceId);
        foreach (var resourceId in rules.PriceCeiling.Keys)
            ids.Add(resourceId);
        foreach (var resourceId in rules.DemandBaselinePerNpc.Keys)
            ids.Add(resourceId);

        foreach (var recipe in catalog.Recipes.Values)
        {
            foreach (var resourceId in recipe.Inputs.Keys)
                ids.Add(resourceId);
            foreach (var resourceId in recipe.Outputs.Keys)
                ids.Add(resourceId);
            if (recipe.RequiresCellResource is int cellResource)
                ids.Add(cellResource);
        }

        return ids;
    }
}
