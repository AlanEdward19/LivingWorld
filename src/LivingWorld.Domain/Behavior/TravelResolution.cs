namespace LivingWorld.Domain;

/// <summary>Converte <see cref="MovementCost.Between"/> em ticks de deslocamento (Fase 4, task
/// 11/NEEDS-14): função pura, sem estado — reusada pelo <c>BehaviorDecisionSystem</c>.</summary>
public static class TravelResolution
{
    /// <summary>Locais distintos sempre consomem pelo menos 1 tick, mesmo com custo bruto
    /// abaixo de 1 (arredonda para cima); o mesmo local consome 0 ticks (sem deslocamento).
    /// <paramref name="movementCostMultiplier"/> (COH-23) escala o custo do mapa — default
    /// 1.0 preserva call sites legados.</summary>
    public static long TicksBetween(
        WorldMap map, CellCoord origin, CellCoord destination, double movementCostMultiplier = 1.0)
    {
        if (origin == destination) return 0;

        double cost = MovementCost.Between(map, origin, destination) * Math.Max(0.1, movementCostMultiplier);
        return Math.Max(1, (long)Math.Ceiling(cost));
    }
}
