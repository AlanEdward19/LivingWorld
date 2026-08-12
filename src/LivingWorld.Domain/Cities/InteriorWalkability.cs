namespace LivingWorld.Domain;

/// <summary>Contrato de caminhabilidade de uma célula de footprint (Fase 15.1, T46/ADR-0017):
/// piso, porta e escada são caminháveis; parede não é.</summary>
public static class InteriorWalkability
{
    public static bool IsWalkable(BuildingMaterial material) => material switch
    {
        BuildingMaterial.Floor => true,
        BuildingMaterial.Door => true,
        BuildingMaterial.Stair => true,
        BuildingMaterial.StoneWall => false,
        BuildingMaterial.WoodWall => false,
        _ => throw new ArgumentOutOfRangeException(nameof(material), material, null),
    };
}
