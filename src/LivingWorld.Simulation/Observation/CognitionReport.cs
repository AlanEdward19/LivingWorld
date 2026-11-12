using System.Globalization;
using System.Text;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Simulation.Observation;

/// <summary>Medida da vida interior (AD-138), só leitura: uso do tempo por adulto e por mês (trabalho e convívio), o ano antes
/// e depois da morte de cônjuge ou filho (luto), a dispersão do dia entre gente do mesmo ofício e as lembranças guardadas.
/// O hash é o mesmo com ou sem ela.</summary>
public sealed partial class CognitionTally(WorldState world, int months) : IWorldEventSink
{
    private const int AdultFrom = 18, AdultUntil = 60;
    private readonly Dictionary<long, (int[] Work, int[] Social)> _use = [];
    public List<(int Month, long Mourner, bool Child)> Bereavements { get; } = [];
    public double DispersionSum, DispersionPairs;
    private readonly Dictionary<long, (int Profession, byte[] Hours)> _day = [];

    public void Record(WorldEvent evt)
    {
        if (evt.SourceSystem != "NpcDeath" || evt.Kind is not (WorldEventKind.Death or WorldEventKind.Starvation
                or WorldEventKind.Dehydration or WorldEventKind.Exhaustion or WorldEventKind.Disease or WorldEventKind.MaternalDeath))
            return;
        if (!long.TryParse(evt.Payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id)) return;
        if (world.FindNpc(new NpcId(id)) is not { } dead) return;
        int month = (int)(evt.Tick / world.Calendar.HoursPerMonth);
        if (dead.Spouse is { } s && world.FindNpc(s) is { IsAlive: true } spouse && spouse.Spouse == dead.Id && IsAdult(spouse))
            Bereavements.Add((month, spouse.Id.Value, false));
        foreach (var parentId in new[] { dead.MotherId, dead.FatherId })
            if (parentId is { } p && world.FindNpc(p) is { IsAlive: true } parent && IsAdult(parent))
                Bereavements.Add((month, parent.Id.Value, true));
    }

    private bool IsAdult(Npc npc) => npc.AgeYears(world.CurrentDate) is >= AdultFrom and < AdultUntil;

    /// <summary>Uma hora: soma trabalho e convívio de cada adulto no mês; no dia 15 guarda a ação de cada hora.</summary>
    public void SampleHour()
    {
        var date = world.CurrentDate;
        int month = (int)(date.TotalHours / world.Calendar.HoursPerMonth);
        if (month >= months) return;
        bool sampleDay = date.Day == 15;
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            if (!IsAdult(npc)) continue;
            var action = npc.CurrentAction;
            bool travelToWork = action == ActionType.Travel && npc.TravelPurpose == ActionType.Work;
            if (action == ActionType.Work || action == ActionType.Socialize || travelToWork)
            {
                if (!_use.TryGetValue(npc.Id.Value, out var use)) _use[npc.Id.Value] = use = (new int[months], new int[months]);
                if (action == ActionType.Socialize) use.Social[month]++;
                else use.Work[month]++;
            }

            if (sampleDay)
            {
                if (!_day.TryGetValue(npc.Id.Value, out var day)) _day[npc.Id.Value] = day = (npc.Profession.Id, new byte[24]);
                day.Hours[date.Hour % 24] = (byte)(1 + (int)(action ?? ActionType.Idle));
            }
        }

        if (sampleDay && date.Hour == world.Calendar.HoursPerDay - 1) CloseDay();
    }

    /// <summary>Fim do dia amostrado: distância média (fração das 24 h em que fazem coisas diferentes) entre pares do mesmo ofício.</summary>
    private void CloseDay()
    {
        foreach (var group in _day.Where(kv => kv.Value.Profession != 0).GroupBy(kv => kv.Value.Profession).OrderBy(g => g.Key))
        {
            var days = group.OrderBy(kv => kv.Key).Select(kv => kv.Value.Hours).ToList();
            for (int i = 0; i < days.Count; i++)
                for (int j = i + 1; j < days.Count; j++)
                {
                    int differ = 0;
                    for (int h = 0; h < 24; h++) if (days[i][h] != days[j][h]) differ++;
                    DispersionSum += differ / 24.0;
                    DispersionPairs++;
                }
        }
        _day.Clear();
    }

    public (int Work, int Social) Use(long npc, int from, int to)
    {
        if (!_use.TryGetValue(npc, out var use)) return (0, 0);
        int w = 0, s = 0;
        for (int m = Math.Max(0, from); m < Math.Min(months, to); m++) { w += use.Work[m]; s += use.Social[m]; }
        return (w, s);
    }

    public bool AliveUntil(long npc, int month) =>
        world.FindNpc(new NpcId(npc)) is { } n && (n.IsAlive || n.DeathDate!.Value.TotalHours / world.Calendar.HoursPerMonth >= month);
}

