using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Economy;

/// <summary>Recipe de produção de um <see cref="Workplace"/>: entrada/saída por
/// trabalhador por ciclo, recurso natural de célula exigido (opcional) e teto de
/// trabalhadores contados por ciclo.</summary>
public sealed record ProductionRecipe(
    IReadOnlyDictionary<int, long> Inputs,
    IReadOnlyDictionary<int, long> Outputs,
    int? RequiresCellResource,
    int MaxWorkersPerCycle)
{
    /// <summary>Ferramenta de que o ofício precisa (AD-144); <c>null</c> = ofício sem ferramenta (o modelo de antes).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ToolUse? Tool { get; init; }

    public static Result<ProductionRecipe> Create(
        IReadOnlyDictionary<int, long> inputs, IReadOnlyDictionary<int, long> outputs,
        int? requiresCellResource, int maxWorkersPerCycle)
    {
        if (maxWorkersPerCycle <= 0)
            return Result<ProductionRecipe>.Fail("MaxWorkersPerCycle: deve ser positivo");

        return Result<ProductionRecipe>.Ok(new ProductionRecipe(inputs, outputs, requiresCellResource, maxWorkersPerCycle));
    }
}

/// <summary>Ferramenta de ofício que se gasta (AD-144): cada trabalhador precisa de <see cref="KitPerWorker"/> unidades do
/// recurso <see cref="ResourceId"/> no estoque do local; cada dia trabalhado gasta <see cref="WearPerWorkerYear"/>/360 por
/// trabalhador presente; sem ferramenta o trabalho rende <see cref="UntooledOutput"/> (mão nua, ferramenta de pau). Quem faz
/// esse recurso trabalha por encomenda: não enche o depósito além do que os clientes da cidade usam.</summary>
public sealed record ToolUse(int ResourceId, double KitPerWorker, double WearPerWorkerYear, double UntooledOutput)
{
    public static Result<ToolUse> Create(int resourceId, double kitPerWorker, double wearPerWorkerYear, double untooledOutput)
    {
        if (!(kitPerWorker > 0)) return Result<ToolUse>.Fail("Tool.KitPerWorker: deve ser positivo");
        if (!(wearPerWorkerYear >= 0)) return Result<ToolUse>.Fail("Tool.WearPerWorkerYear: deve ser >= 0");
        if (!(untooledOutput is >= 0 and <= 1)) return Result<ToolUse>.Fail("Tool.UntooledOutput: deve estar entre 0 e 1");
        return Result<ToolUse>.Ok(new ToolUse(resourceId, kitPerWorker, wearPerWorkerYear, untooledOutput));
    }

    /// <summary>Unidades que equipam <paramref name="workers"/> trabalhadores.</summary>
    public long KitFor(int workers) => (long)Math.Ceiling(KitPerWorker * Math.Max(0, workers));

    /// <summary>Rendimento do trabalho com <paramref name="stock"/> unidades para <paramref name="workers"/> presentes:
    /// <see cref="UntooledOutput"/> sem nada, 1 com o jogo completo de cada um, linear entre os dois.</summary>
    public double OutputFactor(long stock, int workers)
    {
        if (workers <= 0) return 1.0;
        double coverage = Math.Min(1.0, Math.Max(0, stock) / (KitPerWorker * workers));
        return UntooledOutput + (1 - UntooledOutput) * coverage;
    }
}

/// <summary>Catálogo de recipe por <see cref="LocationType"/> e vínculo profissão→local:
/// ausência de entrada em <see cref="Recipes"/> = local sem produção física (guarda,
/// curandeiro, comerciante) — produção sempre 0 por ausência de recipe, nunca por trabalhador
/// ausente (essa distinção é do <c>ProductionSystem</c>, não deste catálogo).</summary>
public sealed record EconomyCatalog(
    IReadOnlyDictionary<int, ProductionRecipe> Recipes,
    HashSet<int> MarketLocationTypeIds,
    IReadOnlyDictionary<int, int> LocationTypeByProfession)
{
    /// <summary>Default de <see cref="WorldState"/> pra cenário sem economia declarada —
    /// nenhuma recipe, nenhum mercado, nenhum vínculo profissão→local.</summary>
    public static readonly EconomyCatalog Empty = new(
        new Dictionary<int, ProductionRecipe>(), [], new Dictionary<int, int>());

    /// <summary>Recurso que alguma receita usa como ferramenta (feito por encomenda).</summary>
    public bool IsTool(int resourceId)
    {
        foreach (var recipe in Recipes.Values)
            if (recipe.Tool?.ResourceId == resourceId) return true;
        return false;
    }
}
