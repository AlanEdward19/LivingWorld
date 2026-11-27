using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Emergence;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>AD-176: rua de verdade — rota pela rede de caminhos e pelo terreno, pisoteio que abre e apaga trilha, superfície com
/// consequência, calçamento como obra paga, acesso a toda casa.</summary>
public class StreetRouteTests
{
    /// <summary>Mundo largo (95×95) sem obstáculo nenhum (o carimbo antes de qualquer prédio): só terreno e chão.</summary>
    private static WorldState Village(ulong seed = 3, StreetRules? streets = null)
    {
        var world = ScenarioRunner.Create(seed, initialPopulation: 300,
            cityLayout: streets is null ? null : CityLayoutRules.Default with { Streets = streets }).World;
        if (world.Ground is { } ground) ground.ObstacleTick = long.MinValue;
        return world;
    }

    /// <summary>Duas células longe de prédio, na mesma linha, terreno plano qualquer.</summary>
    /// <summary>Trecho reto de 20 células secas (e a fileira de cima também, de terreno não mais pesado).</summary>
    private static (CellCoord A, CellCoord B) OpenPair(WorldState world)
    {
        bool Dry(int x, int y) => !world.Map.CellAt(new CellCoord(x, y)).HasWater;
        for (int y = world.Map.Height - 3; y > 2; y--)
            for (int x = 1; x + 21 < world.Map.Width; x++)
                if (Enumerable.Range(x, 20).All(c => Dry(c, y) && Dry(c, y - 1))
                    && Enumerable.Range(x, 20).Sum(c => world.Map.Cost.WeightOf(world.Map.CellAt(new CellCoord(c, y - 1)).Terrain))
                       <= Enumerable.Range(x, 20).Sum(c => world.Map.Cost.WeightOf(world.Map.CellAt(new CellCoord(c, y)).Terrain)))
                    return (new CellCoord(x, y), new CellCoord(x + 19, y));
        throw new InvalidOperationException("mapa sem trecho seco");
    }

    private static void Pave(WorldState world, IEnumerable<CellCoord> cells, GroundSurface surface)
    {
        var ground = world.Ground!;
        foreach (var c in cells) ground.SetSurface(ground.IndexOf(c), surface);
        ground.SurfaceVersion++;
    }

    [Fact]
    public void Route_along_a_trail_is_cheaper_than_the_same_line_on_open_ground()
    {
        var world = Village();
        var (a, b) = OpenPair(world);
        double open = StreetRoutes.CostOf(world, a, b).Cost;
        Pave(world, Enumerable.Range(a.X, b.X - a.X + 1).Select(x => new CellCoord(x, a.Y)), GroundSurface.Dirt);
        double trodden = StreetRoutes.CostOf(world, a, b).Cost;
        Assert.True(trodden < open * 0.85, $"{trodden} >= {open}");
    }

    [Fact]
    public void Npc_detours_to_a_nearby_trail_when_it_is_faster_than_cutting_across()
    {
        var world = Village();
        var (a, b) = OpenPair(world);
        // a trilha corre uma célula acima da reta, de ponta a ponta
        Pave(world, Enumerable.Range(a.X, b.X - a.X + 1).Select(x => new CellCoord(x, a.Y - 1)), GroundSurface.Stone);
        var route = StreetRoutes.Get(world, a, b);
        var st = StreetRoutes.StateOf(world);
        Assert.True(route.E >= 0, $"a rota deveria entrar na rede de caminhos: custo {route.Cost} agua {route.Water} entrada {st.Entry[route.A]} {st.Entry[route.B]} net {st.AnyNet} A {route.A} B {route.B}");
    }

    [Fact]
    public void Building_in_the_way_costs_a_detour()
    {
        var world = Village();
        var (a, b) = OpenPair(world);
        double open = StreetRoutes.CostOf(world, a, b).Cost;
        var city = world.ActiveCities().First();
        var id = world.NextBuildingIdAndAdvance();
        var origin = Enumerable.Range(0, 6).Select(k => new CellCoord((a.X + b.X) / 2, a.Y - k))
            .First(o => BuildingFootprintGenerator.Generate(id, -1, 0).Any(c => c.Cell.Y + o.Y == a.Y));
        world.AddBuilding(new Building(id, city.Id, -1, -1, origin, 0));
        world.Ground!.ObstacleTick = -1;
        Assert.True(StreetRoutes.CostOf(world, a, b).Cost > open);
    }

    [Fact]
    public void Trips_wear_a_trail_and_an_abandoned_trail_fades_back_to_grass()
    {
        var world = Village();
        var (a, b) = OpenPair(world);
        var ground = world.Ground!;
        var system = new StreetGroundSystem();
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        for (int month = 0; month < 3; month++)
        {
            for (int i = 0; i < 400; i++) ground.RecordTrip(a, b);
            system.Tick(world, ctx);
        }
        var middle = new CellCoord((a.X + b.X) / 2, a.Y);
        Assert.NotEqual(GroundSurface.None, ground.SurfaceAt(middle));
        for (int month = 0; month < 36; month++) system.Tick(world, ctx);
        Assert.Equal(GroundSurface.None, ground.SurfaceAt(middle));
    }

