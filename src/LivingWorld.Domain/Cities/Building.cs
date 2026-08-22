namespace LivingWorld.Domain;

/// <summary>Edifício concluído de uma <see cref="City"/> (Fase 8, T3, CITY-03). Mesmo molde de
/// <see cref="Workplace"/>: identidade + tipo do catálogo (<see cref="CityCatalog"/>). <see
/// cref="Position"/>/<see cref="Orientation"/> (Fase 15.1, T44) só existem para prédios
/// autorados no World Creator — <c>null</c> para os que o motor constrói durante a simulação
/// (<see cref="ConstructionSystem"/> ainda não escolhe posição/orientação, G4).</summary>
public sealed class Building(
    BuildingId id, CityId city, int buildingTypeId, long completedAtTick,
    CellCoord? position = null, int? orientation = null, long? clusterFoundingScheduledAtTick = null)
{
    public BuildingId Id { get; } = id;
    public CityId City { get; private set; } = city;
    public int BuildingTypeId { get; } = buildingTypeId;
    public long CompletedAtTick { get; } = completedAtTick;
    public CellCoord? Position { get; } = position;
    public int? Orientation { get; } = orientation;

    /// <summary>Tick em que a fundação de um cluster de overflow contendo este prédio foi agendada
    /// (dynamic-city-growth, T6, CITYGROW-04) — espelha <see cref="City.FoundingScheduledAtTick"/>.
    /// <c>null</c> enquanto nenhuma fundação estiver pendente; impede que o mesmo cluster seja
    /// agendado duas vezes.</summary>
    public long? ClusterFoundingScheduledAtTick { get; private set; } = clusterFoundingScheduledAtTick;

    public void MarkClusterFoundingScheduled(long tick) => ClusterFoundingScheduledAtTick = tick;

    /// <summary>Muda a cidade proprietária do prédio (dynamic-city-growth, T7, CITYGROW-04) —
    /// espelha <see cref="Npc.JoinCity"/>/<see cref="Household.JoinCity"/>: usado quando um cluster
    /// de overflow funda uma cidade nova e este prédio é reatribuído a ela.</summary>
    public void JoinCity(CityId newCity) => City = newCity;
}
