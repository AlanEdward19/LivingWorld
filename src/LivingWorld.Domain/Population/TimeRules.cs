using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population;

/// <summary>Ritmo do mundo sem jogador conectado (ADR-0050, decisão do dono de 2026-11-23: padrão rápido, o ritmo do admin).</summary>
public enum IdleRhythm
{
    /// <summary>Uma hora de mundo por tick, na velocidade do laço que o admin escolhe (o de sempre).</summary>
    Fast = 0,

    /// <summary>Tempo real na escala do mundo, como se houvesse jogador.</summary>
    RealTime = 1,
}

/// <summary>Ritmo do relógio e passo fino com jogador (AD-197c, ADR-0050). Dado de cenário, por mundo: <c>{"Players": {"Time":
/// {"TimeScale": 12, "IdleRhythm": "RealTime"}}}</c>. <see cref="FineDecisions"/> falso (<c>LW_AD197C=off</c>) deixa o Δ fino
/// andar só o relógio, como na AD-197b.</summary>
public sealed record TimeRules
{
    /// <summary>Tempo de mundo por tempo real com jogador conectado. 1 = 1:1 (o padrão do dono); 12 = um dia em duas horas reais.</summary>
    public double TimeScale { get; init; } = 1.0;

    /// <summary>O que o mundo faz quando ninguém está conectado.</summary>
    public IdleRhythm IdleRhythm { get; init; } = IdleRhythm.Fast;

    /// <summary>Período real do tick fino, em ms reais (20 ticks por segundo). O Δ de mundo é este × <see cref="TimeScale"/>.</summary>
    public int FineTickRealMs { get; init; } = 50;

    /// <summary>Horas de mundo por segundo real quando o tempo é acelerado (sono acelerado, jogador sozinho parado; uma hora por tick).</summary>
    public double AcceleratedHoursPerSecond { get; init; } = 8;

    /// <summary>Decisão, ação e rota dos NPCs com granulação fina quando Δ é menor que a hora (AD-197c).</summary>
    public bool FineDecisions { get; init; } = true;

    /// <summary>Déficit (0–100) de fome ou sede que acorda quem dorme acelerado: o mesmo limiar em que a necessidade de
    /// sobrevivência deixa de competir como preferência na decisão (<c>BehaviorDecisionSystem.CriticalDeficit</c>).</summary>
    public int CriticalNeedDeficit { get; init; } = 80;

    /// <summary>Temperatura do lugar (°C) a partir da qual quem dorme acorda queimando: o motor ainda não tem incêndio, só o calor
    /// do lugar (clima, poder de temperatura). 50 °C queima a pele em minutos (escolha do modelo).</summary>
    public double BurnTemperatureC { get; init; } = 50;

    /// <summary>Afeição (0–100) de quem, conversando ao lado, chama quem dorme: quem mora junto sempre chama; de fora, só um conhecido
    /// próximo, não um estranho.</summary>
    public double CallerAffection { get; init; } = 30;

    public static readonly TimeRules Default = new();

    /// <summary>O Δ de mundo do tick fino: <see cref="FineTickRealMs"/> × <see cref="TimeScale"/>, ajustado ao divisor da hora mais
    /// próximo (o tick nunca atravessa a hora cheia).</summary>
    public int FineDeltaMs()
    {
        double wanted = Math.Clamp(FineTickRealMs * TimeScale, 1, WorldMillis.PerHour);
        int best = 1;
        double bestGap = double.MaxValue;
        for (int d = 1; (long)d * d <= WorldMillis.PerHour; d++)
        {
            if (WorldMillis.PerHour % d != 0) continue;
            foreach (int candidate in (ReadOnlySpan<int>)[d, WorldMillis.PerHour / d])
            {
                double gap = Math.Abs(candidate - wanted);
                if (gap < bestGap || (gap == bestGap && candidate < best)) { best = candidate; bestGap = gap; }
            }
        }
        return best;
    }
}
