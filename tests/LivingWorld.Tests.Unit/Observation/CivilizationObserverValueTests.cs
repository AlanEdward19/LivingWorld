using System.Text.Json.Nodes;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Workers;

namespace LivingWorld.Tests.Unit.Observation;

/// <summary>Issue #112 fatia A (valores) e B (guardas): cada métrica contra um valor conhecido, o mapeamento de causa de morte,
/// o filtro de origem do sink e as guardas do comando. Mundo carregado e amostrado sem rodar tick, com eventos injetados no
/// sink: o resultado esperado sai do estado ou da contagem feita à mão no teste.</summary>
public class CivilizationObserverValueTests
{
    private static (WorldState World, YearEventTally Tally, CivilizationObserver Observer) Fresh(ulong seed = 2)
    {
        var root = JsonNode.Parse(ObservatoryFixture.DefaultScenarioJson())!.AsObject();
        root["Seed"] = seed;
        var (world, _) = ScenarioLoaderV2.LoadWorld(root.ToJsonString()).Value;
        var tally = new YearEventTally(world);
        return (world, tally, new CivilizationObserver(world, tally));
    }

    private static List<Npc> Alive(WorldState world) => world.Npcs.Where(n => n.IsAlive).OrderBy(n => n.Id.Value).ToList();

    private static WorldEvent Event(WorldState world, WorldEventKind kind, Npc npc, string source) =>
        new(world.CurrentDate.TotalHours, kind, npc.Id.Value.ToString(), SourceSystem: source);

    [Fact]
    public void Each_death_kind_lands_in_its_documented_cause()
    {
        var (world, tally, observer) = Fresh();
        var people = Alive(world);
        WorldEventKind[] kinds = [WorldEventKind.Starvation, WorldEventKind.Dehydration, WorldEventKind.Disease,
            WorldEventKind.Death, WorldEventKind.Exhaustion, WorldEventKind.MaternalDeath];
        for (int i = 0; i < kinds.Length; i++)
            tally.Record(Event(world, kinds[i], people[i], YearEventTally.DeathSource));

        var w = observer.Sample(world).World;

        Assert.Equal(6, w["deaths"]);
        Assert.Equal(2, w["deaths_hunger_thirst"]);
        Assert.Equal(1, w["deaths_disease"]);
        Assert.Equal(1, w["deaths_age"]);
        Assert.Equal(2, w["deaths_other"]);
    }

    [Fact]
    public void Events_from_other_systems_with_an_npc_id_are_not_counted_and_the_npc_systems_are()
    {
        var (world, tally, observer) = Fresh();
        var npc = Alive(world)[0];
        // A fauna reusa Birth/Death/Starvation e o payload começa com um id numérico que pode ser de NPC.
        tally.Record(Event(world, WorldEventKind.Death, npc, "FaunaLifecycleSystem"));
        tally.Record(Event(world, WorldEventKind.Starvation, npc, "FaunaLifecycleSystem"));
        tally.Record(Event(world, WorldEventKind.Birth, npc, "FaunaLifecycleSystem"));
        tally.Record(Event(world, WorldEventKind.Birth, npc, YearEventTally.BirthSource));

        var w = observer.Sample(world).World;

        Assert.Equal(0, w["deaths"]);
        Assert.Equal(1, w["births"]);
    }

    [Fact]
    public void Cities_merged_counts_the_city_merged_events_of_the_year_and_resets_after_sampling()
    {
        var (world, tally, observer) = Fresh();
        tally.Record(new WorldEvent(1, WorldEventKind.CityMerged, "x"));
        tally.Record(new WorldEvent(1, WorldEventKind.CityMerged, "y"));

        Assert.Equal(2, observer.Sample(world).World["cities_merged"]);
        Assert.Equal(0, observer.Sample(world).World["cities_merged"]);
    }

