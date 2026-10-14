using System.Text.Json;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>player-incarnation (Fase 25): jogador é NPC com controlador externo; sair é desaparecer.</summary>
public class PlayerIncarnationTests
{
    /// <summary>Guarda todos os eventos (o buffer comum só retém os mais recentes).</summary>
    private sealed class AllEvents : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static (WorldState World, WorldClock Clock, BufferingWorldEventSink Sink, TickContext Ctx) Village(ulong seed)
    {
        var (world, _) = ScenarioRunner.Create(seed);
        var sink = new BufferingWorldEventSink();
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        clock.Run(world, 96); // contratações acontecem nos primeiros dias
        sink.DrainAll();
        return (world, clock, sink, new TickContext(world, world.Rng, world.Scheduler, sink));
    }

    private static Npc Pick(WorldState world) =>
        world.Npcs.First(n => n.IsAlive && n.Employer is not null && n.Household is not null
            && world.LifeStageRules.LifeStageOf(n.AgeYears(world.CurrentDate)) == LifeStage.Adult);

    private static JsonObject NpcJson(Npc npc) =>
        JsonNode.Parse(JsonSerializer.Serialize(npc, LivingWorld.Simulation.Core.WorldSnapshot.SnapshotJsonOptions))!.AsObject();

    [Fact]
    public void Incarnating_a_free_living_npc_changes_only_the_controller()
    {
        var (world, _, _, ctx) = Village(1);
        var npc = Pick(world);
        var before = NpcJson(npc);
        Assert.False(before.ContainsKey("ControllerId")); // nulo não entra no hash: mundo antigo idêntico

        var result = PlayerCommands.Incarnate(world, ctx, npc.Id, "alan");

        Assert.True(result.IsSuccess);
        var after = NpcJson(npc);
        Assert.Equal("alan", after["ControllerId"]!.GetValue<string>());
        after.Remove("ControllerId");
        Assert.Equal(before.ToJsonString(), after.ToJsonString());
    }

