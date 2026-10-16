using LivingWorld.Api.World;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure;
using LivingWorld.Infrastructure.EfCore;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Scenarios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LivingWorld.Tests.Integration.World;

public class SavedWorldLibraryTests
{
    [Fact]
    public void Library_lifecycle_preserves_saved_worlds_and_rejects_invalid_transitions_without_http()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = new WorldDbContext(new DbContextOptionsBuilder<WorldDbContext>().UseSqlite(connection).Options);
        context.Database.Migrate();
        var repository = new SqliteWorldRepository(context);
        var sink = new BufferingWorldEventSink();
        var (a, clock) = ScenarioRunner.Create(1, initialPopulation: 4);
        new PersistentWorldRunner(repository, BranchId.Root, 24).Snapshot(a, sink);
        using var library = new SavedWorldLibrary(null, sink);
        a = library.Initialize(a, repository);
        var host = new WorldHost(a, clock);
        var simulation = new SimulationHost(host);
        clock.Run(a, 5);
        string hash = WorldSnapshot.CanonicalHash(a);
        Assert.False(library.Create(host, simulation, a, clock));
        Assert.False(library.Load(Guid.Empty, host, simulation, _ => clock));
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(host.Current));
        Assert.Equal(409, library.Delete(WorldIdentity.WorldIdFor(1)));
        Assert.Equal(404, library.Delete(Guid.Empty));
        var (b, bClock) = ScenarioRunner.Create(2, initialPopulation: 4);
        simulation.PauseFor("admin", "old world", 2, a.CurrentDate.TotalHours);
        Assert.True(library.Create(host, simulation, b, bClock));
        Assert.Null(simulation.AdminPause);
        for (int i = 0; i < 3; i++) simulation.CountPausedCycle();
        Assert.True(simulation.IsPaused);
        Assert.Equal(new ulong[] { 1, 2 }, library.List().Select(w => w.Seed).Order().ToArray());
        var currentRepository = library.Repository;
        Assert.Throws<InvalidOperationException>(() => library.Load(WorldIdentity.WorldIdFor(1), host, simulation,
            _ => throw new InvalidOperationException("clock assembly failed")));
        Assert.Same(currentRepository, library.Repository);
        Assert.Same(b, host.Current);
        Assert.True(library.Load(WorldIdentity.WorldIdFor(1), host, simulation, _ => clock));
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(host.Current));
        Assert.Equal(204, library.Delete(WorldIdentity.WorldIdFor(2)));
        Assert.Equal(404, library.Delete(WorldIdentity.WorldIdFor(2)));
        Assert.Single(library.List());
    }
}
