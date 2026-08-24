using LivingWorld.Domain;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Narrative;
using LivingWorld.Simulation;
using LivingWorld.Simulation.History;
using LivingWorld.Simulation.Narrative;

namespace LivingWorld.Tests.Narrative;

/// <summary>Fase 12, T10 (NARR-05..12 + critérios de sucesso da fase): fechamento de determinismo
/// e custo — llm-on/off preserva `eventIds`/cadeia de distorção (só a prosa varia), leitura nunca
/// altera o hash canônico, transmissão altera, e o sistema narrativo roda fora do tick diário num
/// cenário curto (poucos meses, nunca 10-100 anos — <see cref="NarrativeRendererTests"/> já prova
/// NARR-12 no nível de unidade de <see cref="NarrativeRenderer"/>; aqui o fechamento é ponta a
/// ponta, sobre o pipeline real de agregação/crença). Determinismo entre dois processos não se
/// aplica aqui: nenhum código desta fase itera <c>Dictionary</c>/<c>HashSet</c> para produzir
/// efeito no mundo (<see cref="ChronicleGenerationSystem"/> só expõe seu dicionário via
/// <c>.Values</c> fora do hash canônico), então o risco que <c>DeterminismTwoProcessTests</c>
/// cobre (hash de string randomizado por processo) já está coberto pelos digests de Fase 10
/// (<c>HistoryDistortionDigest</c>) que este pipeline só consome por <c>FactId</c>/<c>ReportId</c>.</summary>
public class NarrativeScenarioTests
{
    private sealed class ScriptedProvider(Func<LlmContext, CancellationToken, Task<LlmResponse>> behavior) : ILlmProvider
    {
        public Task<LlmResponse> GetResponseAsync(LlmContext context, CancellationToken cancellationToken = default) =>
            behavior(context, cancellationToken);
    }

