namespace LivingWorld.Api.Simulation;

/// <summary>#248: o prazo do laço do relógio. O período é 1/tps contado de prazo em prazo (relógio monotônico), e o tempo que o ciclo gasta sai
/// da espera: ciclo de 10 ms num período de 50 ms espera 40, não 50. Atrasado, a espera é zero até recuperar; atrasado mais que
/// <see cref="MaxLagMs"/> (ou <see cref="MaxLagPeriods"/> períodos) desiste do atraso em vez de correr atrás dele. Troca de ritmo (tps)
/// recomeça o prazo de agora.</summary>
public sealed class LoopPacer
{
    public const double MaxLagMs = 250;
    public const double MaxLagPeriods = 5;

    private double _nextMs;
    private double _lastTps = -1;

    /// <summary>Quanto esperar (ms; zero ou menos: nada) depois de um ciclo que acabou em <paramref name="nowMs"/> com o ritmo <paramref name="tps"/>.</summary>
    public double DelayMs(double nowMs, double tps)
    {
        double period = 1000.0 / Math.Max(tps, 0.01);
        if (tps != _lastTps) { _lastTps = tps; _nextMs = nowMs; }
        _nextMs += period;
        if (nowMs - _nextMs > Math.Max(MaxLagMs, MaxLagPeriods * period)) _nextMs = nowMs;
        return _nextMs - nowMs;
    }
}
