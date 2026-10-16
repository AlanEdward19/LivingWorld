using System.Text.Json.Nodes;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Integration.Economy;

/// <summary>economy-depth sobre o cenário ao vivo (<c>scenarios/default.json</c> com 40 NPCs, onde as oficinas novas
/// têm gente): contratação de lenhador e pedreiro (C4), salário e rotina (C5), obra que gasta a receita do estoque da
/// cidade (C9) e escassez de madeira (C12).</summary>
public class EconomyDepthScenarioTests
{
    private static readonly ResourceType Wood = new(3);
    private static readonly ResourceType Stone = new(5);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static (WorldState World, WorldClock Clock) Load(int seed, bool woodScarcity = false, int population = 40, IWorldEventSink? sink = null)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(FindRepoRoot(), "scenarios", "default.json")))!.AsObject();
        node["Seed"] = seed;
        node["InitialPopulation"] = population;
        if (woodScarcity)
        {
            node["Workplaces"]!.AsArray()[1]!["Stock"] = new JsonObject();
            node["Recipes"]!["3"]!["Outputs"] = new JsonObject { ["3"] = 0 };
        }

        var loaded = ScenarioLoaderV2.LoadWorld(node.ToJsonString());
        Assert.True(loaded.IsSuccess, loaded.Error);
        var (world, clock) = loaded.Value;
        return (world, sink is null ? clock : new WorldClock(clock.Systems, sink: sink));
    }

    private static int HousesBuilt(WorldState world) => world.Buildings.Count(b => b.BuildingTypeId >= 10);

    [Fact]
    public void Sawmill_and_quarry_hire_workers()
    {
        var sink = new RecordingSink();
        var (world, clock) = Load(seed: 1, sink: sink);
        var sawmill = world.Workplaces.Single(w => w.LocationType.Id == 3);
        var quarry = world.Workplaces.Single(w => w.LocationType.Id == 4);

        clock.Run(world, 90 * 24);

        var hiredAt = sink.Events.Where(e => e.Kind == WorldEventKind.Hired).Select(e => e.Payload.Split('|')[1]).ToList();
        Assert.Contains(sawmill.Id.Value.ToString(), hiredAt);
        Assert.Contains(quarry.Id.Value.ToString(), hiredAt);
    }

    [Fact]
    public void Lumberjack_is_paid_and_works_in_the_declared_window()
    {
        var (world, _) = Load(seed: 1);
        var sawmill = world.Workplaces.Single(w => w.LocationType.Id == 3);
        var worker = world.Npcs.First(n => n.IsAlive && world.LifeStageRules.LifeStageOf(n.AgeYears(world.CurrentDate)) == LifeStage.Adult);
        worker.SwitchProfession(new LivingWorld.Domain.Population.ProfessionType(3));
        sawmill.Hire(worker.Id);
        worker.Hire(sawmill.Id);
        long before = worker.Wallet.Amount;

        new WagePaymentSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        long tax = world.FindCity(sawmill.City) is null ? 0 : (long)Math.Floor(95 * world.EconomyRules.WageTaxRate); // a cidade retém a alíquota (AD-098)
        Assert.Equal(before + 95 - tax, worker.Wallet.Amount);
        var lumberjack = new LivingWorld.Domain.Population.ProfessionType(3);
        Assert.Equal(ActionType.Work, world.ActionCatalog.RoutineOf(lumberjack, LifeStage.Adult, hour: 9));
        Assert.NotEqual(ActionType.Work, world.ActionCatalog.RoutineOf(lumberjack, LifeStage.Adult, hour: 20));
    }

    [Fact]
    public void House_build_consumes_exactly_the_recipe_from_city_stock()
    {
        var (world, _) = Load(seed: 1);
        var city = world.ActiveCities().Single();
        var recipe = world.CityCatalog.BuildingRecipes[10];
        Assert.Equal(20, recipe.Inputs[Wood]);
        Assert.Equal(10, recipe.Inputs[Stone]);
        city.DepositStock(Wood, 25);
        city.DepositStock(Stone, 10);

        Assert.True(ConstructionSystem.StartConstruction(world, city.Id, 10).IsSuccess);
        new WorldClock([new ConstructionSystem()]).Run(world, 24 * 90);

        Assert.Equal(25 - 20, city.Stock.GetValueOrDefault(Wood));
        Assert.Equal(0, city.Stock.GetValueOrDefault(Stone));
        Assert.Equal(1, HousesBuilt(world));
    }

    [Fact]
    public void Zero_wood_raises_price_and_completes_fewer_buildings()
    {
        int cheaper = 0, fewer = 0;
        for (int seed = 1; seed <= 20; seed++)
        {
            var (control, controlClock) = Load(seed);
            var (treated, treatedClock) = Load(seed, woodScarcity: true);
            controlClock.Run(control, 360 * 24);
            treatedClock.Run(treated, 360 * 24);

            var controlPrice = control.Workplaces.Single(w => w.LocationType.Id == 3).Prices[Wood];
            var treatedPrice = treated.Workplaces.Single(w => w.LocationType.Id == 3).Prices[Wood];
            if (treatedPrice > controlPrice) cheaper++;
            if (HousesBuilt(treated) < HousesBuilt(control)) fewer++;
        }

        Assert.True(cheaper >= 18, $"preco da madeira subiu so em {cheaper}/20 seeds");
        Assert.True(fewer >= 18, $"obras a menos so em {fewer}/20 seeds");
    }

    // --- labor-allocation sobre o cenario ao vivo ---

    private static (WorldState World, WorldClock Clock) LoadWith(int seed, int population, Action<JsonObject>? tweak = null)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(FindRepoRoot(), "scenarios", "default.json")))!.AsObject();
        node["Seed"] = seed;
        node["InitialPopulation"] = population;
        tweak?.Invoke(node);
        var loaded = ScenarioLoaderV2.LoadWorld(node.ToJsonString());
        Assert.True(loaded.IsSuccess, loaded.Error);
        return loaded.Value;
    }

    private static int EmployeesAt(WorldState world, int locationType) =>
        world.Workplaces.Where(w => w.LocationType.Id == locationType).Sum(w => w.Employees.Count);

    [Fact]
    public void Sawmill_and_quarry_get_workers_within_90_days_with_the_default_twenty_npcs()
    {
        int staffed = 0;
        for (int seed = 1; seed <= 20; seed++)
        {
            var (world, clock) = LoadWith(seed, population: 20);
            bool sawmill = false, quarry = false;
            for (int day = 0; day < 90; day++)
            {
                clock.Run(world, 24);
                sawmill |= EmployeesAt(world, 3) >= 1;
                quarry |= EmployeesAt(world, 4) >= 1;
            }

            if (sawmill && quarry) staffed++;
        }

        Assert.True(staffed >= 18, $"serraria e pedreira com gente em so {staffed}/20 seeds");
    }

    /// <summary>Quebra de poço: a água por trabalhador cai a zero. A água encarece e o trabalho na fazenda passa a valer
    /// mais que no controle, então a fatia da população que trabalha lá é maior (fatia, não contagem: sem água morre
    /// gente, e uma população menor confundiria a comparação).</summary>
    [Fact]
    public void A_dry_well_raises_the_share_of_people_working_the_farm_above_the_control()
    {
        int more = 0;
        for (int seed = 1; seed <= 20; seed++)
        {
            var (control, controlClock) = LoadWith(seed, population: 40);
            var (dry, dryClock) = LoadWith(seed, population: 40, node =>
                node["Recipes"]!["1"]!["Outputs"] = new JsonObject { ["1"] = 10, ["2"] = 0 });
            double controlShare = 0, dryShare = 0;
            for (int day = 0; day < 300; day++)
            {
                controlClock.Run(control, 24);
                dryClock.Run(dry, 24);
                if (day < 100) continue;
                controlShare += (double)EmployeesAt(control, 1) / Math.Max(1, control.Npcs.Count(n => n.IsAlive));
                dryShare += (double)EmployeesAt(dry, 1) / Math.Max(1, dry.Npcs.Count(n => n.IsAlive));
            }

            if (dryShare > controlShare) more++;
        }

        Assert.True(more >= 18, $"fatia maior na fazenda so em {more}/20 seeds");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln nao encontrado");
    }
}
