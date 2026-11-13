using System.Text.Json.Serialization;
using LivingWorld.Domain.Behavior;

namespace LivingWorld.Domain.Cognition;

/// <summary>Ganhos sinápticos de um NPC (ADR-0046, AD-142): <see cref="Gains"/>[ação × <see cref="Groups"/> + grupo], em
/// milésimos, multiplicam cada grupo de termos da utilidade por <c>1 + g/1000</c>. <see cref="ActionMeans"/> é o alívio que o
/// NPC costuma ter de cada ação (a expectativa contra a qual a experiência surpreende). <see cref="PendingAction"/> e
/// <see cref="PendingShares"/> guardam, enquanto a ação escolhida pela utilidade dura, a participação (milésimos, com sinal) de
/// cada grupo na nota dela: na conclusão, a recompensa vai para quem a escolheu. Estado canônico (alimenta a decisão); nulo no
/// NPC = nunca decidiu com o cérebro ligado. Os vetores são mutados no lugar por <c>BrainMechanic</c>, que avisa o NPC.</summary>
public sealed class BrainState
{
    public const int Groups = 8;
    public const int Actions = 10;

    [JsonConstructor]
    public BrainState(short[] gains, short[] actionMeans, ActionType? pendingAction = null, short[]? pendingShares = null)
    {
        if (gains.Length != Actions * Groups) throw new ArgumentException($"Gains: {Actions * Groups} valores", nameof(gains));
        if (actionMeans.Length != Actions) throw new ArgumentException($"ActionMeans: {Actions} valores", nameof(actionMeans));
        if (pendingShares is { Length: not Groups }) throw new ArgumentException($"PendingShares: {Groups} valores", nameof(pendingShares));
        Gains = gains;
        ActionMeans = actionMeans;
        PendingAction = pendingAction;
        PendingShares = pendingShares;
    }

    public short[] Gains { get; }
    public short[] ActionMeans { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ActionType? PendingAction { get; private set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public short[]? PendingShares { get; private set; }

    public static BrainState Neutral() => new(new short[Actions * Groups], new short[Actions]);

    public short GainOf(ActionType action, int group) => Gains[(int)action * Groups + group];

    public void Remember(ActionType action, ReadOnlySpan<short> shares)
    {
        PendingShares ??= new short[Groups];
        shares.CopyTo(PendingShares);
        PendingAction = action;
    }

    public void Forget() => PendingAction = null;
}
