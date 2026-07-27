namespace LivingWorld.Domain;

/// <summary>Recipe de produção de um <see cref="Workplace"/> (Fase 5): entrada/saída por
/// trabalhador por ciclo, recurso natural de célula exigido (ECON-08, opcional) e teto de
/// trabalhadores contados por ciclo.</summary>
public sealed record ProductionRecipe(
    IReadOnlyDictionary<int, long> Inputs,
    IReadOnlyDictionary<int, long> Outputs,
    int? RequiresCellResource,
    int MaxWorkersPerCycle)
{
    public static Result<ProductionRecipe> Create(
        IReadOnlyDictionary<int, long> inputs, IReadOnlyDictionary<int, long> outputs,
        int? requiresCellResource, int maxWorkersPerCycle)
    {
        if (maxWorkersPerCycle <= 0)
            return Result<ProductionRecipe>.Fail("MaxWorkersPerCycle: deve ser positivo");

        return Result<ProductionRecipe>.Ok(new ProductionRecipe(inputs, outputs, requiresCellResource, maxWorkersPerCycle));
    }
}

/// <summary>Catálogo de recipe por <see cref="LocationType"/> e vínculo profissão→local (Fase 5,
/// AD-043): ausência de entrada em <see cref="Recipes"/> = local sem produção física (guarda,
/// curandeiro, comerciante) — produção sempre 0 por ausência de recipe, nunca por trabalhador
/// ausente (essa distinção é do <c>ProductionSystem</c>, não deste catálogo).</summary>
public sealed record EconomyCatalog(
    IReadOnlyDictionary<int, ProductionRecipe> Recipes,
    HashSet<int> MarketLocationTypeIds,
    IReadOnlyDictionary<int, int> LocationTypeByProfession)
{
    /// <summary>Default de <see cref="WorldState"/> pra cenário sem economia declarada (T10) —
    /// nenhuma recipe, nenhum mercado, nenhum vínculo profissão→local.</summary>
    public static readonly EconomyCatalog Empty = new(
        new Dictionary<int, ProductionRecipe>(), [], new Dictionary<int, int>());
}
