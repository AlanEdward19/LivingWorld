using System.Globalization;
using System.Text;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Sonda de leitura (AD-164, correções #12): por que a violência doméstica leva à fome. Marca quem estava na casa de
/// uma agressão, quem apanhou, quem perdeu alguém da casa pela agressão (morto, ou o agressor enforcado ou banido) e quem se
/// separou depois dela (até 5 anos), e conta as horas com fome crítica (déficit acima de 80) e a exposição de cada grupo, em todos e nos
/// menores de 5. Para cada agressão, as horas de fome crítica dos moradores 90 dias antes e 90 dias depois (a fome também
/// causa a agressão, então só o depois contra o antes separa a causa). Só observa o mundo.</summary>
public sealed class DomesticHungerProbe(WorldState world) : IWorldEventSink
{
    public static readonly string[] Groups = ["vitima", "perda", "separacao", "casa", "casa_antiga", "outro"];
    public long MeasureFromTick { get; init; }
    public long[] CritHours { get; } = new long[Groups.Length];
    public long[] CritHoursUnder5 { get; } = new long[Groups.Length];
    public double[] PersonYears { get; } = new double[Groups.Length];
    public double[] PersonYearsUnder5 { get; } = new double[Groups.Length];
    public long[] Starved { get; } = new long[Groups.Length];
    public long Assaults, Killed, AggressorRemoved, HungerMotive, SeparationsAfter, VictimAdultHours, VictimWorkHours;
    public long BeforeHours, AfterHours, BeforeHoursUnder5, AfterHoursUnder5, MemberWindows, MemberWindowsUnder5;
    private readonly Dictionary<long, long> _victim = [], _house = [], _loss = [], _separated = [], _aggressorOf = [];
    /// <summary>AD-165: todos os separados (com ou sem agressão) por 5 anos, fora da precedência dos grupos, e o sustento pago.</summary>
    private readonly Dictionary<long, long> _sepAll = [];
    public long SepAllCrit, SepAllCritUnder5, SupportPayments, SupportFood, SupportCoin;
    public double SepAllPersonYears, SepAllPersonYearsUnder5;
    private readonly Dictionary<long, List<long>> _crit = [];
    private readonly List<(long Tick, long[] Members, bool[] Under5)> _events = [];
    private readonly Dictionary<long, long> _victimWindow = [];

    public void Record(WorldEvent evt)
    {
        long now = evt.Tick;
        switch (evt.Kind)
        {
            case WorldEventKind.DomesticViolence:
                {
                    var f = evt.Payload.Split('|');
                    if (f.Length < 6 || !long.TryParse(f[0], CultureInfo.InvariantCulture, out long aggressor)
                        || !long.TryParse(f[1], CultureInfo.InvariantCulture, out long victim)) return;
                    var members = MembersOf(aggressor);
                    foreach (var m in members) { _house[m] = now; _aggressorOf[m] = aggressor; }
                    if (now < MeasureFromTick) return;
                    Assaults++;
                    if (f[3] == "fome") HungerMotive++;
                    if (f[5] == "morto") { Killed++; Lose(members, now); }
                    else { _victim[victim] = now; if (Adult(victim)) _victimWindow[victim] = now; }
                    _events.Add((now, members, members.Select(Under5).ToArray()));
                    return;
                }
            case WorldEventKind.Executed or WorldEventKind.Banished:
                {
                    int bar = evt.Payload.IndexOf('|');
                    if (!long.TryParse(bar < 0 ? evt.Payload : evt.Payload[..bar], CultureInfo.InvariantCulture, out long who)) return;
                    if (!_house.TryGetValue(who, out long t) || now - t > 2 * world.Calendar.HoursPerYear || _aggressorOf.GetValueOrDefault(who) != who) return;
                    if (now >= MeasureFromTick) AggressorRemoved++;
                    Lose(MembersOf(who), now);
                    return;
                }
            case WorldEventKind.Starvation when evt.SourceSystem == YearEventTally.DeathSource && now >= MeasureFromTick:
                {
                    if (long.TryParse(evt.Payload, CultureInfo.InvariantCulture, out long dead)) Starved[GroupOf(dead, now)]++;
                    return;
                }
            case WorldEventKind.SupportPaid when now >= MeasureFromTick:
                {
                    var f = evt.Payload.Split('|');
                    SupportPayments++;
                    if (f.Length > 3 && long.TryParse(f[2], CultureInfo.InvariantCulture, out long food)
                        && long.TryParse(f[3], CultureInfo.InvariantCulture, out long coin)) { SupportFood += food; SupportCoin += coin; }
                    return;
                }
            case WorldEventKind.UnionEnded:
                {
                    var f = evt.Payload.Split('|');
                    if (f.Length < 2 || !long.TryParse(f[0], CultureInfo.InvariantCulture, out long a)
                        || !long.TryParse(f[1], CultureInfo.InvariantCulture, out long b)) return;
                    foreach (var m in MembersOf(a).Concat(MembersOf(b))) _sepAll[m] = now;
                    bool afterAssault = Recent(_house, a, now, 3) || Recent(_house, b, now, 3);
                    if (!afterAssault) return;
                    if (now >= MeasureFromTick) SeparationsAfter++;
                    foreach (var m in MembersOf(a).Concat(MembersOf(b))) _separated[m] = now;
                    return;
                }
        }
    }

    /// <summary>De hora em hora: a fome crítica de cada um, no grupo de maior precedência; o trabalho de quem apanhou (90 dias).</summary>
    public void SampleHour()
    {
        long now = world.CurrentDate.TotalHours;
        if (now < MeasureFromTick) return;
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            if (100 - npc.HungerAt(now) <= NeedDistressTally.Critical) continue;
            long id = npc.Id.Value;
            int g = GroupOf(id, now);
            CritHours[g]++;
            if (npc.AgeYears(world.CurrentDate) < 5) CritHoursUnder5[g]++;
            if (Recent(_sepAll, id, now, 5)) { SepAllCrit++; if (npc.AgeYears(world.CurrentDate) < 5) SepAllCritUnder5++; }
            if (!_crit.TryGetValue(id, out var list)) _crit[id] = list = [];
            list.Add(now);
        }
        if (_victimWindow.Count == 0) return;
        long window = 90L * world.Calendar.HoursPerDay;
        foreach (var (id, start) in _victimWindow.OrderBy(kv => kv.Key).ToList())
        {
            if (now - start > window || world.FindNpc(new NpcId(id)) is not { IsAlive: true } v) { _victimWindow.Remove(id); continue; }
            VictimAdultHours++;
            if (v.CurrentAction == ActionType.Work) VictimWorkHours++;
        }
    }

    /// <summary>Fim do mês: exposição de cada grupo.</summary>
    public void SampleMonth()
    {
        long now = world.CurrentDate.TotalHours;
        if (now < MeasureFromTick) return;
        double share = 1.0 / world.Calendar.MonthsPerYear;
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            int g = GroupOf(npc.Id.Value, now);
            PersonYears[g] += share;
            if (npc.AgeYears(world.CurrentDate) < 5) PersonYearsUnder5[g] += share;
            if (Recent(_sepAll, npc.Id.Value, now, 5)) { SepAllPersonYears += share; if (npc.AgeYears(world.CurrentDate) < 5) SepAllPersonYearsUnder5 += share; }
        }
    }

    /// <summary>Fim da rodada: horas de fome crítica dos moradores 90 dias antes e depois de cada agressão.</summary>
    public void Finish()
    {
        long window = 90L * world.Calendar.HoursPerDay, end = world.CurrentDate.TotalHours;
        foreach (var (tick, members, under5) in _events)
        {
            if (tick - window < MeasureFromTick || tick + window > end) continue;
            for (int i = 0; i < members.Length; i++)
            {
                if (!_crit.TryGetValue(members[i], out var list)) list = [];
                long before = list.Count(t => t >= tick - window && t < tick), after = list.Count(t => t >= tick && t < tick + window);
                MemberWindows++; BeforeHours += before; AfterHours += after;
                if (under5[i]) { MemberWindowsUnder5++; BeforeHoursUnder5 += before; AfterHoursUnder5 += after; }
            }
        }
    }

    private int GroupOf(long id, long now) =>
        Recent(_victim, id, now, 1) ? 0 : Recent(_loss, id, now, 2) ? 1 : Recent(_separated, id, now, 5) ? 2 : Recent(_house, id, now, 1) ? 3 : Recent(_house, id, now, 5) ? 4 : 5;

    private bool Recent(Dictionary<long, long> marks, long id, long now, int years) =>
        marks.TryGetValue(id, out long t) && now - t <= years * world.Calendar.HoursPerYear;

    private void Lose(long[] members, long now) { foreach (var m in members) _loss[m] = now; }

    private long[] MembersOf(long id) =>
        world.FindNpc(new NpcId(id)) is { Household: { } h } && world.FindHousehold(h) is { } home
            ? home.Members.Select(m => m.Value).OrderBy(v => v).ToArray() : [id];

    private bool Under5(long id) => world.FindNpc(new NpcId(id)) is { } n && n.AgeYears(world.CurrentDate) < 5;
    private bool Adult(long id) => world.FindNpc(new NpcId(id)) is { } n && n.AgeYears(world.CurrentDate) is >= 16 and <= 64;

    /// <summary>Texto `chave=valor` que a sonda acrescenta à linha `medida` da seed.</summary>
    public void Append(StringBuilder sb)
    {
        var inv = CultureInfo.InvariantCulture;
        sb.Append(inv, $" dv_agressoes={Assaults} dv_mortos={Killed} dv_agressor_tirado={AggressorRemoved} dv_motivo_fome={HungerMotive} dv_separacoes={SeparationsAfter}");
        sb.Append(inv, $" dv_vit_h={VictimAdultHours} dv_vit_trab_h={VictimWorkHours} dv_janelas={MemberWindows} dv_antes_h={BeforeHours} dv_depois_h={AfterHours} dv_janelas5={MemberWindowsUnder5} dv_antes5_h={BeforeHoursUnder5} dv_depois5_h={AfterHoursUnder5}");
        sb.Append(inv, $" dvg.sep_todos={SepAllCrit}/{SepAllPersonYears:F2}/{SepAllCritUnder5}/{SepAllPersonYearsUnder5:F2}/0 dv_sustento={SupportPayments} dv_sustento_comida={SupportFood} dv_sustento_moeda={SupportCoin}");
        for (int g = 0; g < Groups.Length; g++)
            sb.Append(inv, $" dvg.{Groups[g]}={CritHours[g]}/{PersonYears[g]:F2}/{CritHoursUnder5[g]}/{PersonYearsUnder5[g]:F2}/{Starved[g]}");
    }
}
