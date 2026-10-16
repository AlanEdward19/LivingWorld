using LivingWorld.Domain.Geography;

namespace LivingWorld.Simulation.Visibility;

/// <summary>Visibilidade por
/// raio ao redor da posição atual do personagem. O motor não guarda histórico espacial por NPC
/// (nenhuma "célula já visitada" persistida) — a segunda cláusula do AC2 ("áreas visitadas
/// permanecem visíveis") fica deferida até o domínio ganhar memória espacial; hoje FOW é só
/// raio. <c>adminOverride</c> ignora o raio (AC3: admin libera visão total).</summary>
public static class PlayerVisibilityService
{
    public const int SightRadius = 5;

    public static bool CanSee(CellCoord cell, CellCoord playerLocation, bool adminOverride) =>
        adminOverride || ChebyshevDistance(cell, playerLocation) <= SightRadius;

    private static int ChebyshevDistance(CellCoord a, CellCoord b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}
