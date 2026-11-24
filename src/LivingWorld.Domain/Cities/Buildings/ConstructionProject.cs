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

    /// <summary>AD-184: id da obra (o mesmo contador das obras do chão), dado quando o lote é escolhido; nulo antes.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? WorkId { get; set; }

    /// <summary>AD-184: o canteiro (prédio com obra em curso) que ocupa o lote; nulo antes do lote.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? Canteiro { get; set; }

    /// <summary>AD-184: oferta de jornal da cidade aos diaristas, em milésimos do jornal (nulo = o jornal do dia).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? OfferPm { get; set; }

    /// <summary>AD-184: último tick em que a oferta subiu e o da última hora de turma (parada e retomada).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? LastRaiseTick { get; set; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? LastProgressTick { get; set; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? Stalled { get; set; }

    /// <summary>AD-184: o evento que abriu a obra (causa dos eventos de parada, retomada e fim).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? StartEventId { get; set; }

    /// <summary>AD-195: material da receita já trazido do estoque da cidade (o largo) ao lote e ainda não posto (recurso → unidades);
    /// com a turma que anda (<c>StreetRules.HaulersWalk</c>) o passo da receita só consome daqui. Nulo = nada no lote.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, long>? MaterialOnSite { get; set; }

    /// <summary>AD-195: viagens de material em curso (quem da turma foi ao largo buscar e o que traz).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<ConstructionHaul>? Hauls { get; set; }

    /// <summary>AD-195: perdendo o dia pelo tempo e travada por falta de material (evento uma vez por episódio).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? WeatherLost { get; set; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? MaterialShort { get; set; }

    // SPEC_DEVIATION: design.md declara Consumed só como dado de construção
    // (imutável). ConstructionSystem precisa registrar consumo tick a tick (AC "consumido ao
    // longo dos ticks") — sem mutador, Consumed nunca sairia de vazio.

    /// <summary>Acumula o consumo de <paramref name="resource"/> neste tick
    /// — só <see cref="ConstructionSystem"/> chama.</summary>
    public void RecordConsumption(ResourceType resource, long amount) =>
        _consumed[resource] = _consumed.GetValueOrDefault(resource) + amount;
}

/// <summary>AD-195: uma viagem de material da obra da fila: <see cref="Npc"/> vai ao largo (estágio 0), pega <see cref="Units"/> de
/// <see cref="Resource"/> do estoque da cidade e volta ao lote (estágio 1).</summary>
public sealed class ConstructionHaul
{
    public long Npc { get; set; }
    public int Stage { get; set; }
    public int Resource { get; set; }
    public long Units { get; set; }
}
