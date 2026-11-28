namespace LivingWorld.Domain.Cities.Buildings;

/// <summary>Tamanho da planta de um prédio quando o cenário o declara (<c>Rooms.shapes</c>, dado e não código): <paramref name="Width"/> e
/// <paramref name="Depth"/> em células (1,5 m cada; a porta fica na borda de baixo) e <paramref name="Floors"/> andares. Sem ele vale a regra
/// de sempre por tipo (3×3 ou 3–4 por lado).</summary>
public readonly record struct BuildingShape(int Width, int Depth, int Floors)
{
    public const int MaxSide = 16;
    public const int MaxFloors = 3;
    public bool IsValid => Width is >= 3 and <= MaxSide && Depth is >= 3 and <= MaxSide && Floors is >= 1 and <= MaxFloors;
}
