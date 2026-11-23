using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Layout;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Cities.Works;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>AD-192: as sobras de cidade e rota (08c 146; 08d 165, 166, 170, 177, 182, 183), cada uma com a chave ligada e desligada.</summary>
public class CityLeftoversTests
{
    private static (WorldState World, WorldClock Clock) Village(StreetRules? streets = null, ulong seed = 3) =>
        ScenarioRunner.Create(seed, cityLayout: streets is null ? null : CityLayoutRules.Default with { Streets = streets });

    // ---------------------------------------------------------------- 146

    [Fact]
    public void Strolling_and_talking_walk_the_same_steps_per_hour_by_the_clock()
    {
        double rate = StreetRules.Default.AmbientStepsPerHour;
        int idle = 0, talk = 0;
        for (long t = 0; t < 600; t += 2) idle += BehaviorDecisionSystem.AmbientSteps(t, t + 2, rate);
        for (long t = 0; t < 600; t += 3) talk += BehaviorDecisionSystem.AmbientSteps(t, t + 3, rate);
        Assert.Equal(idle, talk);
    }

    [Fact]
    public void Idling_two_hours_at_a_time_no_longer_walks_one_step_per_action()
    {
        double rate = StreetRules.Default.AmbientStepsPerHour;
        int idle = 0;
        for (long t = 0; t < 600; t += 2) idle += BehaviorDecisionSystem.AmbientSteps(t, t + 2, rate);
        Assert.Equal(200, idle); // o modelo de antes dava 300 (um por ação)
    }

    // ---------------------------------------------------------------- 165

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_new_city_whose_only_house_lies_beyond_the_ring_keeps_it_inside_its_limit_only_with_the_anchor(bool anchor)
    {
        var city = new City(new CityId(Guid.Parse("00000000-0000-0000-0000-000000000165")), new CellCoord(20, 20), 0,
            new CityId(Guid.Parse("00000000-0000-0000-0000-000000000001")), new LivingWorld.Domain.Cities.AggregatePopulationPool(0, 0, 0));
        var lone = new CityBounds(new CellCoord(28, 20), 3, 3); // 6 células além dos 3×3 da cidade sem gente (o anel é 3)
        var (bounds, _) = SpatialBoundsResolver.ResolveCity(city, 0, 64, 64, [lone], absorptionRingCells: 3, chainReachCells: 6,
            anchorLoneBuilding: anchor);
        Assert.Equal(anchor, bounds.Contains(new CellCoord(29, 21)));
    }

    // ---------------------------------------------------------------- tudo o que a cidade tem fica no limite dela

