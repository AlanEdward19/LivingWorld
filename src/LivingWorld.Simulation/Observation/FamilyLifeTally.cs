using System.Globalization;
using System.Text;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Medida da vida familiar de uma rodada (AD-136): uniões e como acabam, casos, filhos de outro pai, recasamentos,
/// crianças que moraram em mais de uma casa, irmãos criados separados e quem segue o ofício de quem o criou. Só lê o mundo
/// (no evento e na amostra mensal); nada é escrito, o hash é o mesmo com ou sem ela.</summary>
public sealed class FamilyLifeTally(WorldState world)
{
    /// <summary>Idade da amostra do ofício da casa e da comparação (anos).</summary>
    public const int RearerSampleAge = 12, CompareAge = 25;

    /// <summary>Meses de infância em casas diferentes para o par de irmãos contar como criado separado.</summary>
    public const int ApartMonths = 12;

    private readonly Dictionary<(long, long), long> _openUnions = [];
    private readonly HashSet<long> _everMarried = [];
    private readonly Dictionary<long, HashSet<long>> _homes = [];
    private readonly HashSet<long> _stepchildren = [];
    private readonly Dictionary<(long, long), (int Overlap, int Apart, bool Half)> _siblingPairs = [];
    private readonly Dictionary<long, (int Profession, double Pressure)> _rearer = [];
    private readonly HashSet<long> _compared = [];

    public long Marriages, Remarriages, EndedByDeath, Births, BirthsToMarried, OtherFatherBirths;
    public double UnionYearsEnded;
    public List<(double Husband, double Wife)> SpouseAges { get; } = [];
    public List<(double Husband, double Wife)> SpouseUpbringing { get; } = [];
    public long UnionsEnded;
    public Dictionary<string, long> EndedByMode { get; } = [];
    public long Affairs, AffairsDiscovered;
    public Dictionary<string, long> AffairOutcomes { get; } = [];
    public long FollowCompared, Followed, HighPressureCompared, HighPressureFollowed, LowPressureCompared, LowPressureFollowed;
    public long VocationChoices, VocationChoicesPressured;

    /// <summary>Pressão do ofício da casa acima da qual o jovem conta como "pressão alta" na medida.</summary>
    public double HighPressureCut { get; init; } = 0.7;

    public void Record(WorldEvent evt)
    {
        var p = evt.Payload.Split('|');
        switch (evt.Kind)
        {
            case WorldEventKind.Marriage:
                long a = Id(p[0]), b = Id(p[1]);
                Marriages++;
                if (_everMarried.Contains(a) || _everMarried.Contains(b)) Remarriages++;
                _everMarried.Add(a);
                _everMarried.Add(b);
                _openUnions[Key(a, b)] = evt.Tick;
                if (world.FindNpc(new NpcId(a)) is { } na && world.FindNpc(new NpcId(b)) is { } nb)
                {
                    var (h, w) = na.Sex == Sex.Male ? (na, nb) : (nb, na);
                    SpouseAges.Add((h.AgeYears(world.CurrentDate), w.AgeYears(world.CurrentDate)));
                    SpouseUpbringing.Add((h.Upbringing, w.Upbringing));
                }
                return;
            case WorldEventKind.UnionEnded:
                Close(Key(Id(p[0]), Id(p[1])), evt.Tick, p[2]);
                return;
            case WorldEventKind.AffairStarted:
                Affairs++;
                return;
            case WorldEventKind.AffairDiscovered:
                AffairsDiscovered++;
                AffairOutcomes[p[3]] = AffairOutcomes.GetValueOrDefault(p[3]) + 1;
                return;
            case WorldEventKind.VocationChosen:
                VocationChoices++;
                if (p[3] == "1") VocationChoicesPressured++;
                return;
            case WorldEventKind.Birth when evt.SourceSystem == YearEventTally.BirthSource:
                Births++;
                if (world.FindNpc(new NpcId(Id(p[1])))?.Spouse is { } husband)
                {
                    BirthsToMarried++;
                    if (husband.Value != Id(p[2])) OtherFatherBirths++;
                }
                return;
            case WorldEventKind.Death or WorldEventKind.Starvation or WorldEventKind.Dehydration
                or WorldEventKind.Exhaustion or WorldEventKind.Disease or WorldEventKind.MaternalDeath
                or WorldEventKind.Accident or WorldEventKind.OldAge
                when evt.SourceSystem == YearEventTally.DeathSource:
                if (!long.TryParse(p[0], CultureInfo.InvariantCulture, out long dead)) return;
                if (world.FindNpc(new NpcId(dead))?.Spouse is { } spouse)
                    Close(Key(dead, spouse.Value), evt.Tick, "morte");
                return;
        }
    }

