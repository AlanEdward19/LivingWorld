using LivingWorld.Api.Visual.Projection;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Culture;
using LivingWorld.Simulation.Extraordinary.Inheritance;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Extraordinary;

public sealed class ExtraordinaryPrevalenceTests
{
    [Fact]
    public void Zero_prevalence_selects_no_one_and_one_selects_every_eligible_person_without_changing_pool()
    {
        var zero = World(prevalence: 0);
        var full = World(prevalence: 1);
        var zeroPool = zero.City.AggregatePool;
        var fullPool = full.City.AggregatePool;

        int zeroCreated = ExtraordinaryPrevalenceSeeder.Seed(zero.World);
        int fullCreated = ExtraordinaryPrevalenceSeeder.Seed(full.World);

        Assert.Equal((0, 0, zeroPool, 5, 5, fullPool),
            (zeroCreated, zero.World.ExtraordinaryCarriers.Count, zero.City.AggregatePool,
                fullCreated, full.World.ExtraordinaryCarriers.Count, full.City.AggregatePool));
        Assert.Equal(full.City.PoolNpcIds.Count,
            full.World.ExtraordinaryCarriers.Count(carrier => full.City.PoolNpcIds.Contains(carrier.CarrierId)));
    }

    [Fact]
    public void Same_seed_and_city_produce_the_same_prevalence_selection()
    {
        var first = World(prevalence: 0.5);
        var second = World(prevalence: 0.5);

        ExtraordinaryPrevalenceSeeder.Seed(first.World);
        ExtraordinaryPrevalenceSeeder.Seed(second.World);

        Assert.Equal(
            first.World.ExtraordinaryCarriers.Select(item => (item.CarrierId, Assert.Single(item.PowerIds))),
            second.World.ExtraordinaryCarriers.Select(item => (item.CarrierId, Assert.Single(item.PowerIds))));
    }

    [Fact]
    public void Global_lod_reports_only_the_known_carrier_count_for_materialized_and_pooled_people()
    {
        var setup = World(prevalence: 1);
        ExtraordinaryPrevalenceSeeder.Seed(setup.World);

        var marker = Assert.Single(GlobalProjector.Build(setup.World).Cities);

        Assert.Equal(5, marker.KnownCarrierCount);
        Assert.DoesNotContain("PowerIds", System.Text.Json.JsonSerializer.Serialize(marker), StringComparison.Ordinal);
        Assert.DoesNotContain("CarrierId", System.Text.Json.JsonSerializer.Serialize(marker), StringComparison.Ordinal);
    }

    [Fact]
    public void Each_power_has_its_own_locus_and_founders_carry_exactly_what_their_alleles_give()
    {
        // AD-156: no lugar do sorteio de um descritor por portador, cada poder tem a sua frequência (prevalência repartida)
        var descriptors = new[] { Descriptor("first"), Descriptor("second") };
        var setup = World(prevalence: 1, descriptors);
        var poolBefore = setup.City.PoolNpcIds.ToArray();
        var layout = PowerGenome.Layout(setup.World.Extraordinary);
        var expected = setup.World.Npcs.Select(n => (n.Id, Genome: PowerGenome.Founder(setup.World, n.Id, n.Sex)))
            .Concat(setup.City.PoolNpcIds.Select(id => (Id: id, Genome: PowerGenome.Founder(setup.World, id, null))))
            .Select(x => (x.Id, Powers: string.Join(',', layout.Where(l => PowerGenome.Copies(x.Genome, l.FirstLocus) > 0).Select(l => l.PowerId))))
            .Where(x => x.Powers.Length > 0).OrderBy(x => x.Id.Value).ToArray();

        ExtraordinaryPrevalenceSeeder.Seed(setup.World);

        Assert.Equal(poolBefore, setup.City.PoolNpcIds);
        Assert.Equal(expected, setup.World.ExtraordinaryCarriers.Select(c => (c.CarrierId, Powers: string.Join(',', c.PowerIds))).ToArray());
    }

    private static (WorldState World, Domain.Cities.City City) World(
        double prevalence, IReadOnlyList<PowerDescriptor>? descriptors = null)
    {
        descriptors ??= [Descriptor("generic")];
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, descriptors, prevalence: prevalence,
                inheritanceRules: new PowerInheritanceRules(new PowerGenetics(Penetrance: 1, OnsetAgeMeanYears: 0, OnsetAgeStdDevYears: 0))));
        var poolIds = world.ReserveNpcIdBlock(3);
        var city = new City(
            world.NextCityId(), new CellCoord(2, 2), 0, null,
            new AggregatePopulationPool(3, 30, 240), poolNpcIds: poolIds);
        world.AddCity(city);
        world.AddNpc(Npc(world.NextNpcIdAndAdvance(), city.Id));
        world.AddNpc(Npc(world.NextNpcIdAndAdvance(), city.Id));
        return (world, city);
    }

    private static PowerDescriptor Descriptor(string id) => new(
        id, "source", ["npc.health:1"], "Passive", [], "Guaranteed", [], [], [], []);

    private static Npc Npc(NpcId id, CityId cityId) => new(
        id, $"npc-{id.Value}", Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(2, 2), null, null, null, 80,
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        ProfessionType.None, new CellCoord(2, 2), city: cityId);
}
