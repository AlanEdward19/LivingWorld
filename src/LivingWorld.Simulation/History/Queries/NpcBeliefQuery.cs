using System.Runtime.CompilerServices;
using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.History.Queries;

/// <summary>Consulta de crença agregada de um NPC para montar contexto de LLM
/// — mesma separação Verdade/Crença de <see cref="HistoryBeliefQuery"/>: só entra o
/// relato distorcido do cânone da cidade do NPC (<see
/// cref="DistortedReport.MoralizedNarrativeSeed"/>), nunca <see cref="HistoryTruthQuery"/> nem
/// <see cref="Fact.Payload"/> bruto. Um fato que nunca virou <see cref="ReportState"/> no cânone
/// da cidade do NPC (segredo conhecido só por outro NPC/outra comunidade) simplesmente não tem
/// slot pra iterar — fica fora do prompt sem checagem extra.</summary>
public static class NpcBeliefQuery
{
    private sealed record CachedCanon(IReadOnlyList<ReportState> Slots, int FactCount, IReadOnlyList<string> Beliefs);
    private static readonly ConditionalWeakTable<WorldState, Dictionary<CityId, CachedCanon>> Caches = new();

    public static IReadOnlyList<string> BeliefsOf(WorldState world, NpcId npcId)
    {
        var npc = world.FindNpc(npcId);
        if (npc is null) return [];

        var city = world.FindCity(npc.City);
        if (city is null) return [];

        if (!world.HistoryRules.Enabled || city.CanonSlots.Count == 0) return [];

        var cache = Caches.GetOrCreateValue(world);
        if (cache.TryGetValue(city.Id, out var cached)
            && ReferenceEquals(cached.Slots, city.CanonSlots) && cached.FactCount == world.Facts.Count)
            return cached.Beliefs;

        var beliefs = new List<string>();
        foreach (var slot in city.CanonSlots)
        {
            var belief = HistoryBeliefQuery.BeliefOf(world, city.Id, slot.OriginFactId);
            if (belief.IsSuccess && !string.IsNullOrEmpty(belief.Value!.MoralizedNarrativeSeed))
                beliefs.Add(belief.Value.MoralizedNarrativeSeed);
        }
        // Hop-0 materialization is pure. Later hops consume distortion RNG, so keep their existing execution semantics.
        IReadOnlyList<string> result = beliefs.Count == 0 ? Array.Empty<string>() : beliefs;
        if (city.CanonSlots.All(slot => slot.HopCount == 0))
            cache[city.Id] = new CachedCanon(city.CanonSlots, world.Facts.Count, result);
        return result;
    }
}
