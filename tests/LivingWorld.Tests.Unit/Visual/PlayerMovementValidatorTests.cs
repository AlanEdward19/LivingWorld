using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Visibility;

namespace LivingWorld.Tests.Visual;

/// <summary>Fase 15, T7 (spec.md story "Modo personagem com FOW", AC1; edge case "movimento
/// inválido"): <see cref="PlayerMovementValidator"/> — só aceita passo pra célula adjacente
/// existente no mapa, com NPC vivo; nunca muta o mundo (validação roda antes do <c>MoveTo</c>).</summary>
public class PlayerMovementValidatorTests
{
    private static (WorldState World, Npc Npc) MakeWorldWithNpc()
    {
        var world = ScenarioRunner.Create(seed: 31, initialPopulation: 1).World;
        return (world, world.Npcs.First());
    }

    [Fact]
    public void Validate_succeeds_for_an_adjacent_cell()
    {
        var (world, npc) = MakeWorldWithNpc();
        var adjacent = new CellCoord(npc.CurrentLocation.X + 1, npc.CurrentLocation.Y);

        var result = PlayerMovementValidator.Validate(world, npc, adjacent);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_fails_for_a_cell_more_than_one_step_away()
    {
        var (world, npc) = MakeWorldWithNpc();

        // Fase 15, Verifier fix (sensor: mutante > 1 -> > 5 sobreviveu porque X+5 caía fora do
        // mapa e o check de bounds mascarava o de distância): alvo a exatamente 2 passos, sempre
        // dentro do grid — isola a regra de distância do check de bounds.
        int width = world.Map.Width;
        int targetX = npc.CurrentLocation.X <= width / 2 ? npc.CurrentLocation.X + 2 : npc.CurrentLocation.X - 2;
        targetX = Math.Clamp(targetX, 0, width - 1);
        var twoStepsAway = new CellCoord(targetX, npc.CurrentLocation.Y);
        Assert.True(world.Map.TryGetCell(twoStepsAway, out _), "alvo do teste precisa existir no mapa");

        var result = PlayerMovementValidator.Validate(world, npc, twoStepsAway);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Validate_fails_for_a_cell_outside_the_map_bounds()
    {
        var (world, npc) = MakeWorldWithNpc();
        var outsideMap = new CellCoord(-1, -1);

        var result = PlayerMovementValidator.Validate(world, npc, outsideMap);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Validate_fails_for_a_dead_npc()
    {
        var (world, npc) = MakeWorldWithNpc();
        npc.Die(world.CurrentDate);
        var adjacent = new CellCoord(npc.CurrentLocation.X + 1, npc.CurrentLocation.Y);

        var result = PlayerMovementValidator.Validate(world, npc, adjacent);

        Assert.False(result.IsSuccess);
    }
}