    [Fact]
    public void Age_unemployment_workshops_and_buildings_match_the_state_for_the_world_and_for_each_city()
    {
        var (world, _, observer) = Fresh();
        var sample = observer.Sample(world);

        Assert.Equal(WorldMetrics(world, null).Count, sample.World["population_materialized"]);
        AssertMatches(world, sample.World, null);
        foreach (var city in sample.Cities)
            AssertMatches(world, city.Metrics, world.ActiveCities().Single(c => c.Id.ToString() == city.Id).Id);

        Assert.True(sample.World["workshops"] > 0 && sample.World["buildings"] > 0 && sample.World["age_mean"] > 0
            && sample.World["age_median"] > 0 && sample.World["unemployment_rate"] > 0, "fixture sem valor: o teste não mediu nada");
    }

    private static List<Npc> WorldMetrics(WorldState world, LivingWorld.Domain.Shared.CityId? city) =>
        Alive(world).Where(n => city is null || n.City == city).ToList();

    private static void AssertMatches(WorldState world, IReadOnlyDictionary<string, double> metrics, LivingWorld.Domain.Shared.CityId? city)
    {
        var people = WorldMetrics(world, city);
        var ages = people.Select(n => n.AgeYears(world.CurrentDate)).Order().ToList();
        double median = ages.Count % 2 == 1 ? ages[ages.Count / 2] : (ages[ages.Count / 2 - 1] + ages[ages.Count / 2]) / 2.0;
        var adults = people.Where(n => world.LifeStageRules.LifeStageOf(n.AgeYears(world.CurrentDate)) == LifeStage.Adult).ToList();

        Assert.Equal(ages.Average(), metrics["age_mean"], 9);
        Assert.Equal(median, metrics["age_median"], 9);
        Assert.Equal((double)adults.Count(n => n.Employer is null) / adults.Count, metrics["unemployment_rate"], 9);
        Assert.Equal(world.Workplaces.Count(w => city is null || w.City == city), metrics["workshops"]);
        Assert.Equal(world.Buildings.Count(b => city is null || b.City == city), metrics["buildings"]);
    }

    [Fact]
    public void Life_expectancy_comes_from_the_deaths_of_the_year_and_is_zero_when_nobody_died()
    {
        var (world, tally, observer) = Fresh();
        Assert.Equal(0, observer.Sample(world).World["life_expectancy_at_birth"]);

        var people = Alive(world);
        var dead = people.Take(2).ToList();
        foreach (var npc in dead) tally.Record(Event(world, WorldEventKind.Death, npc, YearEventTally.DeathSource));

        // Esperado à mão: faixas de 5 anos; exposição = vivos na faixa + metade das mortes dela (os dois "mortos" seguem no estado).
        int width = tally.AgeBandYears;
        var ages = people.Select(n => n.AgeYears(world.CurrentDate)).ToList();
        var deathAges = dead.Select(n => n.AgeYears(world.CurrentDate)).ToList();
        int count = Math.Max(ages.Max() / width, deathAges.Max() / width) + 1;
        var bands = Enumerable.Range(0, count).Select(i =>
        {
            long deaths = deathAges.Count(a => a / width == i);
            return new AgeBand(width, deaths, ages.Count(a => a / width == i) + deaths / 2.0);
        }).ToList();

        double actual = observer.Sample(world).World["life_expectancy_at_birth"];

        Assert.Equal(Simulation.Observation.LifeTable.LifeExpectancyAtBirth(bands), actual, 9);
        Assert.True(actual > 0);
    }

    /// <summary>Duas cidades fundidas em cadeia (A absorvida por B, B absorvida pela cidade ativa) com mortes no mesmo ano; depois
    /// todos os NPCs voltam à cidade ativa (como o estado fica quando o ano fecha). Índice = posição entre os vivos por id.</summary>
    private static (WorldYearSample Sample, string Survivor) ChainMerged(params (int Npc, WorldEventKind Kind, bool InB)[] deaths)
    {
        var (world, tally, observer) = Fresh();
        var people = Alive(world);
        var survivor = people[0].City;
        var a = new City(world.NextCityId(), new CellCoord(8, 8), 0, null, AggregatePopulationPool.Empty, name: "A");
        world.AddCity(a);
        var b = new City(world.NextCityId(), new CellCoord(9, 9), 0, null, AggregatePopulationPool.Empty, name: "B");
        world.AddCity(b);
        foreach (var (npc, kind, inB) in deaths)
        {
            people[npc].JoinCity(inB ? b.Id : a.Id);
            tally.Record(Event(world, kind, people[npc], kind == WorldEventKind.Birth ? YearEventTally.BirthSource : YearEventTally.DeathSource));
        }

        foreach (var p in people) p.JoinCity(survivor);
        a.MarkMergedInto(b.Id);
        b.MarkMergedInto(survivor);
        return (observer.Sample(world), survivor.ToString());
    }

