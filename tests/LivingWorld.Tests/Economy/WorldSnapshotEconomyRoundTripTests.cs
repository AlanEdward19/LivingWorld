using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Economy;

/// <summary>Fase 5, T12: round-trip completo (Serialize → Deserialize) com pelo menos um
/// Workplace com Stock/Treasury/Employees não vazios produz o mesmo hash canônico antes e
/// depois.</summary>
public class WorldSnapshotEconomyRoundTripTests
{
    [Fact]
    public void Round_trip_with_a_populated_workplace_preserves_the_canonical_hash()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 7, initialPopulation: 3);
        clock.Run(world, ticks: 10);

        var npc = world.Npcs.First();
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), npc.CurrentLocation, maxVacancies: 2,
            employees: [npc.Id], stock: new Dictionary<ResourceType, long> { [new ResourceType(1)] = 42 },
            treasury: new Money(100), prices: new Dictionary<ResourceType, long> { [new ResourceType(1)] = 5 });
        world.AddWorkplace(workplace);
        npc.Hire(workplace.Id);

        var before = WorldSnapshot.CanonicalHash(world);
        var rehydrated = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        var after = WorldSnapshot.CanonicalHash(rehydrated);

        Assert.Equal(before, after);
        // ScenarioRunner.Create (T20) já semeia fazenda+ferraria default — procura pelo id do
        // Workplace criado neste teste em vez de assumir que é o único.
        var rehydratedWorkplace = rehydrated.Workplaces.Single(w => w.Id == workplace.Id);
        Assert.Equal(42, rehydratedWorkplace.Stock[new ResourceType(1)]);
        Assert.Equal(new Money(100), rehydratedWorkplace.Treasury);
        Assert.Equal(npc.Id, Assert.Single(rehydratedWorkplace.Employees));
    }
}
