using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LivingWorld.Domain.Performance;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure;
using LivingWorld.Infrastructure.EfCore;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Ecology;
using LivingWorld.Tests.Shared.Baselines;
using LivingWorld.Tests.Shared.Performance;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests.LongRunning.Performance;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ScalePerformanceCollection
{
    public const string Name = "Scale performance";
}

/// <summary>PERF-02: sensor de escala no gate — 1 mês-sim, população pequena.
/// População grande (5k) fica em Category=Scenario.</summary>
[Collection(ScalePerformanceCollection.Name)]
public class ScaleScenarioSensorTests
{
    private const long OneMonthTicks = 30 * 24;
    private const int Seed = 42;

    private static readonly string BaselinesDir = FindRepoRoot("tests", "baselines");

    public sealed record ScaleSensorSample(
        double MicrosPerAliveNpcTick,
        double BytesAllocPerTick,
        double BytesPerAliveNpcPerYear);

    [Theory]
    [InlineData(ScaleScenarioFixture.PopulationSmall)]
    /// <summary>Disco é determinístico; µs/alloc só teto absoluto (variância de GC/carga).</summary>
    public void One_month_scale_run_stays_within_recorded_baseline(int initialPopulation)
    {
        AssertWithinBaseline(initialPopulation);
    }

    [Fact]
    public void Scale_fixture_seeds_fauna_and_flora_at_declared_mass()
    {
        const int pop = ScaleScenarioFixture.PopulationSmall;
        var (world, _) = ScaleScenarioFixture.CreateWorld((ulong)Seed, pop);
        int expected = EcologyScenarioSeeder.EcologyMassCount(pop);
        Assert.Equal(expected, world.Fauna.Count);
        Assert.Equal(expected, world.Flora.Count);
        Assert.Contains(world.Fauna, a => a.Species == "wolf" && a.IsAlive);
        Assert.Contains(world.Fauna, a => a.Species == "rabbit" && a.IsAlive);
    }

    [Theory]
    [Trait("Category", "Scenario")]
    [InlineData(ScaleScenarioFixture.PopulationLarge)]
    public void One_month_large_population_stays_within_recorded_baseline(int initialPopulation)
    {
        AssertWithinBaseline(initialPopulation);
    }

    [Fact(Skip = "Regravar: dotnet test --filter ZZZ_record_scale_sensor_baseline")]
    public void ZZZ_record_scale_sensor_baseline()
    {
        var actual = new Dictionary<int, ScaleSensorSample>
        {
            [ScaleScenarioFixture.PopulationSmall] = Measure(ScaleScenarioFixture.PopulationSmall),
            [ScaleScenarioFixture.PopulationLarge] = Measure(ScaleScenarioFixture.PopulationLarge),
        };
        BaselineFixture.Record(BaselinesDir, "scale-sensor", actual);
    }

    private static void AssertWithinBaseline(int initialPopulation)
    {
        var sample = Measure(initialPopulation);
        var baseline = LoadBaseline()[initialPopulation.ToString()];

        AssertInRelativeBand(
            sample.BytesPerAliveNpcPerYear, baseline.BytesPerAliveNpcPerYear, 0.01);

        var rules = PerfRules.ScaleSensorInitial;
        Assert.True(
            sample.MicrosPerAliveNpcTick <= rules.MaxMicrosPerAliveNpcTick,
            $"MicrosPerAliveNpcTick={sample.MicrosPerAliveNpcTick:F2} excede teto {rules.MaxMicrosPerAliveNpcTick}");
        Assert.True(
            sample.BytesAllocPerTick <= rules.MaxBytesAllocPerTick,
            $"BytesAllocPerTick={sample.BytesAllocPerTick:F0} excede teto {rules.MaxBytesAllocPerTick}");
        Assert.True(
            sample.BytesPerAliveNpcPerYear <= rules.MaxBytesPerAliveNpcPerYear,
            $"BytesPerAliveNpcPerYear={sample.BytesPerAliveNpcPerYear:F0} excede teto {rules.MaxBytesPerAliveNpcPerYear}");
    }

    private static Dictionary<string, ScaleSensorSample> LoadBaseline()
    {
        var path = Path.Combine(BaselinesDir, "scale-sensor.json");
        return JsonSerializer.Deserialize<Dictionary<string, ScaleSensorSample>>(File.ReadAllText(path))!;
    }

    private static void AssertInRelativeBand(double actual, double expected, double relativeTolerance)
    {
        if (expected == 0)
        {
            Assert.Equal(expected, actual);
            return;
        }

        double low = expected * (1 - relativeTolerance);
        double high = expected * (1 + relativeTolerance);
        Assert.True(
            actual >= low && actual <= high,
            $"esperado ~{expected}, obtido {actual} (faixa {low}–{high})");
    }

    private static ScaleSensorSample Measure(int initialPopulation)
    {
        var (world, clock) = ScaleScenarioFixture.CreateWorld((ulong)Seed, initialPopulation);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long aliveNpcTicks = 0;
        long beforeAlloc = GC.GetTotalAllocatedBytes(precise: true);
        var sw = Stopwatch.StartNew();

        for (long t = 0; t < OneMonthTicks; t++)
        {
            aliveNpcTicks += world.Npcs.Count(n => n.IsAlive);
            clock.Tick(world);
        }

        sw.Stop();
        long allocDelta = GC.GetTotalAllocatedBytes(precise: true) - beforeAlloc;

        double microsPerAliveNpcTick = sw.Elapsed.TotalMicroseconds / Math.Max(1, aliveNpcTicks);
        double bytesPerTick = (double)allocDelta / OneMonthTicks;
        double bytesPerAliveNpcPerYear = MeasureDiskBytesPerAliveNpcPerYear(initialPopulation);

        return new ScaleSensorSample(microsPerAliveNpcTick, bytesPerTick, bytesPerAliveNpcPerYear);
    }

    private static double MeasureDiskBytesPerAliveNpcPerYear(int initialPopulation)
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = new WorldDbContext(new DbContextOptionsBuilder<WorldDbContext>().UseSqlite(connection).Options);
        context.Database.Migrate();

        var repository = new SqliteWorldRepository(context);
        var runner = new PersistentWorldRunner(repository, BranchId.Root, snapshotIntervalTicks: OneMonthTicks);
        var sink = new BufferingWorldEventSink();
        var (world, clock) = ScaleScenarioFixture.CreateWorld((ulong)Seed, initialPopulation);

        runner.Run(world, clock, sink, OneMonthTicks);

        int alive = Math.Max(1, world.Npcs.Count(n => n.IsAlive));
        long snapshotBytes = Encoding.UTF8.GetByteCount(repository.LoadLatestSnapshot(BranchId.Root)!.Json);
        long eventBytes = repository.LoadEvents(BranchId.Root)
            .Sum(e => Encoding.UTF8.GetByteCount(e.Kind) + Encoding.UTF8.GetByteCount(e.Payload));

        double monthFactor = 12.0;
        return (snapshotBytes + eventBytes) * monthFactor / alive;
    }

    private static string FindRepoRoot(params string[] tail)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir is null
            ? throw new InvalidOperationException("LivingWorld.sln não encontrado")
            : Path.Combine([dir, .. tail]);
    }
}
