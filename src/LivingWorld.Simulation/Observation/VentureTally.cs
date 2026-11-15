using System.Globalization;
using System.Text;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Production;

namespace LivingWorld.Simulation.Observation;

/// <summary>Negócios abertos por NPC (AD-152): quantos por década, com a origem do capital (poupança, herança, sociedade), o
/// capital médio, e quantos ainda trabalham (gente e folha paga) aos 5 e aos 10 anos. Só lê o evento e, no fim do mês, as
/// oficinas abertas; fora do relógio da simulação.</summary>
public sealed class VentureTally(WorldState world) : IWorldEventSink
{
    public const int Decades = 10;
    public static readonly string[] Origins = ["poupanca", "heranca", "sociedade", "emprestimo"];

    public sealed record Venture(long Tick, long WorkplaceId, string Origin, long Capital, bool Reopened = false, bool Bought = false)
    {
        public bool? Alive5 { get; set; }
        public bool? Alive10 { get; set; }
    }

    public List<Venture> Opened { get; } = [];

    /// <summary>AD-155: meses-oficina em que uma forja de dono ficou parada (sem gente trabalhando ou folha que não fechou), por
    /// década; a fechada por falência conta à parte.</summary>
    public int[] StoppedMonths { get; } = new int[Decades];
    public int[] ClosedMonths { get; } = new int[Decades];

    public void Record(WorldEvent evt)
    {
        if (evt.Kind != WorldEventKind.VentureFounded) return;
        var parts = evt.Payload.Split('|');
        if (parts.Length < 4) return;
        Opened.Add(new Venture(evt.Tick, long.Parse(parts[0], CultureInfo.InvariantCulture), parts[2],
            long.Parse(parts[3], CultureInfo.InvariantCulture), parts.Length > 7 && parts[7] == "reaberta", parts.Length > 7 && parts[7] == "comprada"));
    }

    /// <summary>No fim de cada mês: quem completou 5 ou 10 anos de aberto ganha a marca de vivo ou não.</summary>
    public void SampleMonth()
    {
        long year = world.Calendar.HoursPerYear;
        int decade = Math.Clamp((int)(world.CurrentDate.TotalHours / year / 10), 0, Decades - 1);
        foreach (var w in world.Workplaces)
        {
            if (w.Owner is null || w.LocationType.Id != EconomyTally.ForgeType) continue;
            if (w.Closed) ClosedMonths[decade]++;
            else if (!Operating(w.Id.Value)) StoppedMonths[decade]++;
        }

        foreach (var v in Opened)
        {
            long age = world.CurrentDate.TotalHours - v.Tick;
            if (v.Alive5 is null && age >= 5 * year) v.Alive5 = Operating(v.WorkplaceId);
            if (v.Alive10 is null && age >= 10 * year) v.Alive10 = Operating(v.WorkplaceId);
        }
    }

    private bool Operating(long id) =>
        world.FindWorkplace(new WorkplaceId(id)) is { } w && !w.PayrollFailed && ToolMechanic.WorkingHands(world, w) > 0;

    public static string Summarize(IReadOnlyList<VentureTally> runs, long hoursPerYear)
    {
        var inv = CultureInfo.InvariantCulture;
        var reopened = runs.SelectMany(r => r.Opened).Count(v => v.Reopened);
        var all = runs.SelectMany(r => r.Opened).Where(v => !v.Reopened).ToList();
        var sb = new StringBuilder();
        string origins = string.Join(' ', Origins.Select(o => $"{o}={all.Count(v => v.Origin == o)}"));
        sb.AppendLine(inv, $"negocios abertos por NPC: total={all.Count} seeds_com_algum={runs.Count(r => r.Opened.Count > 0)}/{runs.Count} capital_medio={(all.Count > 0 ? all.Average(v => v.Capital) : 0):F0} {origins} reabertas={reopened} compradas_fechadas={all.Count(v => v.Bought)} seeds_com_emprestimo={runs.Count(r => r.Opened.Any(v => v.Origin == "emprestimo"))}");
        sb.AppendLine(inv, $"  forjas_de_dono_paradas (meses-oficina, soma das seeds) por decada:{string.Concat(Enumerable.Range(0, Decades).Select(d => $" {d * 10}:{runs.Sum(r => r.StoppedMonths[d])}"))} fechadas:{string.Concat(Enumerable.Range(0, Decades).Select(d => $" {d * 10}:{runs.Sum(r => r.ClosedMonths[d])}"))}");
        var five = all.Where(v => v.Alive5 is not null).ToList();
        var ten = all.Where(v => v.Alive10 is not null).ToList();
        sb.AppendLine(inv, $"  sobrevivencia: 5_anos={five.Count(v => v.Alive5 == true)}/{five.Count} 10_anos={ten.Count(v => v.Alive10 == true)}/{ten.Count}");
        sb.Append("  por decada (poupanca/heranca/sociedade/emprestimo):");
        for (int d = 0; d < Decades; d++)
        {
            var inDecade = all.Where(v => Math.Clamp((int)(v.Tick / hoursPerYear / 10), 0, Decades - 1) == d).ToList();
            sb.Append(inv, $" {d * 10}:{string.Join('/', Origins.Select(o => inDecade.Count(v => v.Origin == o)))}");
        }

        return sb.AppendLine().ToString();
    }
}
