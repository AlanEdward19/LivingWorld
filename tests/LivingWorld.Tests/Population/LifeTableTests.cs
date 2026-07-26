using LivingWorld.Domain;

namespace LivingWorld.Tests.Population;

public class LifeTableTests
{
    private static readonly LifeTableBracket[] ValidBrackets =
    [
        new(0, 9, 0.05),
        new(10, 19, 0.01),
    ];

    [Fact]
    public void Contiguous_brackets_covering_MaxLongevityYears_build_successfully()
    {
        var result = LifeTable.Create(20, ValidBrackets);
        Assert.True(result.IsSuccess, result.Error);
    }

    [Fact]
    public void Gap_between_brackets_is_rejected()
    {
        var result = LifeTable.Create(20, [new LifeTableBracket(0, 9, 0.05), new LifeTableBracket(11, 19, 0.01)]);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Overlap_between_brackets_is_rejected()
    {
        var result = LifeTable.Create(20, [new LifeTableBracket(0, 10, 0.05), new LifeTableBracket(9, 19, 0.01)]);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Brackets_not_reaching_MaxLongevityYears_are_rejected()
    {
        var result = LifeTable.Create(20, [new LifeTableBracket(0, 9, 0.05)]);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Mortality_outside_zero_one_is_rejected()
    {
        var result = LifeTable.Create(20, [new LifeTableBracket(0, 19, 1.5)]);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Age_at_or_past_max_longevity_has_certain_mortality()
    {
        var table = LifeTable.Create(20, ValidBrackets).Value!;
        Assert.Equal(1.0, table.AnnualMortality(20, health: 100));
        Assert.Equal(1.0, table.AnnualMortality(25, health: 100));
    }

    [Fact]
    public void Worse_health_never_lowers_mortality()
    {
        var table = LifeTable.Create(20, ValidBrackets).Value!;
        double healthy = table.AnnualMortality(5, health: 100);
        double sickly = table.AnnualMortality(5, health: 20);
        Assert.True(sickly >= healthy);
    }
}
