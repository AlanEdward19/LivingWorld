using LivingWorld.Domain;
using LivingWorld.Domain.Llm;
using LivingWorld.Simulation;
using LivingWorld.Simulation.History;
using LivingWorld.Simulation.Narrative;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LivingWorld.Tests.Narrative;

/// <summary>Fase 12, T8 (NARR-13..15, story "Crença separada de verdade"): limiar de confiança na
/// assimilação de relato em memória semântica.</summary>
public class BeliefAssimilationServiceTests
{
    private static HistoryRules DistortingRules => HistoryRules.Create(
        enabled: true,
        skeletonSignificanceThreshold: 0.5,
        canonSizePerCommunity: 10,
        mediumFidelityByType: new Dictionary<TransmissionMediumType, MediumFidelity>
        {
            [TransmissionMediumType.OralTradition] = new(1.0, 10, DeathConditionType.Decay),
        },
        operatorProbability: HistoryRules.Default.OperatorProbability,
        importanceWeight: 1, transmissibilityWeight: 0, recencyWeight: 0).Value!;

    private static City EnsureCity(WorldState world, CityId cityId)
    {
        if (world.FindCity(cityId) is { } existing)
            return existing;

        var city = new City(cityId, ScenarioRunner.DefaultVillageLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        return city;
    }

    private static (WorldState world, Fact fact, City city, Npc listener) SeedHeardReport(int hopCount = 2)
    {
        var (world, _) = ScenarioRunner.Create(3, historyRules: DistortingRules);
        var listener = world.Npcs[0];
        var city = EnsureCity(world, listener.City);

        var fact = new Fact(new FactId(1), 5, WorldEventKind.Marriage, [listener.Id, new NpcId(2)], city.Id, 0.8, "1|2|cause");
        world.AddFact(fact);

        var report = new ReportState(
            world.NextReportIdAndAdvance(), fact.Id, city.Id, TransmissionMediumType.OralTradition,
            HopCount: hopCount, Weight: fact.Significance, CreatedAtTick: 10, LastHopTick: 10);
        world.RegisterReport(report);
        CanonSlotManager.Admit(city, report, DistortingRules, nowTick: 20);

        return (world, fact, city, listener);
    }

    [Fact]
    public void Assimilate_below_confidence_threshold_registers_exposure_without_mutating_semantic_memory()
    {
        // NARR-13 + edge case (spec.md): relato não aceito por confiança não muta memória
        // semântica do ouvinte; a exposição fica só no retorno (nunca silenciosa).
        var (world, fact, _, listener) = SeedHeardReport();
        int memoryCountBefore = world.CanonicalMemories.Count + world.VolatileMemories.Count;

        // Confiança nunca ultrapassa 1.0 (clamp), então um limiar > 1 força reprovação
        // deterministicamente, sem depender da magnitude de distorção sorteada pelo RNG.
        var outcome = BeliefAssimilationService.Assimilate(world, listener.Id, fact.Id, tick: 30, confidenceThreshold: 1.1);

        Assert.False(outcome.Accepted);
        Assert.Equal(BeliefAssimilationService.BelowThresholdReason, outcome.Reason);
        Assert.Equal(memoryCountBefore, world.CanonicalMemories.Count + world.VolatileMemories.Count);
    }

    [Fact]
    public void Assimilate_above_confidence_threshold_persists_belief_without_altering_the_canonical_fact()
    {
        // NARR-14: relato aceito entra na memória semântica do ouvinte, sem alterar o fato
        // canônico de origem.
        var (world, fact, _, listener) = SeedHeardReport();
        var factBefore = world.FindFact(fact.Id);

        // Confiança nunca é negativa (clamp), então limiar 0 sempre aceita.
        var outcome = BeliefAssimilationService.Assimilate(world, listener.Id, fact.Id, tick: 30, confidenceThreshold: 0.0);

        Assert.True(outcome.Accepted);
        Assert.Equal(BeliefAssimilationService.AssimilatedReason, outcome.Reason);

        var belief = HistoryBeliefQuery.BeliefOf(world, listener.Id, fact.Id).Value!;
        var allMemories = world.CanonicalMemories.Concat(world.VolatileMemories).ToList();
        var inserted = Assert.Single(allMemories, m => m.OwnerId == listener.Id && m.Category == MemoryCategory.Semantic);
        Assert.Equal(belief.MoralizedNarrativeSeed, inserted.Content);

        var factAfter = world.FindFact(fact.Id);
        Assert.Equal(factBefore, factAfter);
        Assert.Equal(fact.Significance, factAfter!.Significance);
        Assert.Equal(fact.Payload, factAfter.Payload);
    }

    [Fact]
    public void Source_never_references_HistoryTruthQuery_only_HistoryBeliefQuery()
    {
        // NARR-15: consultas de jogo/motor permanecem separadas — este serviço de crença nunca
        // referencia a consulta de verdade canônica.
        string repoRoot = FindRepoRoot();
        string file = Path.Combine(repoRoot, "src", "LivingWorld.Simulation", "Narrative", "BeliefAssimilationService.cs");
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetCompilationUnitRoot();

        bool referencesTruthQuery = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(id => id.Identifier.Text == nameof(HistoryTruthQuery));
        bool referencesBeliefQuery = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(id => id.Identifier.Text == nameof(HistoryBeliefQuery));

        Assert.False(referencesTruthQuery);
        Assert.True(referencesBeliefQuery);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
