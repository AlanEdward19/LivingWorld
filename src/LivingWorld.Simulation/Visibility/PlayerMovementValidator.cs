using LivingWorld.Domain;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Visibility;

/// <summary>Fase 15, T7 (spec.md story "Modo personagem com FOW", AC1; edge case "movimento
/// inválido"): valida a intenção de movimento antes de qualquer mutação — nunca muta o mundo
/// quando rejeita (hash canônico inalterado). Um "passo" de clique/WASD só alcança célula
/// adjacente (distância de Chebyshev 1); teleporte pelo mapa é rejeitado.</summary>
public static class PlayerMovementValidator
{
    public static Result<Unit> Validate(WorldState world, Npc npc, CellCoord target)
    {
        if (!npc.IsAlive) return Result<Unit>.Fail("npc morto não pode se mover");
        if (!world.Map.TryGetCell(target, out _)) return Result<Unit>.Fail("célula de destino fora do mapa");
        if (ChebyshevDistance(npc.CurrentLocation, target) > 1) return Result<Unit>.Fail("destino fora do alcance de um passo");

        return Result<Unit>.Ok(Unit.Value);
    }

    private static int ChebyshevDistance(CellCoord a, CellCoord b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}
