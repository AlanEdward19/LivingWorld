using System.Text.Json.Nodes;
using LivingWorld.Api.Realtime;
using LivingWorld.Api.Simulation;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>AD-172: o que o cliente recebe de um mundo RODANDO — snapshot HTTP dos escopos Mundo e Cidade a cada tick e cada delta
/// empurrado (como o fio serializa), com assinante nos dois escopos. Base das formas gravadas e do teste que confere cada uma.</summary>
internal sealed record RunningWorldSamples(
    IReadOnlyList<JsonNode> WorldSnapshots, IReadOnlyList<JsonNode> CitySnapshots, IReadOnlyList<JsonNode> Deltas)
{
    /// <summary>Ticks usados para gravar as formas; o teste de conformidade roda mais que isso.</summary>
    public const int RecordedTicks = 240;
    public const int Population = 40;

    private static readonly Lazy<RunningWorldSamples> Recorded = new(() => Collect(RecordedTicks));

    public static RunningWorldSamples ForRecording => Recorded.Value;

    public static RunningWorldSamples Collect(int ticks)
    {
        using var factory = new LivingWorldApiFactory();
        factory.ResetCanonicalWorld(seed: 1, initialPopulation: Population);
        var services = factory.Services;
        var gateway = services.GetRequiredService<RealtimeGateway>();
        var loop = services.GetRequiredService<TickLoopService>();
        var world = services.GetRequiredService<WorldHost>().Current;
        string cityId = world.ActiveCities().First().Id.ToString();
        var client = factory.CreateClient();
        var (worldReader, leaveWorld) = gateway.SubscribeChannel(new VisualScope(VisualScopeKind.World, ""));
        var (cityReader, leaveCity) = gateway.SubscribeChannel(new VisualScope(VisualScopeKind.City, cityId));
        services.GetRequiredService<SimulationHost>().Resume();
        var worldSnapshots = new List<JsonNode>();
        var citySnapshots = new List<JsonNode>();
        var deltas = new List<JsonNode>();
        for (int t = 0; t <= ticks; t++)
        {
            if (t > 0) loop.RunOneCycle();
            worldSnapshots.Add(JsonNode.Parse(client.GetStringAsync("/visual/subscribe?scope=World&mode=Spectator").Result)!);
            citySnapshots.Add(JsonNode.Parse(client.GetStringAsync($"/visual/subscribe?scope=City&refId={cityId}&mode=Spectator").Result)!);
            while (worldReader.TryRead(out var envelope)) deltas.Add(JsonNode.Parse(VisualWire.Serialize(envelope))!);
            while (cityReader.TryRead(out var envelope)) deltas.Add(JsonNode.Parse(VisualWire.Serialize(envelope))!);
        }

        leaveWorld();
        leaveCity();
        return new RunningWorldSamples(worldSnapshots, citySnapshots, deltas);
    }

    /// <summary>A forma de todas as amostras juntas.</summary>
    public static JsonNode ShapeOf(IEnumerable<JsonNode> samples) => samples.Select(ContractShapes.Of).Aggregate(ContractShapes.Merge);
}
