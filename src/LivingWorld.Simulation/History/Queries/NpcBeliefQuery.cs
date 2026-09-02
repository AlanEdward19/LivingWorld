using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.History.Queries;

/// <summary>Consulta de crença agregada de um NPC para montar contexto de LLM (Fase 11, LLM-05/06)
/// — mesma separação Verdade/Crença de <see cref="HistoryBeliefQuery"/> (HIST-10): só entra o
/// relato distorcido do cânone da cidade do NPC (<see
/// cref="DistortedReport.MoralizedNarrativeSeed"/>), nunca <see cref="HistoryTruthQuery"/> nem
/// <see cref="Fact.Payload"/> bruto. Um fato que nunca virou <see cref="ReportState"/> no cânone
/// da cidade do NPC (segredo conhecido só por outro NPC/outra comunidade) simplesmente não tem
/// slot pra iterar — fica fora do prompt sem checagem extra (LLM-06 AC3).</summary>
public static class NpcBeliefQuery
{
    public static IReadOnlyList<string> BeliefsOf(WorldState world, NpcId npcId)
    {
        var npc = world.FindNpc(npcId);
        if (npc is null) return [];

        var city = world.FindCity(npc.City);
        if (city is null) return [];

        var beliefs = new List<string>();
        foreach (var slot in city.CanonSlots)
        {
            var belief = HistoryBeliefQuery.BeliefOf(world, city.Id, slot.OriginFactId);
            if (belief.IsSuccess)
                beliefs.Add(belief.Value!.MoralizedNarrativeSeed);
        }
        return beliefs;
    }
}
