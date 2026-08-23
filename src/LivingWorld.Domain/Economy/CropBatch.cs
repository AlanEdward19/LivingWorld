namespace LivingWorld.Domain;

public enum CropStatus
{
    Growing = 0,
    Mature = 1,
    Harvested = 2,
}

/// <summary>Lote plantado com maturação e demanda de água declaradas (Fase 15.1, Stage 4, T17).
/// Colheita só depois do tick de maturação e da água entregue — trigo instantâneo é proibido.</summary>
public sealed class CropBatch
{
    public CropBatchId Id { get; }
    public int CropResourceId { get; }
    public CellCoord Plot { get; }
    public long PlantedAtTick { get; }
    public long MatureAtTick { get; }
    public long WaterRequired { get; }
    public long WaterDelivered { get; private set; }
    public CropStatus Status { get; private set; }

    public CropBatch(
        CropBatchId id, int cropResourceId, CellCoord plot, long plantedAtTick, long matureAtTick,
        long waterRequired, long waterDelivered = 0, CropStatus status = CropStatus.Growing)
    {
        Id = id;
        CropResourceId = cropResourceId;
        Plot = plot;
        PlantedAtTick = plantedAtTick;
        MatureAtTick = matureAtTick;
        WaterRequired = waterRequired;
        WaterDelivered = waterDelivered;
        Status = status;
    }

    public static Result<CropBatch> Create(
        CropBatchId id, int cropResourceId, CellCoord plot, long plantedAtTick, long matureAtTick,
        long waterRequired)
    {
        if (cropResourceId <= 0) return Result<CropBatch>.Fail("Crops[].CropResourceId: deve ser > 0");
        if (matureAtTick <= plantedAtTick)
            return Result<CropBatch>.Fail("Crops[].MatureAtTick: deve ser posterior ao plantio");
        if (waterRequired < 0) return Result<CropBatch>.Fail("Crops[].WaterRequired: deve ser >= 0");

        return Result<CropBatch>.Ok(new CropBatch(id, cropResourceId, plot, plantedAtTick, matureAtTick, waterRequired));
    }

    public Result<Unit> ReceiveWater(long amount)
    {
        if (Status == CropStatus.Harvested)
            return Result<Unit>.Fail("crop: lote já colhido");
        if (amount <= 0) return Result<Unit>.Fail("crop: quantidade de água deve ser > 0");
        WaterDelivered += amount;
        return Result<Unit>.Ok(Unit.Value);
    }

    public void MarkMature() => Status = CropStatus.Mature;

    public void MarkHarvested() => Status = CropStatus.Harvested;

    public bool IsHarvestable(long now) =>
        Status != CropStatus.Harvested
        && now >= MatureAtTick
        && WaterDelivered >= WaterRequired;
}
