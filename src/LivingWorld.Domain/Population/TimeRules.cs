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

    /// <summary>#261: as velocidades que o jogador escolhe, em vezes o ritmo do mundo (1 = tempo real, o padrão). Dado do cenário
    /// (<c>Players.Time.Speeds: [1, 3, 10, 60, 300]</c>); 0 é sempre a pausa. Nulo = 1 até <see cref="MaxPlayerSpeedValue"/> (padrão 3).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int[]? Speeds { get; init; }

    /// <summary>#260 (compatível): o teto da lista antiga 1..N; vale só quando <see cref="Speeds"/> não vem. Nulo = 3 e some do JSON.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxPlayerSpeedValue { get; init; }

    /// <summary>#261: o maior Δ do tick em velocidade alta (ms de mundo). Acima disso o motor roda MAIS ticks por segundo em vez de Δ maior,
    /// e andar do corpo, usos, interações e sono seguem corretos (nada pula mais que isto). Nulo = 2000.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxStepMsValue { get; init; }

    /// <summary>#261: com Δ acima disto o corpo não anda por WASD (o comando chega a cada ~0,1 s reais, em mundo são minutos): só por ordem ou rota do
    /// motor (usar móvel, interação, ir até). Nulo = 500 (até 10× no padrão).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? WalkMaxStepMsValue { get; init; }

    [System.Text.Json.Serialization.JsonIgnore] public int MaxStepMs => Math.Clamp(MaxStepMsValue ?? 2000, 50, WorldMillis.PerHour);
    [System.Text.Json.Serialization.JsonIgnore] public int WalkMaxStepMs => WalkMaxStepMsValue ?? 500;

    /// <summary>#261: a maior velocidade (vezes o ritmo do mundo) em que o corpo ainda anda por WASD: a do Δ de <see cref="WalkMaxStepMs"/>. Acima, só por ordem do motor.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int WalkMaxSpeed => (int)Math.Max(1, Math.Floor(WalkMaxStepMs / Math.Max(1.0, FineTickRealMs * TimeScale)));

    /// <summary>A lista efetiva (crescente, sem repetir, de 1 em diante, até 9 botões).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<int> SpeedList => Speeds is { Length: > 0 } s
        ? s.Where(v => v >= 1).Distinct().OrderBy(v => v).Take(9).ToArray()
        : Enumerable.Range(1, Math.Clamp(MaxPlayerSpeedValue ?? 3, 1, 10)).ToArray();

    [System.Text.Json.Serialization.JsonIgnore] public int MaxPlayerSpeed => SpeedList.Max();

    /// <summary>Velocidade válida: 0 (pausa) ou uma da lista.</summary>
    public bool IsValidSpeed(int speed) => speed == 0 || SpeedList.Contains(speed);

    /// <summary>#261: o plano do tick na velocidade do jogador: o Δ (limitado por <see cref="MaxStepMs"/>) e os ticks por segundo real que
    /// fazem o mundo andar <c>speed</c> segundos por segundo.</summary>
    public (int DeltaMs, double TicksPerSecond) SpeedPlan(int speed)
    {
        int delta = FineDeltaMs(Math.Max(1, speed), MaxStepMs);
        double worldMsPerRealSecond = 1000.0 * TimeScale * Math.Max(1, speed);
        return (delta, Math.Max(1000.0 / Math.Max(1, FineTickRealMs), worldMsPerRealSecond / delta));
    }

    public static readonly TimeRules Default = new();

    /// <summary>O Δ de mundo do tick fino: <see cref="FineTickRealMs"/> × <see cref="TimeScale"/>, ajustado ao divisor da hora mais
    /// próximo (o tick nunca atravessa a hora cheia).</summary>
    public int FineDeltaMs() => FineDeltaMs(1.0);

    /// <summary>#260: o mesmo Δ com a velocidade do jogador (1, 2 ou 3 vezes o ritmo do mundo), como o 1×/2×/3× do The Sims.</summary>
    public int FineDeltaMs(double speed) => FineDeltaMs(speed, WorldMillis.PerHour);

    /// <summary>#261: o Δ da velocidade, nunca acima de <paramref name="maxMs"/> (ainda um divisor da hora).</summary>
    public int FineDeltaMs(double speed, int maxMs)
    {
        double wanted = Math.Clamp(FineTickRealMs * TimeScale * Math.Max(1.0, speed), 1, Math.Min(maxMs, WorldMillis.PerHour));
        int best = 1;
        double bestGap = double.MaxValue;
        for (int d = 1; (long)d * d <= WorldMillis.PerHour; d++)
        {
            if (WorldMillis.PerHour % d != 0) continue;
            foreach (int candidate in (ReadOnlySpan<int>)[d, WorldMillis.PerHour / d])
            {
                if (candidate > maxMs) continue;
                double gap = Math.Abs(candidate - wanted);
                if (gap < bestGap || (gap == bestGap && candidate < best)) { best = candidate; bestGap = gap; }
            }
        }
        return best;
    }
}