    /// <summary>Prédios (e canteiros) de cada cidade com alguma célula fora do limite crescido dela.</summary>
    internal static int OutsideTheirCity(WorldState world)
    {
        int outside = 0;
        foreach (var city in world.ActiveCities())
        {
            var bounds = CityOccupancy.ResolveGrownBounds(world, city,
                LivingWorld.Simulation.Cities.Queries.CityPopulationQuery.Population(world, city.Id)).Bounds;
            foreach (var b in world.Buildings)
                if (b.City == city.Id && b.Position is { } at
                    && BuildingFootprintGenerator.Generate(b).Any(c => !bounds.Contains(new CellCoord(at.X + c.Cell.X, at.Y + c.Cell.Y))))
                    outside++;
        }
        return outside;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Every_building_of_the_village_lies_inside_its_limit_only_with_the_rule(bool hold)
    {
        var (world, clock) = Village(StreetRules.Default with { CityHoldsItsBuildings = hold });
        clock.Run(world, world.Calendar.HoursPerDay * world.Calendar.DaysPerMonth * 3);
        Assert.Equal(hold, OutsideTheirCity(world) == 0);
    }

    // ---------------------------------------------------------------- 166

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_new_lot_never_takes_the_ground_of_a_neighbouring_city_with_the_cut(bool cut)
    {
        var (world, _) = Village(StreetRules.Default with { CutAtNeighbourCity = cut, LotsAvoidTrodden = false });
        var mother = world.ActiveCities().First();
        var neighbour = new City(new CityId(Guid.Parse("00000000-0000-0000-0000-000000000166")),
            new CellCoord(mother.Location.X + 4, mother.Location.Y), 0, null, new LivingWorld.Domain.Cities.AggregatePopulationPool(0, 0, 0));
        world.AddCity(neighbour);
        var network = CityStreets.Of(world, mother);
        var bounds = CityOccupancy.ResolveGrownBounds(world, mother, 100).Bounds;
        var ground = CityOccupancy.GroundOfCity(world, mother, bounds, new BuildingId(world.NextBuildingId));
        var barred = CityOccupancy.Barred(world, mother, network, ground);
        Assert.Equal(cut, barred.Bar?.Covers(neighbour.Location) == true);
    }

    // ---------------------------------------------------------------- 170

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_derived_building_records_its_lot_at_the_month_turn_only_with_the_rule(bool record)
    {
        var (world, _) = Village(StreetRules.Default with { RecordAuthoredLots = record });
        var city = world.ActiveCities().First();
        var derived = new Building(world.NextBuildingIdAndAdvance(), city.Id, LivingWorld.Simulation.Population.PopulationSeeder.InitialHouseBuildingTypeId, 0);
        world.AddBuilding(derived);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        new StreetGroundSystem().Tick(world, ctx);
        Assert.Equal(record, derived.Position is not null);
    }

    // ---------------------------------------------------------------- 177

    [Fact]
    public void The_best_entry_and_exit_pair_is_never_slower_and_sometimes_faster_than_the_nearest_cells()
    {
        var (world, clock) = Village();
        clock.Run(world, world.Calendar.HoursPerDay * world.Calendar.DaysPerMonth);
        var ends = world.Households.Where(h => !h.IsHomeless).Select(h => h.Location)
            .Concat(world.Workplaces.Where(w => !w.Closed).Select(w => w.Location)).Distinct().OrderBy(c => (c.Y, c.X)).ToList();
        int better = 0;
        foreach (var a in ends)
            foreach (var b in ends)
            {
                if (a == b) continue;
                double on = StreetRoutes.CostWithEntries(world, a, b, bestPair: true);
                double off = StreetRoutes.CostWithEntries(world, a, b, bestPair: false);
                Assert.True(on <= off + 1e-9, $"{a}->{b}: {on} > {off}");
                if (on < off - 1e-9) better++;
            }
        Assert.True(better > 0);
    }

    // ---------------------------------------------------------------- 182

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_finished_house_blocks_the_route_at_once_only_with_the_rule(bool atOnce)
    {
        var (world, clock) = Village(StreetRules.Default with { ObstacleOnCompletion = atOnce, LotsAvoidTrodden = false });
        clock.Run(world, world.Calendar.HoursPerDay); // um dia depois da virada do mês da criação
        var city = world.ActiveCities().First();
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        var bounds = CityOccupancy.ResolveGrownBounds(world, city, 100).Bounds;
        var candidate = new Building(new BuildingId(world.NextBuildingId), city.Id, LivingWorld.Simulation.Population.PopulationSeeder.InitialHouseBuildingTypeId, 0);
        var lot = CityOccupancy.CandidateLots(world, city, bounds, candidate.Id, candidate.BuildingTypeId, 0, LotProfile.UnknownHousehold, 0, 4).First();
        _ = StreetRoutes.StateOf(world);
        var work = BuildWorks.Open(world, ctx, city, BuildKind.House, candidate.BuildingTypeId, lot.Origin, lot.Orientation, lot.Croft,
            world.Households[0].Id.Value, world.Households[0].Head.Value, 100, 0, "teste");
        var site = world.FindBuilding(new BuildingId(work.Building))!;
        BuildWorks.Finish(world, ctx, work); // pronta no meio do mês
        var cell = BuildingFootprintGenerator.Generate(site).Select(c => new CellCoord(c.Cell.X + lot.Origin.X, c.Cell.Y + lot.Origin.Y)).First();
        var state = StreetRoutes.StateOf(world);
        Assert.Equal(atOnce, state.Owner[world.Ground!.IndexOf(cell)] != 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_new_lot_does_not_cover_the_trodden_ground_only_with_the_rule(bool avoid)
    {
        var (world, _) = Village(StreetRules.Default with { LotsAvoidTrodden = avoid });
        var city = world.ActiveCities().First();
        var ground = world.Ground!;
        var trodden = Enumerable.Range(0, ground.Count).Where(i => ground.SurfaceAt(i) != GroundSurface.None).Select(ground.CellOf).ToHashSet();
        Assert.NotEmpty(trodden);
        var bounds = CityOccupancy.ResolveGrownBounds(world, city, 400).Bounds;
        var candidate = new Building(new BuildingId(world.NextBuildingId), city.Id, LivingWorld.Simulation.Population.PopulationSeeder.InitialHouseBuildingTypeId, 0);
        bool covers = false;
        for (int level = 0; level <= 2; level++)
            foreach (var lot in CityOccupancy.CandidateLots(world, city, bounds, candidate.Id, candidate.BuildingTypeId, 0, LotProfile.UnknownHousehold, level, 32))
                covers |= BuildingFootprintGenerator.Generate(candidate.Id, candidate.BuildingTypeId, lot.Orientation)
                    .Any(c => trodden.Contains(new CellCoord(c.Cell.X + lot.Origin.X, c.Cell.Y + lot.Origin.Y)));
        Assert.Equal(!avoid, covers);
    }

    // ---------------------------------------------------------------- 183

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_paving_trial_counts_the_haul_of_gravel_from_the_river_only_with_the_rule(bool haul)
    {
        var (world, _) = Village(StreetRules.Default with { PrototypeHaulsMaterial = haul });
        var city = world.ActiveCities().First();
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        int cell = world.Ground!.IndexOf(city.Location);
        Assert.True(PavingDecision.OpenPrototype(world, ctx, city, cell, world.StreetRules.PrototypeHours, "1|1.1.1"));
        var work = world.Ground.Works.Single(w => w.Prototype is not null);
        Assert.Equal(haul, work.HoursTotal > world.StreetRules.PrototypeHours);
    }
}
