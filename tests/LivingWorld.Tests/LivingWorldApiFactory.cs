using LivingWorld.Domain;
using LivingWorld.Infrastructure;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Narrative;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests;

/// <summary>Host ASP.NET in-process para testes de endpoint — sempre SQLite <c>:memory:</c> e
/// tick loop desligado, independentemente do ambiente da sessão (<c>run.cmd</c>). Preferir
/// <see cref="IClassFixture{TFixture}"/> / <see cref="ApiEndpointCollection"/> a
/// <c>new WebApplicationFactory&lt;Program&gt;()</c> por teste: cada boot paga Migrate +
/// ScenarioRunner + DI.</summary>
public sealed class LivingWorldApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:World"] = "Data Source=:memory:",
                ["TICK_LOOP_ENABLED"] = "false",
            });
        });
    }

    /// <summary>Recria o mundo canônico do host (mesmo shape de <c>Program.cs</c> no bootstrap)
    /// sem subir outro ASP.NET — barato vs. nova factory. Use entre testes que mutam
    /// <see cref="WorldHost"/> via <c>Replace</c> ou tick loop.</summary>
    public void ResetCanonicalWorld(ulong seed = 1, int initialPopulation = 20)
    {
        var worldHost = Services.GetRequiredService<WorldHost>();
        var sessions = Services.GetRequiredService<ConversationSessionStore>();
        var chronicles = Services.GetRequiredService<ChronicleGenerationSystem>();
        var sink = Services.GetRequiredService<BufferingWorldEventSink>();
        var (world, _) = ScenarioRunner.Create(seed, initialPopulation: initialPopulation, historyRules: HistoryRules.Default);
        var clock = new WorldClock(
            ScenarioRunner.DefaultSystems(
                conversationSessions: sessions, chronicles: chronicles, extraordinary: world.Extraordinary),
            sink: sink);
        worldHost.Replace(world, clock);
        Services.GetRequiredService<SimulationHost>().Pause();
    }
}