    private void Close((long, long) key, long tick, string mode)
    {
        if (!_openUnions.Remove(key, out long since)) return;
        UnionsEnded++;
        UnionYearsEnded += (tick - since) / (double)world.Calendar.HoursPerYear;
        if (mode == "morte") EndedByDeath++;
        EndedByMode[mode] = EndedByMode.GetValueOrDefault(mode) + 1;
    }

    /// <summary>Uma amostra mensal: casa de cada criança, padrasto/madrasta, pares de irmãos e o ofício da casa.</summary>
    public void SampleMonth()
    {
        var now = world.CurrentDate;
        int childMax = world.LifeStageRules.ChildMaxAge;
        var byMother = new Dictionary<long, List<Npc>>();
        var byFather = new Dictionary<long, List<Npc>>();
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            int age = npc.AgeYears(now);
            if (age == CompareAge && _rearer.TryGetValue(npc.Id.Value, out var house) && _compared.Add(npc.Id.Value))
                Compare(npc, house);
            if (age > childMax || npc.Household is not { } homeId) continue;

            if (!_homes.TryGetValue(npc.Id.Value, out var homes)) _homes[npc.Id.Value] = homes = [];
            homes.Add(homeId.Value);
            if (npc.MotherId is { } m) Group(byMother, m.Value, npc);
            if (npc.FatherId is { } f) Group(byFather, f.Value, npc);
            var home = world.FindHousehold(homeId);
            if (home is null) continue;
            foreach (var memberId in home.Members)
            {
                if (memberId == npc.Id || memberId == npc.MotherId || memberId == npc.FatherId) continue;
                if (world.FindNpc(memberId) is not { IsAlive: true, Spouse: { } partner }) continue;
                if (partner == npc.MotherId || partner == npc.FatherId) _stepchildren.Add(npc.Id.Value);
            }
            if (age == RearerSampleAge && !_rearer.ContainsKey(npc.Id.Value) && Rearer(npc, home) is { } r)
                _rearer[npc.Id.Value] = r;
        }

