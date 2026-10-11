using System.Reflection;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Tests.Unit.Snapshot;

/// <summary>Fase 3: BranchId é identidade. Hash por branch e repositório sempre filtrado por branch.</summary>
public class BranchHashTests
{
    [Fact]
    public void Identical_content_on_two_branches_has_distinct_canonical_hashes()
    {
        var (world, _) = ScenarioRunner.Create(seed: 5, initialPopulation: 6);
        var json = WorldSnapshot.Serialize(world);
        Assert.Contains("\"BranchId\":{\"Value\":0}", json);

        var other = WorldSnapshot.Deserialize(json.Replace("\"BranchId\":{\"Value\":0}", "\"BranchId\":{\"Value\":7}"));

        Assert.Equal(new BranchId(7), other.BranchId);
        Assert.NotEqual(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(other));
    }

    [Fact]
    public void Every_repository_method_takes_a_BranchId()
    {
        var methods = typeof(IWorldRepository).GetMethods(BindingFlags.Public | BindingFlags.Instance);
        Assert.NotEmpty(methods);
        foreach (var m in methods)
            Assert.Contains(m.GetParameters(), p => p.ParameterType == typeof(BranchId));
    }
}
