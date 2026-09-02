using LivingWorld.Domain.Cities;
using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.History.Books;

/// <summary>Cânone limitado por comunidade (Fase 10, HIST-08 AC2) — no máximo N relatos vivos
/// por <see cref="City"/>; despejo pelo menor peso.</summary>
public static class CanonSlotManager
{
    public static Result<Unit> Admit(City city, ReportState report, HistoryRules rules, long nowTick)
    {
        if (!rules.Enabled)
            return Result<Unit>.Fail("history_disabled");

        var weighted = report with { Weight = WeightOf(report, nowTick, rules) };
        var slots = city.CanonSlots.ToList();

        if (slots.Count < rules.CanonSizePerCommunity)
        {
            city.SetCanonSlots([.. slots, weighted]);
            return Result<Unit>.Ok(Unit.Value);
        }

        var evict = slots
            .OrderBy(r => WeightOf(r, nowTick, rules))
            .ThenBy(r => r.Id.Value)
            .First();

        slots.Remove(evict);
        slots.Add(weighted);
        city.SetCanonSlots(slots);
        return Result<Unit>.Ok(Unit.Value);
    }

    public static double WeightOf(ReportState report, long nowTick, HistoryRules rules)
    {
        double importance = report.Weight;
        double transmissibility = rules.MediumFidelityByType.TryGetValue(report.Medium, out var medium)
            ? medium.ReachHops
            : 0;
        double age = Math.Max(0, nowTick - report.LastHopTick);
        double recency = 1.0 / (1.0 + age);

        return rules.ImportanceWeight * importance
            + rules.TransmissibilityWeight * transmissibility
            + rules.RecencyWeight * recency;
    }
}
