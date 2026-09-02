using LivingWorld.Domain.Ecology;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Ecology;

/// <summary>T1 — records de cenário e wiring no ScenarioRunner (pré-requisito REALISM-01/07).</summary>
public sealed class SpeciesRulesTests
{
    [Fact]
    public void Animal_and_plant_species_rules_match_design_field_shape()
    {
        var animal = new AnimalSpeciesRules(
            "wolf", HungerDecayPerTick: 0.5, ReproduceEnergyThreshold: 60, ReproduceRadius: 3,
            ReproduceProbability: 0.12, PredatorOf: "rabbit", PredationProbability: 0.25);
        Assert.Equal("wolf", animal.Species);
        Assert.Equal(0.5, animal.HungerDecayPerTick);
        Assert.Equal(60, animal.ReproduceEnergyThreshold);
        Assert.Equal(3, animal.ReproduceRadius);
        Assert.Equal(0.12, animal.ReproduceProbability);
        Assert.Equal("rabbit", animal.PredatorOf);
        Assert.Equal(0.25, animal.PredationProbability);

        var plant = new PlantSpeciesRules(
            "wheat", MinToleratedTemp: 5, MaxToleratedTemp: 35, MaturityStage: 3,
            CropResourceId: 1, YieldPerMaturePlant: 10, ReproduceRadius: 2, ReproduceProbability: 0.2);
        Assert.Equal("wheat", plant.Species);
        Assert.Equal(5f, plant.MinToleratedTemp);
        Assert.Equal(35f, plant.MaxToleratedTemp);
        Assert.Equal(3, plant.MaturityStage);
        Assert.Equal(1, plant.CropResourceId);
        Assert.Equal(10, plant.YieldPerMaturePlant);
        Assert.Equal(2, plant.ReproduceRadius);
        Assert.Equal(0.2, plant.ReproduceProbability);
    }

    [Fact]
    public void ScenarioRunner_Create_wires_default_species_and_biome_season_rules()
    {
        var (world, _) = ScenarioRunner.Create(seed: 3, initialPopulation: 0);

        Assert.Equal(ScenarioRunner.DefaultAnimalSpeciesRules, world.AnimalSpeciesRules);
        Assert.Equal(ScenarioRunner.DefaultPlantSpeciesRules, world.PlantSpeciesRules);
        Assert.Equal(ScenarioRunner.DefaultBiomeSeasonTemperatureRules, world.BiomeSeasonTemperatureRules);
        Assert.Contains(world.AnimalSpeciesRules, r => r.Species == "wolf" && r.PredatorOf == "rabbit");
        Assert.Contains(world.PlantSpeciesRules, r => r.Species == "crop1");
        Assert.Contains(world.BiomeSeasonTemperatureRules, r => r.BiomeId == 1 && r.SeasonDeltas.Count == 4);
    }
}
