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
    public BrainState(short[] gains, short[] actionMeans, long pending = 0)
    {
        if (gains.Length != Actions * Groups) throw new ArgumentException($"Gains: {Actions * Groups} valores", nameof(gains));
        if (actionMeans.Length != Actions) throw new ArgumentException($"ActionMeans: {Actions} valores", nameof(actionMeans));
        Gains = gains;
        ActionMeans = actionMeans;
        Pending = pending;
    }

    [JsonConverter(typeof(GainsJsonConverter))]
    public short[] Gains { get; }
    public short[] ActionMeans { get; }

    /// <summary>AD-160b: a escolha guardada num só número (uma folha do snapshot em vez de três, e zero = nada guardado, omitido):
    /// bits 0–47 = participação de cada grupo na nota, 6 bits com sinal por grupo (±31 = ±100%); bits 48–51 = ação + 1; bits
    /// 52–61 = valor do estado no começo + <see cref="ValueOffset"/> (0–1023 pontos). Leia por <see cref="PendingAction"/>,
    /// <see cref="ShareOf"/> e <see cref="PendingValue"/>. Snapshot do AD-160 (três campos) abre sem a escolha guardada.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long Pending { get; private set; }

    private const int ShareBits = 6, ShareMax = (1 << (ShareBits - 1)) - 1, ValueOffset = 128;

    [JsonIgnore]
    public ActionType? PendingAction => Pending == 0 ? null : (ActionType)((int)((Pending >> 48) & 0xF) - 1);

    /// <summary>O valor que o NPC dava ao próprio estado quando escolheu a ação guardada (pontos).</summary>
    [JsonIgnore]
    public short PendingValue => Pending == 0 ? (short)0 : (short)(((Pending >> 52) & 0x3FF) - ValueOffset);

    /// <summary>Participação (milésimos, com sinal) do grupo na nota da ação guardada.</summary>
    public int ShareOf(int group)
    {
        int raw = (int)((Pending >> (ShareBits * group)) & ((1 << ShareBits) - 1));
        if (raw > ShareMax) raw -= 1 << ShareBits;
        return (int)Math.Round(raw * 1000.0 / ShareMax, MidpointRounding.AwayFromZero);
    }

    public static BrainState Neutral() => new(new short[Actions * Groups], new short[Actions]);

    public short GainOf(ActionType action, int group) => Gains[(int)action * Groups + group];

    public void Remember(ActionType action, ReadOnlySpan<short> shares, double value = 0)
    {
        long packed = 0;
        for (int g = 0; g < Groups; g++)
        {
            int b = (int)Math.Round(Math.Clamp((int)shares[g], -1000, 1000) * ShareMax / 1000.0, MidpointRounding.AwayFromZero);
            packed |= (long)(b & ((1 << ShareBits) - 1)) << (ShareBits * g);
        }
        long v = Math.Clamp((long)Math.Round(value, MidpointRounding.AwayFromZero) + ValueOffset, 0, 0x3FF);
        Pending = packed | ((long)((int)action + 1) << 48) | (v << 52);
    }

    public void Forget() => Pending = 0;
}
