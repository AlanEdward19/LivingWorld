using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.World;

/// <summary>Fase 15.1, T42 (ADR-0017): <c>WorldState.Name</c> é volátil (cosmético, ADR-0014) e
/// precisa sobreviver a snapshot/reidratação, mas nunca ao hash canônico.</summary>
public class WorldNameTests
{
    [Fact]
    public void Rename_sets_the_name_and_it_survives_snapshot_round_trip()
    {
        var (world, _) = ScenarioRunner.Create(seed: 321);
        world.Rename("Vale de Aster");

        var rehydrated = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal("Vale de Aster", rehydrated.Name);
    }

    [Fact]
    public void Default_name_is_empty_when_never_renamed()
    {
        var (world, _) = ScenarioRunner.Create(seed: 322);

        Assert.Equal(string.Empty, world.Name);
    }

    [Fact]
    public void Renaming_does_not_change_the_canonical_hash()
    {
        var (world, _) = ScenarioRunner.Create(seed: 323);
        var beforeHash = WorldSnapshot.CanonicalHash(world);

        world.Rename("Novo Nome");

        Assert.Equal(beforeHash, WorldSnapshot.CanonicalHash(world));
    }
}
