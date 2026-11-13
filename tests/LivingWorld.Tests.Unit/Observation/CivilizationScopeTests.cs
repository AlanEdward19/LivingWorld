using System.Text.Json.Nodes;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Emergence;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Observation;

/// <summary>Issue #112 fatia A, escopo e valor de cada métrica: um mundo de 3 anos (seed 2: várias cidades, crianças) recebe uma
/// cidade B com pool, prédios, oficina, tecnologias e empregos montados à mão. Cada métrica de cada cidade e do mundo é comparada
/// com o que o estado diz para aquele escopo; zerar, trocar ou usar o escopo errado reprova.</summary>
public class CivilizationScopeTests
{
    private sealed record Synth(WorldState World, YearEventTally Tally, CivilizationObserver Observer, City B);

    private static City Biggest(WorldState world, CityId? except) => world.ActiveCities().Where(c => c.Id != except)
        .OrderByDescending(c => world.Npcs.Count(n => n.IsAlive && n.City == c.Id)).ThenBy(c => c.Id.Value).First();

    private static Synth Build(int ageBandYears = 5, ulong seed = 2)
    {
        var root = JsonNode.Parse(ObservatoryFixture.DefaultScenarioJson())!.AsObject();
        root["Seed"] = seed;
        var (world, clock) = ScenarioLoaderV2.LoadWorld(root.ToJsonString()).Value;
        clock.Run(world, world.Calendar.HoursPerYear * 3);

        var first = Biggest(world, null);
        var b = new City(world.NextCityId(), new CellCoord(9, 9), 0, null, new AggregatePopulationPool(10, 1000, 2000),
            treasury: new Money(700), name: "B");
        world.AddCity(b);
        var alive = world.Npcs.Where(n => n.IsAlive).OrderBy(n => n.Id.Value).ToList();
        foreach (var npc in alive.Where(n => n.City == first.Id
            && world.LifeStageRules.LifeStageOf(n.AgeYears(world.CurrentDate)) == LifeStage.Adult).Take(3))
            npc.JoinCity(b.Id);
        for (int i = 0; i < 5; i++) world.AddBuilding(new Building(world.NextBuildingIdAndAdvance(), b.Id, 10, 0));
        world.Workplaces.First().JoinCity(b.Id);

        // Em A e em B: um adulto empregado e os outros desempregados; menores seguem sem vínculo (e não entram no denominador).
        foreach (var city in new[] { first.Id, b.Id })
        {
            var adults = world.Npcs.Where(n => n.IsAlive && n.City == city
                && world.LifeStageRules.LifeStageOf(n.AgeYears(world.CurrentDate)) == LifeStage.Adult).OrderBy(n => n.Id.Value).ToList();
            foreach (var adult in adults) adult.Fire();
            adults[0].Hire(new WorkplaceId(9999));
        }

        var culture = world.Npcs.First().Culture;
        world.AddTechnology(new Technology(culture, "c", new TechnologyVector([1]), 0, first.Id, null, [first.Id]));
        world.AddTechnology(new Technology(culture, "c", new TechnologyVector([2]), 0, first.Id, null, [first.Id, b.Id]));
        world.AddTechnology(new Technology(culture, "c", new TechnologyVector([3]), 0, first.Id, null, []));

        var tally = new YearEventTally(world, ageBandYears);
        return new Synth(world, tally, new CivilizationObserver(world, tally), b);
    }

