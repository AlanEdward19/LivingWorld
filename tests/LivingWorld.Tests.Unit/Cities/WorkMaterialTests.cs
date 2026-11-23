using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
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
        var (world, sink, ctx, work, builder) = HouseWork(StreetRules.Default with { HaulersWalk = false });
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
        var (world, sink, ctx, work, builder) = HouseWork(StreetRules.Default with { HouseGatherHours = 0, HaulersWalk = false });
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
        var (world, _, ctx, work, builder) = HouseWork(StreetRules.Default with { HaulersWalk = false });
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

    // ---------------------------------------------------------------- AD-195: quem carrega anda

    private static void Hour(WorldState world, TickContext ctx) => new WorkHaulSystem().Tick(world, ctx);

    [Fact]
    public void A_hauler_walks_to_the_woods_cuts_carries_back_and_drops_the_load_on_the_lot()
    {
        var (world, sink, ctx, work, builder) = HouseWork(StreetRules.Default);
        work.Helpers = [builder.Id.Value];
        Hour(world, ctx);
        var trip = Assert.Single(work.Hauls);
        Assert.Equal("natureza", trip.Source); // a vila de código não vende madeira e a casa não tem
        var woods = new CellCoord(trip.X, trip.Y);
        Assert.NotEqual(new CellCoord(work.X, work.Y), woods);
        Assert.Equal(woods, LivingWorld.Simulation.Cities.Streets.SiteWork.DestinationOf(world, builder) ?? woods); // o destino é a mata

        builder.MoveTo(woods, world.CurrentDate.TotalHours);
        builder.SetCurrentAction(LivingWorld.Domain.Behavior.ActionType.Work, world.CurrentDate.TotalHours);
        for (long h = 0; h < trip.GatherHours; h++) Hour(world, ctx); // corta na mata, as horas da carga
        Assert.True(builder.IsCarrying, "pegou a carga nas mãos");
        Assert.Equal(work.MaterialResource, builder.CarriedResourceId);
        Assert.True(builder.CarriedQuantity <= WorkHaul.CapacityUnits(world, builder, new ResourceType(work.MaterialResource)));
        Assert.Equal(0, work.MaterialOnSite); // nada chega ao lote sem ela andar até lá

        builder.MoveTo(new CellCoord(work.X, work.Y), world.CurrentDate.TotalHours);
        long carried = builder.CarriedQuantity;
        Hour(world, ctx);
        Assert.False(builder.IsCarrying);
        Assert.Equal(carried, work.MaterialOnSite);
        var arrived = Assert.Single(sink.Events, e => e.Kind == WorldEventKind.WorkMaterialArrived);
        Assert.EndsWith($"|{builder.Id.Value}|natureza", arrived.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public void The_household_brings_the_material_it_already_has_before_buying_or_cutting()
    {
        var (world, _, ctx, work, builder) = HouseWork(StreetRules.Default);
        work.Helpers = [builder.Id.Value];
        var home = world.FindHousehold(new HouseholdId(work.Household))!;
        var timber = new ResourceType(work.MaterialResource);
        home.Deposit(timber, 5);
        Hour(world, ctx);
        var trip = Assert.Single(work.Hauls);
        Assert.Equal(("casa", home.Location), (trip.Source, new CellCoord(trip.X, trip.Y)));
        builder.MoveTo(home.Location, world.CurrentDate.TotalHours);
        Hour(world, ctx);
        Assert.True(builder.IsCarrying);
        Assert.Equal(5 - builder.CarriedQuantity, home.Stock.GetValueOrDefault(timber));
    }

    [Fact]
    public void Several_of_the_team_haul_at_once_while_the_lot_lacks_a_days_material()
    {
        var (world, _, ctx, work, _) = HouseWork(StreetRules.Default);
        var adults = world.Npcs.Where(n => n.IsAlive && n.City.Value == work.City && !n.IsCarrying
            && world.LifeStageRules.LifeStageOf(n.AgeYears(world.CurrentDate)) == LifeStage.Adult).Take(3).Select(n => n.Id.Value).ToList();
        work.Helpers = adults;
        work.MaterialNeeded = 100; // mais do que duas mãos levam: cada um carrega o que aguenta
        for (int h = 0; h < 3; h++) Hour(world, ctx);
        Assert.Equal(3, work.Hauls.Select(t => t.Npc).Distinct().Count());
        Assert.All(work.Hauls, t => Assert.Equal(WorkHaul.CapacityUnits(world, world.FindNpc(new NpcId(t.Npc))!, new ResourceType(work.MaterialResource)), t.Units)); // carga cheia
    }

    [Fact]
    public void Without_the_rule_the_hours_pay_the_trip_on_the_lot_as_in_ad194()
    {
        var (world, sink, ctx, work, builder) = HouseWork(StreetRules.Default.WithAd195Mode("nowalk"));
        work.Helpers = [builder.Id.Value];
        Hour(world, ctx);
        Assert.Empty(work.Hauls);
        BuildWorks.Progress(world, ctx, work, builder, 24, 0, world.CurrentDate.TotalHours);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.WorkMaterialArrived);
    }

    [Fact]
    public void The_city_queue_crew_fetches_from_the_square_stock_and_stops_with_an_event_when_it_runs_out()
    {
        var (world, clock) = ScenarioRunner.Create(3);
        clock.Run(world, 24);
        var sink = new Sink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        var city = world.ActiveCities().First();
        var timber = new ResourceType(3);
        var recipe = BuildingRecipe.Create(new Dictionary<ResourceType, long> { [timber] = 6 }, 6, 4).Value!;
        var worker = world.Npcs.First(n => n.IsAlive && n.City == city.Id && !n.IsCarrying);
        var project = new LivingWorld.Domain.Cities.Buildings.ConstructionProject(city.Id, 1, new Dictionary<ResourceType, long>(), 6)
        { SiteX = city.Location.X + 3, SiteY = city.Location.Y + 3, WorkId = 9_000, Crew = [worker.Id.Value] };
        city.DepositStock(timber, 4);

        QueueHaul.Tick(world, ctx, city, project, recipe);
        Assert.Single(project.Hauls!);
        worker.MoveTo(city.Location, world.CurrentDate.TotalHours);
        QueueHaul.Tick(world, ctx, city, project, recipe);
        Assert.True(worker.IsCarrying, "pegou no largo");
        worker.MoveTo(new CellCoord(project.SiteX, project.SiteY), world.CurrentDate.TotalHours);
        QueueHaul.Tick(world, ctx, city, project, recipe);
        Assert.True(project.MaterialOnSite![timber.Id] > 0);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.WorkMaterialArrived && e.Payload.EndsWith("|largo", StringComparison.Ordinal));

        while (project.Hauls is { Count: > 0 } || city.Stock.GetValueOrDefault(timber) > 0)
        {
            worker.MoveTo(city.Location, world.CurrentDate.TotalHours);
            QueueHaul.Tick(world, ctx, city, project, recipe);
            worker.MoveTo(new CellCoord(project.SiteX, project.SiteY), world.CurrentDate.TotalHours);
            QueueHaul.Tick(world, ctx, city, project, recipe);
            if (sink.Events.Count > 20) break;
        }
        project.MaterialOnSite![timber.Id] = 0; // a parede comeu o que veio; o largo está vazio
        QueueHaul.Tick(world, ctx, city, project, recipe);
        QueueHaul.Tick(world, ctx, city, project, recipe);
        Assert.Single(sink.Events, e => e.Kind == WorldEventKind.WorkMaterialShort);
    }
}
