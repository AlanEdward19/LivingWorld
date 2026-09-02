using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Production;

/// <summary>Scheduler de processos materiais estagiados (Fase 15.1, Stage 4, T14–T17):
/// reserva insumos ao iniciar, devolve no cancelamento/morte, cria a saída só na conclusão.</summary>
public sealed class ResourceProcessSystem : ISimulationSystem
{
    public const string SystemName = "resource-process";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        foreach (var process in world.ResourceProcesses.OrderBy(item => item.Id.Value))
        {
            if (process.Status != ProcessStatus.InProgress) continue;

            var actor = world.FindNpc(process.ActorId);
            if (actor is null || !actor.IsAlive)
            {
                Cancel(world, process);
                continue;
            }

            if (ctx.CurrentTick < process.CompletesAtTick) continue;
            Complete(world, process);
        }
    }

    public static Result<ResourceProcess> Start(WorldState world, Npc npc, ProcessRecipe recipe, long now)
    {
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household)
            return Result<ResourceProcess>.Fail("household: estoque ausente");

        var locationError = ValidateLocation(world, npc, recipe, household, now);
        if (locationError is not null)
            return Result<ResourceProcess>.Fail(locationError);

        var reserved = new Dictionary<ResourceType, long>();
        foreach (var (resourceId, amount) in recipe.Inputs.OrderBy(pair => pair.Key))
        {
            if (amount == 0) continue;
            var resource = new ResourceType(resourceId);
            var withdrawn = household.Withdraw(resource, amount);
            if (!withdrawn.IsSuccess)
            {
                Refund(household, reserved);
                return Result<ResourceProcess>.Fail(withdrawn.Error!);
            }

            reserved[resource] = amount;
        }

        var process = new ResourceProcess(
            world.NextResourceProcessIdAndAdvance(), recipe.Kind, npc.Id, householdId, reserved,
            new ResourceType(recipe.OutputResourceId), recipe.OutputQuantity, now, now + recipe.DurationTicks);
        world.AddResourceProcess(process);
        return Result<ResourceProcess>.Ok(process);
    }

    public static void Cancel(WorldState world, ResourceProcess process)
    {
        if (process.Status != ProcessStatus.InProgress) return;
        if (world.FindHousehold(process.StockHolder) is { } household)
            Refund(household, process.ReservedInputs);
        process.Cancel();
    }

    private static string? ValidateLocation(WorldState world, Npc npc, ProcessRecipe recipe, Household household, long now)
    {
        if (recipe.Kind == ProcessKind.CollectWater)
        {
            if (!world.Map.TryGetCell(npc.CurrentLocation, out var cell) || !cell.HasWater)
                return "source: NPC fora da fonte de água";
            return null;
        }

        if (recipe.Kind == ProcessKind.DeliverWater)
        {
            if (npc.CurrentLocation != household.Location)
                return "destination: NPC fora do estoque alvo";
            if (npc.CarriedResourceId != recipe.OutputResourceId || npc.CarriedQuantity < recipe.OutputQuantity)
                return "carry: carga ausente ou insuficiente";
            return null;
        }

        if (recipe.WorkplaceTypeId is { } workplaceType
            && (npc.Employer is not { } employerId
                || world.FindWorkplace(employerId) is not { } workplace
                || workplace.LocationType.Id != workplaceType
                || npc.CurrentLocation != workplace.Location))
            return "workplace: local de preparação ausente ou NPC fora dele";

        if (recipe.Kind == ProcessKind.Plant && world.FindCropAt(npc.CurrentLocation) is not null)
            return "plot: já há um cultivo neste lote";

        if (recipe.Kind == ProcessKind.WaterCrop && world.FindCropAt(npc.CurrentLocation) is null)
            return "plot: cultivo ausente neste lote";

        if (recipe.Kind == ProcessKind.Harvest)
        {
            var crop = world.FindCropAt(npc.CurrentLocation);
            if (crop is null) return "plot: cultivo ausente neste lote";
            if (!crop.IsHarvestable(now)) return "crop: ainda não maduro ou sem água suficiente";
        }

        return null;
    }

    private static void Complete(WorldState world, ResourceProcess process)
    {
        if (world.FindHousehold(process.StockHolder) is not { } household)
        {
            process.Cancel();
            return;
        }

        var actor = world.FindNpc(process.ActorId);
        switch (process.Kind)
        {
            case ProcessKind.CollectWater:
                if (actor is null
                    || !actor.PickUp(
                        process.Output,
                        process.OutputQuantity,
                        AttributeMechanic.EffectiveCarryCapacity(world, actor)).IsSuccess)
                {
                    Cancel(world, process);
                    return;
                }
                break;
            case ProcessKind.DeliverWater:
                if (actor is null || !actor.GiveCarriedTo(household).IsSuccess)
                {
                    Cancel(world, process);
                    return;
                }
                break;
            case ProcessKind.Plant:
                CropSystem.CompletePlant(world, process, actor);
                ConsumeReserved(world, process);
                break;
            case ProcessKind.WaterCrop:
                CropSystem.CompleteWater(world, process, actor);
                ConsumeReserved(world, process);
                break;
            case ProcessKind.Harvest:
                CropSystem.CompleteHarvest(world, process, actor, household);
                ConsumeReserved(world, process);
                break;
            default:
                ConsumeReserved(world, process);
                household.Deposit(process.Output, process.OutputQuantity);
                world.RecordResourceProduced(process.Output, process.OutputQuantity);
                break;
        }

        process.Complete();
    }

    private static void ConsumeReserved(WorldState world, ResourceProcess process)
    {
        foreach (var (resource, amount) in process.ReservedInputs.OrderBy(pair => pair.Key.Id))
            world.RecordResourceConsumed(resource, amount);
    }

    private static void Refund(Household household, IReadOnlyDictionary<ResourceType, long> reserved)
    {
        foreach (var (resource, amount) in reserved.OrderBy(pair => pair.Key.Id))
            household.Deposit(resource, amount);
    }
}