    private static void AssertScope(WorldState world, IReadOnlyDictionary<string, double> m, IReadOnlyList<City> cities)
    {
        var ids = cities.Select(c => c.Id).ToHashSet();
        var people = world.Npcs.Where(n => n.IsAlive && ids.Contains(n.City)).ToList();
        var ages = people.Select(n => n.AgeYears(world.CurrentDate)).Order().ToList();
        var adults = people.Where(n => world.LifeStageRules.LifeStageOf(n.AgeYears(world.CurrentDate)) == LifeStage.Adult).ToList();
        long pool = cities.Sum(c => c.AggregatePool.Count);
        var wealth = people.Select(n => ((double)n.Wallet.Amount, 1.0)).ToList();
        wealth.AddRange(cities.Where(c => c.AggregatePool.Count > 0).Select(c => ((double)c.AggregatePool.WealthSum / c.AggregatePool.Count, (double)c.AggregatePool.Count)));

        Assert.Equal(people.Count + pool, m["population"]);
        Assert.Equal(people.Count, m["population_materialized"]);
        Assert.Equal(pool, m["population_pool"]);
        Assert.Equal(cities.Sum(c => c.Treasury.Amount), m["treasury"]);
        Assert.Equal(WealthInequality.Gini(wealth), m["gini"], 12);
        Assert.Equal(world.Workplaces.Count(w => ids.Contains(w.City)), m["workshops"]);
        Assert.Equal(world.Buildings.Count(x => ids.Contains(x.City)), m["buildings"]);
        if (ages.Count > 0)
        {
            Assert.Equal(ages.Average(), m["age_mean"], 9);
            Assert.Equal(ages.Count % 2 == 1 ? ages[ages.Count / 2] : (ages[ages.Count / 2 - 1] + ages[ages.Count / 2]) / 2.0, m["age_median"], 9);
        }

        Assert.Equal(adults.Count == 0 ? 0 : (double)adults.Count(n => n.Employer is null) / adults.Count, m["unemployment_rate"], 9);
    }

    [Fact]
    public void Every_metric_of_every_city_and_of_the_world_matches_the_state_of_its_own_scope()
    {
        var s = Build();
        var sample = s.Observer.Sample(s.World);
        var active = s.World.ActiveCities().OrderBy(c => c.Id.Value).ToList();

        Assert.True(active.Count >= 3, "mundo sem várias cidades: o teste de escopo não mediu nada");
        Assert.Equal(active.Select(c => c.Id.ToString()), sample.Cities.Select(c => c.Id));
        AssertScope(s.World, sample.World, active);
        foreach (var city in active) AssertScope(s.World, sample.Cities.Single(c => c.Id == city.Id.ToString()).Metrics, [city]);

        // Sanidade da montagem: os escopos de fato diferem e há criança, adulto empregado e adulto desempregado.
        Assert.Equal(10, sample.Cities.Single(c => c.Id == s.B.Id.ToString()).Metrics["population_pool"]);
        Assert.Equal(5, sample.Cities.Single(c => c.Id == s.B.Id.ToString()).Metrics["buildings"]);
        Assert.True(sample.World["buildings"] > 5 && sample.World["workshops"] > 1 && sample.World["population_pool"] == 10);
        var people = s.World.Npcs.Where(n => n.IsAlive).ToList();
        Assert.Contains(people, n => s.World.LifeStageRules.LifeStageOf(n.AgeYears(s.World.CurrentDate)) == LifeStage.Child);
        Assert.Contains(people, n => n.Employer is not null);
    }

    [Fact]
    public void Unemployed_elders_stay_out_of_the_unemployment_rate_denominator()
    {
        // Procura (em ordem de seed) um mundo com idoso vivo; a taxa só conta adultos, então o idoso sem emprego não entra.
        foreach (ulong seed in new ulong[] { 2, 1, 3, 4, 5, 6, 7, 8 })
        {
            var s = Build(seed: seed);
            var elders = s.World.Npcs.Where(n => n.IsAlive && s.World.LifeStageRules.LifeStageOf(n.AgeYears(s.World.CurrentDate)) == LifeStage.Elder).ToList();
            if (elders.Count == 0) continue;

            foreach (var elder in elders) elder.Fire();
            var sample = s.Observer.Sample(s.World);
            var active = s.World.ActiveCities().OrderBy(c => c.Id.Value).ToList();

            AssertScope(s.World, sample.World, active);
            foreach (var city in active) AssertScope(s.World, sample.Cities.Single(c => c.Id == city.Id.ToString()).Metrics, [city]);
            return;
        }

        Assert.Fail("nenhuma das seeds tem idoso vivo no ano 3: o teste não mediu nada");
    }

    [Fact]
    public void Technologies_count_adoptions_per_city_and_adopted_technologies_in_the_world()
    {
        var s = Build();
        var sample = s.Observer.Sample(s.World);
        var first = s.World.Technologies[0].OriginCity;

        Assert.Equal(2, sample.Cities.Single(c => c.Id == first.ToString()).Metrics["technologies"]);
        Assert.Equal(1, sample.Cities.Single(c => c.Id == s.B.Id.ToString()).Metrics["technologies"]);
        Assert.Equal(2, sample.World["technologies"]);
    }

