namespace LivingWorld.Domain;

/// <summary>Obra em progresso na fila de uma <see cref="City"/> (Fase 8, T3, CITY-03) —
/// <see cref="Advance"/> consome um tick da receita declarada em <see cref="BuildingRecipe.TicksToBuild"/>,
/// nunca abaixo de zero (a conclusão é decidida pelo chamador ao ver <see cref="TicksRemaining"/> == 0,
/// mesmo espírito de <see cref="ResourceStock.Withdraw"/> não deixar estoque negativo).</summary>
public sealed class ConstructionProject(
    CityId city, int buildingTypeId, IReadOnlyDictionary<ResourceType, long> consumed, long ticksRemaining)
{
    public CityId City { get; } = city;
    public int BuildingTypeId { get; } = buildingTypeId;
    public IReadOnlyDictionary<ResourceType, long> Consumed { get; } = consumed;
    public long TicksRemaining { get; private set; } = ticksRemaining;

    public void Advance()
    {
        if (TicksRemaining > 0)
            TicksRemaining--;
    }
}
