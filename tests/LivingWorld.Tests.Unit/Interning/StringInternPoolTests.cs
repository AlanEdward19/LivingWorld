using LivingWorld.Domain.Shared;

namespace LivingWorld.Tests.Unit.Interning;

public class StringInternPoolTests
{
    [Fact]
    public void Same_string_returns_same_id()
    {
        var pool = new StringInternPool();

        int first = pool.Intern("blacksmith");
        int second = pool.Intern("blacksmith");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Different_strings_return_different_ids()
    {
        var pool = new StringInternPool();

        int blacksmith = pool.Intern("blacksmith");
        int farmer = pool.Intern("farmer");

        Assert.NotEqual(blacksmith, farmer);
    }

    [Fact]
    public void Resolve_round_trips_interned_string()
    {
        var pool = new StringInternPool();

        int id = pool.Intern("event:marriage");

        Assert.Equal("event:marriage", pool.Resolve(id));
    }

    [Fact]
    public void Interning_duplicates_does_not_increase_count()
    {
        var pool = new StringInternPool();

        pool.Intern("trait:brave");
        pool.Intern("trait:brave");
        pool.Intern("trait:brave");

        Assert.Equal(1, pool.Count);
    }

    [Fact]
    public void Distinct_strings_increase_count_once_each()
    {
        var pool = new StringInternPool();

        pool.Intern("blacksmith");
        pool.Intern("farmer");
        pool.Intern("blacksmith");

        Assert.Equal(2, pool.Count);
    }

    [Fact]
    public void Ids_are_assigned_sequentially_from_zero()
    {
        var pool = new StringInternPool();

        Assert.Equal(0, pool.Intern("alpha"));
        Assert.Equal(1, pool.Intern("beta"));
        Assert.Equal(0, pool.Intern("alpha"));
    }

    [Fact]
    public void Resolve_throws_for_unknown_id()
    {
        var pool = new StringInternPool();
        pool.Intern("known");

        Assert.Throws<ArgumentOutOfRangeException>(() => pool.Resolve(1));
    }

    [Fact]
    public void Same_intern_sequence_is_deterministic_across_pools()
    {
        var poolA = new StringInternPool();
        var poolB = new StringInternPool();

        string[] values = ["blacksmith", "farmer", "trait:brave", "blacksmith"];

        var idsA = values.Select(poolA.Intern).ToArray();
        var idsB = values.Select(poolB.Intern).ToArray();

        Assert.Equal(idsA, idsB);
    }
}
