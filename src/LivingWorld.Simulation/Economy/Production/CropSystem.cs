using LivingWorld.Domain;

namespace LivingWorld.Simulation.Economy;

/// <summary>Cultivo estagiado <c>plant→water→mature→harvest</c> (Fase 15.1, Stage 4, T17).
/// Substitui o trigo instantâneo da fazenda default; colheita antecipada é rejeitada.</summary>
public sealed class CropSystem : ISimulationSystem
{
    public const string SystemName = "crop-lifecycle";
    public const int DefaultFarmLocationTypeId = 1;
    public const long DefaultMatureDelayTicks = 24;
    public const long DefaultHarvestPerWorker = 10;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.EconomyRules.Enabled) return;

        foreach (var crop in world.CropBatches.OrderBy(item => item.Id.Value))
        {
            if (crop.Status == CropStatus.Growing && ctx.CurrentTick >= crop.MatureAtTick)
                crop.MarkMature();
        }

        var vacancyIndex = VacancyIndex.BuildForTick(world);
        foreach (var workplace in world.Workplaces.OrderBy(item => item.Id.Value))
        {
            if (workplace.LocationType.Id != DefaultFarmLocationTypeId) continue;
            var workers = vacancyIndex.PresentWorkersAt(workplace);
            if (workers.Count == 0) continue;

            _ = ReadCellTemperature(world, workplace.Location, ctx.CurrentTick);

            var crop = world.FindCropAt(workplace.Location);
            if (crop is { } harvestable && harvestable.IsHarvestable(ctx.CurrentTick))
            {
                long yield = DefaultHarvestPerWorker * workers.Count;
                workplace.Deposit(new ResourceType(harvestable.CropResourceId), yield, world.EconomyRules);
                world.RecordResourceProduced(new ResourceType(harvestable.CropResourceId), yield);
                harvestable.MarkHarvested();

                // Replanta no mesmo tick da colheita — sem isso o plantio só acontecia no
                // próximo Tick() (FindCropAt já ignora lotes colhidos), somando um dia ocioso
                // a cada ciclo e cortando a produção de comida pela metade sem motivo.
                var replanted = CropBatch.Create(
                    world.NextCropBatchIdAndAdvance(), world.EconomyRules.FoodResourceId, workplace.Location,
                    ctx.CurrentTick, ctx.CurrentTick + DefaultMatureDelayTicks, waterRequired: 0);
                if (replanted.IsSuccess)
                    world.AddCropBatch(replanted.Value!);
                continue;
            }

            if (crop is { } growing && growing.WaterDelivered < growing.WaterRequired)
            {
                var water = new ResourceType(world.EconomyRules.WaterResourceId);
                long needed = growing.WaterRequired - growing.WaterDelivered;
                long available = workplace.Stock.GetValueOrDefault(water);
                long give = Math.Min(needed, available);
                if (give > 0 && workplace.Withdraw(water, give).IsSuccess)
                    growing.ReceiveWater(give);
                continue;
            }

            if (crop is null)
            {
                var planted = CropBatch.Create(
                    world.NextCropBatchIdAndAdvance(), world.EconomyRules.FoodResourceId, workplace.Location,
                    ctx.CurrentTick, ctx.CurrentTick + DefaultMatureDelayTicks, waterRequired: 0);
                if (planted.IsSuccess)
                    world.AddCropBatch(planted.Value!);
            }
        }
    }

    /// <summary>PWR-76: o cultivo lê a temperatura da célula; a fórmula de rendimento não muda nesta fase.</summary>
    public static float ReadCellTemperature(WorldState world, CellCoord cell, long currentTick) =>
        EnvironmentTemperatureMechanic.EffectiveTemperature(world, cell, currentTick);

    public static Result<ResourceProcess> Plant(WorldState world, Npc npc, long now, long matureDelayTicks, long waterRequired)
    {
        var recipe = ProcessRecipe.Create(
            ProcessKind.Plant, new Dictionary<int, long>(), world.EconomyRules.FoodResourceId, 1, null,
            durationTicks: 1).Value!;
        var started = ResourceProcessSystem.Start(world, npc, recipe, now);
        if (!started.IsSuccess) return started;

        started.Value!.AttachCropPlan(matureDelayTicks, waterRequired);
        return started;
    }

    public static Result<ResourceProcess> Water(WorldState world, Npc npc, long now, long quantity = 1)
    {
        var water = world.EconomyRules.WaterResourceId;
        var recipe = ProcessRecipe.Create(
            ProcessKind.WaterCrop, new Dictionary<int, long> { [water] = quantity }, water, quantity, null, 1).Value!;
        return ResourceProcessSystem.Start(world, npc, recipe, now);
    }

    public static Result<ResourceProcess> Harvest(WorldState world, Npc npc, long now, long quantity)
    {
        var recipe = ProcessRecipe.Create(
            ProcessKind.Harvest, new Dictionary<int, long>(), world.EconomyRules.FoodResourceId, quantity, null, 1).Value!;
        return ResourceProcessSystem.Start(world, npc, recipe, now);
    }

    internal static void CompletePlant(WorldState world, ResourceProcess process, Npc? actor)
    {
        if (actor is null) return;
        long matureDelay = Math.Max(1, process.CropMatureDelayTicks);
        var crop = CropBatch.Create(
            world.NextCropBatchIdAndAdvance(), process.Output.Id, actor.CurrentLocation,
            process.StartedAtTick, process.StartedAtTick + matureDelay, process.CropWaterRequired);
        if (crop.IsSuccess)
            world.AddCropBatch(crop.Value!);
    }

    internal static void CompleteWater(WorldState world, ResourceProcess process, Npc? actor)
    {
        if (actor is null) return;
        world.FindCropAt(actor.CurrentLocation)?.ReceiveWater(process.OutputQuantity);
    }

    internal static void CompleteHarvest(WorldState world, ResourceProcess process, Npc? actor, Household household)
    {
        if (actor is null) return;
        var crop = world.FindCropAt(actor.CurrentLocation);
        if (crop is null || !crop.IsHarvestable(process.CompletesAtTick))
            return;

        household.Deposit(process.Output, process.OutputQuantity);
        world.RecordResourceProduced(process.Output, process.OutputQuantity);
        crop.MarkHarvested();
    }
}
