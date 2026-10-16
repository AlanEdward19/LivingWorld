using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Cities;

/// <summary>Local de trabalho provisionado ao concluir um edifício
/// — opcional; ausente significa só moradia/infraestrutura.</summary>
public sealed record WorkplaceProvision(int LocationTypeId, int MaxVacancies);

/// <summary>Termos de hospedagem de um tipo de edifício (opcional, vem do cenário): aluguel mensal por vaga e
/// quantos meses de atraso o inquilino aguenta antes do despejo. As vagas são <see cref="BuildingRecipe.HousingCapacityProvided"/>.
/// Com <see cref="BuildingRecipe.Pricing"/> o aluguel acompanha o valor do imóvel e <see cref="RentPerMonth"/> fica de reserva.
/// <see cref="NightlyRate"/> (0 = sem diária) é o preço-base da noite para quem está a mais de <see cref="GuestMinDistance"/>
/// células de casa e precisa de cama.</summary>
public sealed record LodgingTerms(long RentPerMonth, int GraceMonths, long NightlyRate = 0, int GuestMinDistance = 0);

/// <summary>Preço dinâmico de um imóvel (moradia ou estabelecimento), do cenário. O valor parte de
/// <see cref="BaseValue"/> e é multiplicado pelo local (centralidade na cidade, comércio por perto, beira d'água) e pela
/// pressão de demanda da cidade, sempre dentro de [<see cref="MinMultiplier"/>, <see cref="MaxMultiplier"/>].
/// O aluguel mensal é o valor vezes <see cref="RentYieldPerMonth"/>. Tudo zerado/neutro = preço fixo.</summary>
public sealed record PricingTerms(
    long BaseValue, double RentYieldPerMonth = 0, double CentralityWeight = 0, double AmenityWeight = 0, int AmenityRadius = 0,
    double WaterfrontWeight = 0, double DemandSensitivity = 0, double MinMultiplier = 1, double MaxMultiplier = 1);

/// <summary>Receita de construção de um tipo de edifício: insumo total,
/// duração em ticks e capacidade de moradia provida ao concluir. Mesmo padrão de validação de
/// <see cref="ProductionRecipe.Create"/>.</summary>
public sealed record BuildingRecipe(
    IReadOnlyDictionary<ResourceType, long> Inputs, long TicksToBuild, long HousingCapacityProvided,
    WorkplaceProvision? Workplace = null, LodgingTerms? Lodging = null, PricingTerms? Pricing = null)
{
    public static Result<BuildingRecipe> Create(
        IReadOnlyDictionary<ResourceType, long> inputs, long ticksToBuild, long housingCapacityProvided,
        WorkplaceProvision? workplace = null, LodgingTerms? lodging = null, PricingTerms? pricing = null)
    {
        foreach (var (resource, amount) in inputs)
            if (amount < 0)
                return Result<BuildingRecipe>.Fail($"Inputs[{resource}]: deve ser >= 0");

        if (ticksToBuild <= 0)
            return Result<BuildingRecipe>.Fail("TicksToBuild: deve ser > 0");

        if (housingCapacityProvided < 0)
            return Result<BuildingRecipe>.Fail("HousingCapacityProvided: deve ser >= 0");

        if (workplace is { MaxVacancies: <= 0 })
            return Result<BuildingRecipe>.Fail("Workplace.MaxVacancies: deve ser > 0 quando declarado");

        if (workplace is { LocationTypeId: <= 0 })
            return Result<BuildingRecipe>.Fail("Workplace.LocationTypeId: deve ser > 0 quando declarado");

        if (lodging is { RentPerMonth: <= 0 })
            return Result<BuildingRecipe>.Fail("Lodging.RentPerMonth: deve ser > 0 quando declarado");

        if (lodging is { GraceMonths: < 0 })
            return Result<BuildingRecipe>.Fail("Lodging.GraceMonths: deve ser >= 0");

        if (lodging is not null && housingCapacityProvided <= 0)
            return Result<BuildingRecipe>.Fail("Lodging: exige HousingCapacityProvided > 0 (são as vagas)");

        if (lodging is { NightlyRate: < 0 })
            return Result<BuildingRecipe>.Fail("Lodging.NightlyRate: deve ser >= 0");

        if (lodging is { NightlyRate: > 0, GuestMinDistance: <= 0 })
            return Result<BuildingRecipe>.Fail("Lodging.GuestMinDistance: deve ser > 0 quando há NightlyRate");

        if (pricing is not null)
        {
            if (pricing.BaseValue <= 0)
                return Result<BuildingRecipe>.Fail("Pricing.BaseValue: deve ser > 0");
            if (pricing.RentYieldPerMonth is < 0 or > 1)
                return Result<BuildingRecipe>.Fail("Pricing.RentYieldPerMonth: fora de [0,1]");
            if (pricing.CentralityWeight < 0 || pricing.AmenityWeight < 0 || pricing.WaterfrontWeight < 0 || pricing.DemandSensitivity < 0)
                return Result<BuildingRecipe>.Fail("Pricing: pesos e sensibilidade devem ser >= 0");
            if (pricing.AmenityRadius < 0)
                return Result<BuildingRecipe>.Fail("Pricing.AmenityRadius: deve ser >= 0");
            if (pricing.MinMultiplier <= 0 || pricing.MinMultiplier > pricing.MaxMultiplier)
                return Result<BuildingRecipe>.Fail("Pricing: MinMultiplier deve estar em (0, MaxMultiplier]");
            if (lodging is not null && pricing.RentYieldPerMonth <= 0)
                return Result<BuildingRecipe>.Fail("Pricing.RentYieldPerMonth: deve ser > 0 num prédio com Lodging");
        }

        return Result<BuildingRecipe>.Ok(new BuildingRecipe(inputs, ticksToBuild, housingCapacityProvided, workplace, lodging, pricing));
    }
}

/// <summary>Catálogo de tipo de edifício por período: id-only, sem
/// nome/apresentação no engine — mesmo padrão de <see cref="EconomyCatalog"/>.</summary>
public sealed record CityCatalog(IReadOnlyDictionary<int, BuildingRecipe> BuildingRecipes)
{
    // Derivados do catálogo imutável, calculados no bootstrap e fora do snapshot/hash.
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasLodging { get; } = BuildingRecipes.Values.Any(recipe => recipe.Lodging is not null);

    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasNightlyLodging { get; } = BuildingRecipes.Values.Any(recipe => recipe.Lodging is { NightlyRate: > 0 });

    /// <summary>Default de <see cref="WorldState"/> para cenário sem cidades declaradas — nenhuma
    /// receita de edifício.</summary>
    public static readonly CityCatalog Empty = new(new Dictionary<int, BuildingRecipe>());
}
