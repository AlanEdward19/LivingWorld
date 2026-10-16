using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using LivingWorld.Api.World;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Integration.World;
using LivingWorld.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration;

public class ApiClockPersistenceBoundaryTests
{
    [Fact]
    public async Task Api_bootstrap_created_and_restored_clocks_never_execute_database_commands_inside_tick()
    {
        // Assina antes da composição EF, que memoriza os listeners ativos.
        using var probe = new EfCommandsOnCallingThread();
        using var factory = new LivingWorldApiFactory();
        using var client = factory.CreateClient();
        var host = factory.Services.GetRequiredService<WorldHost>();
        var library = factory.Services.GetRequiredService<SavedWorldLibrary>();
        AssertPureClock();
        var original = WorldIdentity.WorldIdFor(host.Current.Seed);
        var request = new CreateWorldRequest(WorldCreateEndpointsTests.FullValidScenario(67990).ToJsonString(), "SQL boundary");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/worlds/create", request)).StatusCode);
        AssertPureClock();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/worlds/{original}/load", null)).StatusCode);
        AssertPureClock();

        void AssertPureClock()
        {
            // Conta os contextos reais abertos pela biblioteca, não um repositório substituto de teste.
            Assert.True(probe.CountDuring(() => library.Repository.LoadEvents(BranchId.Root)) > 0,
                "o sensor deve enxergar uma consulta no banco ativo da API");
            Assert.Equal(0, probe.CountDuring(() => host.Clock.Run(host.Current, 30 * 24)));
            Assert.True(probe.CountDuring(() => library.Save(host.Current)) > 0,
                "snapshot deve persistir fora do tick");
        }
    }

    private sealed class EfCommandsOnCallingThread : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = [];
        private readonly IDisposable _all;
        private bool _disposed;
        private int _thread = -1;
        private int _commands;
        public EfCommandsOnCallingThread() => _all = DiagnosticListener.AllListeners.Subscribe(this);
        public int CountDuring(Action action)
        {
            _commands = 0;
            _thread = Environment.CurrentManagedThreadId;
            try { action(); return _commands; }
            finally { _thread = -1; }
        }
        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name != DbLoggerCategory.Name) return;
            lock (_subscriptions)
                if (!_disposed) _subscriptions.Add(listener.Subscribe(this));
        }
        public void OnNext(KeyValuePair<string, object?> entry)
        {
            if (_thread == Environment.CurrentManagedThreadId && entry.Key == RelationalEventId.CommandExecuting.Name)
                _commands++;
        }
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void Dispose()
        {
            _all.Dispose();
            lock (_subscriptions)
            {
                _disposed = true;
                foreach (var subscription in _subscriptions) subscription.Dispose();
            }
        }
    }
}
