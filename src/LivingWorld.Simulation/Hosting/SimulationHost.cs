using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;

namespace LivingWorld.Simulation.Hosting;

/// <summary>Controles do hospedeiro: pausa, velocidade, avanço rápido. Nada aqui é
/// estado do mundo — por isso não aparece em <see cref="WorldState"/> nem no snapshot.</summary>
public sealed class SimulationHost(WorldHost host)
{
    /// <summary>Teto de uma pausa de admin: 30 dias simulados (ADR-0015, risco de admin distraído congelar o mundo).</summary>
    public const int MaxAdminPauseDays = 30;

    /// <summary>Teto padrão quando o admin não informa: 7 dias simulados.</summary>
    public const int DefaultAdminPauseDays = 7;

    public bool IsPaused { get; private set; }
    public double TicksPerSecond { get; private set; } = 1.0;

    /// <summary>Pausa de admin em vigor (quem, por quê, desde quando, até quantos ciclos do relógio); nulo na pausa comum.</summary>
    public AdminPauseState? AdminPause { get; private set; }

    /// <summary>Chamado quando o teto vence e a simulação retoma sozinha (a auditoria registra o fato).</summary>
    public Action<AdminPauseState>? OnPauseExpired { get; set; }

    private long _pausedCycles;

    public void Pause()
    {
        IsPaused = true; // não mexe em AdminPause: uma pausa comum nunca remove o teto de uma pausa de admin
    }

    public void Resume()
    {
        IsPaused = false;
        AdminPause = null;
        _pausedCycles = 0;
    }

    /// <summary>Pausa com teto: retoma sozinha depois de <see cref="AdminPauseState.MaxTicks"/> ciclos do relógio de tempo real
    /// (os ciclos que o mundo andaria, não fosse a pausa).</summary>
    public void PauseFor(string actor, string reason, long maxTicks, long worldTick)
    {
        IsPaused = true;
        _pausedCycles = 0;
        AdminPause = new AdminPauseState(actor, reason, worldTick, maxTicks);
    }

    /// <summary>Um ciclo do relógio de tempo real passou com a simulação pausada. Devolve verdadeiro se o teto venceu e retomou.</summary>
    public bool CountPausedCycle()
    {
        if (!IsPaused || AdminPause is not { } pause || ++_pausedCycles < pause.MaxTicks) return false;

        Resume();
        OnPauseExpired?.Invoke(pause);
        return true;
    }

    public void SetSpeed(double ticksPerSecond)
    {
        if (ticksPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(ticksPerSecond), ticksPerSecond, "Velocidade deve ser positiva.");
        TicksPerSecond = ticksPerSecond;
    }

    /// <summary>AD-197c: o hospedeiro escolhe o Δ e a velocidade do laço pelo ritmo do mundo (<see cref="RhythmPlan"/>). Falso
    /// (<c>LW_AD197C=off</c>): o Δ é o que o admin pediu, como na AD-197b.</summary>
    public bool AutoRhythm { get; set; } = true;

    /// <summary>AD-197c: o admin acelerou o mundo (sempre pode, com ou sem jogador): uma hora por tick.</summary>
    public bool Accelerated { get; private set; }

    /// <summary>AD-197c: Δ fixado pelo admin (<c>POST /simulation/tick-delta</c>), acima do ritmo automático; nulo = automático.</summary>
    public int? ManualDeltaMs { get; private set; }

    /// <summary>O plano do último ciclo (modo, Δ e velocidade do laço).</summary>
    public RhythmPlan LastPlan { get; private set; } = new(RhythmMode.Fast, WorldMillis.HourTick, 1.0);

    public void SetAccelerated(bool on) => Accelerated = on;

    /// <summary>Velocidades que o jogador escolhe (#260): 0 pausa, 1 normal, 2 e 3 vezes o ritmo do mundo. Os valores vêm de dado
    /// (<see cref="TimeRules.MaxPlayerSpeed"/>); aqui só se guarda a escolha.</summary>
    public int PlayerSpeed { get; private set; } = 1;

    /// <summary>#260: a velocidade que vale agora: a do jogador só quando um único jogador conduz um corpo (com dois, o relógio é de todos
    /// e volta a 1×).</summary>
    public int EffectiveSpeed(WorldState world) => PlayerService.PlayersPresent(world) == 1 ? PlayerSpeed : 1;

    /// <summary>O mundo está parado pela pausa do jogador (velocidade 0 com ele sozinho).</summary>
    public bool PlayerPaused(WorldState world) => AutoRhythm && ManualDeltaMs is null && EffectiveSpeed(world) == 0;

