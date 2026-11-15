using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Economy;

/// <summary>Todo parâmetro numérico da economia é cenário-driven — nenhum literal
/// em C#, mesmo padrão de <see cref="NeedsRules"/>. <c>WageTaxRate</c>: fração do salário que a cidade do empregador retém
/// (0 = sem imposto).</summary>
public sealed record EconomyRules(
    bool Enabled,
    int FoodResourceId,
    int WaterResourceId,
    IReadOnlyDictionary<(int ResourceId, int LocationTypeId), long> CapacityByResourceLocation,
    IReadOnlyDictionary<int, double> SpoilagePerDayByResource,
    IReadOnlyDictionary<int, long> WageByProfession,
    IReadOnlyDictionary<int, long> PriceFloor,
    IReadOnlyDictionary<int, long> PriceCeiling,
    double PriceSensitivity,
    IReadOnlyDictionary<int, double> DemandBaselinePerNpc,
    double WageTaxRate = 0.0)
{
    /// <summary>Fração mensal do caixa do empregador acima da reserva de giro que sai como lucro (AD-144, absorve o dízimo
    /// provisório da AD-140): vai à carteira do dono vivo (<see cref="Workplace.Owner"/>) ou, na oficina sem dono (terra comum,
    /// obra da cidade, espólio sem herdeiro), ao tesouro da cidade, que paga a esmola. 0 = o caixa só acumula (o modelo de
    /// antes). O nome serializado continua o antigo para cenário JSON e snapshot gravados antes da troca.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("SurplusLevyShare")]
    public double SurplusDrawShare { get; init; }

    /// <summary>Reserva de giro que o empregador guarda antes da retirada, em meses da folha (mínimo de um salário).</summary>
    public double WorkingCapitalPayrollMonths { get; init; } = 12;

    /// <summary>AD-148 (backlog 04, E1): a produção conta só quem está trabalhando no local, não quem está lá por outro motivo
    /// (na fila do mercado que fica na fazenda). **Desligado por padrão**: com o trabalho consertado a fila caiu a 1,5% dos
    /// lavradores às 10 h, mas ligar isto levou a fome de 3,5% a 9,2% (6 seeds × 100 anos): pede recalibrar a lavoura.</summary>
    public bool PresenceNeedsWork { get; init; }

    /// <summary>AD-151: a refeição que o empregador dá do estoque do local (a "ração da oficina") é parte do pagamento, como o
    /// salário "com comida" da vila pré-industrial: cada unidade de comida vai à caderneta (<see cref="Workplace.Board"/>) ao preço
    /// da venda e sai do líquido do próximo salário, até esta fração dele; o resto a oficina paga em espécie. Ofício sem salário não
    /// tem refeição. A água do poço da propriedade segue livre, como o poço da aldeia.</summary>
    public double BoardMaxWageShare { get; init; } = 1.0;

    /// <summary>Só para medir (AD-151): a ração de antes, comida do estoque à venda sem ninguém pagar.</summary>
    public bool FreeWorkRation { get; init; }

    /// <summary>AD-151: sem refeição do empregador: longe de casa a comida é a que a pessoa leva ou compra (a água do poço segue).</summary>
    public bool NoWorkMeal { get; init; }

    /// <summary>AD-152: o NPC abre o próprio negócio com o próprio dinheiro (<see cref="VentureRules"/>). Nulo: ninguém empreende.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public VentureRules? Venture { get; init; }

    /// <summary>AD-155: empréstimo, capital de giro e falência (<see cref="CreditRules"/>). Nulo: ninguém empresta, nada fecha.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CreditRules? Credit { get; init; }

    public static Result<EconomyRules> Create(
        bool enabled, int foodResourceId, int waterResourceId,
        IReadOnlyDictionary<(int ResourceId, int LocationTypeId), long> capacityByResourceLocation,
        IReadOnlyDictionary<int, double> spoilagePerDayByResource,
        IReadOnlyDictionary<int, long> wageByProfession,
        IReadOnlyDictionary<int, long> priceFloor,
        IReadOnlyDictionary<int, long> priceCeiling,
        double priceSensitivity,
        IReadOnlyDictionary<int, double> demandBaselinePerNpc,
        double wageTaxRate = 0.0)
    {
        foreach (var (key, capacity) in capacityByResourceLocation)
            if (capacity < 0)
                return Result<EconomyRules>.Fail($"CapacityByResourceLocation[{key}]: deve ser >= 0");

        foreach (var (resource, spoilage) in spoilagePerDayByResource)
            if (spoilage < 0)
                return Result<EconomyRules>.Fail($"SpoilagePerDayByResource[{resource}]: deve ser >= 0");

        foreach (var (resource, floor) in priceFloor)
            if (priceCeiling.TryGetValue(resource, out var ceiling) && floor > ceiling)
                return Result<EconomyRules>.Fail($"PriceFloor[{resource}]: não pode exceder PriceCeiling");

        foreach (var (profession, wage) in wageByProfession)
            if (wage < 0)
                return Result<EconomyRules>.Fail($"WageByProfession[{profession}]: deve ser >= 0");

        if (!(wageTaxRate >= 0 && wageTaxRate <= 0.5)) // também recusa NaN
            return Result<EconomyRules>.Fail("WageTaxRate: deve estar entre 0 e 0,5");

        return Result<EconomyRules>.Ok(new EconomyRules(
            enabled, foodResourceId, waterResourceId, capacityByResourceLocation, spoilagePerDayByResource,
            wageByProfession, priceFloor, priceCeiling, priceSensitivity, demandBaselinePerNpc, wageTaxRate));
    }

    /// <summary>Capacidade declarada para o par recurso/local; sem declaração, sem limite (o
    /// cenário só declara o que quer restringir).</summary>
    public long CapacityOf(ResourceType resource, LocationType locationType) =>
        CapacityByResourceLocation.TryGetValue((resource.Id, locationType.Id), out var capacity)
            ? capacity
            : long.MaxValue;

    /// <summary>Default de <see cref="WorldState"/> pra cenário que ainda não declara economia
    /// — <see cref="Enabled"/> falso, nenhum recurso/preço/salário. Nunca usado por um
    /// cenário real (todo cenário econômico chama <see cref="Create"/> com valores dele).</summary>
    public static readonly EconomyRules Disabled = new(
        Enabled: false, FoodResourceId: 0, WaterResourceId: 0,
        CapacityByResourceLocation: new Dictionary<(int, int), long>(),
        SpoilagePerDayByResource: new Dictionary<int, double>(),
        WageByProfession: new Dictionary<int, long>(),
        PriceFloor: new Dictionary<int, long>(),
        PriceCeiling: new Dictionary<int, long>(),
        PriceSensitivity: 0,
        DemandBaselinePerNpc: new Dictionary<int, double>());
}
