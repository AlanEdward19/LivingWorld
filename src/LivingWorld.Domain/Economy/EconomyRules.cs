namespace LivingWorld.Domain;

/// <summary>Todo parâmetro numérico da economia (Fase 5), cenário-driven (R3) — nenhum literal
/// em C#, mesmo padrão de <see cref="NeedsRules"/>.</summary>
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
    IReadOnlyDictionary<int, double> DemandBaselinePerNpc)
{
    public static Result<EconomyRules> Create(
        bool enabled, int foodResourceId, int waterResourceId,
        IReadOnlyDictionary<(int ResourceId, int LocationTypeId), long> capacityByResourceLocation,
        IReadOnlyDictionary<int, double> spoilagePerDayByResource,
        IReadOnlyDictionary<int, long> wageByProfession,
        IReadOnlyDictionary<int, long> priceFloor,
        IReadOnlyDictionary<int, long> priceCeiling,
        double priceSensitivity,
        IReadOnlyDictionary<int, double> demandBaselinePerNpc)
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

        return Result<EconomyRules>.Ok(new EconomyRules(
            enabled, foodResourceId, waterResourceId, capacityByResourceLocation, spoilagePerDayByResource,
            wageByProfession, priceFloor, priceCeiling, priceSensitivity, demandBaselinePerNpc));
    }

    /// <summary>Capacidade declarada para o par recurso/local; sem declaração, sem limite (o
    /// cenário só declara o que quer restringir).</summary>
    public long CapacityOf(ResourceType resource, LocationType locationType) =>
        CapacityByResourceLocation.TryGetValue((resource.Id, locationType.Id), out var capacity)
            ? capacity
            : long.MaxValue;

    /// <summary>Default de <see cref="WorldState"/> pra cenário que ainda não declara economia
    /// (T10) — <see cref="Enabled"/> falso, nenhum recurso/preço/salário. Nunca usado por um
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
