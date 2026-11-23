using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Works;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>AD-194: o material da casa é estoque do canteiro. As horas de quem está no lote vão primeiro a trazer carga (buscada na
/// natureza ou comprada de quem vende, carga por carga); a parede só sobe com material no lote; sem de onde trazer, a obra trava com
/// evento próprio e retoma quando a carga volta; o que sobra volta à casa que pagou. Sem a regra, o material vira horas na abertura.</summary>
public class WorkMaterialTests
{
    private sealed class Sink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static (WorldState World, Sink Sink, TickContext Ctx, BuildWork Work, Npc Builder) HouseWork(StreetRules streets)
    {
        var (world, clock) = ScenarioRunner.Create(3, cityLayout: CityLayoutRules.Default with { Streets = streets });
        clock.Run(world, 24);
        var sink = new Sink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        var city = world.ActiveCities().First();
        var home = world.Households.First(h => !h.IsEmpty && h.Dwelling is not null);
        var builder = home.Members.Select(world.FindNpc).First(n => n is { IsAlive: true })!;
        var work = BuildWorks.Open(world, ctx, city, BuildKind.House, -1, new CellCoord(city.Location.X + 3, city.Location.Y + 3), 0, 0,
            home.Id.Value, home.Head.Value, 480, 0, $"{home.Id.Value}|480|0|0");
        return (world, sink, ctx, work, builder);
    }

    [Fact]
    public void The_first_hours_bring_material_to_the_lot_and_the_wall_rises_only_with_it()
    {
        var (world, sink, ctx, work, builder) = HouseWork(StreetRules.Default);
        Assert.Equal(world.StreetRules.HouseMaterialUnits, work.MaterialNeeded);
        long now = world.CurrentDate.TotalHours;

        BuildWorks.Progress(world, ctx, work, builder, 24, 0, now); // a vila de código não vende madeira: buscada na natureza
        Assert.True(work.MaterialOnSite + work.MaterialPlaced > 0, "a primeira jornada trouxe carga");
        var arrived = Assert.Single(sink.Events, e => e.Kind == WorldEventKind.WorkMaterialArrived);
        var f = arrived.Payload.Split('|');
        Assert.Equal((work.Id.ToString(), builder.Id.Value.ToString(), "natureza"), (f[0], f[7], f[8]));
        long builtBefore = work.HoursTotal - work.HoursLeft;
        Assert.True(builtBefore * work.MaterialNeeded <= (work.MaterialPlaced + work.MaterialOnSite) * work.HoursTotal,
            "a parede não passa do material que há no lote");

        for (int day = 0; day < 200 && world.Ground!.Builds.Contains(work); day++)
            BuildWorks.Progress(world, ctx, work, builder, 8, 0, now + day * 24);
        Assert.DoesNotContain(work, world.Ground!.Builds);
        Assert.Equal(work.MaterialNeeded, work.MaterialPlaced); // pronta: todo o material está na parede
    }

    [Fact]
    public void With_nowhere_to_bring_material_from_the_work_stops_once_and_resumes_when_a_seller_appears()
    {
        var (world, sink, ctx, work, builder) = HouseWork(StreetRules.Default with { HouseGatherHours = 0 });
        long now = world.CurrentDate.TotalHours;
        BuildWorks.Progress(world, ctx, work, builder, 8, 0, now);
        BuildWorks.Progress(world, ctx, work, builder, 8, 0, now + 24);
        Assert.Single(sink.Events, e => e.Kind == WorldEventKind.WorkMaterialShort); // uma vez por episódio
        Assert.Equal(work.HoursTotal, work.HoursLeft); // sem material, a parede não sobe

        var timber = new ResourceType(world.StreetRules.HouseMaterialResourceId);
        var shop = world.Workplaces.First(w => w.City.Value == work.City && !w.Closed);
        shop.Deposit(timber, 100, world.EconomyRules);
        shop.SetPrice(timber, 1);
        foreach (var id in world.FindHousehold(new HouseholdId(work.Household))!.Members)
            world.FindNpc(id)?.CreditWallet(new Money(1_000));
        BuildWorks.Progress(world, ctx, work, builder, 24, 0, now + 48);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.WorkMaterialArrived && e.Payload.EndsWith("|banca", StringComparison.Ordinal));
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.WorkResumed && e.Payload.EndsWith("|material", StringComparison.Ordinal));
        Assert.False(work.MaterialShort);
        Assert.True(work.Spent > 0, "a casa pagou a carga");
    }

    [Fact]
    public void Material_left_on_an_abandoned_lot_goes_back_to_the_household()
    {
        var (world, _, ctx, work, builder) = HouseWork(StreetRules.Default);
        BuildWorks.Progress(world, ctx, work, builder, 17, 0, world.CurrentDate.TotalHours); // uma carga (16 h) e 1 h de parede
        long onSite = work.MaterialOnSite;
        Assert.True(onSite > 0);
        var home = world.FindHousehold(new HouseholdId(work.Household))!;
        var timber = new ResourceType(work.MaterialResource);
        long before = home.Stock.GetValueOrDefault(timber);

        BuildWorks.Abandon(world, ctx, work, "teste");

        Assert.Equal(before + onSite, home.Stock.GetValueOrDefault(timber));
    }

    [Fact]
    public void Without_the_rule_the_material_is_hours_at_opening_and_all_hours_build()
    {
        var (world, sink, ctx, work, builder) = HouseWork(StreetRules.Default.WithoutAd194());
        Assert.Equal(0, work.MaterialNeeded);
        BuildWorks.Progress(world, ctx, work, builder, 8, 0, world.CurrentDate.TotalHours);
        Assert.True(work.HoursLeft < work.HoursTotal);
        Assert.DoesNotContain(sink.Events, e => e.Kind == WorldEventKind.WorkMaterialArrived);
    }
}
