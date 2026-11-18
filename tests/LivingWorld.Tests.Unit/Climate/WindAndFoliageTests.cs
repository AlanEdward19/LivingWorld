using LivingWorld.Domain.Climate;
using LivingWorld.Domain.Ecology;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Fauna;
using LivingWorld.Domain.Flora;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Climate;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Ecology;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Climate;

/// <summary>AD-189: o vento tem direção (rosa do lugar, persistência, advecção na temperatura) e a folhagem brota e cai pelo calor e
/// pela luz do tempo do dia; o pasto do herbívoro lê a folhagem.</summary>
public sealed class WindAndFoliageTests
{
    private const int Days = 360;

    /// <summary>Anos do gerador sozinho, com vento e folhagem, devolvendo cada dia (dia do ano, linha copiada).</summary>
    private static List<(int Doy, WeatherRegionRow Row)> Years(int years, WeatherRules? rules = null, ulong seed = 42)
    {
        var r = rules ?? WeatherRules.Default;
        var rng = new WorldRng(seed);
        var row = new WeatherRegionRow();
        var days = new List<(int, WeatherRegionRow)>();
        for (int d = 0; d < Days * years; d++)
        {
            int doy = d % Days;
            ulong s1 = rng.DeriveState(d), s2 = rng.DeriveState(1_000_000 + d), w1 = rng.DeriveState(2_000_000 + d), w2 = rng.DeriveState(3_000_000 + d);
            WeatherGenerator.Step(r, row, WeatherGenerator.Read(ref s1), WeatherGenerator.Read(ref s2), doy + 0.5, 30, 12, d == 0,
                WeatherGenerator.ReadWind(ref w1), WeatherGenerator.ReadWind(ref w2));
            double seasonal = r.LatitudeDeg < 0 ? (doy + 0.5 + Days / 2.0) % Days : doy + 0.5;
            WeatherGenerator.Foliage(r, row, seasonal, 30, 12, r.DaylightHours(doy + 0.5, Days), d == 0);
            days.Add((doy, new WeatherRegionRow
            {
                WindFromDeg = row.WindFromDeg, AdvectionCenti = row.AdvectionCenti, MeanCenti = row.MeanCenti,
                LeafPm = row.LeafPm, LeafPhase = row.LeafPhase, LeafColdTenths = row.LeafColdTenths,
            }));
        }
        return days;
    }

    private static int Sector(int deg) => (int)Math.Round(deg / 45.0) % 8;

    [Fact]
    public void Wind_direction_follows_the_rose_of_the_place_with_southwesterlies_most_common()
    {
        var days = Years(20);
        var counts = new int[8];
        foreach (var (_, row) in days) counts[Sector(row.WindFromDeg)]++;
        var rose = WeatherRules.Default.WindRoseFrom;
        double total = rose.Sum();

        Assert.Equal(5, Array.IndexOf(counts, counts.Max())); // sudoeste
        for (int k = 0; k < 8; k++)
            Assert.InRange(counts[k] / (double)days.Count, rose[k] / total - 0.05, rose[k] / total + 0.05);
    }

    [Fact]
    public void Wind_direction_persists_from_one_day_to_the_next()
    {
        var days = Years(20);
        int same = 0;
        for (int i = 1; i < days.Count; i++)
            if (Sector(days[i].Row.WindFromDeg) == Sector(days[i - 1].Row.WindFromDeg)) same++;
        var rose = WeatherRules.Default.WindRoseFrom;
        double total = rose.Sum(), byChance = rose.Sum(p => p / total * (p / total));

        Assert.True(same / (double)(days.Count - 1) > byChance + 0.2, $"mesmo setor no dia seguinte {same / (double)(days.Count - 1):P0}, ao acaso {byChance:P0}");
    }

    [Fact]
    public void In_winter_an_easterly_wind_is_colder_than_a_southwesterly_and_the_mean_of_the_month_does_not_move()
    {
        var days = Years(30);
        var winter = days.Where(d => d.Doy < 60).ToList();
        double east = winter.Where(d => Sector(d.Row.WindFromDeg) is 1 or 2).Average(d => d.Row.MeanCenti) / 100.0;
        double southwest = winter.Where(d => Sector(d.Row.WindFromDeg) is 5).Average(d => d.Row.MeanCenti) / 100.0;
        var still = Years(30, WeatherRules.Default with { WindAdvectionC = [] });
        double meanWith = winter.Average(d => d.Row.MeanCenti) / 100.0;
        double meanWithout = still.Where(d => d.Doy < 60).Average(d => d.Row.MeanCenti) / 100.0;

        Assert.True(southwest - east > 2, $"sudoeste {southwest:F1} °C, leste {east:F1} °C");
        Assert.InRange(meanWith - meanWithout, -0.4, 0.4);
    }

    [Fact]
    public void The_variance_of_the_day_stays_the_one_of_the_source_with_advection()
    {
        static double Sd(IEnumerable<double> xs) { var a = xs.ToArray(); double m = a.Average(); return Math.Sqrt(a.Sum(x => (x - m) * (x - m)) / a.Length); }
        var with = Years(30).Where(d => d.Doy < 30).Select(d => d.Row.MeanCenti / 100.0);
        var without = Years(30, WeatherRules.Default with { WindAdvectionC = [] }).Where(d => d.Doy < 30).Select(d => d.Row.MeanCenti / 100.0);

        Assert.InRange(Sd(with) / Sd(without), 0.9, 1.12);
    }