    [Fact]
    public void Births_and_deaths_recorded_in_a_merged_city_are_reported_by_the_city_that_absorbed_it()
    {
        var s = Build();
        var absorbing = Biggest(s.World, s.B.Id);
        var absorbed = new City(s.World.NextCityId(), new CellCoord(8, 8), 0, null, AggregatePopulationPool.Empty, name: "C");
        s.World.AddCity(absorbed);
        var people = s.World.Npcs.Where(n => n.IsAlive).OrderBy(n => n.Id.Value).Take(2).ToList();
        var origins = people.Select(p => p.City).ToList();
        foreach (var p in people) p.JoinCity(absorbed.Id);
        s.Tally.Record(new WorldEvent(1, WorldEventKind.Birth, people[0].Id.Value.ToString(), SourceSystem: YearEventTally.BirthSource));
        s.Tally.Record(new WorldEvent(1, WorldEventKind.Death, people[1].Id.Value.ToString(), SourceSystem: YearEventTally.DeathSource));
        for (int i = 0; i < people.Count; i++) people[i].JoinCity(origins[i]);
        absorbed.MarkMergedInto(absorbing.Id);

        var sample = s.Observer.Sample(s.World);
        var mine = sample.Cities.Single(c => c.Id == absorbing.Id.ToString()).Metrics;

        Assert.Equal(1, mine["births"]);
        Assert.Equal(1, mine["deaths"]);
        Assert.Equal(1, mine["deaths_age"]);
        Assert.DoesNotContain(sample.Cities, c => c.Id == absorbed.Id.ToString());
        Assert.Equal(1, sample.World["births"]);
        Assert.Equal(1, sample.World["deaths"]);
        Assert.Equal(0, sample.Cities.Where(c => c.Id != absorbing.Id.ToString()).Sum(c => c.Metrics["births"] + c.Metrics["deaths"]));
    }

    [Fact]
    public void Each_cause_has_its_own_count_per_city_and_in_the_world()
    {
        var s = Build();
        var people = s.World.Npcs.Where(n => n.IsAlive).OrderBy(n => n.Id.Value).ToList();
        WorldEventKind[] kinds = [WorldEventKind.Starvation, WorldEventKind.Starvation, WorldEventKind.Dehydration,
            WorldEventKind.Disease, WorldEventKind.Disease, WorldEventKind.Death,
            WorldEventKind.Exhaustion, WorldEventKind.Exhaustion, WorldEventKind.MaternalDeath, WorldEventKind.MaternalDeath];
        var perCity = new Dictionary<string, double[]>();
        for (int i = 0; i < kinds.Length; i++)
        {
            s.Tally.Record(new WorldEvent(1, kinds[i], people[i].Id.Value.ToString(), SourceSystem: YearEventTally.DeathSource));
            int cause = YearEventTally.CauseOf(kinds[i]) switch { "hunger_thirst" => 0, "disease" => 1, "age" => 2, _ => 3 };
            var counts = perCity.TryGetValue(people[i].City.ToString(), out var c) ? c : perCity[people[i].City.ToString()] = new double[4];
            counts[cause]++;
        }

        var birthsByCity = new Dictionary<string, double>();
        foreach (var baby in people.Skip(kinds.Length).Take(3))
        {
            s.Tally.Record(new WorldEvent(1, WorldEventKind.Birth, baby.Id.Value.ToString(), SourceSystem: YearEventTally.BirthSource));
            birthsByCity[baby.City.ToString()] = birthsByCity.GetValueOrDefault(baby.City.ToString()) + 1;
        }

        var sample = s.Observer.Sample(s.World);

        Assert.Equal(3, sample.World["births"]);
        Assert.Equal(sample.World["cities_active"], sample.Cities.Count);
        Assert.All(sample.Cities, c => Assert.Equal(birthsByCity.GetValueOrDefault(c.Id), c.Metrics["births"]));
        Assert.Equal([3.0, 2.0, 1.0, 4.0], new[] { sample.World["deaths_hunger_thirst"], sample.World["deaths_disease"], sample.World["deaths_age"], sample.World["deaths_other"] });
        Assert.Equal(10, sample.World["deaths"]);
        foreach (var city in sample.Cities)
        {
            var expected = perCity.GetValueOrDefault(city.Id) ?? new double[4];
            Assert.Equal(expected, new[] { city.Metrics["deaths_hunger_thirst"], city.Metrics["deaths_disease"], city.Metrics["deaths_age"], city.Metrics["deaths_other"] });
            Assert.Equal(expected.Sum(), city.Metrics["deaths"]);
        }
    }

