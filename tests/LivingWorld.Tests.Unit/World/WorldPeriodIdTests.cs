using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.World;

/// <summary>ADR-0027: <c>WorldState.PeriodId</c> é volátil (só rotula), sobrevive a snapshot/reidratação, snapshot antigo sem o campo
/// reidrata com vazio e nunca entra no hash canônico.</summary>
public class WorldPeriodIdTests
{
    [Fact]
    public void Assigned_period_survives_the_snapshot_round_trip()
    {
        var (world, _) = ScenarioRunner.Create(seed: 331);
        world.AssignPeriod("vila-pequena");

        var rehydrated = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal("vila-pequena", rehydrated.PeriodId);
    }

    [Fact]
    public void Default_is_empty_and_an_old_snapshot_without_the_field_rehydrates_empty()
    {
        var (world, _) = ScenarioRunner.Create(seed: 332);
        Assert.Equal(string.Empty, world.PeriodId);

        var json = System.Text.Json.Nodes.JsonNode.Parse(WorldSnapshot.Serialize(world))!.AsObject();
        json.Remove("PeriodId");

        Assert.Equal(string.Empty, WorldSnapshot.Deserialize(json.ToJsonString()).PeriodId);
    }

    [Fact]
    public void Assigning_the_period_does_not_change_the_canonical_hash()
    {
        var (world, _) = ScenarioRunner.Create(seed: 333);
        var before = WorldSnapshot.CanonicalHash(world);

        world.AssignPeriod("grande-metropole");

        Assert.Equal(before, WorldSnapshot.CanonicalHash(world));
    }
}