    [Fact]
    public void Incarnating_a_controlled_npc_is_a_conflict_and_leaves_the_hash_alone()
    {
        var (world, _, _, ctx) = Village(2);
        var npc = Pick(world);
        Assert.True(PlayerCommands.Incarnate(world, ctx, npc.Id, "alan").IsSuccess);
        string hash = WorldSnapshot.CanonicalHash(world);

        var again = PlayerCommands.Incarnate(world, ctx, npc.Id, "outro");
        var otherNpc = world.Npcs.First(n => n.IsAlive && n.Id != npc.Id);
        var sameLogin = PlayerCommands.Incarnate(world, ctx, otherNpc.Id, "alan");

        Assert.StartsWith(PlayerCommands.Conflict, again.Error);
        Assert.StartsWith(PlayerCommands.Conflict, sameLogin.Error);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(world));
        Assert.StartsWith(PlayerCommands.NotFound, PlayerCommands.Incarnate(world, ctx, new LivingWorld.Domain.Shared.NpcId(999_999), "x").Error);
    }

    [Fact]
    public void A_controlled_npc_ages_and_needs_exactly_like_an_equal_one_for_a_year()
    {
        var (a, clockA, _, _) = Village(3);
        var (b, clockB, _, ctxB) = Village(3);
        var controlled = Pick(b);
        var twin = a.FindNpc(controlled.Id)!;
        Assert.True(PlayerCommands.Incarnate(b, ctxB, controlled.Id, "alan").IsSuccess);

        clockA.Run(a, a.Calendar.HoursPerYear);
        clockB.Run(b, b.Calendar.HoursPerYear);

        long now = a.CurrentDate.TotalHours;
        Assert.Equal(twin.IsAlive, controlled.IsAlive);
        Assert.Equal(twin.AgeYears(a.CurrentDate), controlled.AgeYears(b.CurrentDate));
        Assert.Equal(twin.HungerAt(now), controlled.HungerAt(now));
        Assert.Equal(twin.SleepAt(now), controlled.SleepAt(now));
    }

    [Fact]
    public void The_requested_action_replaces_the_autonomous_choice_and_none_hands_it_back()
    {
        var (world, clock, _, ctx) = Village(4);
        var npc = Pick(world);
        Assert.True(PlayerCommands.Incarnate(world, ctx, npc.Id, "alan").IsSuccess);

        Assert.True(PlayerCommands.Act(world, npc.Id, "alan", ActionType.Idle).IsSuccess);
        Assert.False(PlayerCommands.Act(world, npc.Id, "alan", ActionType.UsePower).IsSuccess);
        Assert.StartsWith(PlayerCommands.NotFound, PlayerCommands.Act(world, npc.Id, "intruso", ActionType.Idle).Error);
        clock.Run(world, 12);
        Assert.Equal(ActionType.Idle, npc.CurrentAction);

        Assert.True(PlayerCommands.Act(world, npc.Id, "alan", null).IsSuccess);
        Assert.Null(npc.ControlledAction);
    }

    [Fact]
    public void Logout_emits_the_disappearance_frees_the_job_and_the_home_and_the_family_misses_the_npc()
    {
        var (world, clock, sink, ctx) = Village(5);
        var npc = Pick(world);
        var workplace = world.FindWorkplace(npc.Employer!.Value)!;
        var household = world.FindHousehold(npc.Household!.Value)!;
        var family = household.Members.Where(m => m != npc.Id).ToList();
        Assert.NotEmpty(family);
        Assert.True(PlayerCommands.Incarnate(world, ctx, npc.Id, "alan").IsSuccess);
        sink.DrainAll();

        var result = PlayerCommands.Logout(world, ctx, "alan");

        Assert.True(result.IsSuccess);
        var events = sink.DrainAll();
        Assert.Contains(events, e => e.Kind == WorldEventKind.PlayerDisappeared);
        Assert.Null(npc.ControllerId);
        Assert.NotNull(npc.DisappearedAtTick);
        Assert.DoesNotContain(npc.Id, workplace.Employees);
        Assert.Null(npc.Employer);
        Assert.DoesNotContain(npc.Id, household.Members);
        Assert.NotNull(npc.Household); // vive sozinho em outro domicílio
        Assert.NotEqual(household.Id, npc.Household);
        Assert.All(family, id => Assert.Contains(
            world.CanonicalMemories.Concat(world.VolatileMemories),
            m => m.OwnerId == id && m.Participants.Contains(npc.Id) && m.Content.Contains("desapareceu", StringComparison.Ordinal)));

        clock.Run(world, world.Calendar.HoursPerYear); // um ano depois: o NPC não congelou
        Assert.Null(npc.ControllerId);
        Assert.True(npc.HungerAt(world.CurrentDate.TotalHours) != 100 || !npc.IsAlive);
    }

    [Fact]
    public void A_disappeared_npc_that_dies_goes_through_the_ordinary_death_rules()
    {
        var (world, _, sink, ctx) = Village(6);
        var npc = Pick(world);
        Assert.True(PlayerCommands.Incarnate(world, ctx, npc.Id, "alan").IsSuccess);
        Assert.True(PlayerCommands.Logout(world, ctx, "alan").IsSuccess);
        sink.DrainAll();

        NpcDeath.Apply(world, ctx, npc, WorldEventKind.Death);

        Assert.False(npc.IsAlive);
        Assert.Contains(sink.DrainAll(), e => e.Kind == WorldEventKind.Death);
        Assert.StartsWith(PlayerCommands.NotFound, PlayerCommands.Logout(world, ctx, "alan").Error);
    }

    [Fact]
    public void Controller_fields_survive_a_snapshot_round_trip()
    {
        var (world, _, _, ctx) = Village(7);
        var npc = Pick(world);
        PlayerCommands.Incarnate(world, ctx, npc.Id, "alan");
        PlayerCommands.Act(world, npc.Id, "alan", ActionType.Work);

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal("alan", restored.FindNpc(npc.Id)!.ControllerId);
        Assert.Equal(ActionType.Work, restored.FindNpc(npc.Id)!.ControlledAction);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
    }

    private static (WorldState World, WorldClock Clock, AllEvents Sink, Npc Missing, List<Npc> Family) Disappeared(ulong seed)
    {
        var (world, _) = ScenarioRunner.Create(seed);
        var sink = new AllEvents();
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        clock.Run(world, 96);
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        var npc = world.Npcs.First(n => n.IsAlive && n.Employer is not null && n.Household is not null
            && world.FindHousehold(n.Household!.Value)!.Members.Count >= 3
            && n.AgeYears(world.CurrentDate) is >= 22 and <= 35);
        var family = world.FindHousehold(npc.Household!.Value)!.Members.Where(m => m != npc.Id).Select(m => world.FindNpc(m)!).ToList();
        npc.CreditWallet(new LivingWorld.Domain.Shared.Money(5000)); // sem casa nem emprego, só quem tem moeda come durante a procura
        Assert.True(PlayerCommands.Incarnate(world, ctx, npc.Id, "alan").IsSuccess);
        Assert.True(PlayerCommands.Logout(world, ctx, "alan").IsSuccess);
        sink.Events.Clear();
        return (world, clock, sink, npc, family);
    }

    [Fact]
    public void The_family_searches_and_the_missing_npc_is_found_deterministically()
    {
        (bool Found, long? Tick) Run()
        {
            var (world, clock, sink, missing, _) = Disappeared(5);
            clock.Run(world, (PlayerSearchSystem.SearchDays + 1) * world.Calendar.HoursPerDay);
            var events = sink.Events;
            bool found = events.Any(e => e.Kind == WorldEventKind.DisappearedFound);
            Assert.Equal(found, missing.DisappearedAtTick is null);
            Assert.NotEqual(found, events.Any(e => e.Kind == WorldEventKind.SearchGaveUp)); // exatamente um desfecho
            return (found, missing.DisappearedAtTick);
        }

        var first = Run();
        Assert.Equal(first, Run());
    }

    [Fact]
    public void A_found_npc_gets_a_memory_and_a_stronger_tie_with_the_finder()
    {
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var (world, clock, sink, missing, family) = Disappeared(seed);
            clock.Run(world, (PlayerSearchSystem.SearchDays + 1) * world.Calendar.HoursPerDay);
            var found = sink.Events.FirstOrDefault(e => e.Kind == WorldEventKind.DisappearedFound);
            if (found is null) continue;

            long finderId = long.Parse(found.Payload.Split('|')[1]);
            Assert.Contains(world.CanonicalMemories.Concat(world.VolatileMemories),
                m => m.OwnerId.Value == finderId && m.Content.Contains("reencontrou", StringComparison.Ordinal));
            Assert.Null(missing.DisappearedAtTick);
            return;
        }

        Assert.Fail("nenhuma das 12 seeds terminou em reencontro");
    }

    [Fact]
    public void When_nobody_can_search_the_family_gives_up_and_mourns_with_a_stronger_memory()
    {
        var (world, clock, sink, missing, family) = Disappeared(6);
        var elsewhere = new LivingWorld.Domain.Cities.City(
            world.NextCityId(), new LivingWorld.Domain.Geography.CellCoord(7, 7), 0, null, LivingWorld.Domain.Cities.AggregatePopulationPool.Empty);
        world.AddCity(elsewhere);
        foreach (var relative in family) relative.JoinCity(elsewhere.Id); // ninguém na cidade do desaparecido

        clock.Run(world, (PlayerSearchSystem.SearchDays + 1) * world.Calendar.HoursPerDay);

        var events = sink.Events;
        Assert.DoesNotContain(events, e => e.Kind == WorldEventKind.DisappearedFound);
        Assert.Contains(events, e => e.Kind == WorldEventKind.SearchGaveUp);
        Assert.NotNull(missing.DisappearedAtTick);
        Assert.All(family.Where(f => f.IsAlive), f => Assert.Contains(
            world.CanonicalMemories.Concat(world.VolatileMemories),
            m => m.OwnerId == f.Id && m.Content.Contains("deixou de procurar", StringComparison.Ordinal)));
    }

    [Fact]
    public void If_the_missing_npc_dies_during_the_search_the_search_ends_and_the_estate_is_settled()
    {
        var (world, clock, sink, missing, family) = Disappeared(7);
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        missing.CreditWallet(new LivingWorld.Domain.Shared.Money(500));
        long before = family.Sum(f => f.Wallet.Amount);

        NpcDeath.Apply(world, ctx, missing, WorldEventKind.Death);
        clock.Run(world, (PlayerSearchSystem.SearchDays + 1) * world.Calendar.HoursPerDay);

        var events = sink.Events;
        Assert.DoesNotContain(events, e => e.Kind is WorldEventKind.DisappearedFound or WorldEventKind.SearchGaveUp);
        Assert.Contains(events, e => e.Kind == WorldEventKind.EstateSettled);
        Assert.True(family.Sum(f => f.Wallet.Amount) >= before);
    }
}
