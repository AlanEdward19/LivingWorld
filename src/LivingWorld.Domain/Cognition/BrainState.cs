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

    /// <summary>Expectativa de uma ação que o NPC ainda não viveu (AD-160): zero. Com a regra da primeira experiência, uma
    /// expectativa aprendida nunca é guardada como zero (vira ±1), então zero sempre quer dizer "ainda não viveu".</summary>
    public const short Unknown = 0;

    [JsonConstructor]
    public BrainState(short[] gains, short[] actionMeans, ActionType? pendingAction = null, long pendingShareBytes = 0)
    {
        if (gains.Length != Actions * Groups) throw new ArgumentException($"Gains: {Actions * Groups} valores", nameof(gains));
        if (actionMeans.Length != Actions) throw new ArgumentException($"ActionMeans: {Actions} valores", nameof(actionMeans));
        Gains = gains;
        ActionMeans = actionMeans;
        PendingAction = pendingAction;
        PendingShareBytes = pendingAction is null ? 0 : pendingShareBytes;
    }

    public short[] Gains { get; }
    public short[] ActionMeans { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ActionType? PendingAction { get; private set; }

    /// <summary>AD-160: a participação de cada grupo na nota da ação guardada, um byte com sinal por grupo (±127 = ±100%) num só
    /// número. No AD-142 eram 8 int16 numa lista, que ficava no NPC mesmo depois de esquecida e mudava 8 folhas do snapshot a cada
    /// decisão. Esquecer zera. Leia por <see cref="ShareOf"/>. Snapshot do AD-142 abre sem a ação guardada (perde um passo).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long PendingShareBytes { get; private set; }

    /// <summary>Participação (milésimos, com sinal) do grupo na nota da ação guardada.</summary>
    public int ShareOf(int group) => (int)Math.Round((sbyte)(PendingShareBytes >> (8 * group)) * 1000.0 / 127, MidpointRounding.AwayFromZero);

    public static BrainState Neutral() => new(new short[Actions * Groups], new short[Actions]);

    public short GainOf(ActionType action, int group) => Gains[(int)action * Groups + group];

    public void Remember(ActionType action, ReadOnlySpan<short> shares)
    {
        long packed = 0;
        for (int g = 0; g < Groups; g++)
        {
            int b = (int)Math.Round(Math.Clamp((int)shares[g], -1000, 1000) * 127 / 1000.0, MidpointRounding.AwayFromZero);
            packed |= (long)(byte)(sbyte)b << (8 * g);
        }
        PendingShareBytes = packed;
        PendingAction = action;
    }

    public void Forget()
    {
        PendingAction = null;
        PendingShareBytes = 0;
    }
}
