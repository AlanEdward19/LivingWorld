using LivingWorld.Domain;

namespace LivingWorld.Tests.Economy;

public class MarketTransactionTests
{
    private static TransactionContext MakeContext() => new(
        BuyerWallet: new Money(100),
        SellerWallet: new Money(0),
        SellerStock: 10,
        BuyerStock: 0,
        Resource: new ResourceType(1),
        UnitPrice: 5,
        Quantity: 3);

    [Fact]
    public void Execute_happy_path_applies_all_four_effects_in_order()
    {
        var result = MarketTransaction.Execute(MakeContext());

        Assert.True(result.IsSuccess);
        var ctx = result.Value!;
        Assert.Equal(new Money(85), ctx.BuyerWallet);
        Assert.Equal(new Money(15), ctx.SellerWallet);
        Assert.Equal(7, ctx.SellerStock);
        Assert.Equal(3, ctx.BuyerStock);
    }

    [Fact]
    public void Execute_fails_and_leaves_nothing_observable_when_buyer_funds_insufficient()
    {
        var ctx = MakeContext() with { BuyerWallet = new Money(1) };

        var result = MarketTransaction.Execute(ctx);

        Assert.False(result.IsSuccess);
        Assert.Equal(new Money(1), ctx.BuyerWallet);
        Assert.Equal(10, ctx.SellerStock);
    }

    [Fact]
    public void Execute_fails_and_leaves_nothing_observable_when_seller_stock_insufficient()
    {
        var ctx = MakeContext() with { SellerStock = 1 };

        var result = MarketTransaction.Execute(ctx);

        Assert.False(result.IsSuccess);
        Assert.Equal(new Money(100), ctx.BuyerWallet);
        Assert.Equal(1, ctx.SellerStock);
    }

    public static IEnumerable<object[]> AllStepIndexes() =>
        Enumerable.Range(1, MarketTransaction.Steps.Count).Select(i => new object[] { i });

    [Theory]
    [MemberData(nameof(AllStepIndexes))]
    public void Execute_aborts_at_the_injected_step_with_no_partial_effect(int failAtStep)
    {
        var ctx = MakeContext();

        var result = MarketTransaction.Execute(ctx, failAtStep);

        Assert.False(result.IsSuccess);
        Assert.Equal(new Money(100), ctx.BuyerWallet);
        Assert.Equal(new Money(0), ctx.SellerWallet);
        Assert.Equal(10, ctx.SellerStock);
        Assert.Equal(0, ctx.BuyerStock);
    }
}
