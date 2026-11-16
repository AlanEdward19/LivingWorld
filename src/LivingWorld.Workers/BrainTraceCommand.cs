using System.Globalization;
using LivingWorld.Domain.Behavior;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Sonda do cérebro (AD-160): <c>brain-trace &lt;seed-inicial&gt; &lt;seeds&gt; &lt;anos&gt;</c>. Roda o cenário padrão com o cérebro
/// ligado (<c>LW_BRAIN_VARIANT</c> escolhe a variante, ver <see cref="BrainVariant"/>) e, em cada decisão urgente, compara a escolha com
/// os ganhos do NPC à da utilidade pura no mesmo estado (<see cref="BrainDecisionTally"/>). Imprime, por década, quantas decisões o
/// cérebro trocou, a matriz utilidade → cérebro e alguns casos. Só lê o mundo.</summary>
public static class BrainTraceCommand
{
    public static int Run(ulong firstSeed, int seeds, int years)
    {
        if (Environment.GetEnvironmentVariable("LW_TRACE_AWAY") == "1") return Away(firstSeed, seeds, years);
        var inv = CultureInfo.InvariantCulture;
        BrainDecisionTally.Diagnose = true;
        var rules = BrainVariant.FromEnvironment() ?? LivingWorld.Domain.Cognition.BrainRules.Default;
        var outputs = new string[seeds];
        Parallel.For(0, seeds, i =>
        {
            ulong seed = firstSeed + (ulong)i;
            var w = new StringWriter(inv);
            var (world, clock) = BrainDecisionTally.CreateWorld(seed, rules);
            for (int decade = 0; decade * 10 < years && world.AliveNpcIndex.Alive.Count > 0; decade++)
            {
                BrainDecisionTally.Reset();
                var tally = BrainDecisionTally.Current;
                tally.MaxSamples = decade == 0 || decade * 10 + 10 >= years ? 6 : 0;
                clock.Run(world, Math.Min(10, years - decade * 10) * world.Calendar.HoursPerYear);
                w.WriteLine($"seed={seed} anos={decade * 10}-{Math.Min(years, decade * 10 + 10)} vivos={world.AliveNpcIndex.Alive.Count} "
                    + $"decisoes={tally.Decisions} trocadas={Pct(tally.Changed, tally.Decisions)} margem_media={tally.MarginSum / Math.Max(1, tally.Decisions):F2} "
                    + $"criticas={tally.Critical} criticas_trocadas={Pct(tally.CriticalChanged, tally.Critical)} "
                    + $"criticas_fora_da_comida={tally.CriticalAwayFromSurvival}");
                var pairs = new List<(long N, string Pair)>();
                for (int a = 0; a < tally.Matrix.GetLength(0); a++)
                    for (int b = 0; b < tally.Matrix.GetLength(1); b++)
                        if (a != b && tally.Matrix[a, b] > 0) pairs.Add((tally.Matrix[a, b], $"{(ActionType)a}->{(ActionType)b}"));
                w.WriteLine("  trocas: " + string.Join(" ", pairs.OrderByDescending(p => p.N).Take(8).Select(p => $"{p.Pair}={p.N}")));
                // AD-160b: ganho médio por (ação, grupo) entre os vivos, os maiores em módulo (viés sistemático aparece aqui)
                var brains = world.Npcs.Where(n => n.IsAlive && n.Brain is not null).Select(n => n.Brain!).ToList();
                if (brains.Count > 0)
                {
                    var means = Enumerable.Range(0, LivingWorld.Domain.Cognition.BrainState.Actions * LivingWorld.Domain.Cognition.BrainState.Groups)
                        .Select(i => (I: i, M: brains.Average(b => (double)b.Gains[i]))).OrderByDescending(x => Math.Abs(x.M)).Take(8);
                    w.WriteLine("  ganhos_medios: " + string.Join(" ", means.Select(x =>
                        $"{(ActionType)(x.I / LivingWorld.Domain.Cognition.BrainState.Groups)}.{(LivingWorld.Simulation.Behavior.Decision.UtilityGroup)(x.I % LivingWorld.Domain.Cognition.BrainState.Groups)}={x.M:F1}")));
                }
                foreach (var s in tally.Samples) w.WriteLine("  caso " + s);
            }

            outputs[i] = w.ToString();
        });
        foreach (var o in outputs) Console.Write(o);
        return 0;
    }

    /// <summary>AD-160b: quem está com sede ou fome crítica longe de casa, o que está fazendo (ação, propósito da viagem, faixa de
    /// idade, empregado), com o cérebro desligado e ligado, mesmas seeds. Horas-pessoa somadas.</summary>
    private static int Away(ulong firstSeed, int seeds, int years)
    {
        var rules = BrainVariant.FromEnvironment() ?? LivingWorld.Domain.Cognition.BrainRules.Default;
        var lines = new string[seeds * 2];
        Parallel.For(0, seeds * 2, k =>
        {
            ulong seed = firstSeed + (ulong)(k / 2);
            bool on = k % 2 == 1;
            var (world, clock) = on ? BrainDecisionTally.CreateWorld(seed, rules) : ScenarioRunner.Create(seed);
            var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
            long hours = (long)years * world.Calendar.HoursPerYear;
            for (long h = 0; h < hours && world.AliveNpcIndex.Alive.Count > 0; h++)
            {
                clock.Run(world, 1);
                long now = world.CurrentDate.TotalHours;
                foreach (var npc in world.AliveNpcIndex.Alive)
                {
                    if (npc.ThirstAt(now) >= 20 && npc.HungerAt(now) >= 20) continue;
                    var home = npc.Household is { } hid ? world.Households.FirstOrDefault(x => x.Id == hid) : null;
                    if (home is null || home.IsHomeless || npc.CurrentLocation == home.Location) continue;
                    int age = npc.AgeYears(world.CurrentDate);
                    string purpose = npc.TravelPurpose is { } tp ? tp.ToString() : "-";
                    string stage = age < 16 ? "crianca" : age < 65 ? "adulto" : "velho";
                    string key = npc.CurrentAction + "/" + purpose + "/" + stage + "/" + (npc.Employer is null ? "sem" : "emp");
                    counts[key] = counts.GetValueOrDefault(key) + 1;
                }
            }
            lines[k] = "seed=" + seed + " cerebro=" + (on ? 1 : 0) + " total=" + counts.Values.Sum() + " "
                + string.Join(" ", counts.OrderByDescending(c => c.Value).Take(10).Select(c => c.Key + "=" + c.Value));
        });
        foreach (var l in lines) Console.WriteLine(l);
        return 0;
    }

    private static string Pct(long a, long b) => b == 0 ? "-" : (100.0 * a / b).ToString("F2", CultureInfo.InvariantCulture) + "%";
}

/// <summary>Variantes do cérebro para os relatórios (AD-160): <c>LW_BRAIN_VARIANT</c> vazio = <see cref="LivingWorld.Domain.Cognition.BrainRules.Default"/>.</summary>
public static class BrainVariant
{
    public static LivingWorld.Domain.Cognition.BrainRules? FromEnvironment() =>
        Environment.GetEnvironmentVariable("LW_BRAIN_VARIANT") is { Length: > 0 } name
            ? LivingWorld.Domain.Cognition.BrainRules.Variant(name)
            : null;
}
