using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Llm;

namespace LivingWorld.Tests.Unit.Llm;

/// <summary>Fase 11, story "Sessão de conversa segura" (LLM-01, LLM-02, AC1/AC4): <see
/// cref="ConversationAvailabilityPolicy"/> decide Accepted/Rejected com motivo determinístico e
/// nunca força o NPC a parar a ação corrente — só ações declaradas <c>Forbidden</c> no cenário
/// impedem aceitar.</summary>
public class ConversationAvailabilityPolicyTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static NeedsRules MakeNeedsRules(int urgencyThreshold = 70) => NeedsRules.Create(
        hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
        urgencyThreshold, maxActionSelectionSteps: 10, hysteresisEnabled: false,
        continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;

    private static LlmRules MakeLlmRules(
        double hostileTrustThreshold = 20,
        ActionType forbiddenAction = ActionType.Sleep,
        ActionType pausedAction = ActionType.Work)
    {
        var compatibility = Enum.GetValues<ActionType>().ToDictionary(a => a, _ => ConversationCompatibility.Compatible);
        compatibility[forbiddenAction] = ConversationCompatibility.Forbidden;
        compatibility[pausedAction] = ConversationCompatibility.RequiresPause;
        return LlmRules.Create(hostileTrustThreshold, compatibility).Value!;
    }

    private static Npc MakeNpc(ActionType? currentAction, int hunger = 100, int thirst = 100, int sleep = 100, int social = 100)
    {
        var location = new CellCoord(1, 1);
        return new Npc(
            new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: ProfessionType.None, currentLocation: location,
            hunger: hunger, thirst: thirst, sleep: sleep, social: social,
            currentAction: currentAction);
    }

    [Fact]
    public void No_current_action_is_accepted_and_compatible()
    {
        var npc = MakeNpc(currentAction: null);

        var decision = ConversationAvailabilityPolicy.Evaluate(npc, MakeNeedsRules(), MakeLlmRules(), null, now: 0);

        Assert.Equal(ConversationStartDecision.Accepted, decision.Result);
        Assert.Equal(ConversationCompatibility.Compatible, decision.Compatibility);
    }

    [Fact]
    public void Compatible_current_action_is_accepted_and_action_keeps_running()
    {
        var npc = MakeNpc(currentAction: ActionType.Idle);

        var decision = ConversationAvailabilityPolicy.Evaluate(npc, MakeNeedsRules(), MakeLlmRules(), null, now: 0);

        Assert.Equal(ConversationStartDecision.Accepted, decision.Result);
        Assert.Equal(ConversationCompatibility.Compatible, decision.Compatibility);
    }

    [Fact]
    public void Action_requiring_pause_is_still_accepted()
    {
        var npc = MakeNpc(currentAction: ActionType.Work);

        var decision = ConversationAvailabilityPolicy.Evaluate(
            npc, MakeNeedsRules(), MakeLlmRules(pausedAction: ActionType.Work), null, now: 0);

        Assert.Equal(ConversationStartDecision.Accepted, decision.Result);
        Assert.Equal(ConversationCompatibility.RequiresPause, decision.Compatibility);
    }

    [Fact]
    public void Forbidden_action_with_urgent_need_is_rejected_busy()
    {
        // Sono zerado -> HasUrgentNeed verdadeiro (déficit 100, sempre urgente).
        var npc = MakeNpc(currentAction: ActionType.Sleep, sleep: 0);

        var decision = ConversationAvailabilityPolicy.Evaluate(
            npc, MakeNeedsRules(), MakeLlmRules(forbiddenAction: ActionType.Sleep), null, now: 0);

        Assert.Equal(ConversationStartDecision.RejectedBusy, decision.Result);
        Assert.Equal(ConversationCompatibility.Forbidden, decision.Compatibility);
    }

    [Fact]
    public void Forbidden_action_without_urgent_need_is_rejected_unavailable()
    {
        var npc = MakeNpc(currentAction: ActionType.Sleep, sleep: 100);

        var decision = ConversationAvailabilityPolicy.Evaluate(
            npc, MakeNeedsRules(urgencyThreshold: 100), MakeLlmRules(forbiddenAction: ActionType.Sleep), null, now: 0);

        Assert.Equal(ConversationStartDecision.RejectedUnavailable, decision.Result);
        Assert.Equal(ConversationCompatibility.Forbidden, decision.Compatibility);
    }

    [Fact]
    public void Hostile_relationship_rejects_regardless_of_current_action()
    {
        var npc = MakeNpc(currentAction: null);
        var hostile = Relationship.Initial(firstContactTick: 0); // Trust = 0 (piso)

        var decision = ConversationAvailabilityPolicy.Evaluate(
            npc, MakeNeedsRules(), MakeLlmRules(hostileTrustThreshold: 10), hostile, now: 0);

        Assert.Equal(ConversationStartDecision.RejectedHostile, decision.Result);
    }

    [Fact]
    public void No_relationship_record_is_never_treated_as_hostile()
    {
        var npc = MakeNpc(currentAction: null);

        var decision = ConversationAvailabilityPolicy.Evaluate(
            npc, MakeNeedsRules(), MakeLlmRules(hostileTrustThreshold: 10), relationshipToInitiator: null, now: 0);

        Assert.Equal(ConversationStartDecision.Accepted, decision.Result);
    }
}
