using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Shared;
using LivingWorld.Tests.Shared.Population;

namespace LivingWorld.Tests.LongRunning.Population;

/// <summary>Uma corrida compartilhada (ADR-0044): a vila padrão (<c>ScenarioRunner.Create(seed)</c>, regras de família padrão)
/// nas seeds 1..20 por 100 anos, simulada uma vez por processo e lida por vários testes que antes a recomputavam:
/// o século e o braço real de 10 anos de <see cref="FamilyDiversityCalibrationTests"/>, a população do ano 10 de
/// <see cref="FamilyPairedScenarioTests"/> e de <see cref="LaborAllocationVillageTests"/>, e as trocas de emprego dos anos 1–3.
/// Ninguém recebe o mundo: a corrida guarda só valores observados no fim de cada ano, então nenhum teste altera o que outro lê.
/// O braço de controle (deriva neutra) dos 10 primeiros anos corre ao lado, como antes, num mundo próprio.
/// Equivalência com a corrida isolada: <see cref="DefaultVillageCenturyTests"/>.</summary>
public static class DefaultVillageCentury
{
    public const int Seeds = 20;
    public const int GateYears = 10;
    public const int CenturyYears = 100;

    /// <summary>Índice = ano (0 = criação). <paramref name="LaborMoves"/> é acumulado (Fired de LaborAllocation).</summary>
    public sealed record Run(
        ulong Seed, int[] Alive, double[] VitalityCv, int[] LaborMoves,
        double GateVitalityDifference, double GateOpennessDifference, string GateHash, double CenturySelfDifference, double CenturyOpennessSelfDifference);

    private static readonly Lazy<Run[]> All = new(SimulateAll);

    /// <summary>As 20 corridas, na ordem das seeds (1..20).</summary>
    public static IReadOnlyList<Run> Runs => All.Value;

    /// <summary>O século é o caminho crítico do <c>--full</c>: com <c>LW_PREWARM_SHARED_RUNS=1</c> (o verify.sh liga) ele começa
    /// quando o assembly carrega, em vez de esperar o xUnit chegar ao primeiro leitor. Só antecipa; o resultado é o mesmo.</summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Prewarm()
    {
        if (Environment.GetEnvironmentVariable("LW_PREWARM_SHARED_RUNS") == "1")
            new Thread(() => { try { _ = All.Value; } catch { /* o leitor relança pelo Lazy */ } }) { IsBackground = true }.Start();
    }

    /// <summary>Uma thread dedicada por seed, as 20 juntas: cada seed é uma corrida longa e sequencial de 100 anos, então
    /// ondas de 8 (o grau de <see cref="ParallelSeeds"/>) deixariam a última onda sozinha no fim do gate. A falha cita a seed.</summary>
    private static Run[] SimulateAll()
    {
        var runs = new Run[Seeds];
        var failures = new Exception?[Seeds];
        var threads = Enumerable.Range(0, Seeds).Select(index => new Thread(() =>
        {
            try { runs[index] = Simulate((ulong)index + 1); }
            catch (Exception ex) { failures[index] = ex; }
        })).ToArray();
        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();
        for (int index = 0; index < Seeds; index++)
        {
            if (failures[index] is { } ex)
                throw new InvalidOperationException($"seed {index + 1} falhou: {ex.Message}", ex);
        }
        return runs;
    }

    private sealed class LaborMoveCounter : IWorldEventSink
    {
        public int Moves { get; private set; }

        public void Record(WorldEvent evt)
        {
            if (evt.Kind == WorldEventKind.Fired && evt.SourceSystem == "LaborAllocation") Moves++;
        }
    }

    private static Run Simulate(ulong seed)
    {
        var (real, _) = ScenarioRunner.Create(seed);
        var counter = new LaborMoveCounter();
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: counter);
        var control = NeutralDriftScenarioHarness.Create(seed, true);
        var alive = new int[CenturyYears + 1];
        var cv = new double[CenturyYears + 1];
        var moves = new int[CenturyYears + 1];
        alive[0] = real.Npcs.Count(n => n.IsAlive);
        cv[0] = Cv(real.Npcs.Where(n => n.IsAlive).Select(n => n.Vitality));
        double gateVitality = 0, gateOpenness = 0;
        string gateHash = "";
        for (int year = 1; year <= CenturyYears; year++)
        {
            clock.Run(real, real.Calendar.HoursPerYear);
            if (year <= GateYears) control.Clock.Run(control.World, control.World.Calendar.HoursPerYear);
            alive[year] = real.Npcs.Count(n => n.IsAlive);
            cv[year] = Cv(real.Npcs.Where(n => n.IsAlive).Select(n => n.Vitality));
            moves[year] = counter.Moves;
            if (year != GateYears) continue;
            var living = real.Npcs.Where(n => n.IsAlive).ToArray();
            var neutral = control.World.Npcs.Where(n => n.IsAlive).ToArray();
            gateVitality = Cv(living.Select(n => n.Vitality)) - Cv(neutral.Select(n => n.Vitality));
            gateOpenness = Cv(living.Select(n => (double)n.Personality.Openness)) - Cv(neutral.Select(n => (double)n.Personality.Openness));
            gateHash = WorldSnapshot.CanonicalHash(real);
        }
        // O século antigo não tinha controle: comparava a vila com ela mesma (diferença sempre 0); mantido igual.
        var last = real.Npcs.Where(n => n.IsAlive).ToArray();
        return new Run(seed, alive, cv, moves, gateVitality, gateOpenness, gateHash,
            Cv(last.Select(n => n.Vitality)) - Cv(last.Select(n => n.Vitality)),
            Cv(last.Select(n => (double)n.Personality.Openness)) - Cv(last.Select(n => (double)n.Personality.Openness)));
    }

    /// <summary>Mínimo acumulado até <paramref name="years"/> (inclusive o ano 0), na mesma ordem de dobra de antes.</summary>
    public static double MinimumCv(Run run, int years)
    {
        double minimum = run.VitalityCv[0];
        for (int year = 1; year <= years; year++) minimum = Math.Min(minimum, run.VitalityCv[year]);
        return minimum;
    }

    public static int MinimumAlive(Run run, int years) => run.Alive.Take(years + 1).Min();

    public static double Cv(IEnumerable<double> values)
    {
        double[] all = values.ToArray();
        if (all.Length == 0 || all.Average() == 0) return 0;
        double mean = all.Average();
        return Math.Sqrt(all.Sum(v => Math.Pow(v - mean, 2)) / all.Length) / mean;
    }
}
