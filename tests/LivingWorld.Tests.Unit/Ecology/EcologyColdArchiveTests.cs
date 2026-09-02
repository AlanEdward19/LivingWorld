using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Population;

namespace LivingWorld.Tests.Ecology;

/// <summary>REALISM-21 — animal/planta morta sai do hot com registro frio (idade NPC / morte flora).</summary>
public sealed class EcologyColdArchiveTests
{
    [Fact]
    public void Dead_animals_leave_hot_fauna_after_cold_archive_years()
    {
        var perf = PerfRules.Create(1.0, 100, 2000, coldArchiveAfterYears: 1).Value!;
        var animal = new Animal(
            new AnimalId(1), "wolf", new CellCoord(1, 1), false, null,
            LazyNeed.Initial(0, 0, 0), DeathTick: 0);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 3, ScenarioRunner.DefaultMap(3),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(false, []),
            fauna: [animal],
            perfRules: perf);

        Assert.Contains(world.Fauna, a => a.Id.Value == 1);

        new WorldClock([new ColdArchiveSystem()]).Run(world, world.Calendar.HoursPerYear);

        Assert.DoesNotContain(world.Fauna, a => a.Id.Value == 1);
        Assert.NotNull(world.ColdArchive.LookupAnimal(1));
        Assert.Equal("wolf", world.ColdArchive.LookupAnimal(1)!.Species);
    }

    [Fact]
    public void Dead_plants_leave_hot_flora_with_cold_record_on_death()
    {
        var rules = new PlantSpeciesRules(
            "wheat", MinToleratedTemp: 50, MaxToleratedTemp: 60, MaturityStage: 3,
            CropResourceId: 1, YieldPerMaturePlant: 1, ReproduceRadius: 1, ReproduceProbability: 0);
        var plant = new Plant(new PlantId(1), "wheat", new CellCoord(2, 2), GrowthStage: 0);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 5, ScenarioRunner.DefaultMap(5),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(false, []),
            flora: [plant],
            plantSpeciesRules: [rules],
            nextPlantId: 2);

        new WorldClock([new FloraLifecycleSystem()]).Tick(world);

        Assert.DoesNotContain(world.Flora, p => p.Id.Value == 1);
        Assert.NotNull(world.ColdArchive.LookupPlant(1));
        Assert.Equal("wheat", world.ColdArchive.LookupPlant(1)!.Species);
    }

    [Fact]
    public void Ecology_cold_archive_is_deterministic_across_two_runs()
    {
        static WorldState Run()
        {
            var perf = PerfRules.Create(1.0, 100, 2000, coldArchiveAfterYears: 1).Value!;
            var world = new WorldState(
                ScenarioRunner.DefaultCalendar, 11, ScenarioRunner.DefaultMap(11),
                ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
                ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
                ScenarioRunner.DefaultLifeStageRules,
                extraordinary: new ExtraordinaryScenarioData(false, []),
                fauna:
                [
                    new Animal(new AnimalId(1), "deer", new CellCoord(1, 1), false, null,
                        LazyNeed.Initial(0, 0, 0), DeathTick: 0),
                ],
                perfRules: perf);
            new WorldClock([new ColdArchiveSystem()]).Run(world, world.Calendar.HoursPerYear);
            return world;
        }

        var a = Run();
        var b = Run();
        Assert.Equal(WorldSnapshot.CanonicalHash(a), WorldSnapshot.CanonicalHash(b));
        Assert.Empty(a.Fauna);
        Assert.NotNull(a.ColdArchive.LookupAnimal(1));
    }
}
