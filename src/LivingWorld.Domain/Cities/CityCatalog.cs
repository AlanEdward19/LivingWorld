namespace LivingWorld.Domain;

/// <summary>Receita de construção de um tipo de edifício (Fase 8, T3, CITY-03): insumo total,
/// duração em ticks e capacidade de moradia provida ao concluir. Mesmo padrão de validação de
/// <see cref="ProductionRecipe.Create"/>.</summary>
public sealed record BuildingRecipe(
    IReadOnlyDictionary<ResourceType, long> Inputs, long TicksToBuild, long HousingCapacityProvided)
{
    public static Result<BuildingRecipe> Create(
        IReadOnlyDictionary<ResourceType, long> inputs, long ticksToBuild, long housingCapacityProvided)
    {
        foreach (var (resource, amount) in inputs)
            if (amount < 0)
                return Result<BuildingRecipe>.Fail($"Inputs[{resource}]: deve ser >= 0");

        if (ticksToBuild <= 0)
            return Result<BuildingRecipe>.Fail("TicksToBuild: deve ser > 0");

        if (housingCapacityProvided < 0)
            return Result<BuildingRecipe>.Fail("HousingCapacityProvided: deve ser >= 0");

        return Result<BuildingRecipe>.Ok(new BuildingRecipe(inputs, ticksToBuild, housingCapacityProvided));
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