    private static City EnsureCity(WorldState world, CityId cityId)
    {
        if (world.FindCity(cityId) is { } existing)
            return existing;

        var city = new City(cityId, ScenarioRunner.DefaultVillageLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        return city;
    }

    private static (WorldState world, Fact fact, City city, Npc npc) BuildScenario(ulong seed = 7)
    {
        var (world, _) = ScenarioRunner.Create(seed, historyRules: HistoryRules.Default);
        var npc = world.Npcs[0];
        var city = EnsureCity(world, npc.City);
        var fact = new Fact(new FactId(1), 5, WorldEventKind.Marriage, [npc.Id, new NpcId(2)], city.Id, 0.8, "1|2|cause");
        world.AddFact(fact);
        var report = new ReportState(
            world.NextReportIdAndAdvance(), fact.Id, city.Id, TransmissionMediumType.OralTradition,
            HopCount: 2, Weight: fact.Significance, CreatedAtTick: 10, LastHopTick: 10);
        world.RegisterReport(report);
        CanonSlotManager.Admit(city, report, HistoryRules.Default, nowTick: 20);
        return (world, fact, city, npc);
    }

    // --- NARR-12: llm-on/off preserva eventIds/estrutura de claims ---

    [Fact]
    public async Task Chronicle_pipeline_with_llm_on_and_off_yields_identical_eventIds_only_prose_may_differ()
    {
        var (world, fact, city, _) = BuildScenario();
        var topFacts = WindowedHistoryAggregator.TopFacts(world, city.Id, periodStartTick: 0, periodEndTick: 100, topK: 5);
        var claims = topFacts.Select(f => new NarrativeClaim($"{f.Kind} (evento {f.Id.Value}): {f.Payload}", (IReadOnlyList<long>)[f.Id.Value])).ToList();
        var draft = new NarrativeDraft(city.Id, 0, 100, claims);
        var provider = new ScriptedProvider((_, _) =>
            Task.FromResult(new LlmResponse($"Recorda-se o evento de {fact.Kind}.", "neutral", "none", [], [])));

        var withoutLlm = await NarrativeRenderer.RenderAsync(new NarrativeId(1), NarrativeType.Chronicle, draft, llmProvider: null);
        var withLlm = await NarrativeRenderer.RenderAsync(new NarrativeId(1), NarrativeType.Chronicle, draft, provider);

        Assert.NotEmpty(withoutLlm.Claims);
        Assert.Equal(withoutLlm.Claims.Select(c => c.EventIds), withLlm.Claims.Select(c => c.EventIds));
        Assert.Equal(withoutLlm.Claims.Select(c => c.Text), withLlm.Claims.Select(c => c.Text));
    }

    // --- NARR-09..11: mesma seed e mesmo mundo reproduzem a mesma estrutura narrativa ---

    [Fact]
    public void Same_seed_and_facts_produce_identical_chronicle_biography_and_belief_distance_across_independent_worlds()
    {
        var (worldA, factA, cityA, npcA) = BuildScenario(seed: 11);
        var (worldB, factB, cityB, npcB) = BuildScenario(seed: 11);

        var chronicleA = new ChronicleGenerationSystem().GenerateChronicle(worldA, cityA.Id, 0, 100);
        var chronicleB = new ChronicleGenerationSystem().GenerateChronicle(worldB, cityB.Id, 0, 100);
        Assert.Equal(chronicleA.Prose, chronicleB.Prose);
        Assert.Equal(chronicleA.Claims.Select(c => c.EventIds), chronicleB.Claims.Select(c => c.EventIds));

        var biographyA = NpcBiographyQuery.Timeline(worldA, npcA.Id).Value!;
        var biographyB = NpcBiographyQuery.Timeline(worldB, npcB.Id).Value!;
        Assert.Equal(biographyA.Select(f => f.Id.Value), biographyB.Select(f => f.Id.Value));

        var beliefA = HistoryBeliefQuery.BeliefOf(worldA, cityA.Id, factA.Id).Value!;
        var beliefB = HistoryBeliefQuery.BeliefOf(worldB, cityB.Id, factB.Id).Value!;
        Assert.Equal(beliefA.DistortedMagnitude, beliefB.DistortedMagnitude);
        Assert.Equal(beliefA.DistanceFromFact, beliefB.DistanceFromFact);
    }

    // --- Edge case (spec.md): leitura não altera hash; transmissão altera ---

    [Fact]
    public void Reading_chronicles_biographies_and_beliefs_never_changes_the_worlds_canonical_hash()
    {
        var (world, fact, city, npc) = BuildScenario();
        string hashBefore = WorldSnapshot.CanonicalHash(world);

        new ChronicleGenerationSystem().GenerateChronicle(world, city.Id, 0, 100);
        NpcBiographyQuery.Timeline(world, npc.Id);
        HistoryBeliefQuery.BeliefOf(world, city.Id, fact.Id);
        // limiar impossível (>1.0) força reprovação: exposição pura, sem mutação de memória.
        BeliefAssimilationService.Assimilate(world, npc.Id, fact.Id, tick: 30, confidenceThreshold: 1.1);

        string hashAfter = WorldSnapshot.CanonicalHash(world);
        Assert.Equal(hashBefore, hashAfter);
    }

    [Fact]
    public void Registering_a_new_report_transmission_changes_the_worlds_canonical_hash()
    {
        var (world, fact, city, _) = BuildScenario();
        string hashBefore = WorldSnapshot.CanonicalHash(world);

        var newHop = new ReportState(
            world.NextReportIdAndAdvance(), fact.Id, city.Id, TransmissionMediumType.Song,
            HopCount: 0, Weight: fact.Significance, CreatedAtTick: 30, LastHopTick: 30);
        world.RegisterReport(newHop);

        string hashAfter = WorldSnapshot.CanonicalHash(world);
        Assert.NotEqual(hashBefore, hashAfter);
    }

    [Fact]
    public void Assimilating_a_belief_above_the_confidence_threshold_changes_the_worlds_canonical_hash()
    {
        var (world, fact, _, npc) = BuildScenario();
        string hashBefore = WorldSnapshot.CanonicalHash(world);

        // limiar 0 sempre aceita (confiança nunca é negativa) — grava memória semântica
        // canônica (importância 100 >= limiar default 50 de canonicidade).
        var outcome = BeliefAssimilationService.Assimilate(world, npc.Id, fact.Id, tick: 30, confidenceThreshold: 0.0);

        string hashAfter = WorldSnapshot.CanonicalHash(world);
        Assert.True(outcome.Accepted);
        Assert.NotEqual(hashBefore, hashAfter);
    }

    // --- Custo/scheduling: sistema narrativo fora do tick diário, cenário curto ---

    [Fact]
    public void Chronicle_system_publishes_only_at_month_boundaries_across_a_short_multi_month_run_never_daily()
    {
        var (world, _) = ScenarioRunner.Create(1, initialPopulation: 0, historyRules: HistoryRules.Default);
        var city = world.ActiveCities().Single().Id;
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [], city, 0.7, "relevant"));
        var system = new ChronicleGenerationSystem();
        var clock = new WorldClock([system]);
        long month = world.Calendar.HoursPerMonth;

        // Cenário curto (3 meses, não 10-100 anos): fecha três meses e confirma que só os três
        // fecham-de-mês publicaram — nenhum tick diário intermediário adicionou nada.
        for (int m = 1; m <= 3; m++)
        {
            clock.Run(world, month - 1);
            Assert.Equal(m - 1, system.Chronicles.Count);

            clock.Tick(world);
            Assert.Equal(m, system.Chronicles.Count);
        }
    }
}
