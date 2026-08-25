namespace LivingWorld.Domain;

/// <summary>Organismo vegetal individual (PWR-101). Não substitui o estoque econômico de cultivo.</summary>
public readonly record struct PlantId(long Value)
{
    public override string ToString() => $"plant-{Value}";
}

public sealed record Plant(
    PlantId Id,
    string Species,
    CellCoord Position,
    int GrowthStage);
