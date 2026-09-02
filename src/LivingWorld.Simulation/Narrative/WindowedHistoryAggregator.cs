using LivingWorld.Domain;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History;

namespace LivingWorld.Simulation.Narrative;

/// <summary>Agrega <see cref="Fact"/>s por `(local, período)` e ordena por significância antes
/// da renderização textual (Fase 12, NARR-05..07). Reusa <see cref="HistoryIndex.ByYear"/> para
/// não varrer <c>WorldState.Facts</c> por completo — mesma disciplina de custo da Fase 10.</summary>
public static class WindowedHistoryAggregator
{
    /// <summary>Retorna os <paramref name="topK"/> fatos mais significativos de
    /// <paramref name="location"/> (ou de qualquer local, se <c>null</c>) dentro de
    /// <c>[periodStartTick, periodEndTick)</c>, ordenados por significância decrescente
    /// (desempate por <see cref="FactId"/> crescente, para determinismo).</summary>
    public static IReadOnlyList<Fact> TopFacts(
        WorldState world, CityId? location, long periodStartTick, long periodEndTick, int topK)
    {
        if (topK <= 0)
            return [];

        int startYear = (int)(periodStartTick / world.Calendar.HoursPerYear);
        int endYear = (int)((periodEndTick - 1) / world.Calendar.HoursPerYear);

        var candidates = new List<Fact>();
        for (int year = startYear; year <= endYear; year++)
        {
            foreach (var factId in world.HistoryIndex.ByYear(year))
            {
                var fact = world.FindFact(factId);
                if (fact is null)
                    continue;
                if (fact.Tick < periodStartTick || fact.Tick >= periodEndTick)
                    continue;
                if (location is not null && fact.Location != location)
                    continue;
                candidates.Add(fact);
            }
        }

        return candidates
            .OrderByDescending(f => f.Significance)
            .ThenBy(f => f.Id.Value)
            .Take(topK)
            .ToList();
    }
}
