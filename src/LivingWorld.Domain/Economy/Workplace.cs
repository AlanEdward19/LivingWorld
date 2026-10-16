using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Economy;

/// <summary>Local de produção/estoque/mercado — uma única entidade cobre
/// casa de trabalho, celeiro, loja, mercado e oficina; o papel é decidido pelo
/// <see cref="LocationType"/> do catálogo do cenário, não por subclasse. Mesmo molde de
/// <see cref="Household"/> (lista + construtor único de reidratação).</summary>
public sealed class Workplace
{
    public WorkplaceId Id { get; }
    public LocationType LocationType { get; }
    public CellCoord Location { get; }
    public int MaxVacancies { get; }

    /// <summary>Cidade proprietária deste workplace (dynamic-city-growth, ghost-town fix) — mesmo
    /// molde de <see cref="Building.City"/>/<see cref="Npc.City"/>. Default (Guid vazio) para
    /// workplaces autorados em cenário sem cidade associada, mesmo comportamento de antes desta
    /// mudança.</summary>
    public CityId City { get; private set; }

    /// <summary>Prédio onde este local funciona; <c>null</c> para workplace legado sem edifício.
    /// <see cref="Location"/> é a origem do footprint desse prédio.</summary>
    public BuildingId? Building { get; private set; }

    /// <summary>Dono da oficina (estate-inheritance): quem herda o espólio dele herda também a oficina. Só entra por
    /// autoria do cenário ou por herança; <c>null</c> é oficina sem dono (comum, da cidade).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NpcId? Owner { get; private set; }

    private readonly List<NpcId> _employees;
    public IReadOnlyList<NpcId> Employees => _employees;

    private readonly Dictionary<ResourceType, long> _stock;
    public IReadOnlyDictionary<ResourceType, long> Stock => _stock;

    public Money Treasury { get; private set; }

    private readonly Dictionary<ResourceType, long> _prices;

    /// <summary>Só relevante quando <see cref="LocationType"/> está em
    /// <see cref="EconomyCatalog.MarketLocationTypeIds"/> — vazio caso contrário.</summary>
    public IReadOnlyDictionary<ResourceType, long> Prices => _prices;

    public Workplace(
        WorkplaceId id, LocationType locationType, CellCoord location, int maxVacancies,
        IReadOnlyList<NpcId> employees, IReadOnlyDictionary<ResourceType, long> stock, Money treasury,
        IReadOnlyDictionary<ResourceType, long> prices, CityId city = default, BuildingId? building = null,
        NpcId? owner = null)
    {
        Id = id;
        LocationType = locationType;
        Location = location;
        MaxVacancies = maxVacancies;
        _employees = employees.ToList();
        _stock = new Dictionary<ResourceType, long>(stock);
        Treasury = treasury;
        _prices = new Dictionary<ResourceType, long>(prices);
        City = city;
        Building = building;
        Owner = owner;
    }

    /// <summary>Falha quando <see cref="Employees"/> já está no teto de
    /// <see cref="MaxVacancies"/> — nunca aceita além da vaga declarada.</summary>
    public Result<Unit> Hire(NpcId npc)
    {
        if (_employees.Count >= MaxVacancies)
            return Result<Unit>.Fail("Employees: MaxVacancies atingido");

        if (!_employees.Contains(npc))
            _employees.Add(npc);
        return Result<Unit>.Ok(Unit.Value);
    }

    public void Fire(NpcId npc) => _employees.Remove(npc);

    public void JoinCity(CityId city) => City = city;

    public void AssignOwner(NpcId? owner) => Owner = owner;

    /// <summary>Soma até o limite de <see cref="EconomyRules.CapacityOf"/>; devolve as unidades
    /// perdidas por excesso de capacidade — nunca descartadas em silêncio, o chamador
    /// é quem decide registrar a perda como evento.</summary>
    public long Deposit(ResourceType resource, long amount, EconomyRules rules) =>
        ResourceStock.Deposit(_stock, resource, amount, rules.CapacityOf(resource, LocationType));

    /// <summary>Falha sem mutar o estoque quando insuficiente — nunca fica negativo.</summary>
    public Result<long> Withdraw(ResourceType resource, long amount) => ResourceStock.Withdraw(_stock, resource, amount);

    /// <summary>Recalculado pelo <c>MarketPricingSystem</c> — só relevante quando este
    /// <see cref="Workplace"/> é mercado (<see cref="EconomyCatalog.MarketLocationTypeIds"/>).</summary>
    public void SetPrice(ResourceType resource, long price) => _prices[resource] = price;

    public void CreditTreasury(Money amount) => Treasury += amount;

    /// <summary>Delega a <see cref="Money.TryDebit"/> — falha deixa <see cref="Treasury"/>
    /// byte-idêntica ao estado anterior.</summary>
    public Result<Money> TryDebitTreasury(Money amount)
    {
        var result = Treasury.TryDebit(amount);
        if (result.IsSuccess)
            Treasury = result.Value;
        return result;
    }
}
