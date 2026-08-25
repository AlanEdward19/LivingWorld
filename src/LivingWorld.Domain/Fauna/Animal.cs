namespace LivingWorld.Domain;

/// <summary>Entidade animal mínima (PWR-77). Não é NPC: sem personalidade, profissão ou família.</summary>
public readonly record struct AnimalId(long Value)
{
    public override string ToString() => $"animal-{Value}";
}

public sealed record Animal(
    AnimalId Id,
    string Species,
    CellCoord Position,
    bool IsAlive,
    string? VectorDisease = null);
