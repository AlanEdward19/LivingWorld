using LivingWorld.Domain;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Narrative;

/// <summary>Monta a linha do tempo de um NPC a partir dos <see cref="Fact"/>s em que ele participa
/// (Fase 12, NARR-16..17) — reusa <see cref="HistoryIndex.ByEntity"/> (mesma disciplina de custo
/// de <see cref="WindowedHistoryAggregator"/>, evita varrer <c>WorldState.Facts</c> por completo).
/// Ordena por tick crescente (desempate por <see cref="FactId"/>, para determinismo) e corta todo
/// evento posterior ao tick de morte do NPC — a biografia de alguém morto para no fim da vida
/// dele, nunca depois.</summary>
public static class NpcBiographyQuery
{
    public static Result<IReadOnlyList<Fact>> Timeline(WorldState world, NpcId npcId)
    {
        var npc = world.FindNpc(npcId);
        if (npc is null)
            return Result<IReadOnlyList<Fact>>.Fail("Npc: não existe");

        long? deathTick = npc.DeathDate?.TotalHours;

        var facts = new List<Fact>();
        foreach (var factId in world.HistoryIndex.ByEntity(npcId))
        {
            var fact = world.FindFact(factId);
            if (fact is null)
                continue;
            if (deathTick is not null && fact.Tick > deathTick.Value)
                continue;
            facts.Add(fact);
        }

        IReadOnlyList<Fact> ordered = facts
            .OrderBy(f => f.Tick)
            .ThenBy(f => f.Id.Value)
            .ToList();
        return Result<IReadOnlyList<Fact>>.Ok(ordered);
    }
}