        var counted = new HashSet<(long, long)>();
        foreach (var groups in new[] { byMother, byFather })
            foreach (var (_, kids) in groups.OrderBy(kv => kv.Key))
                for (int i = 0; i < kids.Count; i++)
                    for (int j = i + 1; j < kids.Count; j++)
                    {
                        var key = Key(kids[i].Id.Value, kids[j].Id.Value);
                        if (!counted.Add(key)) continue;
                        bool half = kids[i].MotherId != kids[j].MotherId || kids[i].FatherId != kids[j].FatherId;
                        var (overlap, apart, _) = _siblingPairs.GetValueOrDefault(key);
                        _siblingPairs[key] = (overlap + 1, apart + (kids[i].Household != kids[j].Household ? 1 : 0), half);
                    }
    }

    /// <summary>O ofício da casa aos 12 anos: o do pai se mora com ele, senão o do chefe adulto da casa; e a pressão
    /// registrada na escolha, se houve.</summary>
    private (int, double)? Rearer(Npc child, Domain.Population.Family.Household home)
    {
        double pressure = child.Vocation?.Pressure ?? 0;
        if (child.FatherId is { } fatherId && home.Members.Contains(fatherId)
            && world.FindNpc(fatherId) is { IsAlive: true } father && father.Profession != ProfessionType.None)
            return (father.Profession.Id, pressure);
        if (home.Head != child.Id && world.FindNpc(home.Head) is { IsAlive: true } head && head.Profession != ProfessionType.None
            && head.AgeYears(world.CurrentDate) > world.LifeStageRules.ChildMaxAge)
            return (head.Profession.Id, pressure);
        return null;
    }

    private void Compare(Npc adult, (int Profession, double Pressure) house)
    {
        if (adult.Profession == ProfessionType.None) return;
        double pressure = adult.Vocation?.Pressure ?? house.Pressure;
        bool followed = adult.Profession.Id == house.Profession;
        FollowCompared++;
        if (followed) Followed++;
        if (pressure >= HighPressureCut) { HighPressureCompared++; if (followed) HighPressureFollowed++; }
        else { LowPressureCompared++; if (followed) LowPressureFollowed++; }
    }

    private static void Group(Dictionary<long, List<Npc>> groups, long parent, Npc child)
    {
        if (!groups.TryGetValue(parent, out var list)) groups[parent] = list = [];
        list.Add(child);
    }

    private static long Id(string s) => long.Parse(s, CultureInfo.InvariantCulture);
    private static (long, long) Key(long a, long b) => a <= b ? (a, b) : (b, a);

    public int ChildrenTracked => _homes.Count;
    public int ChildrenInSeveralHomes => _homes.Values.Count(h => h.Count > 1);
    public int Stepchildren => _stepchildren.Count;
    public int SiblingPairs => _siblingPairs.Values.Count(v => v.Overlap >= ApartMonths);
    public int SiblingPairsApart => _siblingPairs.Values.Count(v => v.Overlap >= ApartMonths && v.Apart >= ApartMonths);
    public int HalfSiblingPairs => _siblingPairs.Values.Count(v => v.Overlap >= ApartMonths && v.Half);

    /// <summary>Soma de N rodadas, em texto (uma linha por grupo de medida).</summary>
    public static string Summarize(IReadOnlyList<FamilyLifeTally> tallies)
    {
        var inv = CultureInfo.InvariantCulture;
        double Sum(Func<FamilyLifeTally, double> f) => tallies.Sum(f);
        string Pct(double n, double d) => (d > 0 ? 100 * n / d : 0).ToString("F1", inv) + "%";
        var modes = tallies.SelectMany(t => t.EndedByMode).GroupBy(kv => kv.Key).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key}={g.Sum(kv => kv.Value)}");
        var outcomes = tallies.SelectMany(t => t.AffairOutcomes).GroupBy(kv => kv.Key).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key}={g.Sum(kv => kv.Value)}");
        double ended = Sum(t => t.UnionsEnded), death = Sum(t => t.EndedByDeath);
        var sb = new StringBuilder();
        sb.AppendLine(inv, $"unioes casamentos={Sum(t => t.Marriages)} recasamentos={Sum(t => t.Remarriages)} ({Pct(Sum(t => t.Remarriages), Sum(t => t.Marriages))}) terminadas={ended} por_morte={Pct(death, ended)} modos: {string.Join(' ', modes)} duracao_media_anos={(ended > 0 ? Sum(t => t.UnionYearsEnded) / ended : 0):F1}");
        sb.AppendLine(inv, $"casos iniciados={Sum(t => t.Affairs)} descobertos={Sum(t => t.AffairsDiscovered)} desfechos: {string.Join(' ', outcomes)} filhos_de_outro_pai={Sum(t => t.OtherFatherBirths)} ({Pct(Sum(t => t.OtherFatherBirths), Sum(t => t.BirthsToMarried))} dos nascidos de casadas)");
        sb.AppendLine(inv, $"criancas acompanhadas={Sum(t => t.ChildrenTracked)} em_mais_de_uma_casa={Pct(Sum(t => t.ChildrenInSeveralHomes), Sum(t => t.ChildrenTracked))} com_padrasto_ou_madrasta={Pct(Sum(t => t.Stepchildren), Sum(t => t.ChildrenTracked))} pares_de_irmaos={Sum(t => t.SiblingPairs)} criados_separados(>={ApartMonths}m)={Sum(t => t.SiblingPairsApart)} ({Pct(Sum(t => t.SiblingPairsApart), Sum(t => t.SiblingPairs))}) meios_irmaos={Sum(t => t.HalfSiblingPairs)}");
        sb.AppendLine(inv, $"oficio segue_o_da_casa={Pct(Sum(t => t.Followed), Sum(t => t.FollowCompared))} (n={Sum(t => t.FollowCompared)}) pressao_alta={Pct(Sum(t => t.HighPressureFollowed), Sum(t => t.HighPressureCompared))} (n={Sum(t => t.HighPressureCompared)}) pressao_baixa={Pct(Sum(t => t.LowPressureFollowed), Sum(t => t.LowPressureCompared))} (n={Sum(t => t.LowPressureCompared)}) escolhas={Sum(t => t.VocationChoices)} sob_pressao={Sum(t => t.VocationChoicesPressured)}");
        var ages = tallies.SelectMany(t => t.SpouseAges).ToList();
        var upbringing = tallies.SelectMany(t => t.SpouseUpbringing).ToList();
        sb.AppendLine(inv, $"casal correlacao_idade={Pearson(ages):F2} correlacao_criacao={Pearson(upbringing):F2} idade_media_marido={(ages.Count > 0 ? ages.Average(x => x.Husband) : 0):F1} esposa={(ages.Count > 0 ? ages.Average(x => x.Wife) : 0):F1}");
        return sb.ToString();
    }

    public static double Pearson(IReadOnlyList<(double X, double Y)> pairs)
    {
        if (pairs.Count < 3) return 0;
        double mx = pairs.Average(p => p.X), my = pairs.Average(p => p.Y), sxy = 0, sxx = 0, syy = 0;
        foreach (var (x, y) in pairs)
        {
            sxy += (x - mx) * (y - my);
            sxx += (x - mx) * (x - mx);
            syy += (y - my) * (y - my);
        }
        return sxx > 0 && syy > 0 ? sxy / Math.Sqrt(sxx * syy) : 0;
    }
}
