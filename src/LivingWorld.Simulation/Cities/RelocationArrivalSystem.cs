using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Conclui migração de household só quando todos os membros vivos chegam ao destino
/// (Fase 15.1, Stage 4, T11, LWV-04.2) — nunca troca <see cref="Npc.City"/> no meio do
/// deslocamento.</summary>
public sealed class RelocationArrivalSystem : ISimulationSystem
{
    public const string SystemName = "cities-relocation-arrival";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.CityRules.Enabled) return;

        foreach (var household in world.Households.OrderBy(h => h.Id.Value))
        {
            if (household.PendingRelocationCity is not { } destinationCityId) continue;
            var destination = world.FindCity(destinationCityId);
            if (destination is null)
            {
                household.CompleteRelocation(household.City);
                continue;
            }

            var aliveMembers = household.Members
                .Select(id => world.FindNpc(id))
                .Where(npc => npc is { IsAlive: true })
                .ToList();
            if (aliveMembers.Count == 0)
            {
                household.CompleteRelocation(destinationCityId);
                continue;
            }

            if (!aliveMembers.All(npc => npc!.CurrentLocation == destination.Location))
                continue;

            foreach (var npc in aliveMembers)
                npc!.JoinCity(destinationCityId);
            household.CompleteRelocation(destinationCityId);
        }
    }
}
