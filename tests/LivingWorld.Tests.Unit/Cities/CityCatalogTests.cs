using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>Fase 8, T3 (CITY-03): <see cref="BuildingRecipe"/>/<see cref="ConstructionProject"/> —
/// receita rejeita insumo negativo/duração não positiva; obra decrementa ticks sem passar de zero.</summary>
public class CityCatalogTests
{
    private static readonly ResourceType Timber = new(1);

    [Fact]
    public void BuildingRecipe_Create_rejects_negative_input()
    {
        var result = BuildingRecipe.Create(
            new Dictionary<ResourceType, long> { [Timber] = -1 }, ticksToBuild: 10, housingCapacityProvided: 4);

        Assert.False(result.IsSuccess);
        Assert.Contains("Inputs", result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BuildingRecipe_Create_rejects_ticks_to_build_not_positive(long ticks)
    {
        var result = BuildingRecipe.Create(
            new Dictionary<ResourceType, long> { [Timber] = 10 }, ticksToBuild: ticks, housingCapacityProvided: 4);

        Assert.False(result.IsSuccess);
        Assert.Contains("TicksToBuild", result.Error);
    }

    [Fact]
    public void BuildingRecipe_Create_rejects_negative_housing_capacity()
    {
        var result = BuildingRecipe.Create(
            new Dictionary<ResourceType, long> { [Timber] = 10 }, ticksToBuild: 10, housingCapacityProvided: -1);

        Assert.False(result.IsSuccess);
        Assert.Contains("HousingCapacityProvided", result.Error);
    }

    [Fact]
    public void BuildingRecipe_Create_succeeds_with_valid_values()
    {
        var result = BuildingRecipe.Create(
            new Dictionary<ResourceType, long> { [Timber] = 10 }, ticksToBuild: 10, housingCapacityProvided: 4);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ConstructionProject_Advance_decrements_ticks_remaining()
    {
        var project = new ConstructionProject(
            new CityId(Guid.NewGuid()), buildingTypeId: 1,
            consumed: new Dictionary<ResourceType, long>(), ticksRemaining: 3);

        project.Advance();

        Assert.Equal(2, project.TicksRemaining);
    }

    [Fact]
    public void ConstructionProject_Advance_never_goes_below_zero()
    {
        var project = new ConstructionProject(
            new CityId(Guid.NewGuid()), buildingTypeId: 1,
            consumed: new Dictionary<ResourceType, long>(), ticksRemaining: 0);

        project.Advance();

        Assert.Equal(0, project.TicksRemaining);
    }

    [Fact]
    public void CityCatalog_Empty_has_no_building_recipes()
    {
        Assert.Empty(CityCatalog.Empty.BuildingRecipes);
    }
}
