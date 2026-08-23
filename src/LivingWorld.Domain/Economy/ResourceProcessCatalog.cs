namespace LivingWorld.Domain;

public enum PreparationState
{
    Raw = 0,
    Prepared = 1,
}

public enum ProcessKind
{
    Cook = 0,
    CollectWater = 1,
    DeliverWater = 2,
    Plant = 3,
    Harvest = 4,
    WaterCrop = 5,
}

public enum ProcessStatus
{
    InProgress = 0,
    Completed = 1,
    Cancelled = 2,
}

public sealed record ResourceSpec(int Id, PreparationState Preparation, bool Edible)
{
    public static Result<ResourceSpec> Create(int id, PreparationState preparation, bool edible)
    {
        if (id <= 0) return Result<ResourceSpec>.Fail("Resources[].Id: deve ser > 0");
        if (!Enum.IsDefined(preparation))
            return Result<ResourceSpec>.Fail($"Resources[{id}].Preparation: valor desconhecido");

        return Result<ResourceSpec>.Ok(new ResourceSpec(id, preparation, edible));
    }
}

public sealed record ResourceCatalog(IReadOnlyDictionary<int, ResourceSpec> Specs)
{
    public static readonly ResourceCatalog Empty = new(new Dictionary<int, ResourceSpec>());

    public bool IsEdible(ResourceType resource) =>
        !Specs.TryGetValue(resource.Id, out var spec) || spec.Edible;
}

public sealed record ProcessRecipe(
    ProcessKind Kind,
    IReadOnlyDictionary<int, long> Inputs,
    int OutputResourceId,
    long OutputQuantity,
    int? WorkplaceTypeId,
    long DurationTicks)
{
    public static Result<ProcessRecipe> Create(
        ProcessKind kind, IReadOnlyDictionary<int, long> inputs, int outputResourceId, long outputQuantity,
        int? workplaceTypeId, long durationTicks)
    {
        if (!Enum.IsDefined(kind))
            return Result<ProcessRecipe>.Fail("ProcessRecipes[].Kind: valor desconhecido");
        foreach (var (resource, amount) in inputs)
            if (amount < 0)
                return Result<ProcessRecipe>.Fail($"ProcessRecipes[].Inputs[{resource}]: deve ser >= 0");
        if (outputResourceId <= 0)
            return Result<ProcessRecipe>.Fail("ProcessRecipes[].OutputResourceId: deve ser > 0");
        if (outputQuantity <= 0)
            return Result<ProcessRecipe>.Fail("ProcessRecipes[].OutputQuantity: deve ser > 0");
        if (durationTicks <= 0)
            return Result<ProcessRecipe>.Fail("ProcessRecipes[].DurationTicks: deve ser > 0");
        if (workplaceTypeId is <= 0)
            return Result<ProcessRecipe>.Fail("ProcessRecipes[].WorkplaceTypeId: deve ser > 0 quando declarado");

        return Result<ProcessRecipe>.Ok(new ProcessRecipe(
            kind, inputs, outputResourceId, outputQuantity, workplaceTypeId, durationTicks));
    }
}

public sealed class ResourceProcess
{
    public ResourceProcessId Id { get; }
    public ProcessKind Kind { get; }
    public NpcId ActorId { get; }
    public HouseholdId StockHolder { get; }
    public IReadOnlyDictionary<ResourceType, long> ReservedInputs { get; }
    public ResourceType Output { get; }
    public long OutputQuantity { get; }
    public long StartedAtTick { get; }
    public long CompletesAtTick { get; }
    public ProcessStatus Status { get; private set; }
    public long CropMatureDelayTicks { get; private set; }
    public long CropWaterRequired { get; private set; }

    public ResourceProcess(
        ResourceProcessId id, ProcessKind kind, NpcId actorId, HouseholdId stockHolder,
        IReadOnlyDictionary<ResourceType, long> reservedInputs, ResourceType output, long outputQuantity,
        long startedAtTick, long completesAtTick, ProcessStatus status = ProcessStatus.InProgress,
        long cropMatureDelayTicks = 0, long cropWaterRequired = 0)
    {
        Id = id;
        Kind = kind;
        ActorId = actorId;
        StockHolder = stockHolder;
        ReservedInputs = reservedInputs;
        Output = output;
        OutputQuantity = outputQuantity;
        StartedAtTick = startedAtTick;
        CompletesAtTick = completesAtTick;
        Status = status;
        CropMatureDelayTicks = cropMatureDelayTicks;
        CropWaterRequired = cropWaterRequired;
    }

    public void AttachCropPlan(long matureDelayTicks, long waterRequired)
    {
        CropMatureDelayTicks = matureDelayTicks;
        CropWaterRequired = waterRequired;
    }

    public void Complete() => Status = ProcessStatus.Completed;
    public void Cancel() => Status = ProcessStatus.Cancelled;
}
