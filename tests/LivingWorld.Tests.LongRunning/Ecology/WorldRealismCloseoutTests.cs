using LivingWorld.Domain.Ecology;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Ecology;
using LivingWorld.Simulation.Geography;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Ecology;

/// <summary>T22 closeout (agente): cenário de referência com fauna/flora/clima e 0 poderes.
/// Smoke curto prova variação auditável; 10 anos = Category=Scenario (AD-029 — 100 anos fica
/// no objetivo #1 / LifeTable, não no closeout da 16.4).</summary>
public sealed class WorldRealismCloseoutTests
{
    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    /// <summary>Confirma que o cenário default (objetivo #1) semeia ecologia com 0 poderes.</summary>
    [Fact]
    public void Reference_scenario_seeds_ecology_with_zero_powers()
    {
        var (world, _) = ScenarioRunner.Create(seed: 42);
        Assert.False(world.Extraordinary.Enabled);
        Assert.NotEmpty(world.AnimalSpeciesRules);
        Assert.NotEmpty(world.PlantSpeciesRules);
        Assert.NotEmpty(world.BiomeSeasonTemperatureRules);
        Assert.True(world.Fauna.Count(a => a.IsAlive) >= 8, "fauna default semeada");
        Assert.True(world.Flora.Count >= 8, "flora default semeada");
        Assert.Equal(ScenarioRunner.DefaultInitialPopulation, world.Npcs.Count(n => n.IsAlive));
    }

    /// <summary>1 mês-sim: fome + avanço de estágio + temperatura (reprodução coberta pelos
    /// Independent Tests de T6/T9 — O(n²) em massa não cabe no gate do agente).</summary>
    [Fact]
    public void Reference_ecology_hunger_and_stages_vary_over_one_month_zero_powers()
    {
        const long oneMonthTicks = 30 * 24;
        var sink = new RecordingSink();
        var animalRules = new AnimalSpeciesRules[]
        {
            new("wolf", HungerDecayPerTick: 2.0, ReproduceEnergyThreshold: 99, ReproduceRadius: 3,
                ReproduceProbability: 0, PredatorOf: null, PredationProbability: 0),
            new("rabbit", HungerDecayPerTick: 1.5, ReproduceEnergyThreshold: 99, ReproduceRadius: 2,
                ReproduceProbability: 0, PredatorOf: null, PredationProbability: 0),
        };
        var plantRules = new PlantSpeciesRules[]
        {
            new("wheat", MinToleratedTemp: 5, MaxToleratedTemp: 35, MaturityStage: 3,
                CropResourceId: 1, YieldPerMaturePlant: 10, ReproduceRadius: 2, ReproduceProbability: 0),
        };
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(false, []),
            animalSpeciesRules: animalRules,
            plantSpeciesRules: plantRules,
            biomeSeasonTemperatureRules: ScenarioRunner.DefaultBiomeSeasonTemperatureRules);
        EcologyScenarioSeeder.Seed(world, animalCount: 8, plantCount: 12, ScenarioRunner.DefaultVillageLocation);

        int startAlive = world.Fauna.Count(a => a.IsAlive);
        int startMaxStage = world.Flora.Max(p => p.GrowthStage);

        new WorldClock(
            [new FaunaLifecycleSystem(), new FloraLifecycleSystem(), new TemperatureSeasonSystem()],
            sink: sink).Run(world, oneMonthTicks);

        int endAlive = world.Fauna.Count(a => a.IsAlive);
        int endMaxStage = world.Flora.Count == 0 ? 0 : world.Flora.Max(p => p.GrowthStage);
        int faunaDeaths = sink.Events.Count(e =>
            e.SourceSystem == FaunaLifecycleSystem.SystemName
            && e.Kind is WorldEventKind.Death or WorldEventKind.Starvation);
        int floraMatured = sink.Events.Count(e =>
            e.SourceSystem == FloraLifecycleSystem.SystemName
            && e.Kind == WorldEventKind.PlantMatured);

        Assert.False(world.Extraordinary.Enabled);
        Assert.True(endAlive < startAlive || faunaDeaths > 0,
            "fauna deve perder energia/morrer sem poderes");
        Assert.True(endMaxStage > startMaxStage || floraMatured > 0,
            "flora deve avançar estágio sob temperatura sazonal");
    }

    /// <summary>10 anos — Category=Scenario (AD-029/AD-030). Usa regras hunger-only
    /// (repro/predação 0) como o sensor de escala: Independent Tests já cobrem repro O(n²);
    /// o default com repro ligada escala até MaxAliveFauna e trava o closeout em horas.</summary>
    [Fact]
    [Trait("Category", "Scenario")]
    public void Reference_scenario_ten_years_completes_with_ecology_active()
    {
        const long tenYearsTicks = 10 * 12 * 30 * 24;
        var (world, clock) = ScenarioRunner.Create(
            seed: 42,
            animalSpeciesRules: CloseoutAnimalSpeciesRules,
            plantSpeciesRules: CloseoutPlantSpeciesRules);
        clock.Run(world, tenYearsTicks);
        Assert.True(world.Npcs.Any(n => n.IsAlive), "NPCs sobrevivem no horizonte de referência");
        Assert.True(world.Fauna.Any(a => a.IsAlive) || world.Flora.Count > 0,
            "ecologia permanece materializada após 10 anos");
    }

    /// <summary>Mesmo padrão de <c>ScaleScenarioFixture</c>: fome/estágio sim, repro/predação
    /// desligadas no horizonte longo (AD-030).</summary>
    private static readonly AnimalSpeciesRules[] CloseoutAnimalSpeciesRules =
    [
        new("wolf", HungerDecayPerTick: 0.5, ReproduceEnergyThreshold: 99, ReproduceRadius: 3,
            ReproduceProbability: 0, PredatorOf: null, PredationProbability: 0),
        new("rabbit", HungerDecayPerTick: 0.35, ReproduceEnergyThreshold: 99, ReproduceRadius: 2,
            ReproduceProbability: 0, PredatorOf: null, PredationProbability: 0),
    ];

    private static readonly PlantSpeciesRules[] CloseoutPlantSpeciesRules =
    [
        new("wheat", MinToleratedTemp: 5, MaxToleratedTemp: 35, MaturityStage: 3,
            CropResourceId: 1, YieldPerMaturePlant: 10, ReproduceRadius: 2, ReproduceProbability: 0),
    ];
}
