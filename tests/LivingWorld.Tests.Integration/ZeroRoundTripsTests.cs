using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure;
using LivingWorld.Infrastructure.EfCore;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests.Integration;

/// <summary>Task 11: o banco só é tocado nas fronteiras de snapshot. 1 mês de ticks
/// rodando puro em memória não devem gerar nenhum comando SQL (1 ano fica em Scenario).</summary>
public class ZeroRoundTripsTests
{
    private const long OneMonthInHours = 30 * 24;
    private const long OneYearInHours = 12 * 30 * 24;

    [Fact]
    public void Running_ticks_between_snapshot_boundaries_executes_zero_db_commands()
    {
        AssertZeroRoundTrips(OneMonthInHours);
    }

    [Trait("Category", "Scenario")]
    [Fact]
    public void Running_ticks_between_snapshot_boundaries_over_1_year_executes_zero_db_commands()
    {
        AssertZeroRoundTrips(OneYearInHours);
    }

    private static void AssertZeroRoundTrips(long ticks)
    {
        var interceptor = new CountingCommandInterceptor();
        using var context = OpenInMemoryDb(interceptor);
        var repository = new SqliteWorldRepository(context);
        var runner = new PersistentWorldRunner(repository, BranchId.Root, snapshotIntervalTicks: ticks);
        var sink = new BufferingWorldEventSink();
        var (world, _) = ScenarioRunner.Create(seed: 42);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);

        // Roda todos os ticks fora da fronteira de snapshot (o construtor do mundo e a abertura
        // do banco já rodaram comandos antes deste ponto — zeramos o contador aqui).
        var midpointCount = interceptor.Count;
        for (long i = 0; i < ticks; i++)
            clock.Tick(world);

        Assert.Equal(midpointCount, interceptor.Count); // nenhum comando executado durante o laço de tick

        // Sensor de mutação (R5): a fronteira de snapshot precisa gerar ao menos um comando —
        // senão o teste acima passaria com o interceptor quebrado, sem medir nada.
        runner.Snapshot(world, sink);
        Assert.True(interceptor.Count > midpointCount, "snapshot deveria ter gerado ao menos um comando de banco");
    }

    private static WorldDbContext OpenInMemoryDb(CountingCommandInterceptor interceptor)
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<WorldDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        var context = new WorldDbContext(options);
        context.Database.Migrate();
        return context;
    }
}
