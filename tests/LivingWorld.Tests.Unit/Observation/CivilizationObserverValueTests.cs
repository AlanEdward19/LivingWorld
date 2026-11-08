using System.Text.Json.Nodes;
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
