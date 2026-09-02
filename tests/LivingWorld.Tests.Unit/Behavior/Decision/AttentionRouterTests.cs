using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Behavior;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 16.3 T27 (COH-43): AttentionRouter escopa NPCs relevantes a um evento.</summary>
public class AttentionRouterTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static WorldState EmptyWorld(ulong seed = 1) => new(
        Calendar, seed, ScenarioRunner.DefaultMap(seed),
        ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
        ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
        ScenarioRunner.DefaultLifeStageRules);

    private static Npc MakeNpc(long id, CellCoord loc, HouseholdId? household = null) => new(
        new NpcId(id), $"npc-{id}", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30),
        new CultureId(1), loc, null, null, household, 100, Neutral, ProfessionType.None, loc);

    private static WorldEvent Evt(string payload, WorldEventKind kind = WorldEventKind.ResourceLost) =>
        new(Tick: 1, kind, payload, EventId: 1, CauseEventId: null, SourceSystem: "test");

    [Fact]
    public void Low_magnitude_price_change_does_not_route_whole_city()
    {
        var world = EmptyWorld(10);
        var origin = new CellCoord(1, 1);
        // 20 NPCs espalhados — cidade "cheia"
        for (long i = 1; i <= 20; i++)
        {
            var cell = new CellCoord((int)(i % 5), (int)(i / 5));
            world.AddNpc(MakeNpc(i, cell));
        }

        // Só o NPC 3 tem intent Buy ativo
        world.FindNpc(new NpcId(3))!.SetIntent(ActionType.Buy, tick: 1);

        var evt = Evt($"{AttentionRouter.PriceChangePrefix}0.01|{origin.X}|{origin.Y}");
        var routed = AttentionRouter.RouteRelevantNpcs(world, evt, AttentionRules.Default);

        Assert.Contains(new NpcId(3), routed);
        Assert.True(routed.Count < world.Npcs.Count / 2,
            $"baixa magnitude não deve acordar a cidade: routed={routed.Count}, pop={world.Npcs.Count}");
        Assert.Single(routed);
    }

    [Fact]
    public void High_magnitude_price_change_routes_nearby_and_intent_dependents()
    {
        var world = EmptyWorld(11);
        var origin = new CellCoord(2, 2);
        world.AddNpc(MakeNpc(1, origin)); // perto
        world.AddNpc(MakeNpc(2, new CellCoord(20, 20))); // longe, sem intent
        var hungry = MakeNpc(3, new CellCoord(15, 15));
        hungry.SetIntent(ActionType.Eat, tick: 1);
        world.AddNpc(hungry);

        var evt = Evt($"{AttentionRouter.PriceChangePrefix}0.20|{origin.X}|{origin.Y}");
        var routed = AttentionRouter.RouteRelevantNpcs(world, evt, AttentionRules.Default);

        Assert.Contains(new NpcId(1), routed);
        Assert.Contains(new NpcId(3), routed);
        Assert.DoesNotContain(new NpcId(2), routed);
    }

    [Fact]
    public void Household_event_routes_members()
    {
        var world = EmptyWorld(12);
        var hid = new HouseholdId(7);
        var a = MakeNpc(1, new CellCoord(0, 0), hid);
        var b = MakeNpc(2, new CellCoord(1, 0), hid);
        var outsider = MakeNpc(3, new CellCoord(0, 0));
        var household = new Household(hid, new CellCoord(0, 0), a.Id, [a.Id, b.Id]);
        world.AddHousehold(household);
        world.AddNpc(a);
        world.AddNpc(b);
        world.AddNpc(outsider);

        var evt = Evt($"{AttentionRouter.HouseholdPrefix}{hid.Value}");
        var routed = AttentionRouter.RouteRelevantNpcs(world, evt, AttentionRules.Default);

        Assert.Contains(a.Id, routed);
        Assert.Contains(b.Id, routed);
        Assert.DoesNotContain(outsider.Id, routed);
    }

    [Fact]
    public void Npc_event_routes_related_above_relationship_threshold()
    {
        var world = EmptyWorld(13);
        var subject = MakeNpc(1, new CellCoord(0, 0));
        var friend = MakeNpc(2, new CellCoord(1, 0));
        var stranger = MakeNpc(3, new CellCoord(2, 0));
        world.AddNpc(subject);
        world.AddNpc(friend);
        world.AddNpc(stranger);

        var strong = world.GetOrCreateRelationship(new RelationshipKey(subject.Id, friend.Id), now: 1);
        for (int i = 0; i < 20; i++)
            strong.ApplyEvent(RelationshipEventType.Cohabitation, ScenarioRunner.DefaultFamilyRules);

        // stranger sem relação → abaixo do limiar (não entra)

        var evt = Evt($"{AttentionRouter.NpcPrefix}{subject.Id.Value}");
        var routed = AttentionRouter.RouteRelevantNpcs(world, evt, AttentionRules.Default);

        Assert.Contains(subject.Id, routed);
        Assert.Contains(friend.Id, routed);
        Assert.DoesNotContain(stranger.Id, routed);
    }

    [Fact]
    public void Threat_routes_npcs_inside_threat_radius()
    {
        var world = EmptyWorld(14);
        var epicenter = new CellCoord(5, 5);
        world.AddNpc(MakeNpc(1, epicenter));
        world.AddNpc(MakeNpc(2, new CellCoord(5 + AttentionRules.DefaultThreatRadiusCells, 5)));
        world.AddNpc(MakeNpc(3, new CellCoord(5 + AttentionRules.DefaultThreatRadiusCells + 1, 5)));

        var evt = Evt($"{AttentionRouter.ThreatPrefix}{epicenter.X}|{epicenter.Y}", WorldEventKind.CombatResolved);
        var routed = AttentionRouter.RouteRelevantNpcs(world, evt, AttentionRules.Default);

        Assert.Contains(new NpcId(1), routed);
        Assert.Contains(new NpcId(2), routed);
        Assert.DoesNotContain(new NpcId(3), routed);
    }

    [Fact]
    public void Intent_dependent_npc_is_routed_on_low_magnitude_economic_event()
    {
        var world = EmptyWorld(15);
        var dependent = MakeNpc(1, new CellCoord(0, 0));
        dependent.SetIntent(ActionType.Buy, tick: 1, target: "food");
        world.AddNpc(dependent);
        world.AddNpc(MakeNpc(2, new CellCoord(0, 0))); // sem intent

        var evt = Evt($"{AttentionRouter.PriceChangePrefix}0.01|0|0");
        var routed = AttentionRouter.RouteRelevantNpcs(world, evt, AttentionRules.Default);

        Assert.Contains(dependent.Id, routed);
        Assert.DoesNotContain(new NpcId(2), routed);
    }

    [Fact]
    public void Disabled_rules_route_nobody()
    {
        var world = EmptyWorld(16);
        world.AddNpc(MakeNpc(1, new CellCoord(0, 0)));
        var evt = Evt($"{AttentionRouter.ThreatPrefix}0|0");

        var routed = AttentionRouter.RouteRelevantNpcs(world, evt, AttentionRules.Disabled);

        Assert.Empty(routed);
    }

    [Fact]
    public void RouteRelevantNpcs_is_deterministic_for_same_seed_and_event()
    {
        static List<long> Run()
        {
            var world = EmptyWorld(17);
            for (long i = 1; i <= 5; i++)
                world.AddNpc(MakeNpc(i, new CellCoord((int)i, 0)));
            world.FindNpc(new NpcId(2))!.SetIntent(ActionType.Eat, 1);
            world.FindNpc(new NpcId(4))!.SetIntent(ActionType.Buy, 1);
            var evt = Evt($"{AttentionRouter.PriceChangePrefix}0.01|0|0");
            return AttentionRouter.RouteRelevantNpcs(world, evt, AttentionRules.Default)
                .Select(id => id.Value).OrderBy(v => v).ToList();
        }

        Assert.Equal(Run(), Run());
    }
}
