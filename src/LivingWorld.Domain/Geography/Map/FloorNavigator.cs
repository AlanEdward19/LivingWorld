using LivingWorld.Domain.Geography.Spatial;

namespace LivingWorld.Domain.Geography.Map;

/// <summary>Navegação vertical entre andares — aritmética pura, sem
/// estado escondido: <see cref="Up"/> seguido de <see cref="Down"/> (ou vice-versa) sempre
/// devolve o andar original.</summary>
public static class FloorNavigator
{
    public static FloorLevel Up(FloorLevel floor) => new(floor.Value + 1);

    public static FloorLevel Down(FloorLevel floor) => new(floor.Value - 1);
}