    [Fact]
    public void Streets_off_keeps_the_straight_line_travel_of_before()
    {
        var world = Village(streets: StreetRules.Off);
        Assert.Null(world.Ground);
        var (a, b) = OpenPair(world);
        Assert.Equal(LivingWorld.Domain.Behavior.TravelResolution.TicksBetween(world.Map, a, b), StreetRoutes.Ticks(world, a, b));
    }

    [Fact]
    public void Hauler_does_not_fit_a_route_across_open_ground_but_fits_along_a_trail()
    {
        var world = Village();
        var (a, b) = OpenPair(world);
        Assert.False(StreetRoutes.CartFits(world, a, b));
        Pave(world, Enumerable.Range(a.X, b.X - a.X + 1).Select(x => new CellCoord(x, a.Y)), GroundSurface.Dirt);
        Assert.True(StreetRoutes.CartFits(world, a, b));
    }

    [Fact]
    public void Every_house_of_the_seeded_village_has_a_walking_route_and_an_access_kind()
    {
        var (world, _) = ScenarioRunner.Create(7);
        var streets = CityStreets.AllStreetCells(world);
        var square = world.ActiveCities().First().Location;
        foreach (var house in world.Buildings.Where(b => b.Position is not null))
        {
            var access = StreetRoutes.AccessOf(world, house, streets);
            Assert.True(access is not null, $"casa {house.Id.Value} tipo {house.BuildingTypeId} concl {house.CompletedAtTick} carimbo {world.Ground!.ObstacleTick} donos {StreetRoutes.StateOf(world).OwnerId.Length}");
            Assert.NotEqual("none", access);
            var inside = BuildingFootprintGenerator.Generate(house)[0].Cell;
            var cell = new CellCoord(house.Position!.Value.X + inside.X, house.Position.Value.Y + inside.Y);
            Assert.True(double.IsFinite(StreetRoutes.CostOf(world, cell, square).Cost));
        }
    }

    /// <summary>AD-179 (reescrito): antes o sistema diário pagava diaristas sem eles irem à obra e o trecho virava cascalho sozinho.
    /// Agora a obra abre paga pelo tesouro e não anda sem gente no trecho; o andamento pelas horas de quem está lá é
    /// <c>SiteWorkTests</c>.</summary>
    [Fact]
    public void Paving_work_with_the_technique_opens_paid_by_the_treasury_and_does_not_advance_by_itself()
    {
        var (world, _) = ScenarioRunner.Create(5, costWeights: ScenarioRunner.RegionalCostWeights); // AD-197o2: o tempo poupado só paga a obra no mapa de região
        var city = world.ActiveCities().First();
        var ground = world.Ground!;
        var culture = world.Npcs.First().Culture;
        world.AddTechnology(new Technology(culture, PavingTechnique.Category, new TechnologyVector([3, 0, 3]), 0, city.Id, null, [city.Id]));
        var cell = new CellCoord(city.Location.X, city.Location.Y);
        int index = ground.IndexOf(cell);
        ground.SetSurface(index, GroundSurface.Dirt);
        ground.AddPasses(index, 5000);
        city.CreditTreasury(new Money(1_000_000));
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        PavingDecision.Monthly(world, ctx);
        var work = Assert.Single(ground.Works);
        Assert.Empty(work.Payers); // longe de casa: o tesouro paga
        long left = work.HoursLeft;
        Assert.True(left > 0);
        var works = new PavingWorksSystem();
        for (int day = 0; day < 120; day++) works.Tick(world, ctx); // só o sistema, sem ninguém andar até o trecho
        Assert.Equal(left, Assert.Single(ground.Works).HoursLeft);
        Assert.Equal(GroundSurface.None, ground.PavingAt(index));
    }

    [Fact]
    public void Without_the_technique_nobody_paves()
    {
        var (world, _) = ScenarioRunner.Create(5);
        var city = world.ActiveCities().First();
        var ground = world.Ground!;
        int index = ground.IndexOf(city.Location);
        ground.SetSurface(index, GroundSurface.Dirt);
        ground.AddPasses(index, 5000);
        city.CreditTreasury(new Money(1_000_000));
        PavingDecision.Monthly(world, new TickContext(world, world.Rng, world.Scheduler));
        Assert.Empty(ground.Works);
    }

    [Fact]
    public void Reopened_world_walks_and_wears_the_same_ground_as_the_one_that_kept_running()
    {
        var (world, clock) = ScenarioRunner.Create(11);
        clock.Run(world, world.Calendar.HoursPerMonth * 4 + 37);
        var copy = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        var copyClock = new WorldClock(ScenarioRunner.DefaultSystems());
        clock.Run(world, world.Calendar.HoursPerMonth * 3);
        copyClock.Run(copy, copy.Calendar.HoursPerMonth * 3);
        Assert.True(world.Ground!.Cells.Count > 0);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(copy));
    }
}

