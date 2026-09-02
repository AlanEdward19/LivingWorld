using LivingWorld.Domain.Cities;
using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History.Books;
using LivingWorld.Simulation.History.Queries;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.History.Queries;

/// <summary>Fase 11, T4 (LLM-05/06), story "Contexto por crença e memória do NPC": <see
/// cref="NpcBeliefQuery"/> só agrega o cânone de crença da cidade do NPC (<see
/// cref="HistoryBeliefQuery"/>) — nunca <see cref="HistoryTruthQuery"/>/<see
/// cref="Fact.Payload"/> bruto, e nunca um fato que não virou relato no cânone daquela
/// cidade.</summary>
public class NpcBeliefQueryTests
{
    /// <summary>Moralization forçada a 100% por hop: garante que <see
    /// cref="DistortedReport.MoralizedNarrativeSeed"/> saia sempre preenchido (nunca vazio),
    /// então o teste consegue provar "existe versão distorcida" sem depender de sorte de RNG.</summary>
    private static HistoryRules ForcedMoralizationRules => HistoryRules.Create(
        enabled: true,
        skeletonSignificanceThreshold: 0.5,
        canonSizePerCommunity: 10,
        mediumFidelityByType: new Dictionary<TransmissionMediumType, MediumFidelity>
        {
            [TransmissionMediumType.OralTradition] = new(1.0, 10, DeathConditionType.Decay),
        },
        operatorProbability: new Dictionary<DistortionOperator, double> { [DistortionOperator.Moralization] = 1.0 },
        importanceWeight: 1,
        transmissibilityWeight: 0,
        recencyWeight: 0).Value!;

    private static City EnsureCity(WorldState world, CityId cityId, AggregatePopulationPool? pool = null)
    {
        if (world.FindCity(cityId) is { } existing) return existing;
        var city = new City(cityId, ScenarioRunner.DefaultVillageLocation, 0, null, pool ?? AggregatePopulationPool.Empty);
        world.AddCity(city);
        return city;
    }

    private static ReportState AdmitReport(WorldState world, City city, Fact fact, HistoryRules rules, int hopCount)
    {
        var report = new ReportState(
            world.NextReportIdAndAdvance(), fact.Id, city.Id, TransmissionMediumType.OralTradition,
            HopCount: hopCount, Weight: fact.Significance, CreatedAtTick: 10, LastHopTick: 10);
        world.RegisterReport(report);
        CanonSlotManager.Admit(city, report, rules, nowTick: 20);
        return report;
    }

    [Fact]
    public void BeliefsOf_returns_the_distorted_narrative_and_never_the_raw_truth_payload()
    {
        var rules = ForcedMoralizationRules;
        var (world, _) = ScenarioRunner.Create(3, historyRules: rules);
        var npc = world.Npcs[0];
        var city = EnsureCity(world, npc.City);

        var fact = new Fact(new FactId(1), 5, WorldEventKind.Marriage, [npc.Id, new NpcId(2)], city.Id, 0.8, "1|2|cause");
        world.AddFact(fact);
        AdmitReport(world, city, fact, rules, hopCount: 1);

        var beliefs = NpcBeliefQuery.BeliefsOf(world, npc.Id);

        Assert.Single(beliefs);
        Assert.NotEqual("", beliefs[0]);
        Assert.DoesNotContain(beliefs, b => b == fact.Payload || b.Contains(fact.Payload));
    }

    [Fact]
    public void BeliefsOf_never_leaks_a_secret_only_reported_in_another_npcs_community()
    {
        var rules = ForcedMoralizationRules;
        var (world, _) = ScenarioRunner.Create(3, historyRules: rules);

        var npcA = world.Npcs[0];
        var cityA = EnsureCity(world, npcA.City);

        var npcB = new NpcId(500);
        var cityBId = new CityId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var cityB = EnsureCity(world, cityBId);

        // Fato público: participa dos dois, relatado no cânone de A — controle de que
        // BeliefsOf(A) não está simplesmente sempre vazio.
        var publicFact = new Fact(new FactId(1), 5, WorldEventKind.Marriage, [npcA.Id, new NpcId(2)], cityA.Id, 0.8, "public");
        world.AddFact(publicFact);
        AdmitReport(world, cityA, publicFact, rules, hopCount: 1);

        // Segredo: só participante é NpcB, relatado apenas no cânone da cidade de NpcB —
        // nunca chega ao cânone de A.
        var secretFact = new Fact(new FactId(2), 6, WorldEventKind.Marriage, [npcB], cityB.Id, 0.8, "secret");
        world.AddFact(secretFact);
        AdmitReport(world, cityB, secretFact, rules, hopCount: 1);

        var beliefsForA = NpcBeliefQuery.BeliefsOf(world, npcA.Id);

        Assert.Single(beliefsForA);
        Assert.Equal(cityA.CanonSlots.Count, beliefsForA.Count);
    }

    [Fact]
    public void BeliefsOf_returns_empty_for_unknown_npc()
    {
        var (world, _) = ScenarioRunner.Create(3, historyRules: HistoryRules.Disabled);

        var beliefs = NpcBeliefQuery.BeliefsOf(world, new NpcId(999_999));

        Assert.Empty(beliefs);
    }
}
