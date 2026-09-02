namespace LivingWorld.Domain;

/// <summary>Obra em progresso na fila de uma <see cref="City"/> (Fase 8, T3, CITY-03) —
/// <see cref="Advance"/> consome um tick da receita declarada em <see cref="BuildingRecipe.TicksToBuild"/>,
/// nunca abaixo de zero (a conclusão é decidida pelo chamador ao ver <see cref="TicksRemaining"/> == 0,
/// mesmo espírito de <see cref="ResourceStock.Withdraw"/> não deixar estoque negativo).</summary>
public sealed class ConstructionProject(
    CityId city, int buildingTypeId, IReadOnlyDictionary<ResourceType, long> consumed, long ticksRemaining)
{
    private readonly Dictionary<ResourceType, long> _consumed = new(consumed);

    public CityId City { get; private set; } = city;
    public int BuildingTypeId { get; } = buildingTypeId;
    public IReadOnlyDictionary<ResourceType, long> Consumed => _consumed;
    public long TicksRemaining { get; private set; } = ticksRemaining;

    public void Advance()
    {
        if (TicksRemaining > 0)
            TicksRemaining--;
    }

    public void JoinCity(CityId city) => City = city;

    // SPEC_DEVIATION (Fase 8, T10): design.md declara Consumed só como dado de construção
    // (imutável). ConstructionSystem precisa registrar consumo tick a tick (AC "consumido ao
    // longo dos ticks") — sem mutador, Consumed nunca sairia de vazio.

    /// <summary>Acumula o consumo de <paramref name="resource"/> neste tick (Fase 8, T10,
    /// CITY-03) — só <see cref="ConstructionSystem"/> chama.</summary>
    public void RecordConsumption(ResourceType resource, long amount) =>
        _consumed[resource] = _consumed.GetValueOrDefault(resource) + amount;
}
