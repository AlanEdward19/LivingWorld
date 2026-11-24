using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Visibility;

/// <summary>Valida a intenção de movimento antes de qualquer mutação — nunca muta o mundo
/// quando rejeita (hash canônico inalterado). Um "passo" de clique/WASD só alcança célula
/// adjacente (distância de Chebyshev 1); teleporte pelo mapa é rejeitado. AD-197d: o chão também
/// (<see cref="LivingWorld.Simulation.Players.PlayerGround.Step"/>): parede, porta e gente na célula.</summary>
public static class PlayerMovementValidator
{
    public static Result<Unit> Validate(WorldState world, Npc npc, CellCoord target, CellCoord? from = null)
    {
        var origin = from ?? npc.CurrentLocation;
        if (!npc.IsAlive) return Result<Unit>.Fail("npc morto não pode se mover");
        if (!world.Map.TryGetCell(target, out _)) return Result<Unit>.Fail("célula de destino fora do mapa");
        if (ChebyshevDistance(origin, target) != 1) return Result<Unit>.Fail("destino fora do alcance de um passo");

        return LivingWorld.Simulation.Players.PlayerGround.Step(world, npc, origin, target);
    }

    private static int ChebyshevDistance(CellCoord a, CellCoord b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}
