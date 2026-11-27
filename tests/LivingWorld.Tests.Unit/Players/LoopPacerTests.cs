using LivingWorld.Api.Simulation;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>#248: o laço do relógio anda por prazo: o tempo do ciclo não atrasa o mundo.</summary>
public class LoopPacerTests
{
    /// <summary>Roda o laço simulado por <paramref name="seconds"/> com ciclos de <paramref name="cycleMs"/> e devolve quantos ticks couberam.</summary>
    private static int Ticks(double seconds, double cycleMs, double tps, Func<double, double>? extra = null)
    {
        var pacer = new LoopPacer();
        double now = 0;
        int ticks = 0;
        while (now < seconds * 1000)
        {
            now += cycleMs + (extra?.Invoke(now) ?? 0);
            ticks++;
            double delay = pacer.DelayMs(now, tps);
            now += Math.Max(delay, 0);
        }
        return ticks;
    }

    [Fact]
    public void A_cycle_that_takes_time_does_not_slow_the_world()
    {
        Assert.InRange(Ticks(10, 0, 20), 199, 201);
        Assert.InRange(Ticks(10, 12, 20), 199, 201); // antes: 10 s / (50 + 12) ms = 161 ticks
        Assert.InRange(Ticks(10, 30, 20), 199, 201);
    }

    [Fact]
    public void A_cycle_longer_than_the_period_runs_back_to_back_and_a_long_stall_is_forgiven()
    {
        Assert.InRange(Ticks(10, 80, 20), 120, 126); // ciclo de 80 ms num período de 50: o que dá é 1/0,08 s, sem espera
        // uma travada de 2 s no meio não vira 40 ciclos de recuperação disparados: o prazo recomeça
        int stalled = Ticks(10, 5, 20, now => now is > 4000 and < 4010 ? 2000 : 0);
        Assert.InRange(stalled, 150, 175);
    }

    [Fact]
    public void A_new_pace_restarts_the_deadline_from_now()
    {
        var pacer = new LoopPacer();
        Assert.InRange(pacer.DelayMs(100, 20), 49, 50);
        Assert.InRange(pacer.DelayMs(1000, 1), 999, 1000); // o admin baixou a velocidade: 1 tick por segundo, de agora
    }
}