    [Fact]
    public void Life_expectancy_uses_only_the_deaths_and_ages_of_its_own_scope()
    {
        // Morte numa faixa com 3 ou mais vivos e fora da última (aberta): q fica abaixo de 1 e a exposição pesa no resultado.
        // (AD-140: com 2 vivos, faixa de 5 anos e 1 morte, q = 5m/(1 + 2,5m) com m = 1/2,5 dá exatamente 1, e0 = 2,5 em qualquer
        // escopo, e o teste deixava de distinguir cidade de mundo; o AD-138 tinha baixado para 2. A vila de 20 pessoas muda de
        // trajetória a cada fatia, então o teste usa a primeira seed que tem essa faixa, em vez de uma seed fixa.)
        Synth s = null!;
        City city = null!;
        Npc? dead = null;
        int width = 0;
        int Age(Npc n) => n.AgeYears(s.World.CurrentDate);
        foreach (ulong seed in new ulong[] { 2, 3, 4, 5, 6, 7 })
        {
            s = Build(seed: seed);
            width = s.Tally.AgeBandYears;
            (city, dead) = s.World.ActiveCities().Where(c => c.Id != s.B.Id).OrderBy(c => c.Id.Value).Select(c =>
            {
                var local = s.World.Npcs.Where(n => n.IsAlive && n.City == c.Id).ToList();
                var pick = local.Where(n => local.Count(o => Age(o) / width == Age(n) / width) >= 3 && Age(n) / width < local.Max(Age) / width)
                    .OrderBy(n => n.Id.Value).FirstOrDefault();
                return (c, pick);
            }).FirstOrDefault(x => x.pick is not null);
            if (dead is not null) break;
        }

        Assert.NotNull(dead);
        s.Tally.Record(new WorldEvent(1, WorldEventKind.Death, dead!.Id.Value.ToString(), SourceSystem: YearEventTally.DeathSource));

        double Expected(IReadOnlyCollection<Npc> scope)
        {
            var ages = scope.Select(Age).ToList();
            int deathBand = Age(dead) / width;
            int count = Math.Max(ages.Max() / width, deathBand) + 1;
            var bands = Enumerable.Range(0, count).Select(i =>
            {
                long deaths = i == deathBand ? 1 : 0;
                return new AgeBand(width, deaths, ages.Count(a => a / width == i) + deaths / 2.0);
            }).ToList();
            return Simulation.Observation.LifeTable.LifeExpectancyAtBirth(bands);
        }

        var sample = s.Observer.Sample(s.World);
        var activeIds = s.World.ActiveCities().Select(c => c.Id).ToHashSet();
        double mine = sample.Cities.Single(c => c.Id == city.Id.ToString()).Metrics["life_expectancy_at_birth"];

        Assert.Equal(Expected(s.World.Npcs.Where(n => n.IsAlive && n.City == city.Id).ToList()), mine, 9);
        Assert.Equal(Expected(s.World.Npcs.Where(n => n.IsAlive && activeIds.Contains(n.City)).ToList()), sample.World["life_expectancy_at_birth"], 9);
        Assert.NotEqual(mine, sample.World["life_expectancy_at_birth"]);
        Assert.All(sample.Cities.Where(c => c.Id != city.Id.ToString()), c => Assert.Equal(0, c.Metrics["life_expectancy_at_birth"]));
    }

    [Fact]
    public void Living_people_outside_any_active_city_are_reported_as_unassigned_and_not_counted_in_the_population()
    {
        var s = Build();
        var before = s.Observer.Sample(s.World).World;
        var lost = s.World.Npcs.First(n => n.IsAlive);
        lost.JoinCity(new CityId(Guid.NewGuid()));

        var after = new CivilizationObserver(s.World, s.Tally).Sample(s.World).World;

        Assert.Equal(0, before["population_unassigned"]);
        Assert.Equal(1, after["population_unassigned"]);
        Assert.Equal(before["population"] - 1, after["population"]);
    }
}