    [Fact]
    public void Deaths_and_causes_of_chained_merged_cities_add_up_in_the_surviving_city()
    {
        // Seed 2: vivos 13, 18, 19, 3, 15 têm idades fixas; o índice é o id do NPC.
        // A (fundida em B): 13 idade, 18 doença, 3 idade; nascem 0 e 1. B (fundida na ativa): 19 idade, 15 doença; nasce 4.
        var (sample, survivor) = ChainMerged((0, WorldEventKind.Birth, false), (1, WorldEventKind.Birth, false), (4, WorldEventKind.Birth, true),
            (13, WorldEventKind.Death, false), (18, WorldEventKind.Disease, false), (3, WorldEventKind.Death, false),
            (19, WorldEventKind.Death, true), (15, WorldEventKind.Disease, true));

        var mine = sample.Cities.Single(c => c.Id == survivor).Metrics;
        // Nascimentos: 2 (A) + 1 (B) = 3. Mortes: 3 (A) + 2 (B) = 5. Idade: 2 (A) + 1 (B) = 3. Doença: 1 (A) + 1 (B) = 2.
        Assert.Equal(3, mine["births"]);
        Assert.Equal(5, mine["deaths"]);
        Assert.Equal(3, mine["deaths_age"]);
        Assert.Equal(2, mine["deaths_disease"]);
        Assert.Equal(0, mine["deaths_hunger_thirst"] + mine["deaths_other"]);
        Assert.Equal(5, sample.World["deaths"]);
        Assert.Equal(3, sample.World["deaths_age"]);
        Assert.Equal(2, sample.World["deaths_disease"]);
    }

    [Fact]
    public void Deaths_in_the_same_age_band_of_two_merged_cities_add_up_in_life_expectancy()
    {
        var people = Alive(Fresh().World);
        var date = Fresh().World.CurrentDate;
        Assert.All(new[] { 2, 8, 10 }, i => Assert.Equal(59, people[i].AgeYears(date)));

        // A tem 1 morte e B tem 2 na faixa 11 (55-59 anos). Os 20 vivos voltam à cidade ativa; faixas de 5 anos.
        var (sample, survivor) = ChainMerged(
            (2, WorldEventKind.Death, false), (8, WorldEventKind.Death, true), (10, WorldEventKind.Death, true));

        // Faixa 11 (última, aberta): 3 mortes; exposição = 3 vivos de 59 anos + 3/2 = 4,5; m = 3/4,5 = 2/3.
        // Faixas 0-10 sem mortes: q = 0, l = 1, cada uma L = 5·(1+1)/2 = 5, total 11·5 = 55. Aberta: L = l/m = 1/(2/3) = 1,5.
        // e0 = 56,5. Se a soma virasse atribuição, a faixa teria 1 ou 2 mortes (e0 = 58,5 ou 57).
        Assert.Equal(56.5, sample.Cities.Single(c => c.Id == survivor).Metrics["life_expectancy_at_birth"], 9);
        Assert.Equal(56.5, sample.World["life_expectancy_at_birth"], 9);
    }

