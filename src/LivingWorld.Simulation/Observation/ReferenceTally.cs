using System.Globalization;
using System.Text;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Contadores da rodada de referência (AD-163, `a-medir` grupos 1 e 3) que os outros relatórios não guardam por seed:
/// esposas agredidas por ano (o que a OMS mede, backlog correções #12), o CV mínimo de vitalidade ano a ano (08 item 14), a volta
/// da casa depois de uma compra cortada pela carga (08b item 77) e as lembranças por tipo no fim. Só lê eventos e o mundo
/// (amostra mensal e anual); o hash é o mesmo com ou sem ela.</summary>
public sealed class ReferenceTally(WorldState world) : IWorldEventSink
{
    public const int ReturnBins = 24 * 60; // horas até a próxima ida da mesma casa, até 60 dias; o resto cai no último
    public long MeasureFromTick { get; init; }
    public double WifeYears;
    public long WivesAssaulted, WifeAssaults;
    public double MinVitalityCv = double.MaxValue;
    public long[] ReturnAfterCut { get; } = new long[ReturnBins + 1];
    public long[] ReturnAfterTrip { get; } = new long[ReturnBins + 1];
    public long CutTrips, Trips;
    public SortedDictionary<string, long> Memories { get; } = new(StringComparer.Ordinal);
    private readonly Dictionary<long, (long Tick, bool Cut)> _lastTrip = [];
    private readonly HashSet<long> _victimsThisYear = [];
    private int _year = -1;

    /// <summary>Liga a sonda de idas ao mercado do mundo (só observa).</summary>
    public void Attach() => world.TripProbe = OnTrip;

    private void OnTrip(long household, long cutG)
    {
        long now = world.CurrentDate.TotalHours;
        if (now < MeasureFromTick) return;
        if (_lastTrip.TryGetValue(household, out var last))
        {
            long gap = Math.Min(ReturnBins, now - last.Tick);
            (last.Cut ? ReturnAfterCut : ReturnAfterTrip)[gap]++;
        }
        Trips++;
        if (cutG > 0) CutTrips++;
        _lastTrip[household] = (now, cutG > 0);
    }

    public void Record(WorldEvent evt)
    {
        if (evt.Kind != WorldEventKind.DomesticViolence || evt.Tick < MeasureFromTick) return;
        var f = evt.Payload.Split('|');
        if (f.Length < 3 || f[2] != "conjuge" || !long.TryParse(f[1], CultureInfo.InvariantCulture, out long victim)) return;
        if (world.FindNpc(new NpcId(victim)) is not { Sex: Sex.Female }) return;
        WifeAssaults++;
        int year = (int)(evt.Tick / world.Calendar.HoursPerYear);
        if (year != _year) { _year = year; _victimsThisYear.Clear(); }
        if (_victimsThisYear.Add(victim)) WivesAssaulted++;
    }

    /// <summary>Fim do mês: anos de esposa (mulher com cônjuge vivo).</summary>
    public void SampleMonth()
    {
        if (world.CurrentDate.TotalHours < MeasureFromTick) return;
        double share = 1.0 / world.Calendar.MonthsPerYear;
        foreach (var npc in world.AliveNpcIndex.Alive)
            if (npc.Sex == Sex.Female && npc.Spouse is { } s && world.FindNpc(s) is { IsAlive: true })
                WifeYears += share;
    }

    /// <summary>Fim do ano (desde o início, como o piso do teste FAM-32): CV da vitalidade dos vivos.</summary>
    public void SampleYear()
    {
        double sum = 0, sq = 0;
        int n = 0;
        foreach (var npc in world.AliveNpcIndex.Alive) { sum += npc.Vitality; sq += npc.Vitality * npc.Vitality; n++; }
        if (n == 0 || sum <= 0) return;
        double mean = sum / n;
        MinVitalityCv = Math.Min(MinVitalityCv, Math.Sqrt(Math.Max(0, sq / n - mean * mean)) / mean);
    }

    /// <summary>Fim da rodada: lembranças dos adultos (18+) vivos por tipo.</summary>
    public void Finish()
    {
        world.TripProbe = null;
        foreach (var npc in world.AliveNpcIndex.Alive.OrderBy(n => n.Id.Value))
        {
            if (npc.AgeYears(world.CurrentDate) < 18 || npc.Psyche is not { } psyche) continue;
            foreach (var m in psyche.Memories) Memories[m.Kind.ToString()] = Memories.GetValueOrDefault(m.Kind.ToString()) + 1;
        }
    }

    public static double Median(long[] bins)
    {
        long total = bins.Sum(), half = (total + 1) / 2, run = 0;
        for (int i = 0; i < bins.Length; i++) { run += bins[i]; if (total > 0 && run >= half) return i; }
        return double.NaN;
    }

    public static double ShareBelow(long[] bins, int hours)
    {
        long total = bins.Sum();
        return total == 0 ? double.NaN : bins.Take(hours).Sum() / (double)total;
    }

    /// <summary>Texto `chave=valor` do que esta medida acrescenta à linha `medida` da seed.</summary>
    public void Append(StringBuilder sb)
    {
        var inv = CultureInfo.InvariantCulture;
        sb.Append(inv, $" esposas_agredidas={WivesAssaulted} agressoes_esposa={WifeAssaults} anos_esposa={WifeYears:F1} cv_vit_min={(MinVitalityCv == double.MaxValue ? 0 : MinVitalityCv):F4}");
        sb.Append(inv, $" idas={Trips} idas_cortadas={CutTrips} volta_corte_med_h={Median(ReturnAfterCut):F0} volta_normal_med_h={Median(ReturnAfterTrip):F0} volta_corte_24h={ShareBelow(ReturnAfterCut, 24):F3} volta_normal_24h={ShareBelow(ReturnAfterTrip, 24):F3} n_volta_corte={ReturnAfterCut.Sum()}");
        foreach (var (kind, n) in Memories) sb.Append(inv, $" mem.{kind}={n}");
    }
}
