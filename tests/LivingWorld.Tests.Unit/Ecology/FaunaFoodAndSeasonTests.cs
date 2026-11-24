using LivingWorld.Domain.Ecology;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Fauna;
using LivingWorld.Domain.Flora;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Ecology;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Ecology;

/// <summary>AD-135, achado 22: o herbívoro come pasto para viver, a cria nasce na estação e em ninhadas espaçadas pela gestação.</summary>
public sealed class FaunaFoodAndSeasonTests
{
    private static readonly AnimalSpeciesRules Grazer = new(
        "rabbit", HungerDecayPerTick: 0.35, ReproduceEnergyThreshold: 50, ReproduceRadius: 2, ReproduceProbability: 0,
        PredatorOf: null, PredationProbability: 0, Diet: AnimalSpeciesRules.FloraDiet, ForageRadius: 2, MealEnergy: 25,
        GrazeStageCost: 10);

    private static readonly PlantSpeciesRules Forage = new("grass", -50, 80, 100000, 1, 0, 0, 0);

    private static WorldState World(AnimalSpeciesRules rules, IReadOnlyList<Plant> plants, int month = 0)
    {
        var energy = LazyNeed.Initial(100, 0, rules.HungerDecayPerTick);
        var a = new Animal(new AnimalId(1), rules.Species, new CellCoord(1, 1), true, null, energy);
        var b = new Animal(new AnimalId(2), rules.Species, new CellCoord(2, 1), true, null, energy);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 5, ScenarioRunner.DefaultMap(5),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(false, []),
            fauna: [a, b], animalSpeciesRules: [rules], nextAnimalId: 3,
            flora: plants, plantSpeciesRules: [Forage], nextPlantId: 100);
        world.CurrentDate = world.CurrentDate.AddHours((long)month * 30 * 24);
        return world;
    }

    [Fact]
    public void A_grazer_with_pasture_nearby_outlives_one_without()
    {
        var fed = World(Grazer, [new Plant(new PlantId(1), "grass", new CellCoord(1, 2), 5000)]);
        var starved = World(Grazer, []);
        var clock = new WorldClock([new FaunaLifecycleSystem()]);

        clock.Run(fed, 30 * 24);
        clock.Run(starved, 30 * 24);

        Assert.Equal(2, fed.Fauna.Count(a => a.IsAlive));
        Assert.Equal(0, starved.Fauna.Count(a => a.IsAlive));
    }

    [Fact]
    public void Grazing_eats_the_plant()
    {
        var world = World(Grazer, [new Plant(new PlantId(1), "grass", new CellCoord(1, 2), 5000)]);

        new WorldClock([new FaunaLifecycleSystem()]).Run(world, 30 * 24);

        Assert.True(world.Flora.Single().GrowthStage < 5000);
    }

    [Fact]
    public void Breeding_only_happens_inside_the_season()
    {
        var breeder = Grazer with { HungerDecayPerTick = 0, ReproduceProbability = 1, BreedStartMonth = 3, BreedEndMonth = 5 };
        var winter = World(breeder, [], month: 0);
        var spring = World(breeder, [], month: 3);

        new WorldClock([new FaunaLifecycleSystem()]).Run(winter, 24);
        new WorldClock([new FaunaLifecycleSystem()]).Run(spring, 24);

        Assert.Equal(2, winter.Fauna.Count);
        Assert.True(spring.Fauna.Count > 2);
    }

    [Fact]
    public void A_pair_has_one_litter_per_gestation_not_one_per_day()
    {
        var breeder = Grazer with { HungerDecayPerTick = 0, ReproduceProbability = 1, GestationDays = 30, LitterSize = 3 };
        var world = World(breeder, []);

        new WorldClock([new FaunaLifecycleSystem()]).Run(world, 10 * 24);

        Assert.Equal(5, world.Fauna.Count); // o par e uma ninhada de 3; os filhotes só cruzam depois de uma gestação
    }
}

/// <summary>AD-135: no cenário padrão a colônia de coelhos se sustenta do pasto por anos, sem bater no teto.</summary>
public sealed class DefaultFaunaPopulationTests
{
    [Fact]
    public void Default_rabbits_survive_five_years_below_the_cap()
    {
        var (world, clock) = ScenarioRunner.Create(2);
        var counts = new List<string>();
        int minRabbits = int.MaxValue, maxAlive = 0;
        for (int month = 0; month < 60; month++)
        {
            clock.Run(world, 30 * 24);
            int rabbits = world.Fauna.Count(a => a.IsAlive && a.Species == "rabbit");
            int wolves = world.Fauna.Count(a => a.IsAlive && a.Species == "wolf");
            counts.Add($"{rabbits}/{wolves}/{world.Flora.Count}");
            if (month >= 12) minRabbits = Math.Min(minRabbits, rabbits);
            maxAlive = Math.Max(maxAlive, world.Fauna.Count(a => a.IsAlive));
        }

        Assert.True(minRabbits >= 10 && maxAlive < FaunaLifecycleSystem.MaxAliveFauna, string.Join(" ", counts));
    }
}
