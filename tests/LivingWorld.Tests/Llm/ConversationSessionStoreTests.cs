using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Llm;

/// <summary>Fase 11, LLM-03, story "Sessão de conversa segura" (ACs 1-3): ciclo
/// start/send/end de <see cref="ConversationSessionStore"/> — expiração agendada via <see
/// cref="EventScheduler"/>/<see cref="TickContext.ScheduleEvent"/>, nunca varredura por tick
/// (integração real com o scheduler, não chamada direta a <c>HandleEvent</c>).</summary>
public class ConversationSessionStoreTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly LifeStageRules Stages = LifeStageRules.Create(childMaxAge: 14, adultMaxAge: 64).Value!;

    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static NeedsRules MakeNeedsRules(int urgencyThreshold = 70) => NeedsRules.Create(
        hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
        urgencyThreshold, maxActionSelectionSteps: 10, hysteresisEnabled: false,
        continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;

    private static ActionCatalog MakeActionCatalog() => ActionCatalog.Create(
        maxDurationHours: Enum.GetValues<ActionType>().ToDictionary(a => a, _ => 8),
        routineSlots: [],
        defaultAction: ActionType.Idle).Value!;

    private static LlmRules MakeLlmRules(ActionType forbiddenAction = ActionType.Sleep) =>
        LlmRules.Create(
            hostileTrustThreshold: 20,
            actionCompatibility: Enum.GetValues<ActionType>().ToDictionary(
                a => a, a => a == forbiddenAction ? ConversationCompatibility.Forbidden : ConversationCompatibility.Compatible))
            .Value!;

    private static (WorldState World, TickContext Ctx, Npc Npc) BuildWorld(ActionType? currentAction = null)
    {
        var map = ScenarioRunner.DefaultMap(seed: 1);
        var world = new WorldState(
            Calendar, seed: 1, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            MakeNeedsRules(), MakeActionCatalog(), Stages);
        var location = new CellCoord(1, 1);

        var npc = new Npc(
            new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: ProfessionType.None, currentLocation: location,
            currentAction: currentAction);

        world.AddNpc(npc);
        world.AdvanceNpcIdTo(2);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        return (world, ctx, npc);
    }

    /// <summary>Avança o mundo em ticks de 1 hora, despachando eventos vencidos por tick — mesmo
    /// papel de <see cref="WorldClock"/>, mas restrito ao(s) sistema(s) sob teste.</summary>
    private static void AdvanceTicks(WorldState world, TickContext ctx, ConversationSessionStore store, long ticks)
    {
        for (long i = 0; i < ticks; i++)
        {
            world.CurrentDate = world.CurrentDate.AddHours(1);
            foreach (var evt in world.Scheduler.PopDue(world.CurrentDate.TotalHours))
                if (evt.SystemName == ConversationSessionStore.SystemName)
                    store.HandleEvent(world, ctx, evt);
        }
    }

    [Fact]
    public void StartConversation_accepted_creates_an_active_session()
    {
        var (_, ctx, npc) = BuildWorld();
        var store = new ConversationSessionStore();

        var (decision, session) = store.StartConversation(npc, MakeNeedsRules(), MakeLlmRules(), null, ctx, expireAfterTicks: 10);

        Assert.Equal(ConversationStartDecision.Accepted, decision);
        Assert.NotNull(session);
        Assert.True(session!.IsActive);
        Assert.Equal(npc.Id, session.NpcId);
        Assert.Equal(ctx.CurrentTick, session.OpenedAtTick);
    }

    [Fact]
    public void StartConversation_rejected_creates_no_session()
    {
        var (_, ctx, npc) = BuildWorld(currentAction: ActionType.Sleep);

        var store = new ConversationSessionStore();

        var (decision, session) = store.StartConversation(
            npc, MakeNeedsRules(urgencyThreshold: 100), MakeLlmRules(forbiddenAction: ActionType.Sleep), null, ctx, expireAfterTicks: 10);

        Assert.Equal(ConversationStartDecision.RejectedUnavailable, decision);
        Assert.Null(session);
    }

    [Fact]
    public void SendPlayerMessage_registers_turns_in_turn_id_order()
    {
        var (_, ctx, npc) = BuildWorld();
        var store = new ConversationSessionStore();
        var (_, session) = store.StartConversation(npc, MakeNeedsRules(), MakeLlmRules(), null, ctx, expireAfterTicks: 10);

        store.SendPlayerMessage(session!.SessionId, "oi", ctx);
        store.SendPlayerMessage(session.SessionId, "tudo bem?", ctx);

        var turns = store.TurnsOf(session.SessionId);
        Assert.Equal(2, turns.Count);
        Assert.Equal("oi", turns[0].PlayerText);
        Assert.Equal("tudo bem?", turns[1].PlayerText);
        Assert.True(turns[0].TurnId < turns[1].TurnId);
    }

    [Fact]
    public void SendPlayerMessage_on_unknown_session_reports_not_found()
    {
        var (_, ctx, _) = BuildWorld();
        var store = new ConversationSessionStore();

        var result = store.SendPlayerMessage(sessionId: 999, "oi", ctx);

        Assert.Equal(SendMessageResult.SessionNotFound, result);
    }

    [Fact]
    public void EndConversation_deactivates_session_but_keeps_turn_history()
    {
        var (_, ctx, npc) = BuildWorld();
        var store = new ConversationSessionStore();
        var (_, session) = store.StartConversation(npc, MakeNeedsRules(), MakeLlmRules(), null, ctx, expireAfterTicks: 10);
        store.SendPlayerMessage(session!.SessionId, "oi", ctx);

        var result = store.EndConversation(session.SessionId);

        Assert.Equal(EndConversationResult.Ok, result);
        Assert.False(store.Find(session.SessionId)!.IsActive);
        Assert.Single(store.TurnsOf(session.SessionId));
    }

    [Fact]
    public void SendPlayerMessage_after_end_reports_session_inactive()
    {
        var (_, ctx, npc) = BuildWorld();
        var store = new ConversationSessionStore();
        var (_, session) = store.StartConversation(npc, MakeNeedsRules(), MakeLlmRules(), null, ctx, expireAfterTicks: 10);
        store.EndConversation(session!.SessionId);

        var result = store.SendPlayerMessage(session.SessionId, "ainda aí?", ctx);

        Assert.Equal(SendMessageResult.SessionInactive, result);
    }

    [Fact]
    public void Session_expires_by_scheduled_event_not_before_its_target_tick()
    {
        var (world, ctx, npc) = BuildWorld();
        var store = new ConversationSessionStore();
        var (_, session) = store.StartConversation(npc, MakeNeedsRules(), MakeLlmRules(), null, ctx, expireAfterTicks: 3);

        AdvanceTicks(world, ctx, store, ticks: 2);
        Assert.True(store.Find(session!.SessionId)!.IsActive);

        AdvanceTicks(world, ctx, store, ticks: 1);
        Assert.False(store.Find(session.SessionId)!.IsActive);
    }

    [Fact]
    public void Expired_session_keeps_its_turn_history()
    {
        var (world, ctx, npc) = BuildWorld();
        var store = new ConversationSessionStore();
        var (_, session) = store.StartConversation(npc, MakeNeedsRules(), MakeLlmRules(), null, ctx, expireAfterTicks: 1);
        store.SendPlayerMessage(session!.SessionId, "oi", ctx);

        AdvanceTicks(world, ctx, store, ticks: 1);

        Assert.False(store.Find(session.SessionId)!.IsActive);
        Assert.Single(store.TurnsOf(session.SessionId));
    }
}
