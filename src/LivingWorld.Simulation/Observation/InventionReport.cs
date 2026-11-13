using System.Globalization;
using System.Text;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Invention;

namespace LivingWorld.Simulation.Observation;

/// <summary>Medida da invenção numa rodada (AD-141): contagem do sistema (tentativas, falhas por modo, invenções, ensino, cópia,
/// perda, viagens por água a pé e na embarcação) e o estado final (designs vivos, lápides, quem sabe, itens). Só lê o mundo.</summary>
public sealed record InventionSummary(
    ulong Seed, InventionTally Tally, int LiveDesigns, int Tombstones, int Knowers, int Items, IReadOnlyList<int> DesignIds, string FirstInvention)
{
    public static InventionSummary? Of(WorldState world)
    {
        if (world.Invention is not { } ledger) return null;
        var inv = CultureInfo.InvariantCulture;
        var first = ledger.Designs.Where(d => d.Origin == DesignOrigin.Invented).OrderBy(d => d.Tick).FirstOrDefault();
        string firstText = first is null ? "-" : string.Create(inv,
            $"npc {first.InventorId} ano {first.Tick / world.Calendar.HoursPerYear}: {Describe(first, world.InventionRules!)}");
        int knowers = ledger.Known.Select(k => k.NpcId).Distinct().Count(id => world.FindNpc(new NpcId(id)) is { IsAlive: true });
        var ids = ledger.Designs.Select(d => d.Id).Concat(ledger.Tombstones.Select(t => t.DesignId)).Distinct().Order().ToList();
        return new InventionSummary(world.Seed, world.InventionTally, ledger.Designs.Count, ledger.Tombstones.Count, knowers, ledger.Items.Count, ids, firstText);
    }

    /// <summary>Partes em texto curto (forma, medidas, material, parede, revestimento, junção) e o desempenho medido.</summary>
    public static string Describe(Design design, InventionRules rules)
    {
        var inv = CultureInfo.InvariantCulture;
        var perf = PhysicsEvaluator.Evaluate(design.Parts, rules, rules.ReferenceRiderKg);
        var parts = design.Parts.Select(p => string.Create(inv,
            $"{p.Shape} {p.LengthMm}x{p.WidthMm}x{p.HeightMm} m{p.Material}{(p.WallMm > 0 ? $" oco{p.WallMm}" : "")}{(p.Coating > 0 ? $" rev{p.Coating}" : "")} {p.Joint}@{p.OffsetYMm}"));
        return string.Create(inv,
            $"[{string.Join("; ", parts)}] massa={perf.MassG / 1000}kg carga={perf.PayloadG / 1000}kg calado={perf.DraftMm}mm GM={perf.MetacentricMm}mm v={perf.SpeedMmS}mm/s horas={perf.BuildMinutes / 60}");
    }

    public static string Summarize(IReadOnlyList<InventionSummary> runs)
    {
        if (runs.Count == 0) return "";
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        long Sum(Func<InventionTally, long> f) => runs.Sum(r => f(r.Tally));
        sb.AppendLine(inv, $"invencao seeds={runs.Count} ideias_descartadas={Sum(t => t.IdeasDiscarded)} prototipos={Sum(t => t.Attempts)} prototipos_falhos={Sum(t => t.PrototypeFailures)} (afundou={Sum(t => t.Sank)} virou={Sum(t => t.Capsized)} quebrou={Sum(t => t.Broke)} lento={Sum(t => t.TooSlow)} invalido={Sum(t => t.Invalid)})");
        sb.AppendLine(inv, $"invencao invencoes={Sum(t => t.Inventions)} seeds_com_invencao={runs.Count(r => r.Tally.Inventions > 0)} ensinados={Sum(t => t.Taught)} copiados={Sum(t => t.Copied)} engenharia_reversa={Sum(t => t.ReverseEngineered)} construidos={Sum(t => t.Built)} construcoes_falhas={Sum(t => t.BuildFailures)} perdidos={Sum(t => t.Lost)}");
        sb.AppendLine(inv, $"invencao final designs_vivos={runs.Sum(r => r.LiveDesigns)} lapides={runs.Sum(r => r.Tombstones)} quem_sabe={runs.Sum(r => r.Knowers)} itens={runs.Sum(r => r.Items)}");
        long foot = Sum(t => t.FootWaterTrips), craft = Sum(t => t.CraftWaterTrips);
        sb.AppendLine(inv, $"invencao viagens_por_agua a_pe={foot} na_embarcacao={craft} ({100.0 * craft / Math.Max(1, foot + craft):F1}%) horas_poupadas={Sum(t => t.HoursSavedX10) / 10.0:F0}");
        double overlap = 0; int pairs = 0;
        for (int i = 0; i + 1 < runs.Count; i++)
        {
            var a = runs[i].DesignIds.ToHashSet();
            var b = runs[i + 1].DesignIds;
            if (a.Count == 0 || b.Count == 0) continue;
            overlap += (double)b.Count(a.Contains) / Math.Min(a.Count, b.Count);
            pairs++;
        }
        sb.AppendLine(inv, $"invencao sobreposicao_media_entre_seeds_vizinhas={(pairs == 0 ? 0 : 100 * overlap / pairs):F1}% (pares={pairs})");
        foreach (var r in runs.Where(r => r.FirstInvention != "-").Take(3)) sb.AppendLine(inv, $"invencao primeira seed={r.Seed}: {r.FirstInvention}");
        return sb.ToString();
    }
}
