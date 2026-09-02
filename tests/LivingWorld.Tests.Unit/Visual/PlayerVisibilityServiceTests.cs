using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Visibility;

namespace LivingWorld.Tests.Unit.Visual;

/// <summary>Fase 15, T7 (spec.md story "Modo personagem com FOW", AC1/AC3): <see
/// cref="PlayerVisibilityService"/> — raio ao redor do personagem, override admin ignora o raio.</summary>
public class PlayerVisibilityServiceTests
{
    [Fact]
    public void CanSee_is_true_for_a_cell_within_the_sight_radius()
    {
        var player = new CellCoord(10, 10);
        var cell = new CellCoord(10 + PlayerVisibilityService.SightRadius, 10);

        Assert.True(PlayerVisibilityService.CanSee(cell, player, adminOverride: false));
    }

    [Fact]
    public void CanSee_is_false_for_a_cell_beyond_the_sight_radius()
    {
        var player = new CellCoord(10, 10);
        var cell = new CellCoord(10 + PlayerVisibilityService.SightRadius + 1, 10);

        Assert.False(PlayerVisibilityService.CanSee(cell, player, adminOverride: false));
    }

    [Fact]
    public void CanSee_is_always_true_with_admin_override_regardless_of_distance()
    {
        var player = new CellCoord(0, 0);
        var farCell = new CellCoord(1000, 1000);

        Assert.True(PlayerVisibilityService.CanSee(farCell, player, adminOverride: true));
    }
}