    [Fact]
    public void A_death_in_an_age_band_above_every_living_person_still_counts_in_life_expectancy()
    {
        var (world, tally, observer) = Fresh();
        var people = Alive(world);
        var home = people[0].City;
        var elsewhere = new City(world.NextCityId(), new CellCoord(8, 8), 0, null, AggregatePopulationPool.Empty, name: "P");
        world.AddCity(elsewhere);
        Assert.All(new[] { 2, 8, 10 }, i => Assert.Equal(59, people[i].AgeYears(world.CurrentDate)));
        Assert.Equal(53, people.Where(n => n.City == home && !new long[] { 2, 8, 10 }.Contains(n.Id.Value)).Max(n => n.AgeYears(world.CurrentDate)));
        tally.Record(Event(world, WorldEventKind.Death, people[2], YearEventTally.DeathSource));
        foreach (var i in new[] { 2, 8, 10 }) people[i].JoinCity(elsewhere.Id); // os de 59 anos saem: o mais velho que sobra tem 53 (faixa 10)

        var mine = observer.Sample(world).Cities.Single(c => c.Id == home.ToString()).Metrics;

        // Faixa 11 (55-59, acima de todo vivo da cidade): 1 morte, exposição 0 + 1/2 = 0,5, m = 2. Faixas 0-10 sem mortes:
        // 11·5 = 55; a última, aberta, dá L = l/m = 1/2 = 0,5. e0 = 55,5. Sem contar a faixa da morte, a aberta seria a 10 e e0 = 52,5.
        Assert.Equal(55.5, mine["life_expectancy_at_birth"], 9);
    }

    [Fact]
    public void A_city_with_nobody_alive_at_year_end_still_has_life_expectancy_from_its_deaths()
    {
        var (world, tally, observer) = Fresh();
        var people = Alive(world);
        var home = people[0].City;
        var elsewhere = new City(world.NextCityId(), new CellCoord(8, 8), 0, null, AggregatePopulationPool.Empty, name: "P");
        world.AddCity(elsewhere);
        Assert.Equal(59, people[2].AgeYears(world.CurrentDate));
        tally.Record(Event(world, WorldEventKind.Death, people[2], YearEventTally.DeathSource));
        foreach (var p in people) p.JoinCity(elsewhere.Id);
        Assert.DoesNotContain(world.Npcs, n => n.IsAlive && n.City == home); // pré-condição: zero vivos na cidade

        var sample = observer.Sample(world);

        // Sem vivos, só a morte define as faixas: faixa 11 (55-59): 1 morte, exposição 0 + 1/2 = 0,5, m = 2. Faixas 0-10 sem mortes
        // (taxa 0, q = 0): 11·5 = 55; a aberta dá L = l/m = 1/2 = 0,5. e0 = 55,5 (cidade vazia não pode virar 0).
        Assert.Equal(55.5, sample.Cities.Single(c => c.Id == home.ToString()).Metrics["life_expectancy_at_birth"], 9);
    }

    [Fact]
    public void A_world_with_nobody_alive_at_year_end_still_has_life_expectancy_from_the_deaths_of_the_year()
    {
        var (world, tally, observer) = Fresh();
        var people = Alive(world);
        Assert.Equal(59, people[2].AgeYears(world.CurrentDate));
        tally.Record(Event(world, WorldEventKind.Death, people[2], YearEventTally.DeathSource));
        foreach (var p in people) p.Die(world.CurrentDate);
        Assert.DoesNotContain(world.Npcs, n => n.IsAlive); // pré-condição: extinção

        // Mesma conta da cidade vazia: 1 morte na faixa 11, exposição 0,5, e0 = 55 + 0,5 = 55,5.
        Assert.Equal(55.5, observer.Sample(world).World["life_expectancy_at_birth"], 9);
    }

    [Fact]
    public void A_city_founded_on_the_last_tick_of_a_year_counts_in_that_year_and_not_in_the_next()
    {
        var (world, _, observer) = Fresh();
        long start = world.CurrentDate.TotalHours;
        var founded = new City(world.NextCityId(), new CellCoord(8, 8), start + 100, null, AggregatePopulationPool.Empty, name: "F");
        world.AddCity(founded);

        // Ano 1 termina no tick start+100: a janela é (start, start+100] e a cidade está exatamente no limite superior.
        world.CurrentDate = world.CurrentDate.AddHours(100);
        long first = (long)observer.Sample(world).World["cities_founded"];
        // Ano 2 termina em start+200: janela (start+100, start+200], o limite inferior é aberto, então não conta de novo.
        world.CurrentDate = world.CurrentDate.AddHours(100);
        long second = (long)observer.Sample(world).World["cities_founded"];

        // As cidades do cenário nascem no tick start (fora da janela do ano 1, limite inferior aberto): só a nova conta.
        Assert.Equal(1, first);
        Assert.Equal(0, second);
    }

