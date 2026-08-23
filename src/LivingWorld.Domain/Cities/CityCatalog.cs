namespace LivingWorld.Domain;

/// <summary>Local de trabalho provisionado ao concluir um edifício (Fase 15.1, Stage 4, T10,
/// LWV-04.1) — opcional; ausente significa só moradia/infraestrutura.</summary>
public sealed record WorkplaceProvision(int LocationTypeId, int MaxVacancies);

/// <summary>Receita de construção de um tipo de edifício (Fase 8, T3, CITY-03): insumo total,
/// duração em ticks e capacidade de moradia provida ao concluir. Mesmo padrão de validação de
/// <see cref="ProductionRecipe.Create"/>.</summary>
public sealed record BuildingRecipe(
    IReadOnlyDictionary<ResourceType, long> Inputs, long TicksToBuild, long HousingCapacityProvided,
    WorkplaceProvision? Workplace = null)
{
    public static Result<BuildingRecipe> Create(
        IReadOnlyDictionary<ResourceType, long> inputs, long ticksToBuild, long housingCapacityProvided,
        WorkplaceProvision? workplace = null)
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

        return Result<BuildingRecipe>.Ok(new BuildingRecipe(inputs, ticksToBuild, housingCapacityProvided, workplace));
    }
}

/// <summary>Catálogo de tipo de edifício por período (Fase 8, T3, AD-023): id-only, sem
/// nome/apresentação no engine — mesmo padrão de <see cref="EconomyCatalog"/>.</summary>
public sealed record CityCatalog(IReadOnlyDictionary<int, BuildingRecipe> BuildingRecipes)
{
    /// <summary>Default de <see cref="WorldState"/> para cenário sem cidades declaradas — nenhuma
    /// receita de edifício.</summary>
    public static readonly CityCatalog Empty = new(new Dictionary<int, BuildingRecipe>());
}
