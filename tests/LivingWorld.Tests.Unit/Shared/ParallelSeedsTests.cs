using LivingWorld.Tests.Shared;

namespace LivingWorld.Tests.Unit.Shared;

/// <summary>O helper dos testes de cenário em paralelo (#97 A.1): se ele engolisse falha ou embaralhasse a ordem, os asserts dos
/// corpos paralelos deixariam de proteger o mundo sem ninguém perceber.</summary>
public class ParallelSeedsTests
{
    [Fact]
    public void Map_returns_results_in_seed_order_even_when_later_seeds_finish_first()
    {
        ulong[] seeds = [5, 3, 9, 1, 7, 2, 8, 4, 6, 10, 11, 12];

        var results = ParallelSeeds.Map(seeds, seed =>
        {
            SpinWait.SpinUntil(() => false, TimeSpan.FromMilliseconds((13 - (int)seed) * 2)); // seeds maiores terminam antes
            return seed * 10;
        });

        Assert.Equal(seeds.Select(s => s * 10), results);
    }

    [Fact]
    public void Map_over_a_range_covers_both_ends_once()
    {
        var results = ParallelSeeds.Map(3, 7, seed => seed);

        Assert.Equal(new ulong[] { 3, 4, 5, 6, 7 }, results);
    }

    [Fact]
    public void Degree_is_bounded_by_the_processor_count_and_never_exceeded_by_the_bodies()
    {
        Assert.InRange(ParallelSeeds.Degree, 1, 8);
        Assert.Equal(Math.Min(Environment.ProcessorCount, 8), ParallelSeeds.Degree);

        int running = 0, peak = 0;
        ParallelSeeds.ForEach(1, 64, _ =>
        {
            int now = Interlocked.Increment(ref running);
            int seen;
            while (now > (seen = Volatile.Read(ref peak)) && Interlocked.CompareExchange(ref peak, now, seen) != seen) { }
            SpinWait.SpinUntil(() => false, TimeSpan.FromMilliseconds(2));
            Interlocked.Decrement(ref running);
        });

        Assert.InRange(peak, 1, ParallelSeeds.Degree);
    }

    [Fact]
    public void A_failing_body_fails_the_call_and_names_the_seed_not_an_aggregate()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ParallelSeeds.ForEach(1, 8, seed => { if (seed == 6) throw new InvalidOperationException("boom"); }));

        Assert.Contains("seed 6", ex.Message);
        Assert.Contains("boom", ex.Message);
    }

    [Fact]
    public void The_original_exception_is_kept_as_the_inner_exception()
    {
        var original = new ArgumentException("original");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ParallelSeeds.Map(1, 4, seed => seed == 2 ? throw original : seed));

        Assert.Same(original, ex.InnerException);
    }

    [Fact]
    public void When_several_seeds_fail_the_first_in_the_list_is_reported()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ParallelSeeds.ForEach(1, 16, seed =>
            {
                if (seed is 3 or 9 or 14) throw new InvalidOperationException($"falha {seed}");
            }));

        Assert.Contains("seed 3 ", ex.Message);
    }

    [Fact]
    public void An_explicit_seed_list_reports_the_real_seed_of_the_failing_world()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ParallelSeeds.ForEach([1UL, 2, 1001, 1002], seed => { if (seed == 1002) throw new InvalidOperationException("x"); }));

        Assert.Contains("seed 1002 ", ex.Message);
    }

    [Fact]
    public void An_empty_range_runs_nothing_and_does_not_overflow()
    {
        int calls = 0;

        var results = ParallelSeeds.Map(5, 4, seed => { Interlocked.Increment(ref calls); return seed; });
        ParallelSeeds.ForEach(1, 0, _ => Interlocked.Increment(ref calls));
        ParallelSeeds.ForEach([], _ => Interlocked.Increment(ref calls));

        Assert.Empty(results);
        Assert.Equal(0, calls);
    }
}
