using System.Text;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Tests.LongRunning.Performance;

/// <summary>Experimento do ADR-0046 (AD-142), não é gate: vila padrão com e sem cérebro, mesmas seeds. Rode com
/// <c>LW_BRAIN_OUT=arquivo</c> (opcional <c>LW_BRAIN_SEEDS</c>, <c>LW_BRAIN_YEARS</c>). Escreve por seed e braço: vivos,
/// diversidade de dias entre adultos do mesmo ofício (distância L1 média entre os perfis hora × ação dos
/// últimos 30 dias), bytes de snapshot (JSON por vivo; positional base + 12 diffs mensais do último ano por NPC-ano) e, ligado,
/// o tamanho dos ganhos e a correlação pais → filho.</summary>
[Trait("Category", "Scenario")]
[Trait("Category", "Measure")]
[Trait("Category", "Report")] // sem asserção: fora do gate (scripts/test.sh), roda no --nightly e quando pedido no --filter
public class BrainExperimentHarness
{
    private static int Env(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out int v) && v > 0 ? v : fallback;

    [Fact]
    public void Measure()
    {
        int seeds = Env("LW_BRAIN_SEEDS", 5), years = Env("LW_BRAIN_YEARS", 10);
        var sb = new StringBuilder();
        // Cada (seed, braço) é um mundo independente: em paralelo, com as linhas na mesma ordem de antes (seed, sem e com cérebro).
        var arms = Enumerable.Range(1, seeds).SelectMany(s => new[] { ((ulong)s, false), ((ulong)s, true) }).ToArray();
        var lines = new string[arms.Length];
        Parallel.For(0, arms.Length, new ParallelOptions { MaxDegreeOfParallelism = LivingWorld.Tests.Shared.ParallelSeeds.Degree },
            i => lines[i] = Run(arms[i].Item1, years, arms[i].Item2));
        foreach (var line in lines) sb.AppendLine(line);
        File.WriteAllText(Environment.GetEnvironmentVariable("LW_BRAIN_OUT") ?? Path.Combine(Path.GetTempPath(), "lw-brain.txt"), sb.ToString());
    }

    private static string Run(ulong seed, int years, bool brain)
    {
        var (world, _) = ScenarioRunner.Create(seed);
        // AD-160: LW_BRAIN_VARIANT=frozen liga o mecanismo com ganhos presos em zero (separa os bytes do que muda a cada decisão)
        var rules = LivingWorld.Domain.Cognition.BrainRules.Variant(Environment.GetEnvironmentVariable("LW_BRAIN_VARIANT"))
            ?? LivingWorld.Domain.Cognition.BrainRules.Default;
        if (brain) world.UseBrain(rules);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems());
        int hoursPerDay = world.Calendar.HoursPerDay, month = 30 * hoursPerDay;
        clock.Run(world, (years - 1) * world.Calendar.HoursPerYear);

        long bytes = PositionalSnapshot.EncodeFull(world, out var snapshotBase).Length;
        // AD-160b: os mesmos snapshots do mesmo mundo sem o estado do cérebro (tirado e devolvido): a diferença é o custo do cérebro
        // em bytes sem o ruído de comparar mundos que divergiram
        SnapshotBase? bareBase = null;
        long bareBytes = brain ? WithoutBrains(world, () => PositionalSnapshot.EncodeFull(world, out bareBase).Length) : 0;
        // AD-160b: o que do cérebro muda entre a base do ano e o fim (cada campo mudado entra em todo delta seguinte)
        var startBrains = world.Npcs.Where(n => n.IsAlive && n.Brain is not null)
            .ToDictionary(n => n.Id.Value, n => (Gains: (short[])n.Brain!.Gains.Clone(), Means: (short[])n.Brain.ActionMeans.Clone(),
                Pending: n.Brain.Pending));
        double aliveSum = 0;
        var profiles = new Dictionary<long, (ProfessionType Profession, int[] Counts)>();
        for (int m = 0; m < 12; m++)
        {
            if (m < 11) clock.Run(world, month);
            else
                for (int h = 0; h < month; h++)
                {
                    int hour = (int)(world.CurrentDate.TotalHours % hoursPerDay);
                    foreach (var npc in world.Npcs)
                    {
                        if (!npc.IsAlive || npc.Profession == ProfessionType.None || npc.AgeYears(world.CurrentDate) < 16) continue;
                        if (!profiles.TryGetValue(npc.Id.Value, out var p))
                            profiles[npc.Id.Value] = p = (npc.Profession, new int[hoursPerDay * 10]);
                        if (npc.CurrentAction is { } a) p.Counts[hour * 10 + (int)a]++;
                    }
                    clock.Run(world, 1);
                }
            aliveSum += world.Npcs.Count(n => n.IsAlive);
            bytes += PositionalSnapshot.EncodeDelta(world, snapshotBase).Length;
            if (brain) bareBytes += WithoutBrains(world, () => PositionalSnapshot.EncodeDelta(world, bareBase!).Length);
        }

