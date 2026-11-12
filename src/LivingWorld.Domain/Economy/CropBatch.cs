using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Economy;

public enum CropStatus
{
    Growing = 0,
    Mature = 1,
    Harvested = 2,
    /// <summary>Campo sazonal entre a colheita e a semeadura (AD-135): sem lavoura, celeiro e semente seguem.</summary>
    Fallow = 3,
}

/// <summary>Lote plantado com maturação e demanda de água declaradas.
/// Colheita só depois do tick de maturação e da água entregue — trigo instantâneo é proibido.</summary>
public sealed class CropBatch
{
    public CropBatchId Id { get; }
    public int CropResourceId { get; }
    public CellCoord Plot { get; }
    public long PlantedAtTick { get; }
    public long MatureAtTick { get; private set; }
    public long WaterRequired { get; }
    public long WaterDelivered { get; private set; }
    public CropStatus Status { get; private set; }

    /// <summary>Celeiro do campo sazonal (AD-135): grão colhido guardado até a próxima safra.</summary>
    public long Barn { get; private set; }
    /// <summary>Semente separada na colheita para o próximo plantio (não vai ao mercado).</summary>
    public long SeedReserve { get; private set; }
    /// <summary>Semente lançada na terra no ano agrícola corrente (proxy da área cultivada).</summary>
    public long LastSeedSown { get; private set; }
    /// <summary>Grão colhido na última safra (para leitura de rendimento).</summary>
    public long LastHarvest { get; private set; }
    /// <summary>Semente que gerou a safra madura ou a última colhida.</summary>
    public long CropSeed { get; private set; }
    /// <summary>Semente ponderada pela habilidade de quem semeou, em milésimos: potencial de rendimento da lavoura.</summary>
    public long EffectiveSeedMilli { get; private set; }
    /// <summary>Grau-dia acumulado desde a semeadura.</summary>
    public double Development { get; private set; }
    /// <summary>Grão maduro ainda em pé, à espera da foice.</summary>
    public long Standing { get; private set; }

    public CropBatch(
        CropBatchId id, int cropResourceId, CellCoord plot, long plantedAtTick, long matureAtTick,
        long waterRequired, long waterDelivered = 0, CropStatus status = CropStatus.Growing,
        long barn = 0, long seedReserve = 0, long lastSeedSown = 0, long lastHarvest = 0,
        long cropSeed = 0, long effectiveSeedMilli = 0, double development = 0, long standing = 0)
    {
        CropSeed = cropSeed;
        EffectiveSeedMilli = effectiveSeedMilli;
        Development = development;
        Standing = standing;
        Barn = barn;
        SeedReserve = seedReserve;
        LastSeedSown = lastSeedSown;
        LastHarvest = lastHarvest;
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

    /// <summary>Começa o ano agrícola: lavoura nova, ainda sem semente na terra.</summary>
    public void StartSeason()
    {
        Status = CropStatus.Growing;
        MatureAtTick = long.MaxValue;
        LastSeedSown = 0;
        EffectiveSeedMilli = 0;
        Development = 0;
        Standing = 0;
    }

    /// <summary>Semente lançada; <paramref name="effectiveMilli"/> já traz o peso da habilidade de quem semeou.</summary>
    public void Sow(long seed, long effectiveMilli)
    {
        LastSeedSown += seed;
        EffectiveSeedMilli += effectiveMilli;
    }

    public void Develop(double degreeDays) => Development += degreeDays;

    /// <summary>A lavoura amadurece com <paramref name="standing"/> de grão em pé.</summary>
    public void Ripen(long tick, long standing)
    {
        Status = CropStatus.Mature;
        MatureAtTick = tick;
        Standing = Math.Max(0, standing);
        CropSeed = LastSeedSown;
        LastHarvest = 0;
    }

    /// <summary>Ceifa <paramref name="amount"/> do grão em pé (limitado ao que há); devolve o ceifado.</summary>
    public long Reap(long amount)
    {
        long reaped = Math.Clamp(amount, 0, Standing);
        Standing -= reaped;
        LastHarvest += reaped;
        return reaped;
    }

    /// <summary>Grão em pé perdido (debulha, ave, chuva); devolve o perdido.</summary>
    public long LoseStanding(long amount)
    {
        long lost = Math.Clamp(amount, 0, Standing);
        Standing -= lost;
        return lost;
    }

    /// <summary>Fim do ano agrícola: o que restou em pé se perde; devolve o perdido.</summary>
    public long LayFallow()
    {
        long lost = Standing;
        Standing = 0;
        Status = CropStatus.Fallow;
        MatureAtTick = long.MaxValue;
        return lost;
    }

    public void StoreBarn(long amount) => Barn += Math.Max(0, amount);

    public long TakeBarn(long amount)
    {
        long taken = Math.Clamp(amount, 0, Barn);
        Barn -= taken;
        return taken;
    }

    public void StoreSeed(long amount) => SeedReserve += Math.Max(0, amount);

    public long TakeSeed(long amount)
    {
        long taken = Math.Clamp(amount, 0, SeedReserve);
        SeedReserve -= taken;
        return taken;
    }

    public void MarkHarvested() => Status = CropStatus.Harvested;

    public bool IsHarvestable(long now) =>
        Status != CropStatus.Harvested
        && now >= MatureAtTick
        && WaterDelivered >= WaterRequired;
}
