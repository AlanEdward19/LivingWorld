using LivingWorld.Domain;

namespace LivingWorld.Tests.Economy;

/// <summary>Fase 5, T2: <see cref="EconomyRules"/> — todo parâmetro numérico da economia,
/// cenário-driven (R3, AD-041/044/045), mesmo padrão de validação de <see cref="NeedsRules"/>.</summary>
public class EconomyRulesTests
{
    private static Result<EconomyRules> CreateWith(
        IReadOnlyDictionary<(int, int), long>? capacity = null,
        IReadOnlyDictionary<int, double>? spoilage = null,
        IReadOnlyDictionary<int, long>? wage = null,
        IReadOnlyDictionary<int, long>? floor = null,
        IReadOnlyDictionary<int, long>? ceiling = null) =>
        EconomyRules.Create(
            enabled: true, foodResourceId: 1, waterResourceId: 2,
            capacityByResourceLocation: capacity ?? new Dictionary<(int, int), long>(),
            spoilagePerDayByResource: spoilage ?? new Dictionary<int, double>(),
            wageByProfession: wage ?? new Dictionary<int, long>(),
            priceFloor: floor ?? new Dictionary<int, long>(),
            priceCeiling: ceiling ?? new Dictionary<int, long>(),
            priceSensitivity: 0.5,
            demandBaselinePerNpc: new Dictionary<int, double>());

    [Fact]
    public void Create_fails_naming_the_field_for_negative_capacity()
    {
        var result = CreateWith(capacity: new Dictionary<(int, int), long> { [(1, 1)] = -1 });

        Assert.False(result.IsSuccess);
        Assert.Contains("CapacityByResourceLocation", result.Error);
    }

    [Fact]
    public void Create_fails_naming_the_field_for_negative_spoilage()
    {
        var result = CreateWith(spoilage: new Dictionary<int, double> { [1] = -0.1 });

        Assert.False(result.IsSuccess);
        Assert.Contains("SpoilagePerDayByResource", result.Error);
    }

    [Fact]
    public void Create_fails_naming_the_field_when_floor_exceeds_ceiling()
    {
        var result = CreateWith(
            floor: new Dictionary<int, long> { [1] = 100 },
            ceiling: new Dictionary<int, long> { [1] = 50 });

        Assert.False(result.IsSuccess);
        Assert.Contains("PriceFloor", result.Error);
    }

    [Fact]
    public void Create_fails_naming_the_field_for_negative_wage()
    {
        var result = CreateWith(wage: new Dictionary<int, long> { [1] = -5 });

        Assert.False(result.IsSuccess);
        Assert.Contains("WageByProfession", result.Error);
    }

    [Fact]
    public void Create_succeeds_with_valid_ranges()
    {
        var result = CreateWith(
            capacity: new Dictionary<(int, int), long> { [(1, 1)] = 500 },
            spoilage: new Dictionary<int, double> { [1] = 0.0 },
            wage: new Dictionary<int, long> { [1] = 10 },
            floor: new Dictionary<int, long> { [1] = 5 },
            ceiling: new Dictionary<int, long> { [1] = 50 });

        Assert.True(result.IsSuccess);
        Assert.Equal(500, result.Value!.CapacityOf(new ResourceType(1), new LocationType(1)));
    }
}