public static class CognitionReport
{
    public sealed record SeedResult(ulong Seed, CognitionTally Tally, int Months, string Memory);

    public static SeedResult Run(ulong seed, int years)
    {
        var (world, baseClock) = ScenarioRunner.Create(seed);
        int months = years * world.Calendar.MonthsPerYear;
        var tally = new CognitionTally(world, months);
        var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, tally);
        long hours = (long)months * world.Calendar.HoursPerMonth;
        for (long h = 0; h < hours && world.AliveNpcIndex.Alive.Count > 0; h++)
        {
            clock.Run(world, 1);
            tally.SampleHour();
        }

        return new SeedResult(seed, tally, months, CognitionTally.MemorySummary(world));
    }

    public static string Summarize(IReadOnlyList<SeedResult> runs)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        foreach (bool child in new[] { false, true })
        {
            double wb = 0, wa = 0, sb0 = 0, sa = 0, wPrev = 0, sPrev = 0;
            var quarterWork = new double[4];
            var quarterSocial = new double[4];
            int n = 0;
            foreach (var r in runs)
                foreach (var (month, mourner, isChild) in r.Tally.Bereavements.OrderBy(b => b.Mourner).ThenBy(b => b.Month))
                {
                    if (isChild != child || month < 24 || month + 12 > r.Months || !r.Tally.AliveUntil(mourner, month + 12)) continue;
                    var before = r.Tally.Use(mourner, month - 12, month);
                    var prior = r.Tally.Use(mourner, month - 24, month - 12);
                    var after = r.Tally.Use(mourner, month, month + 12);
                    if (before.Work + before.Social == 0) continue;
                    n++;
                    wb += before.Work; wa += after.Work; sb0 += before.Social; sa += after.Social;
                    wPrev += prior.Work; sPrev += prior.Social;
                    for (int q = 0; q < 4; q++)
                    {
                        var u = r.Tally.Use(mourner, month + 3 * q, month + 3 * q + 3);
                        quarterWork[q] += u.Work;
                        quarterSocial[q] += u.Social;
                    }
                }

            string who = child ? "filho" : "conjuge";
            sb.AppendLine(inv, $"luto_{who} n={n} trabalho_depois/antes={Ratio(wa, wb):F3} convivio_depois/antes={Ratio(sa, sb0):F3} controle(ano_antes/ano_anterior) trabalho={Ratio(wb, wPrev):F3} convivio={Ratio(sb0, sPrev):F3}");
            sb.Append(inv, $"luto_{who}_trimestres trabalho=");
            sb.Append(string.Join(",", quarterWork.Select(q => Ratio(4 * q, wb).ToString("F2", inv))));
            sb.Append(" convivio=");
            sb.AppendLine(string.Join(",", quarterSocial.Select(q => Ratio(4 * q, sb0).ToString("F2", inv))));
        }

        double pairs = runs.Sum(r => r.Tally.DispersionPairs);
        sb.AppendLine(inv, $"dispersao_do_dia_mesmo_oficio={runs.Sum(r => r.Tally.DispersionSum) / Math.Max(1, pairs):F3} (fração das 24 h em que dois do mesmo ofício fazem coisas diferentes, pares={pairs})");
        foreach (var r in runs) sb.AppendLine(inv, $"seed={r.Seed} {r.Memory}");
        return sb.ToString();
    }

    private static double Ratio(double a, double b) => b > 0 ? a / b : 0;
}