    [Fact]
    public void Oak_leaves_bud_in_april_are_full_by_june_turn_and_fall_in_autumn_and_are_gone_by_december()
    {
        var days = Years(20);
        var budburst = new List<int>();
        var full = new List<int>();
        var falling = new List<int>();
        for (int y = 0; y < 20; y++)
        {
            var year = days.Skip(y * Days).Take(Days).ToList();
            budburst.Add(year.First(d => d.Row.LeafPm > 0).Doy);
            full.Add(year.First(d => d.Row.LeafPm >= 1000).Doy);
            falling.Add(year.First(d => d.Row.LeafPhase == LeafPhase.Falling && d.Doy > 180).Doy);
            Assert.Equal(0, year[345].Row.LeafPm); // meio de dezembro: nua
            Assert.Equal(0, year[30].Row.LeafPm); // começo de fevereiro: nua
            Assert.True(year[240].Row.LeafPhase == LeafPhase.Full); // fim de agosto: cheia
        }

        Assert.InRange(budburst.Average(), 95, 130);  // abril, começo de maio
        Assert.InRange(full.Average(), 135, 170);     // meados de maio a meados de junho
        Assert.InRange(falling.Average(), 285, 325);  // meados de outubro a meados de novembro
        Assert.True(budburst.Max() - budburst.Min() >= 5, "a primavera quente adianta a brotação e a fria atrasa");
    }

    [Fact]
    public void In_the_southern_hemisphere_the_leaves_follow_the_other_half_of_the_year()
    {
        var south = WeatherRules.Default with { LatitudeDeg = -35 };
        var days = Years(3, south);
        var year = days.Skip(Days).Take(Days).ToList();

        Assert.Equal(0, year[180 + 30].Row.LeafPm); // agosto no sul: inverno
        Assert.Equal(LeafPhase.Full, year[60].Row.LeafPhase); // março no sul: fim do verão
    }

    [Fact]
    public void A_world_started_in_summer_has_full_leaves_on_the_first_day()
    {
        var r = WeatherRules.Default;
        var row = new WeatherRegionRow { MeanCenti = 1600 };
        WeatherGenerator.Foliage(r, row, 200.5, 30, 12, r.DaylightHours(200.5, Days), first: true);

        Assert.Equal(LeafPhase.Full, row.LeafPhase);
        Assert.Equal(1000, row.LeafPm);
    }

    [Fact]
    public void The_world_keeps_wind_and_leaves_in_the_snapshot_and_the_hash()
    {
        var (world, clock) = ScenarioRunner.Create(3, initialPopulation: 5);
        clock.Run(world, 130 * 24);
        var row = WeatherNow.RowAt(world, world.ActiveCities().First().Location)!;
        var reopened = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        var again = WeatherNow.RowAt(reopened, reopened.ActiveCities().First().Location)!;

        Assert.True(row.LeafPm > 0);
        Assert.Equal(row.WindFromDeg, again.WindFromDeg);
        Assert.Equal(row.LeafPm, again.LeafPm);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(reopened));

        string before = WorldSnapshot.CanonicalHash(world);
        row.WindFromDeg = (row.WindFromDeg + 90) % 360;
        world.TouchWeather();
        Assert.NotEqual(before, WorldSnapshot.CanonicalHash(world));
    }

    [Fact]
    public void Turning_the_wind_rose_changes_the_world()
    {
        var (a, ca) = ScenarioRunner.Create(4, initialPopulation: 5);
        var (b, cb) = ScenarioRunner.Create(4, initialPopulation: 5,
            climateRules: ClimateRules.Default with { Weather = WeatherRules.Default with { WindRoseFrom = [24, 19, 14, 7, 9, 8, 7, 12] } });
        ca.Run(a, 40 * 24);
        cb.Run(b, 40 * 24);

        Assert.NotEqual(WorldSnapshot.CanonicalHash(a), WorldSnapshot.CanonicalHash(b));
    }

    private static readonly AnimalSpeciesRules Grazer = new(
        "rabbit", HungerDecayPerTick: 0.35, ReproduceEnergyThreshold: 50, ReproduceRadius: 2, ReproduceProbability: 0,
        PredatorOf: null, PredationProbability: 0, Diet: AnimalSpeciesRules.FloraDiet, ForageRadius: 2, MealEnergy: 25,
        GrazeStageCost: 10);

    private static WorldState GrazingWorld(int leafPm)
    {
        var energy = LazyNeed.Initial(100, 0, Grazer.HungerDecayPerTick);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 5, ScenarioRunner.DefaultMap(5),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(false, []),
            fauna: [new Animal(new AnimalId(1), Grazer.Species, new CellCoord(1, 1), true, null, energy)],
            animalSpeciesRules: [Grazer], nextAnimalId: 2,
            flora: [new Plant(new PlantId(1), "grass", new CellCoord(1, 2), 100000)],
            plantSpeciesRules: [new PlantSpeciesRules("grass", -50, 80, 100000, 1, 0, 0, 0)], nextPlantId: 100,
            climateRules: ClimateRules.Default);
        foreach (var region in world.Map.Regions)
            world.Weather!.Regions.Add(new WeatherRegionRow { Region = region.Id.Value, LeafPm = leafPm, MeanCenti = 1000 });
        world.Weather!.Day = 0;
        world.Weather.Reindex();
        return world;
    }

    [Fact]
    public void A_rabbit_on_bare_winter_pasture_eats_more_of_it_than_one_in_full_leaf()
    {
        var bare = GrazingWorld(0);
        var full = GrazingWorld(1000);
        new WorldClock([new FaunaLifecycleSystem()]).Run(bare, 20 * 24);
        new WorldClock([new FaunaLifecycleSystem()]).Run(full, 20 * 24);

        Assert.Equal(0.5, WeatherNow.ForageShare(bare, new CellCoord(1, 2)));
        Assert.True(bare.Flora.Single().GrowthStage < full.Flora.Single().GrowthStage,
            $"pasto que sobrou: nu {bare.Flora.Single().GrowthStage}, cheio {full.Flora.Single().GrowthStage}");
    }
}
