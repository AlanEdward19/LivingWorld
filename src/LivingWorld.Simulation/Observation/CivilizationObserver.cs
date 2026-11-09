using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Observatório de civilização (issue #112 fatia A): lê o <see cref="WorldState"/> ao fim de cada ano
/// e devolve uma <see cref="WorldYearSample"/>. Serviço puro de leitura, chamado fora do tick pelo CLI ou teste:
/// não é <c>ISimulationSystem</c>, não escreve no mundo e não emite evento (o hash canônico não muda). Nascimentos,
/// mortes e cidades fundidas vêm do <see cref="YearEventTally"/> (sink do ano); o resto vem do estado.</summary>
public sealed class CivilizationObserver
{
    private readonly YearEventTally _tally;
    private long _previousPopulation;
    private long _previousTick;
    private int _year;

    public CivilizationObserver(WorldState world, YearEventTally tally)
    {
        _tally = tally;
        _previousPopulation = world.ActiveCities().Sum(c => CityPopulation(world, c.Id));
        _previousTick = world.CurrentDate.TotalHours;
    }

    public WorldYearSample Sample(WorldState world)
    {
        var (tallies, merged) = _tally.Drain();
        var active = world.ActiveCities().OrderBy(c => c.Id.Value).ToList();
        var activeIds = active.Select(c => c.Id).ToHashSet();

        // Cidade fundida já saiu de ActiveCities: o que ela contou no ano vai para a cidade que a absorveu.
        var byCity = new Dictionary<CityId, CityTally>();
        foreach (var (id, tally) in tallies)
        {
            var target = id;
            while (!activeIds.Contains(target) && world.FindCity(target)?.MergedIntoCityId is { } next) target = next;
            if (!byCity.TryGetValue(target, out var sum)) byCity[target] = sum = new CityTally();
            sum.Births += tally.Births;
            sum.Deaths += tally.Deaths;
            foreach (var (cause, n) in tally.ByCause) sum.ByCause[cause] = sum.ByCause.GetValueOrDefault(cause) + n;
            for (int i = 0; i < tally.DeathsByBand.Count; i++)
            {
                while (sum.DeathsByBand.Count <= i) sum.DeathsByBand.Add(0);
                sum.DeathsByBand[i] += tally.DeathsByBand[i];
            }
        }

        var aliveByCity = world.Npcs.Where(n => n.IsAlive).GroupBy(n => n.City).ToDictionary(g => g.Key, g => g.ToList());
        var cities = new List<CitySample>();
        var all = new Group();
        foreach (var city in active)
        {
            var group = Group.Of(world, city, aliveByCity.GetValueOrDefault(city.Id) ?? [], byCity.GetValueOrDefault(city.Id), _tally.AgeBandYears);
            cities.Add(new CitySample(city.Id.ToString(), group.ToMetrics(_tally.AgeBandYears)));
            all.Add(group);
        }

        var worldMetrics = all.ToMetrics(_tally.AgeBandYears);
        long tick = world.CurrentDate.TotalHours;
        long population = all.Materialized + all.Pool;
        worldMetrics["technologies"] = world.Technologies.Count(t => t.AdoptedBy.Count > 0);
        worldMetrics["cities_active"] = active.Count;
        worldMetrics["cities_founded"] = world.Cities.Count(c => c.FoundedAtTick > _previousTick && c.FoundedAtTick <= tick);
        worldMetrics["cities_merged"] = merged;
        worldMetrics["population_delta"] = population - _previousPopulation;
        worldMetrics["net_migration"] = population - _previousPopulation - all.Births + all.Deaths;
        worldMetrics["population_unassigned"] = aliveByCity.Where(kv => !activeIds.Contains(kv.Key)).Sum(kv => kv.Value.Count);

        _previousPopulation = population;
        _previousTick = tick;
        return new WorldYearSample(++_year, tick, worldMetrics, cities);
    }

    private static long CityPopulation(WorldState world, CityId city) =>
        world.Npcs.LongCount(n => n.IsAlive && n.City == city) + (world.FindCity(city)?.AggregatePool.Count ?? 0);

    /// <summary>Somas brutas de um conjunto de cidades; razões e medianas saem delas, nunca da média das médias.</summary>
    private sealed class Group
    {
        public long Materialized, Pool, Births, Deaths, Treasury, Workshops, Buildings, Technologies, Adults, Unemployed;
        public readonly Dictionary<string, long> Causes = [];
        public readonly List<long> DeathsByBand = [];
        public readonly List<int> Ages = [];
        public readonly List<(double Value, double Weight)> Wealth = [];

        public static Group Of(WorldState world, City city, List<Domain.Population.Npc> alive, CityTally? tally, int bandYears)
        {
            var g = new Group { Materialized = alive.Count, Pool = city.AggregatePool.Count };
            foreach (var npc in alive)
            {
                int age = npc.AgeYears(world.CurrentDate);
                g.Ages.Add(age);
                g.Wealth.Add((npc.Wallet.Amount, 1));
                if (world.LifeStageRules.LifeStageOf(age) == LifeStage.Adult)
                {
                    g.Adults++;
                    if (npc.Employer is null) g.Unemployed++;
                }
            }

            if (city.AggregatePool.Count > 0)
                g.Wealth.Add(((double)city.AggregatePool.WealthSum / city.AggregatePool.Count, city.AggregatePool.Count));
            if (tally is not null)
            {
                g.Births = tally.Births;
                g.Deaths = tally.Deaths;
                foreach (var (cause, n) in tally.ByCause) g.Causes[cause] = n;
                g.DeathsByBand.AddRange(tally.DeathsByBand);
            }

            g.Treasury = city.Treasury.Amount;
            g.Workshops = world.Workplaces.Count(w => w.City == city.Id);
            g.Buildings = world.Buildings.Count(b => b.City == city.Id);
            g.Technologies = world.Technologies.Count(t => t.AdoptedBy.Contains(city.Id));
            return g;
        }

        public void Add(Group other)
        {
            Materialized += other.Materialized; Pool += other.Pool; Births += other.Births; Deaths += other.Deaths;
            Treasury += other.Treasury; Workshops += other.Workshops; Buildings += other.Buildings;
            Technologies += other.Technologies; Adults += other.Adults; Unemployed += other.Unemployed;
            foreach (var (cause, n) in other.Causes) Causes[cause] = Causes.GetValueOrDefault(cause) + n;
            for (int i = 0; i < other.DeathsByBand.Count; i++)
            {
                while (DeathsByBand.Count <= i) DeathsByBand.Add(0);
                DeathsByBand[i] += other.DeathsByBand[i];
            }

            Ages.AddRange(other.Ages);
            Wealth.AddRange(other.Wealth);
        }

        public Dictionary<string, double> ToMetrics(int bandYears)
        {
            var sortedAges = Ages.Order().ToList();
            double median = sortedAges.Count == 0 ? 0
                : sortedAges.Count % 2 == 1 ? sortedAges[sortedAges.Count / 2]
                : (sortedAges[sortedAges.Count / 2 - 1] + sortedAges[sortedAges.Count / 2]) / 2.0;
            return new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["population"] = Materialized + Pool,
                ["population_materialized"] = Materialized,
                ["population_pool"] = Pool,
                ["births"] = Births,
                ["deaths"] = Deaths,
                ["deaths_hunger_thirst"] = Causes.GetValueOrDefault("hunger_thirst"),
                ["deaths_disease"] = Causes.GetValueOrDefault("disease"),
                ["deaths_age"] = Causes.GetValueOrDefault("age"),
                ["deaths_other"] = Causes.GetValueOrDefault("other"),
                ["age_mean"] = Ages.Count == 0 ? 0 : (double)Ages.Sum(a => (long)a) / Ages.Count,
                ["age_median"] = median,
                ["life_expectancy_at_birth"] = LifeExpectancy(bandYears),
                ["gini"] = WealthInequality.Gini(Wealth),
                ["treasury"] = Treasury,
                ["unemployment_rate"] = Adults == 0 ? 0 : (double)Unemployed / Adults,
                ["workshops"] = Workshops,
                ["buildings"] = Buildings,
                ["technologies"] = Technologies,
            };
        }

        /// <summary>Tábua de período do ano: exposição da faixa = vivos no fim do ano + metade das mortes dela
        /// (quem morreu viveu em média meio ano). Só materializados têm idade; o pool fica fora (e não morre).</summary>
        private double LifeExpectancy(int bandYears)
        {
            int count = Math.Max(Ages.Count == 0 ? 0 : Ages.Max() / bandYears + 1, DeathsByBand.Count);
            if (count == 0 || Deaths == 0) return 0;

            var bands = new List<AgeBand>();
            for (int i = 0; i < count; i++)
            {
                long deaths = i < DeathsByBand.Count ? DeathsByBand[i] : 0;
                long alive = Ages.LongCount(a => a / bandYears == i);
                bands.Add(new AgeBand(bandYears, deaths, alive + deaths / 2.0));
            }

            return LifeTable.LifeExpectancyAtBirth(bands);
        }
    }
}
