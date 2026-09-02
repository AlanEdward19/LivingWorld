using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Narrative;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Narrative;

/// <summary>Fase 12, T6: <see cref="ChronicleGenerationSystem"/> (NARR-05..08 + edge case de
/// concorrência/idempotência e "nunca no tick diário").</summary>
public class ChronicleGenerationSystemTests
{
    [Fact]
    public void Frequency_is_monthly_never_daily()
    {
        // spec.md Edge Cases: "sistema narrativo SHALL não executar no tick diário" — a garantia
        // vem da própria frequência declarada, que é quem o WorldClock consulta para decidir se
        // chama Tick() num dado tick.
        var system = new ChronicleGenerationSystem();

        Assert.Equal(TickFrequency.Monthly, system.Frequency);
    }

    [Fact]
    public void GenerateChronicle_references_the_most_significant_fact_of_the_window()
    {
        // NARR-05/06: agrega e ordena por significância antes de renderizar; a crônica publicada
        // referencia pelo menos um eventId entre os K mais significativos da janela.
        var (world, _) = ScenarioRunner.Create(1, initialPopulation: 0, historyRules: HistoryRules.Default);
        var city = world.ActiveCities().Single().Id;
        var lowFact = new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.CourtshipRejected, [], city, 0.2, "low");
        var highFact = new Fact(world.NextFactIdAndAdvance(), 20, WorldEventKind.Death, [], city, 0.9, "high");
        world.AddFact(lowFact);
        world.AddFact(highFact);
        var system = new ChronicleGenerationSystem();

        var doc = system.GenerateChronicle(world, city, periodStartTick: 0, periodEndTick: 100, topK: 1);

        Assert.Single(doc.Claims);
        Assert.Equal(new long[] { highFact.Id.Value }, doc.Claims[0].EventIds);
    }

    [Fact]
    public void GenerateChronicle_never_falls_back_to_generic_filler_when_relevant_facts_exist()
    {
        // NARR-07: quando o agregador encontra fatos relevantes, o sistema reprova saída
        // genérica de preenchimento sem citação relevante.
        var (world, _) = ScenarioRunner.Create(1, initialPopulation: 0, historyRules: HistoryRules.Default);
        var city = new CityId(Guid.NewGuid());
        world.AddCity(MakeCity(city));
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [], city, 0.7, "relevant"));
        var system = new ChronicleGenerationSystem();

        var doc = system.GenerateChronicle(world, city, periodStartTick: 0, periodEndTick: 100);

        Assert.NotEqual("sem registros ancorados para este período.", doc.Prose);
        Assert.NotEmpty(doc.Claims);
    }

    [Fact]
    public void GenerateChronicle_without_llm_is_deterministic_across_independent_runs()
    {
        // NARR-08: quando o provider de LLM está indisponível (o padrão deste job, que nunca liga
        // um), a crônica é publicada via template determinístico com o mesmo conjunto de claims.
        var (world, _) = ScenarioRunner.Create(1, initialPopulation: 0, historyRules: HistoryRules.Default);
        var city = new CityId(Guid.NewGuid());
        world.AddCity(MakeCity(city));
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [], city, 0.7, "relevant"));

        var doc1 = new ChronicleGenerationSystem().GenerateChronicle(world, city, 0, 100);
        var doc2 = new ChronicleGenerationSystem().GenerateChronicle(world, city, 0, 100);

        Assert.Equal(doc1.Prose, doc2.Prose);
        Assert.Equal(doc1.Claims[0].EventIds, doc2.Claims[0].EventIds);
    }

    [Fact]
    public void GenerateChronicle_is_idempotent_when_the_same_window_key_is_processed_twice()
    {
        // Edge case (spec.md): dois jobs concorrentes processando a mesma janela SHALL garantir
        // idempotência da publicação por chave (local, periodStart, periodEnd) — a segunda
        // chamada devolve o documento já publicado, sem reprocessar nem duplicar.
        var (world, _) = ScenarioRunner.Create(1, initialPopulation: 0, historyRules: HistoryRules.Default);
        var city = new CityId(Guid.NewGuid());
        world.AddCity(MakeCity(city));
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [], city, 0.7, "first"));
        var system = new ChronicleGenerationSystem();

        var first = system.GenerateChronicle(world, city, periodStartTick: 0, periodEndTick: 100);
        // Fato novo surge "depois" (ex.: outro job já teria processado a janela) — o reprocesso
        // da mesma chave não pode nem duplicar a publicação nem incorporar o fato novo.
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 50, WorldEventKind.Marriage, [], city, 0.9, "later"));
        var second = system.GenerateChronicle(world, city, periodStartTick: 0, periodEndTick: 100);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Prose, second.Prose);
        Assert.Single(system.Chronicles);
    }

    [Fact]
    public void Tick_publishes_one_chronicle_per_city_only_at_the_month_boundary_never_at_daily_ticks()
    {
        // Fecha o laço end-to-end: registrado num WorldClock real, o job só publica quando o mês
        // fecha (nunca em fronteira diária), uma crônica por cidade conhecida (NARR-05..08 +
        // "nunca no tick diário").
        var (world, _) = ScenarioRunner.Create(1, initialPopulation: 0, historyRules: HistoryRules.Default);
        var city = world.ActiveCities().Single().Id;
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [], city, 0.7, "relevant"));
        var system = new ChronicleGenerationSystem();
        var clock = new WorldClock([system]);

        clock.Run(world, world.Calendar.HoursPerMonth - 1);
        Assert.Empty(system.Chronicles);

        clock.Tick(world); // fecha o mês (HoursPerMonth-ésimo tick)
        Assert.Equal(world.ActiveCities().Count(), system.Chronicles.Count);
        Assert.Single(system.Chronicles, chronicle => chronicle.Prose.Contains("relevant", StringComparison.Ordinal));
    }

    private static City MakeCity(CityId id) =>
        new(id, new CellCoord(0, 0), foundedAtTick: 0, foundedFromCityId: null, AggregatePopulationPool.Empty);
}
