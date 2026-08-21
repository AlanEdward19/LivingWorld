using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Forma e evolui relações por convivência diária (Fase 7, T11, FAM-01..05) — único
/// sistema que escreve em <see cref="WorldState.Relationships"/>.</summary>
public sealed class RelationshipSystem : ISimulationSystem
{
    public const string SystemName = "population-relationship";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.FamilyRules;
        long now = ctx.CurrentTick;

        foreach (var household in world.Households.OrderBy(h => h.Id.Value))
            ApplyCohabitationForMembers(world, rules, now, household.Members);

        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            var present = workplace.Employees
                .OrderBy(id => id.Value)
                .Where(id => world.FindNpc(id) is { IsAlive: true, CurrentLocation: var loc } npc
                               && loc == workplace.Location)
                .ToList();
            ApplyCohabitationForMembers(world, rules, now, present);
        }

        // Sem OrderBy aqui de propósito: DecayTowardNeutral só lê/escreve o próprio Relationship
        // (sem RNG, sem side effect cruzado com outras entradas), então o estado final não
        // depende da ordem de iteração — só o resultado de Hash(world) importa, e ele é idêntico
        // com ou sem ordenação. Ordenar 16M+ relações por dia (achado real em cenários de 10k
        // pop, baseline-timings.md T2 revisado) era custo puro sem efeito observável.
        long lossThresholdHours = (long)rules.ContactLossThresholdDays * world.CurrentDate.Calendar.HoursPerDay;
        foreach (var (_, relationship) in world.Relationships)
        {
            if (now - relationship.LastContactTick <= lossThresholdHours)
                continue;
            relationship.DecayTowardNeutral(rules);
        }
    }

    private static void ApplyCohabitationForMembers(
        WorldState world, FamilyRules rules, long now, IReadOnlyList<NpcId> memberIds)
    {
        var alive = memberIds
            .OrderBy(id => id.Value)
            .Where(id => world.FindNpc(id) is { IsAlive: true })
            .ToList();

        for (int i = 0; i < alive.Count; i++)
        {
            for (int j = 0; j < alive.Count; j++)
            {
                if (i == j) continue;
                var from = alive[i];
                var to = alive[j];
                var relationship = world.GetOrCreateRelationship(new RelationshipKey(from, to), now);
                relationship.ApplyEvent(RelationshipEventType.Cohabitation, rules);
                relationship.MarkContact(now);
            }
        }
    }
}
