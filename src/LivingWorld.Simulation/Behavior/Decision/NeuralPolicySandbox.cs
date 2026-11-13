using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Opção (a) do ADR-0046, só no sandbox: rede por NPC 28-16-10 em ponto fixo (pesos int16, acumulador int32, ReLU
/// cortada), para trocar a estimativa de custo por número medido. Não entra no mundo nem no tick: nenhum sistema a chama.
/// Soma inteira não depende da ordem, então o resultado é o mesmo em qualquer máquina.</summary>
public sealed class NeuralPolicySandbox
{
    public const int Inputs = 28, Hidden = 16, Outputs = 10;
    private const int HiddenShift = 10, HiddenCap = 32767;

    private readonly short[] _w1 = new short[Hidden * Inputs];
    private readonly int[] _b1 = new int[Hidden];
    private readonly short[] _w2 = new short[Outputs * Hidden];
    private readonly int[] _b2 = new int[Outputs];

    /// <summary>Pesos sorteados do stream dado, em ±<paramref name="scale"/> (sem treino: o custo não depende dos valores).</summary>
    public static NeuralPolicySandbox Random(WorldRng rng, int scale = 256)
    {
        var net = new NeuralPolicySandbox();
        for (int i = 0; i < net._w1.Length; i++) net._w1[i] = (short)((rng.NextDouble() * 2 - 1) * scale);
        for (int i = 0; i < net._w2.Length; i++) net._w2[i] = (short)((rng.NextDouble() * 2 - 1) * scale);
        return net;
    }

    /// <summary>Entradas do contexto em int16: 4 déficits, 6 emoções (milésimos), 8 de contexto, 10 traços.</summary>
    public static void Encode(DecisionContext ctx, Span<short> x)
    {
        x[0] = (short)(100 - ctx.Needs.Hunger); x[1] = (short)(100 - ctx.Needs.Thirst);
        x[2] = (short)(100 - ctx.Needs.Sleep); x[3] = (short)(100 - ctx.Needs.Social);
        var m = ctx.Mood;
        x[4] = (short)(m.Grief * 1000); x[5] = (short)(m.Fear * 1000); x[6] = (short)(m.Anger * 1000);
        x[7] = (short)(m.Joy * 1000); x[8] = (short)(m.Shame * 1000); x[9] = (short)(m.Loneliness * 1000);
        x[10] = (short)Math.Min(1000, ctx.WalletAmount);
        x[11] = (short)(ctx.Household?.Members.Count ?? 0);
        x[12] = (short)(ctx.FamineMemory * 10);
        x[13] = (short)(ctx.Comfort.Eat * 1000); x[14] = (short)(ctx.Comfort.Sleep * 1000);
        x[15] = (short)ctx.RelevantMemories.Count; x[16] = (short)ctx.RelevantBeliefs.Count;
        x[17] = (short)(ctx.CurrentAction is { } a ? (int)a + 1 : 0);
        var p = ctx.Personality;
        x[18] = (short)p.Extroversion; x[19] = (short)p.Agreeableness; x[20] = (short)p.Conscientiousness;
        x[21] = (short)p.EmotionalStability; x[22] = (short)p.Openness; x[23] = (short)p.Ambition;
        x[24] = (short)p.Loyalty; x[25] = (short)p.Altruism; x[26] = (short)p.Impulsivity; x[27] = (short)p.RiskAversion;
    }

    /// <summary>Ação de maior saída; empate exato fica com o menor id, como na utilidade.</summary>
    public ActionType Decide(ReadOnlySpan<short> x)
    {
        Span<int> h = stackalloc int[Hidden];
        for (int j = 0; j < Hidden; j++)
        {
            int acc = _b1[j];
            int row = j * Inputs;
            for (int i = 0; i < Inputs; i++) acc += _w1[row + i] * x[i];
            h[j] = Math.Clamp(acc >> HiddenShift, 0, HiddenCap);
        }

        int best = 0;
        long bestScore = long.MinValue;
        for (int o = 0; o < Outputs; o++)
        {
            long acc = _b2[o];
            int row = o * Hidden;
            for (int j = 0; j < Hidden; j++) acc += (long)_w2[row + j] * h[j];
            if (acc > bestScore) { bestScore = acc; best = o; }
        }

        return (ActionType)best;
    }

    public ActionType Decide(DecisionContext ctx)
    {
        Span<short> x = stackalloc short[Inputs];
        Encode(ctx, x);
        return Decide(x);
    }
}
