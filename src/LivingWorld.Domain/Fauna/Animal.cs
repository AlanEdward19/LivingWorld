using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;

namespace LivingWorld.Domain.Fauna;

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
    string? VectorDisease,
    LazyNeed Energy,
    long? DeathTick = null);
