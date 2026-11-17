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
        AddHouseWithoutStreet(world);
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

    /// <summary>AD-173: uma casa gravada fora da frente de rua, sem quintal e sem caminho até a rua (como a de um degrau sem rua),
    /// dentro do limite da cidade: o escopo Cidade passa a mandar lote com <c>street</c> nulo e listas vazias.</summary>
    private static void AddHouseWithoutStreet(LivingWorld.Simulation.Core.WorldState world)
    {
        var city = world.ActiveCities().First();
        long population = LivingWorld.Simulation.Cities.Queries.CityPopulationQuery.Population(world, city.Id);
        var bounds = LivingWorld.Simulation.Cities.CityOccupancy.ResolveGrownBounds(world, city, population).Bounds;
        var streets = LivingWorld.Simulation.Cities.CityStreets.AllStreetCells(world);
        var taken = new HashSet<LivingWorld.Domain.Geography.CellCoord>();
        foreach (var (b, origin, _) in LivingWorld.Simulation.Cities.CityStreets.Placed(world, city))
            foreach (var c in LivingWorld.Domain.Cities.Buildings.BuildingFootprintGenerator.Generate(b))
                taken.Add(new(c.Cell.X + origin.X, c.Cell.Y + origin.Y));
        for (int y = bounds.Origin.Y; y < bounds.Origin.Y + bounds.Height; y++)
            for (int x = bounds.Origin.X; x < bounds.Origin.X + bounds.Width; x++)
            {
                var house = new LivingWorld.Domain.Cities.Buildings.Building(
                    new LivingWorld.Domain.Shared.BuildingId(world.NextBuildingId), city.Id, -1, 0, new(x, y), 0, croftCells: 0);
                var cells = LivingWorld.Domain.Cities.Buildings.BuildingFootprintGenerator.Generate(house)
                    .Select(c => new LivingWorld.Domain.Geography.CellCoord(c.Cell.X + x, c.Cell.Y + y)).ToList();
                if (!cells.All(bounds.Contains) || cells.Any(c => taken.Contains(c) || streets.Contains(c))) continue;
                if (LivingWorld.Simulation.Cities.CityStreets.LotOf(world, city, house, new(x, y), 0).Street is not null) continue;
                world.NextBuildingIdAndAdvance();
                world.AddBuilding(house);
                return;
            }
        throw new InvalidOperationException("sem lugar para a casa sem rua na amostra");
    }

    /// <summary>A forma de todas as amostras juntas.</summary>
    public static JsonNode ShapeOf(IEnumerable<JsonNode> samples) => samples.Select(ContractShapes.Of).Aggregate(ContractShapes.Merge);
}
