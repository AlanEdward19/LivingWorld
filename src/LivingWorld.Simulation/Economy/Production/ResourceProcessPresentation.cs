using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy.Production;

/// <summary>Projeção de processos materiais, carga em trânsito e cultivos
/// (Fase 15.1, Stage 4, T15–T17) — progresso visual derivado do estado canônico.</summary>
public static class ResourceProcessPresentation
{
    public const long ProcessIdOffset = 2_000_000;
    public const long CarryIdOffset = 3_000_000;
    public const long CropIdOffset = 4_000_000;

    public static MaterialProcessSnapshot ToProcess(WorldState world, ResourceProcess process)
    {
        long duration = Math.Max(1, process.CompletesAtTick - process.StartedAtTick);
        long remaining = Math.Max(0, process.CompletesAtTick - world.CurrentDate.TotalHours);
        double progress = 1.0 - remaining / (double)duration;
        var actor = world.FindNpc(process.ActorId);
        return new MaterialProcessSnapshot(
            ProcessIdOffset + process.Id.Value, KindOf(process.Kind), process.ActorId.Value,
            Math.Clamp(progress, 0, 1), DescriptorOf(process.Kind), remaining,
            actor?.CurrentLocation);
    }

    public static MaterialProcessSnapshot? CarryOf(Npc npc) =>
        npc.IsAlive && npc.IsCarrying
            ? new MaterialProcessSnapshot(
                CarryIdOffset + npc.Id.Value, "water", npc.Id.Value, 1, "carry-water", 0, npc.CurrentLocation)
            : null;

    public static MaterialProcessSnapshot ToCrop(WorldState world, CropBatch crop)
    {
        long duration = Math.Max(1, crop.MatureAtTick - crop.PlantedAtTick);
        long remaining = Math.Max(0, crop.MatureAtTick - world.CurrentDate.TotalHours);
        double progress = crop.Status == CropStatus.Mature ? 1 : Math.Clamp(1.0 - remaining / (double)duration, 0, 1);
        string descriptor = crop.WaterDelivered < crop.WaterRequired
            ? "water-crop"
            : crop.Status == CropStatus.Mature ? "harvest-crop" : "plant-crop";
        return new MaterialProcessSnapshot(
            CropIdOffset + crop.Id.Value, "crop", crop.Id.Value, progress, descriptor, remaining, crop.Plot);
    }

    private static string KindOf(ProcessKind kind) => kind switch
    {
        ProcessKind.Cook => "cook",
        ProcessKind.CollectWater or ProcessKind.DeliverWater => "water",
        ProcessKind.Plant or ProcessKind.Harvest or ProcessKind.WaterCrop => "crop",
        _ => "process",
    };

    private static string DescriptorOf(ProcessKind kind) => kind switch
    {
        ProcessKind.Cook => "cook-food",
        ProcessKind.CollectWater => "collect-water",
        ProcessKind.DeliverWater => "deliver-water",
        ProcessKind.Plant => "plant-crop",
        ProcessKind.Harvest => "harvest-crop",
        ProcessKind.WaterCrop => "water-crop",
        _ => "process",
    };
}

public sealed record MaterialProcessSnapshot(
    long Id, string Kind, long TargetId, double Progress, string DescriptorKey, long RemainingHours, CellCoord? Location);
