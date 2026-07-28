namespace LivingWorld.Domain;

/// <summary>Edifício concluído de uma <see cref="City"/> (Fase 8, T3, CITY-03). Mesmo molde de
/// <see cref="Workplace"/>: identidade + tipo do catálogo (<see cref="CityCatalog"/>), sem nome
/// nem apresentação no engine (AD-023).</summary>
public sealed class Building(BuildingId id, CityId city, int buildingTypeId, long completedAtTick)
{
    public BuildingId Id { get; } = id;
    public CityId City { get; } = city;
    public int BuildingTypeId { get; } = buildingTypeId;
    public long CompletedAtTick { get; } = completedAtTick;
}
