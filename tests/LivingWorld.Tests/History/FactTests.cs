using System.Reflection;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.History;

/// <summary>Fase 10, T2: <see cref="Fact"/> esqueleto imutável (HIST-01).</summary>
public class FactTests
{
    [Fact]
    public void Fact_creation_preserves_all_skeleton_fields()
    {
        var fact = new Fact(
            new FactId(1),
            Tick: 100,
            WorldEventKind.Death,
            Participants: [new NpcId(7)],
            Location: null,
            Significance: 0.9,
            Payload: "7");

        Assert.Equal(new FactId(1), fact.Id);
        Assert.Equal(100, fact.Tick);
        Assert.Equal(WorldEventKind.Death, fact.Kind);
        Assert.Single(fact.Participants);
        Assert.Equal(0.9, fact.Significance);
        Assert.Equal("7", fact.Payload);
    }

    [Fact]
    public void Fact_exposes_no_mutation_methods_or_setters()
    {
        Assert.True(typeof(Fact).IsSealed);
        Assert.Null(typeof(Fact).GetMethod("Update"));
        Assert.Null(typeof(Fact).GetMethod("Mutate"));

        foreach (var prop in typeof(Fact).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var set = prop.SetMethod;
            if (set is null || !set.IsPublic)
                continue;
            Assert.Contains(
                "IsExternalInit",
                set.ReturnParameter.GetRequiredCustomModifiers().Select(t => t.Name));
        }
    }

    [Fact]
    public void WorldState_assigns_monotonic_fact_ids()
    {
        var (world, _) = ScenarioRunner.Create(1);
        world.AddFact(new Fact(world.NextFactIdAndAdvance(), 1, WorldEventKind.Birth, [], null, 0.8, "a"));
        Assert.Equal(new FactId(1), world.NextFactIdAndAdvance());
        Assert.Single(world.Facts);
    }
}
