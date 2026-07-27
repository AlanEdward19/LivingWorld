namespace LivingWorld.Domain;

/// <summary>Local de produção/estoque/mercado da Fase 5 (AD-043) — uma única entidade cobre
/// casa de trabalho, celeiro, loja, mercado e oficina; o papel é decidido pelo
/// <see cref="LocationType"/> do catálogo do cenário, não por subclasse. Mesmo molde de
/// <see cref="Household"/> (lista + construtor único de reidratação).</summary>
public sealed class Workplace
{
    public WorkplaceId Id { get; }
    public LocationType LocationType { get; }
    public CellCoord Location { get; }
    public int MaxVacancies { get; }

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
        IReadOnlyDictionary<ResourceType, long> prices)
    {
        Id = id;
        LocationType = locationType;
        Location = location;
        MaxVacancies = maxVacancies;
        _employees = employees.ToList();
        _stock = new Dictionary<ResourceType, long>(stock);
        Treasury = treasury;
        _prices = new Dictionary<ResourceType, long>(prices);
    }

    /// <summary>Falha (ECON-20) quando <see cref="Employees"/> já está no teto de
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

    /// <summary>Soma até o limite de <see cref="EconomyRules.CapacityOf"/>; devolve as unidades
    /// perdidas por excesso de capacidade (ECON-02) — nunca descartadas em silêncio, o chamador
    /// é quem decide registrar a perda como evento.</summary>
    public long Deposit(ResourceType resource, long amount, EconomyRules rules) =>
        ResourceStock.Deposit(_stock, resource, amount, rules.CapacityOf(resource, LocationType));

    /// <summary>Falha sem mutar o estoque quando insuficiente — nunca fica negativo.</summary>
    public Result<long> Withdraw(ResourceType resource, long amount) => ResourceStock.Withdraw(_stock, resource, amount);

    /// <summary>Recalculado pelo <c>MarketPricingSystem</c> (T16) — só relevante quando este
    /// <see cref="Workplace"/> é mercado (<see cref="EconomyCatalog.MarketLocationTypeIds"/>).</summary>
    public void SetPrice(ResourceType resource, long price) => _prices[resource] = price;

    public void CreditTreasury(Money amount) => Treasury += amount;

    /// <summary>Delega a <see cref="Money.TryDebit"/> — falha deixa <see cref="Treasury"/>
    /// byte-idêntica ao estado anterior (ECON-22).</summary>
    public Result<Money> TryDebitTreasury(Money amount)
    {
        var result = Treasury.TryDebit(amount);
        if (result.IsSuccess)
            Treasury = result.Value;
        return result;
    }
}