    [Fact]
    public void Age_mean_is_zero_in_a_scope_without_living_people_and_the_report_summary_uses_that_zero()
    {
        var (aliveWorld, _, aliveObserver) = Fresh();
        var aliveSample = aliveObserver.Sample(aliveWorld);
        var (extinctWorld, _, extinctObserver) = Fresh();
        var empty = new City(extinctWorld.NextCityId(), new CellCoord(8, 8), 0, null, AggregatePopulationPool.Empty, name: "E");
        extinctWorld.AddCity(empty);
        foreach (var npc in Alive(extinctWorld)) npc.Die(extinctWorld.CurrentDate);
        Assert.DoesNotContain(extinctWorld.Npcs, n => n.IsAlive); // pré-condição: nenhum vivo no mundo nem na cidade vazia
        var extinctSample = extinctObserver.Sample(extinctWorld);

        Assert.Equal(0, extinctSample.World["age_mean"]);
        Assert.Equal(0, extinctSample.Cities.Single(c => c.Id == empty.Id.ToString()).Metrics["age_mean"]);

        // Idades dos 20 vivos do seed 2: 21+9+59+41+4+4+27+50+59+34+59+14+33+38+53+41+17+15+37+39 = 654; média 654/20 = 32,7.
        Assert.Equal(32.7, aliveSample.World["age_mean"], 9);
        string json = CivilizationReport.ToJson("x.json", [new SeedRun(1, [aliveSample]), new SeedRun(2, [extinctSample])]);
        var mean = System.Text.Json.Nodes.JsonNode.Parse(json)!["summary"]!["final"]!["age_mean"]!;

        // Seeds com 32,7 e 0: min 0, média (32,7 + 0)/2 = 16,35, máx 32,7. Com NaN no 0, o resumo todo viraria 0.
        Assert.Equal(0, mean["min"]!.GetValue<double>(), 9);
        Assert.Equal(16.35, mean["mean"]!.GetValue<double>(), 9);
        Assert.Equal(32.7, mean["max"]!.GetValue<double>(), 9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void The_year_tally_rejects_a_non_positive_age_band_width(int width)
    {
        var (world, _, _) = Fresh();
        Assert.Throws<ArgumentOutOfRangeException>(() => new YearEventTally(world, width));
    }

    [Fact]
    public void Diversity_is_zero_with_no_metrics_and_with_runs_that_have_no_years()
    {
        var (world, _, observer) = Fresh();
        var sample = observer.Sample(world);
        SeedRun[] two = [new(1, [sample]), new(2, [sample])];
        SeedRun[] noYears = [new(1, []), new(2, [])];

        // Sem métricas não há o que tirar a média: 0 (não 0/0). Sem anos, nada a comparar: 0 (não NaN).
        Assert.Equal(0, TrajectoryDiversity.Compute(two, []).Index);
        Assert.Equal(0, TrajectoryDiversity.Compute(noYears).Index);
    }

    [Fact]
    public void The_report_summary_skips_a_seed_without_years_instead_of_failing()
    {
        var (world, _, observer) = Fresh();
        var sample = observer.Sample(world);

        var root = System.Text.Json.Nodes.JsonNode.Parse(CivilizationReport.ToJson("x.json", [new SeedRun(1, [sample]), new SeedRun(2, [])]))!;

        Assert.Equal(0, root["summary"]!["years"]!.GetValue<int>()); // menor série: a seed vazia
        Assert.Equal(20, root["summary"]!["final"]!["population_materialized"]!["mean"]!.GetValue<double>(), 9); // só a seed com anos entra
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -3)]
    public void The_report_command_rejects_non_positive_seeds_and_years(int seeds, int years)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CivilizationReportCommand.BuildJson(CivilizationReportTests.ScenarioPath(), 1, seeds, years));
    }
}
