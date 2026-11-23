using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Tests.Shared.Cities;

namespace LivingWorld.Tests.Integration.Cities;

/// <summary>AD-194: o mundo da cidade média criado pelo caminho do menu (o payload do período, o mesmo da API) abre obra de casa para
/// o casal que mora de favor, e a carga de material chega ao lote. Seed 1: na seed 2 a cidade não tem lote de rua dentro dos limites
/// (diagnóstico da AD-194, a cargo da fatia do limite da cidade).</summary>
public class MenuWorkOpensTests
{
    private sealed class Sink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) { if (evt.Kind is WorldEventKind.BuildWorkStarted or WorldEventKind.WorkMaterialArrived) Events.Add(evt); }
    }

    [Fact]
    public void The_menu_city_opens_a_house_work_and_material_reaches_the_lot()
    {
        var (world, baseClock) = MenuWorlds.Load("cidade-media", 1);
        var sink = new Sink();
        var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, sink);
        long limit = world.Calendar.HoursPerYear * 4;
        for (long h = 0; h < limit && !sink.Events.Any(e => e.Kind == WorldEventKind.WorkMaterialArrived); h += world.Calendar.HoursPerMonth)
            clock.Run(world, world.Calendar.HoursPerMonth);
        var houses = sink.Events.Where(e => e.Kind == WorldEventKind.BuildWorkStarted && e.Payload.Split('|')[2] == "casa")
            .Select(e => e.Payload.Split('|')[0]).ToHashSet();
        Assert.NotEmpty(houses);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.WorkMaterialArrived && houses.Contains(e.Payload.Split('|')[0]));
    }
}
