using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>godot-api-readiness C1/C3: andares e escada derivados de (id, tipo).</summary>
public class BuildingLayoutGeneratorTests
{
    private static IEnumerable<(BuildingId Id, int Type)> Sample() =>
        Enumerable.Range(1, 60).SelectMany(i => new[] { -1, 0, 1, 2, 7, 14 }.Select(t => (new BuildingId(i), t)));

    [Fact]
    public void Floors_are_pure_and_at_least_one()
    {
        foreach (var (id, type) in Sample())
        {
            int first = BuildingLayoutGenerator.FloorsOf(id, type);
            Assert.InRange(first, 1, 3);
            Assert.Equal(first, BuildingLayoutGenerator.FloorsOf(id, type));
        }

        var forward = Sample().Select(s => BuildingLayoutGenerator.FloorsOf(s.Id, s.Type)).ToArray();
        var backward = Sample().Reverse().Select(s => BuildingLayoutGenerator.FloorsOf(s.Id, s.Type)).Reverse().ToArray();
        Assert.Equal(forward, backward);
        Assert.Equal(2, BuildingLayoutGenerator.FloorsOf(new BuildingId(5), -1));
        Assert.Contains(forward, f => f > 1);
    }

    [Fact]
    public void Multi_floor_has_one_stair_at_same_xy()
    {
        var multi = Sample().Where(s => BuildingLayoutGenerator.FloorsOf(s.Id, s.Type) > 1).ToArray();
        Assert.NotEmpty(multi);
        foreach (var (id, type) in multi)
        {
            var layout = BuildingLayoutGenerator.Generate(id, type);
            Assert.Equal(layout.Floors, layout.FloorLayouts.Count);
            var stairs = layout.FloorLayouts
                .Select(f => f.Cells.Where(c => c.Material == BuildingMaterial.Stair).Select(c => c.Cell).ToArray())
                .ToArray();
            Assert.All(stairs, s => Assert.Single(s));
            Assert.Single(stairs.Select(s => s[0]).Distinct());
        }
    }

    [Fact]
    public void Single_floor_has_no_stair()
    {
        foreach (var (id, type) in Sample().Where(s => BuildingLayoutGenerator.FloorsOf(s.Id, s.Type) == 1))
        {
            var layout = BuildingLayoutGenerator.Generate(id, type);
            Assert.DoesNotContain(layout.FloorLayouts[0].Cells, c => c.Material == BuildingMaterial.Stair);
        }
    }

    [Fact]
    public void Door_exists_only_on_ground_floor()
    {
        foreach (var (id, type) in Sample().Where(s => BuildingLayoutGenerator.FloorsOf(s.Id, s.Type) > 1))
        {
            var layout = BuildingLayoutGenerator.Generate(id, type);
            Assert.Single(layout.FloorLayouts[0].Cells, c => c.Material == BuildingMaterial.Door);
            Assert.All(layout.FloorLayouts.Skip(1), f => Assert.DoesNotContain(f.Cells, c => c.Material == BuildingMaterial.Door));
        }
    }
}
