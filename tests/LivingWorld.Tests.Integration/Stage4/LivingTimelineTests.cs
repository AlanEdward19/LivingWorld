using LivingWorld.Api.Visual.Catalogs;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History.Books;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Integration.Stage4;

public class LivingTimelineTests
{
    private static HistoryRules ForcedMoralizationRules => HistoryRules.Create(
        enabled: true, skeletonSignificanceThreshold: 0.5, canonSizePerCommunity: 10,
        mediumFidelityByType: new Dictionary<TransmissionMediumType, MediumFidelity>
        {
            [TransmissionMediumType.OralTradition] = new(1.0, 10, DeathConditionType.Decay),
        },
        operatorProbability: new Dictionary<DistortionOperator, double> { [DistortionOperator.Moralization] = 1.0 },
        importanceWeight: 1, transmissibilityWeight: 0, recencyWeight: 0).Value!;

    [Fact]
    public void Every_world_event_kind_has_exactly_one_audience_label()
    {
        Assert.Equal(
            Enum.GetValues<WorldEventKind>().Order(),
            LivingEventPresentationCatalog.MappedKinds.Order());
    }

    [Fact]
    public void Every_capability_mapped_event_renders_a_non_technical_label()
    {
        var mapped = LivingWorldCapabilityCatalog.All.SelectMany(capability => capability.Events).Distinct();

        foreach (var kind in mapped)
        {
            var label = LivingEventPresentationCatalog.Describe(kind);
            Assert.False(string.IsNullOrWhiteSpace(label));
            Assert.NotEqual(kind.ToString(), label);
        }
    }

    [Fact]
    public void Visual_timeline_replaces_raw_payload_with_the_public_label()
    {
        const string secret = "truth-only|npc-4|cause";
        var (world, _) = ScenarioRunner.Create(seed: 62, initialPopulation: 1);
        var evt = new WorldEvent(12, WorldEventKind.Death, secret);

        var visual = Assert.Single(LivingScopeProjector.Build(
            world, new VisualScope(VisualScopeKind.World, ""), [evt]).Events);

        Assert.Equal("Um habitante faleceu", visual.Label);
        Assert.DoesNotContain(secret, visual.Label);
        Assert.DoesNotContain("Payload", typeof(NotableVisualEvent).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public void Npc_inspection_exposes_distorted_belief_and_never_truth_payload()
    {
        var rules = ForcedMoralizationRules;
        var (world, _) = ScenarioRunner.Create(seed: 63, initialPopulation: 1, historyRules: rules);
        var npc = world.Npcs[0];
        var city = new City(npc.City, npc.CurrentLocation, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        const string truth = "truth-only-payload";
        var fact = new Fact(new FactId(1), 5, WorldEventKind.Marriage, [npc.Id], city.Id, 0.8, truth);
        world.AddFact(fact);
        var report = new ReportState(world.NextReportIdAndAdvance(), fact.Id, city.Id,
            TransmissionMediumType.OralTradition, 1, fact.Significance, 10, 10);
        world.RegisterReport(report);
        CanonSlotManager.Admit(city, report, rules, 20);

        var dto = NpcInspectionQuery.Inspect(world, npc.Id).Value!;

        Assert.Single(dto.Beliefs);
        Assert.DoesNotContain(dto.Beliefs, belief => belief.Contains(truth, StringComparison.Ordinal));
    }

    [Fact]
    public void Fact_without_a_report_in_the_npcs_community_is_not_visible_knowledge()
    {
        var rules = ForcedMoralizationRules;
        var (world, _) = ScenarioRunner.Create(seed: 64, initialPopulation: 1, historyRules: rules);
        var npc = world.Npcs[0];
        var fact = new Fact(new FactId(1), 5, WorldEventKind.Marriage, [npc.Id], npc.City, 0.8, "unheard-secret");
        world.AddFact(fact);

        var dto = NpcInspectionQuery.Inspect(world, npc.Id).Value!;

        Assert.Empty(dto.Beliefs);
    }

    [Fact]
    public void Unknown_future_event_uses_a_readable_fallback_without_exposing_a_payload()
    {
        var label = LivingEventPresentationCatalog.Describe((WorldEventKind)999);

        Assert.Equal("Um acontecimento foi registrado", label);
        Assert.DoesNotContain("999", label);
    }
}
