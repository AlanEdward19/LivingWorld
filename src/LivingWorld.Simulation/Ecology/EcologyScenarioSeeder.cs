using LivingWorld.Domain.Fauna;
using LivingWorld.Domain.Flora;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Simulation.Ecology;

/// <summary>Semeadura determinística de fauna/flora para cenários de referência e escala.
/// Posições derivadas do grid — nunca RNG solto.</summary>
public static class EcologyScenarioSeeder
{
    /// <summary>Vila default (objetivo #1): população modesta perto do núcleo.</summary>
    public static void SeedDefault(WorldState world) =>
        Seed(world, animalCount: 40, plantCount: 400, ScenarioRunner.DefaultVillageLocation); // AD-135: pasto bastante para a colônia passar o inverno

    /// <summary>Cenário de escala: N ≈ metade da população NPC inicial.</summary>
    public static void SeedMass(WorldState world, int initialPopulation) =>
        Seed(world, EcologyMassCount(initialPopulation), EcologyMassCount(initialPopulation),
            ScenarioRunner.DefaultVillageLocation);

    public static int EcologyMassCount(int initialPopulation) =>
        Math.Max(50, initialPopulation / 5);

    public static void Seed(WorldState world, int animalCount, int plantCount, CellCoord origin)
    {
        if (animalCount <= 0 && plantCount <= 0)
            return;

        var cells = world.Map.Cells
            .Select(c => c.Coord)
            .OrderBy(c => c.X)
            .ThenBy(c => c.Y)
            .ToList();
        if (cells.Count == 0)
            return;

        var rulesBySpecies = world.AnimalSpeciesRules
            .ToDictionary(r => r.Species, StringComparer.Ordinal);
        // AD-135: predador raro como na natureza (um lobo para ~10 presas, uma alcateia de quatro na vila padrão), não um para três
        int wolves = animalCount <= 0 ? 0 : Math.Max(1, animalCount / 10);
        int rabbits = Math.Max(0, animalCount - wolves);

        if (rulesBySpecies.TryGetValue("wolf", out var wolfRules))
        {
            for (int i = 0; i < wolves; i++)
            {
                var cell = cells[(i * 3 + origin.X + origin.Y) % cells.Count];
                world.AddAnimal(new Animal(
                    world.NextAnimalIdAndAdvance(),
                    "wolf",
                    cell,
                    true,
                    null,
                    LazyNeed.Initial(100, 0, wolfRules.HungerDecayPerTick)));
            }
        }

        if (rulesBySpecies.TryGetValue("rabbit", out var rabbitRules))
        {
            for (int i = 0; i < rabbits; i++)
            {
                var cell = cells[((i / 2) * 7 + origin.X + 2) % cells.Count]; // aos pares, no pasto semeado: coelho vive em colônia
                world.AddAnimal(new Animal(
                    world.NextAnimalIdAndAdvance(),
                    "rabbit",
                    cell,
                    true,
                    null,
                    LazyNeed.Initial(100, 0, rabbitRules.HungerDecayPerTick)));
            }
        }

        if (world.PlantSpeciesRules.Count > 0)
        {
            string species = world.PlantSpeciesRules[0].Species;
            // vegetação estabelecida no começo do mundo (AD-135): o pasto já está crescido, não brotando do zero
            int stage = Math.Max(0, world.PlantSpeciesRules[0].MaturityStage);
            for (int i = 0; i < plantCount; i++)
            {
                var cell = cells[(i + origin.X + 2) % cells.Count]; // AD-135: pasto contíguo (um prado), não uma planta a cada sete células
                world.AddPlant(new Plant(
                    world.NextPlantIdAndAdvance(),
                    species,
                    cell,
                    GrowthStage: stage));
            }
        }
    }
}
