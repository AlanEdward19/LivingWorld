using LivingWorld.Domain;

namespace LivingWorld.Tests;

public class MoneyTests
{
    [Fact]
    public void Constructor_with_negative_amount_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(-1));
    }

    [Fact]
    public void Constructor_with_zero_or_positive_amount_succeeds()
    {
        Assert.Equal(0, new Money(0).Amount);
        Assert.Equal(10, new Money(10).Amount);
    }

    [Fact]
    public void TryDebit_beyond_balance_fails_and_leaves_original_value_untouched()
    {
        var original = new Money(10);

        var result = original.TryDebit(new Money(11));

        Assert.False(result.IsSuccess);
        Assert.Equal("insufficient_funds", result.Error);
        Assert.Equal(10, original.Amount);
    }

    [Fact]
    public void TryDebit_within_balance_succeeds()
    {
        var original = new Money(10);

        var result = original.TryDebit(new Money(4));

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value.Amount);
        Assert.Equal(10, original.Amount);
    }
}
