using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Population;

/// <summary>Fase 8: todo NPC vivo pertence a uma cidade existente (amostrado por dia, 1 ano). A vila padrão da seed 42 vem da
/// corrida compartilhada <see cref="DefaultVillageSeed42"/> (ADR-0044), que faz esta mesma checagem a cada dia do ano 1.</summary>
public class EveryNpcHasACityTests
{
    [Fact]
    public void Every_living_npc_belongs_to_an_existing_city_at_every_daily_sample()
    {
        Assert.NotEmpty(ScenarioRunner.Create(seed: DefaultVillageSeed42.Seed).World.Cities);
        Assert.Null(DefaultVillageSeed42.Value.CityFailureYear1);
    }

    /// <summary>Nulo se todo vivo está numa cidade existente; senão a mensagem do primeiro que não está.</summary>
    internal static string? CityFailure(WorldState world, long tick)
    {
        var cityIds = world.Cities.Select(c => c.Id).ToHashSet();
        foreach (var npc in world.Npcs.Where(n => n.IsAlive))
            if (!cityIds.Contains(npc.City)) return $"NPC {npc.Id} sem cidade válida no tick {tick}";
        return null;
    }
}
