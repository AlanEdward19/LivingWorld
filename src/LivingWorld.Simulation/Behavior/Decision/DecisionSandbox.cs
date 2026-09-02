using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Ambiente isolado que roda o mesmo pipeline de
/// <see cref="BehaviorDecisionSystem.SelectByUtility"/> com <see cref="DecisionContext"/>
/// sintético — sem tocar <see cref="WorldState"/>, tick de mundo ou RNG de mundo (SBX-01).</summary>
public static class DecisionSandbox
{
    /// <summary>Executa o pipeline de utility com contexto sintético; nenhuma escrita em
    /// <see cref="WorldState"/>.</summary>
    public static DecisionSandboxResult Decide(
        DecisionContext context,
        NeedsRules rules,
        EconomyRules economy,
        DecisionSandboxRequest? request = null)
    {
        request ??= DecisionSandboxRequest.Default;

        var decision = BehaviorDecisionSystem.SelectByUtility(
            context,
            rules,
            economy,
            request.ContinuityAction,
            request.PowerRules,
            request.WakeReason,
            request.PreviousIntent);

        return new DecisionSandboxResult(decision.Action, decision.PendingPower, decision.Trace);
    }
}

/// <summary>Parâmetros opcionais do sandbox — espelham os argumentos de wake/continuidade de
/// <see cref="BehaviorDecisionSystem.SelectByUtility"/> sem exigir <see cref="WorldState"/>.</summary>
public sealed record DecisionSandboxRequest(
    ActionType? ContinuityAction = null,
    PowerUtilityRules? PowerRules = null,
    WakeReason WakeReason = WakeReason.UrgentNeed,
    ActionType? PreviousIntent = null)
{
    public static DecisionSandboxRequest Default { get; } = new();
}

/// <summary>Resultado volátil de uma decisão no sandbox — mesma forma que o motor principal
/// expõe via <c>UtilityDecision</c>, sem mutar NPC/mundo.</summary>
public sealed record DecisionSandboxResult(
    ActionType Action,
    PendingPowerInvocation? PendingPower,
    DecisionTrace Trace);
