using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Population;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>Fase 5, T3: <see cref="EconomyCatalog"/>/<see cref="ProductionRecipe"/> —
/// recipe por <see cref="LocationType"/> (ECON-06/07/08), local sem produção física fica
/// ausente do catálogo (AD-043).</summary>
public class EconomyCatalogTests
{
    [Fact]
    public void ProductionRecipe_Create_fails_when_max_workers_per_cycle_is_not_positive()
    {
        var result = ProductionRecipe.Create(
            inputs: new Dictionary<int, long>(), outputs: new Dictionary<int, long> { [1] = 1 },
            requiresCellResource: null, maxWorkersPerCycle: 0);

        Assert.False(result.IsSuccess);
        Assert.Contains("MaxWorkersPerCycle", result.Error);
    }

    [Fact]
    public void ProductionRecipe_with_empty_inputs_is_valid_for_a_gathering_profession()
    {
        var result = ProductionRecipe.Create(
            inputs: new Dictionary<int, long>(), outputs: new Dictionary<int, long> { [1] = 2 },
            requiresCellResource: null, maxWorkersPerCycle: 5);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Inputs);
    }

    [Fact]
    public void ProductionRecipe_can_require_a_cell_resource()
    {
        var result = ProductionRecipe.Create(
            inputs: new Dictionary<int, long>(), outputs: new Dictionary<int, long> { [3] = 1 },
            requiresCellResource: 3, maxWorkersPerCycle: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.RequiresCellResource);
    }

    [Fact]
    public void LocationType_absent_from_Recipes_means_no_physical_production()
    {
        var recipe = ProductionRecipe.Create(
            inputs: new Dictionary<int, long>(), outputs: new Dictionary<int, long> { [1] = 1 },
            requiresCellResource: null, maxWorkersPerCycle: 1).Value!;
        var catalog = new EconomyCatalog(
            Recipes: new Dictionary<int, ProductionRecipe> { [1] = recipe }, // LocationType 1 = fazenda
            MarketLocationTypeIds: [],
            LocationTypeByProfession: new Dictionary<int, int>());

        // LocationType 2 = comerciante/guarda: sem entrada em Recipes, produção sempre 0.
        Assert.False(catalog.Recipes.ContainsKey(2));
    }
}
