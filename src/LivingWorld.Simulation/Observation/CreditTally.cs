using System.Globalization;
using System.Text;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Crédito de uma rodada (AD-155): empréstimos por década, por tipo de credor e finalidade; quitados, calotes (e o que o credor
/// perdeu), falências e o que houve com os empregados que ficaram livres (de volta ao trabalho, ainda sem, foram embora ou morreram,
/// um ano depois), demissões por falta de caixa, e a moeda total do mundo (o maior desvio da conservação). Só lê eventos e, no fim do
/// mês, o mundo; fora do relógio da simulação.</summary>
public sealed class CreditTally(WorldState world) : IWorldEventSink
{
    public const int Decades = 10;
    public static readonly string[] Kinds = ["parente", "conhecido", "rico", "cidade"];
    public static readonly string[] Purposes = ["abertura", "giro", "reabertura", "comida"];

    public int[,] Granted { get; } = new int[Decades, Kinds.Length];
    public int[] ByPurpose { get; } = new int[Purposes.Length];
    public long Principal { get; private set; }
    public int Repaid { get; private set; }
    public int Defaulted { get; private set; }
    public long Lost { get; private set; }
    public Dictionary<string, int> DefaultHow { get; } = new(StringComparer.Ordinal);
    public int Bankruptcies { get; private set; }
    public int Freed { get; private set; }
    public int LaidOff { get; private set; }
    public int Reabsorbed { get; private set; }
    public int StillIdle { get; private set; }
    public int Gone { get; private set; }
    public long MaxDrift { get; private set; }

    /// <summary>Empréstimos de comida por tomador (para ver se é a mesma casa rolando dívida).</summary>
    public Dictionary<long, int> FoodBorrowers { get; } = [];
    private long? _initialMoney;
    private readonly List<(long Npc, long Tick)> _watch = [];

    public void Record(WorldEvent evt)
    {
        var p = evt.Payload.Split('|');
        switch (evt.Kind)
        {
            case WorldEventKind.LoanGranted when p.Length >= 7:
                int decade = Math.Clamp((int)(evt.Tick / world.Calendar.HoursPerYear / 10), 0, Decades - 1);
                int kind = Array.IndexOf(Kinds, p[2]);
                if (kind >= 0) Granted[decade, kind]++;
                int purpose = Array.IndexOf(Purposes, p[4]);
                if (purpose >= 0) ByPurpose[purpose]++;
                long who = long.Parse(p[3], CultureInfo.InvariantCulture);
                if (p[4] == "comida") FoodBorrowers[who] = FoodBorrowers.GetValueOrDefault(who) + 1;
                Principal += long.Parse(p[5], CultureInfo.InvariantCulture);
                break;
            case WorldEventKind.LoanRepaid when p.Length >= 4:
                Repaid++;
                break;
            case WorldEventKind.LoanDefaulted when p.Length >= 6:
                Defaulted++;
                Lost += long.Parse(p[3], CultureInfo.InvariantCulture);
                DefaultHow[p[5]] = DefaultHow.GetValueOrDefault(p[5]) + 1;
                break;
            case WorldEventKind.WorkplaceBankrupt:
                Bankruptcies++;
                break;
            case WorldEventKind.Fired when p.Length >= 2 && evt.SourceSystem is "economy-credit" or "economy-working-capital":
                if (evt.SourceSystem == "economy-credit") Freed++; else LaidOff++;
                _watch.Add((long.Parse(p[0], CultureInfo.InvariantCulture), evt.Tick));
                break;
        }
    }

    /// <summary>Fim do mês: quem ficou livre há um ano vira contagem; a moeda total confere com a cunhada e a destruída.</summary>
    public void SampleMonth()
    {
        long now = world.CurrentDate.TotalHours, year = world.Calendar.HoursPerYear;
        for (int i = _watch.Count - 1; i >= 0; i--)
        {
            if (now - _watch[i].Tick < year) continue;
            var npc = world.FindNpc(new NpcId(_watch[i].Npc));
            if (npc is not { IsAlive: true }) Gone++;
            else if (npc.Employer is not null) Reabsorbed++;
            else StillIdle++;
            _watch.RemoveAt(i);
        }

        long total = world.MoneyDestroyed.Amount - world.MoneyMinted.Amount;
        foreach (var n in world.Npcs) total += n.Wallet.Amount;
        foreach (var w in world.Workplaces) total += w.Treasury.Amount;
        foreach (var c in world.Cities) total += c.Treasury.Amount;
        _initialMoney ??= total;
        MaxDrift = Math.Max(MaxDrift, Math.Abs(total - _initialMoney.Value));
    }

    public static string Summarize(IReadOnlyList<CreditTally> runs)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        int granted = runs.Sum(r => r.ByPurpose.Sum());
        int seedsWith = runs.Count(r => r.ByPurpose.Sum() > 0);
        int closed = runs.Sum(r => r.Repaid + r.Defaulted);
        sb.AppendLine(inv, $"credito emprestimos={granted} seeds_com_algum={seedsWith}/{runs.Count} principal_medio={(granted > 0 ? runs.Sum(r => r.Principal) / (double)granted : 0):F0} finalidade[{string.Join(' ', Purposes.Select((p, i) => $"{p}={runs.Sum(r => r.ByPurpose[i])}"))}]");
        sb.AppendLine(inv, $"  quitados={runs.Sum(r => r.Repaid)} calotes={runs.Sum(r => r.Defaulted)} (taxa={(closed > 0 ? 100.0 * runs.Sum(r => r.Defaulted) / closed : 0):F1}% dos encerrados) perdido_pelos_credores={runs.Sum(r => r.Lost)} como[{string.Join(' ', runs.SelectMany(r => r.DefaultHow).GroupBy(k => k.Key).OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Sum(x => x.Value)}"))}]");
        sb.AppendLine(inv, $"  falencias={runs.Sum(r => r.Bankruptcies)} empregados_liberados={runs.Sum(r => r.Freed)} demitidos_por_caixa={runs.Sum(r => r.LaidOff)} um_ano_depois: trabalhando={runs.Sum(r => r.Reabsorbed)} sem_trabalho={runs.Sum(r => r.StillIdle)} partiram_ou_morreram={runs.Sum(r => r.Gone)}");
        sb.Append("  por decada (parente/conhecido/rico/cidade):");
        for (int d = 0; d < Decades; d++)
            sb.Append(inv, $" {d * 10}:{string.Join('/', Enumerable.Range(0, Kinds.Length).Select(k => runs.Sum(r => r.Granted[d, k])))}");
        sb.AppendLine();
        var food = runs.SelectMany(r => r.FoodBorrowers.Values).OrderBy(x => x).ToList();
        sb.AppendLine(inv, $"  comida: tomadores_distintos={food.Count} emprestimos_por_tomador media={(food.Count > 0 ? food.Average() : 0):F1} mediana={(food.Count > 0 ? food[food.Count / 2] : 0)} p90={(food.Count > 0 ? food[food.Count * 9 / 10] : 0)} max={(food.Count > 0 ? food[^1] : 0)}");
        sb.AppendLine(inv, $"  moeda_total desvio_maximo={runs.Max(r => r.MaxDrift)} (0 = conservada)");
        return sb.ToString();
    }
}