        var alive = world.Npcs.Where(n => n.IsAlive).ToList();
        int json = Encoding.UTF8.GetByteCount(WorldSnapshot.Serialize(world));
        string line = $"seed={seed} brain={(brain ? 1 : 0)} alive={alive.Count} "
            + $"dayDiversity={DayDiversity(profiles):F4} jsonPerAlive={json / Math.Max(1, alive.Count)} "
            + $"bytesPerNpcYear={bytes / Math.Max(1.0, aliveSum / 12):F0}"
            + (brain ? $" brainBytesShare={100.0 * (bytes - bareBytes) / Math.Max(1, bareBytes):F2}%" : "");
        if (!brain) return line;
        int same = 0, gainsChanged = 0, gainCells = 0, meansChanged = 0, pendingChanged = 0;
        long brainJson = 0;
        foreach (var n in alive.Where(n => n.Brain is not null))
        {
            brainJson += System.Text.Json.JsonSerializer.Serialize(n.Brain).Length;
            if (!startBrains.TryGetValue(n.Id.Value, out var b0)) continue;
            same++;
            int cells = n.Brain!.Gains.Where((g, i) => g != b0.Gains[i]).Count();
            gainCells += cells;
            if (cells > 0) gainsChanged++;
            if (!n.Brain.ActionMeans.SequenceEqual(b0.Means)) meansChanged++;
            if (n.Brain.Pending != b0.Pending) pendingChanged++;
        }
        line += $" brainJsonPerNpc={brainJson / Math.Max(1, alive.Count(n => n.Brain is not null))} yearChanged[gains={gainsChanged / (double)Math.Max(1, same):F2}"
            + $" cells={gainCells / (double)Math.Max(1, same):F1} means={meansChanged / (double)Math.Max(1, same):F2} pending={pendingChanged / (double)Math.Max(1, same):F2}]";

        var gains = alive.Where(n => n.Brain is not null).SelectMany(n => n.Brain!.Gains).ToList();
        var brainRules = world.BrainRules!;
        double meanAbs = gains.Count == 0 ? 0 : gains.Average(g => Math.Abs((double)g));
        double saturated = gains.Count == 0 ? 0 : gains.Count(g => g <= brainRules.MinGain || g >= brainRules.MaxGain) / (double)gains.Count;
        return line + $" withBrain={alive.Count(n => n.Brain is not null)} meanAbsGain={meanAbs:F1} saturated={saturated:P2} "
            + $"parentChildCorr={ParentChildCorrelation(world):F3}";
    }

    /// <summary>Roda <paramref name="encode"/> com o cérebro de todos os NPCs tirado e o devolve igual depois (só medida).</summary>
    private static long WithoutBrains(WorldState world, Func<long> encode)
    {
        var saved = world.Npcs.Where(n => n.Brain is not null).Select(n => (Npc: n, Brain: n.Brain)).ToList();
        foreach (var (npc, _) in saved) npc.SetBrain(null);
        try { return encode(); }
        finally { foreach (var (npc, brain) in saved) npc.SetBrain(brain); }
    }

    /// <summary>Média, por ofício, da distância L1 entre os perfis normalizados (hora do dia × ação) de cada par de adultos.</summary>
    private static double DayDiversity(Dictionary<long, (ProfessionType Profession, int[] Counts)> profiles)
    {
        double sum = 0;
        long pairs = 0;
        foreach (var group in profiles.Values.GroupBy(p => p.Profession))
        {
            var vectors = group.Select(p => { double t = Math.Max(1, p.Counts.Sum()); return p.Counts.Select(c => c / t).ToArray(); }).ToList();
            for (int i = 0; i < vectors.Count; i++)
                for (int j = i + 1; j < vectors.Count; j++)
                {
                    double d = 0;
                    for (int k = 0; k < vectors[i].Length; k++) d += Math.Abs(vectors[i][k] - vectors[j][k]);
                    sum += d;
                    pairs++;
                }
        }

        return pairs == 0 ? 0 : sum / pairs;
    }

    /// <summary>Correlação entre a média dos ganhos dos pais e os do filho, sobre todos os vivos com pai e mãe com cérebro.</summary>
    private static double ParentChildCorrelation(WorldState world)
    {
        var xs = new List<double>();
        var ys = new List<double>();
        foreach (var child in world.Npcs.Where(n => n.IsAlive && n.Brain is not null))
        {
            if (child.MotherId is not { } m || child.FatherId is not { } f) continue;
            if (world.FindNpc(m)?.Brain is not { } mother || world.FindNpc(f)?.Brain is not { } father) continue;
            for (int i = 0; i < child.Brain!.Gains.Length; i++)
            {
                xs.Add((mother.Gains[i] + father.Gains[i]) / 2.0);
                ys.Add(child.Brain.Gains[i]);
            }
        }

        if (xs.Count < 2) return double.NaN;
        double mx = xs.Average(), my = ys.Average(), sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < xs.Count; i++) { sxy += (xs[i] - mx) * (ys[i] - my); sxx += (xs[i] - mx) * (xs[i] - mx); syy += (ys[i] - my) * (ys[i] - my); }
        return sxx <= 0 || syy <= 0 ? double.NaN : sxy / Math.Sqrt(sxx * syy);
    }
}