    /// <summary>Escolhe a velocidade do jogador; a validação (só quem joga sozinho, valor do dado) é de <see cref="PlayerService.SetSpeed"/>.</summary>
    public void SetPlayerSpeed(int speed) => PlayerSpeed = speed;

    public void SetManualDelta(int? deltaMs) => ManualDeltaMs = deltaMs;

    /// <summary>#256: criar ou abrir outro mundo larga o que o admin fixou no mundo anterior (Δ manual e acelerado). Sem isto o Δ de
    /// uma sessão de teste sobrevivia ao mundo novo e o jogador via o relógio correr mesmo em tempo real.</summary>
    public void ResetRhythmOverrides()
    {
        ManualDeltaMs = null;
        Accelerated = false;
        PlayerSpeed = 1;
    }

    /// <summary>O ritmo agora (ADR-0050, decisão 3): Δ fixado pelo admin; acelerado (admin, ou o pulo de quem joga sozinho): uma
    /// hora por tick; com jogador: tempo real na escala do mundo (<see cref="TimeRules.TimeScale"/>); sem ninguém: o
    /// <see cref="TimeRules.IdleRhythm"/> do mundo (padrão rápido, o ritmo do admin).</summary>
    public RhythmPlan Plan(WorldState world, PlayerService players)
    {
        if (!AutoRhythm) return new RhythmPlan(RhythmMode.Manual, players.Log.TickDeltaMs, TicksPerSecond);
        if (ManualDeltaMs is { } manual) return new RhythmPlan(RhythmMode.Manual, manual, TicksPerSecond);
        var time = world.Players.Time;
        if (Accelerated || players.Skip is not null)
            return new RhythmPlan(RhythmMode.Accelerated, WorldMillis.HourTick, Math.Max(TicksPerSecond, time.AcceleratedHoursPerSecond));
        var (stepMs, tps) = time.SpeedPlan(Math.Max(1, EffectiveSpeed(world)));
        var real = new RhythmPlan(RhythmMode.RealTime, stepMs, tps);
        if (PlayerService.PlayersPresent(world) > 0) return real;
        return time.IdleRhythm == IdleRhythm.RealTime ? real : new RhythmPlan(RhythmMode.Fast, WorldMillis.HourTick, TicksPerSecond);
    }

    /// <summary>Um ciclo do relógio (AD-197c): encerra o pulo de tempo que tem de acabar, troca o Δ se o ritmo mudou (as duas coisas
    /// no registro do jogador, para o replay) e anda um tick. <paramref name="beforeRecord"/> é chamado antes de qualquer registro
    /// (quem persiste fixa a base do registro antes de o mundo mudar).</summary>
    public RhythmPlan Cycle(PlayerService players, Action? beforeRecord = null)
    {
        var world = host.Current;
        if (AutoRhythm && players.SkipEnding(world) is { } reason)
        {
            beforeRecord?.Invoke();
            players.EndSkip(world, reason);
        }
        var plan = Plan(world, players);
        if (plan.DeltaMs != players.Log.TickDeltaMs)
        {
            beforeRecord?.Invoke();
            players.SetTickDelta(world, "host", plan.DeltaMs);
        }
        host.Clock.Tick(world, plan.DeltaMs);
        LastPlan = plan;
        return plan;
    }

    /// <summary>Avanço rápido: roda N ticks imediatamente, sem esperar tempo real e
    /// independente de pausa — é uma ação explícita do host, não o loop de tempo real.</summary>
    public void FastForward(long ticks) => host.Clock.Run(host.Current, ticks);

    /// <summary>Avança exatamente a duração de um ano do calendário do mundo atual.</summary>
    public void FastForwardOneYear() => FastForward(host.Current.Calendar.HoursPerYear);
}

/// <summary>Modo do relógio (AD-197c): rápido (uma hora por tick, sem jogador), tempo real (Δ fino na escala do mundo), acelerado
/// (admin ou pulo de quem joga sozinho) ou manual (Δ fixado pelo admin, ou ritmo automático desligado).</summary>
public enum RhythmMode { Fast, RealTime, Accelerated, Manual }

/// <summary>Δ do tick e velocidade do laço (ticks por segundo real) de um ciclo.</summary>
public readonly record struct RhythmPlan(RhythmMode Mode, int DeltaMs, double TicksPerSecond);

/// <summary>Pausa global de admin (ADR-0015): estado do hospedeiro, fora do snapshot e do hash.</summary>
public sealed record AdminPauseState(string Actor, string Reason, long PausedAtTick, long MaxTicks);
