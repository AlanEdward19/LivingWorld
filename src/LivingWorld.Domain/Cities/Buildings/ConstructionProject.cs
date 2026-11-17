using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Cities.Buildings;

/// <summary>Obra em progresso na fila de uma <see cref="City"/> —
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

    /// <summary>AD-179: lote onde a obra acontece (a turma trabalha ali); -1 = ainda não escolhido.</summary>
    public int SiteX { get; set; } = -1;
    public int SiteY { get; set; } = -1;

    /// <summary>AD-179: diaristas da turma (ids de NPC); a obra só anda pelas horas deles no lote.</summary>
    public List<long> Crew { get; set; } = [];

    /// <summary>AD-179: horas trabalhadas no lote ainda não gastas pelo passo da fila (cada hora de turma = um tick da receita).</summary>
    public long HoursBanked { get; set; }

    /// <summary>AD-179: horas-homem trabalhadas e jornal pago até agora.</summary>
    public long HoursWorked { get; set; }
    public long Paid { get; set; }

    /// <summary>AD-179: horas de carregar o material do estoque da cidade (o largo) até o lote, ainda por fazer.</summary>
    public long HaulHoursLeft { get; set; }

    // SPEC_DEVIATION: design.md declara Consumed só como dado de construção
    // (imutável). ConstructionSystem precisa registrar consumo tick a tick (AC "consumido ao
    // longo dos ticks") — sem mutador, Consumed nunca sairia de vazio.

    /// <summary>Acumula o consumo de <paramref name="resource"/> neste tick
    /// — só <see cref="ConstructionSystem"/> chama.</summary>
    public void RecordConsumption(ResourceType resource, long amount) =>
        _consumed[resource] = _consumed.GetValueOrDefault(resource) + amount;
}
