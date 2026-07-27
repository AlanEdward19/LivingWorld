using LivingWorld.Domain;
using LivingWorld.Infrastructure;
using LivingWorld.Simulation;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests;

/// <summary>Task 11: o banco só é tocado nas fronteiras de snapshot. 3650 ticks (10 anos)
/// rodando puro em memória não devem gerar nenhum comando SQL.</summary>
public class ZeroRoundTripsTests
{
    private const long TenYearsInHours = 10 * 12 * 30 * 24;

    [Fact]
    public void Running_3650_ticks_between_snapshot_boundaries_executes_zero_db_commands()
    {
        var interceptor = new CountingCommandInterceptor();
        using var context = OpenInMemoryDb(interceptor);
        var repository = new SqliteWorldRepository(context);
        var runner = new PersistentWorldRunner(repository, BranchId.Root, snapshotIntervalTicks: TenYearsInHours);
        var sink = new BufferingWorldEventSink();
        var (world, _) = ScenarioRunner.Create(seed: 42);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);

        // Roda todos os ticks fora da fronteira de snapshot (o construtor do mundo e a abertura
        // do banco já rodaram comandos antes deste ponto — zeramos o contador aqui).
        var midpointCount = interceptor.Count;
        for (long i = 0; i < TenYearsInHours; i++)
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
