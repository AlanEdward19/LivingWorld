namespace LivingWorld.Domain;

/// <summary>Navegação vertical entre andares (Fase 15.1, T46/ADR-0017) — aritmética pura, sem
/// estado escondido: <see cref="Up"/> seguido de <see cref="Down"/> (ou vice-versa) sempre
/// devolve o andar original.</summary>
public static class FloorNavigator
{
    public static FloorLevel Up(FloorLevel floor) => new(floor.Value + 1);

    public static FloorLevel Down(FloorLevel floor) => new(floor.Value - 1);
}
