using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Migration;

/// <summary>Conclui migração de household quando quem conduz a mudança (o chefe, ou o primeiro adulto) chega ao destino
/// (AD-190): a casa, a despensa e a cidade de todos passam ao lugar novo, e quem ainda está no caminho segue para a casa nova.
/// Antes da chegada de quem conduz, ninguém troca de <see cref="Npc.City"/>.</summary>
[LivingCapability("MIGRATION")]
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
                household.CompleteRelocation(destinationCityId, destination.Location);
                continue;
            }

            // AD-190: a mudança se completa quando quem a conduz chega (o chefe; sem ele vivo, o primeiro adulto da casa): a casa e
            // a despensa passam ao lugar novo e quem ainda vem no caminho segue para lá. Antes esperava todos os vivos no destino ao
            // mesmo tempo, e quem chegava com fome voltava à despensa da casa velha para comer: a família ficava indo e vindo entre
            // as duas, e as crianças morriam de sede e de fome no caminho com comida e água em casa (metrópole do menu, seed 2).
            var leader = aliveMembers.FirstOrDefault(npc => npc!.Id == household.Head)
                ?? aliveMembers.OrderBy(npc => npc!.Id.Value).FirstOrDefault(npc => world.LifeStageRules.LifeStageOf(npc!.AgeYears(world.CurrentDate))
                    != LivingWorld.Domain.Population.Body.LifeStage.Child)
                ?? aliveMembers.OrderBy(npc => npc!.Id.Value).First();
            if (leader!.CurrentLocation != destination.Location)
                continue;

            foreach (var npc in aliveMembers.OrderBy(npc => npc!.Id.Value))
                npc!.JoinCity(destinationCityId);
            household.CompleteRelocation(destinationCityId, destination.Location);
        }
    }
}
