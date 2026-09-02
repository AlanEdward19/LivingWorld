using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Behavior.Resolvers;

/// <summary>Projeção de descanso (Fase 15.1, Stage 4, T13, LWV-03.1/LWV-06): qualidade, lugar,
/// duração restante e bloqueio — só enquanto a ação canônica é <see cref="ActionType.Sleep"/>.</summary>
public static class RestPresentation
{
    public const long ProcessIdOffset = 1_000_000;

    public static NpcRestStatusDto? Of(WorldState world, Npc npc)
    {
        if (!npc.IsAlive || npc.CurrentAction != ActionType.Sleep) return null;

        var rest = RestPlaceResolver.Resolve(world, npc);
        bool atPlace = rest.Location == npc.CurrentLocation;
        bool reachable = RestPlaceResolver.IsReachable(world.Map, npc.CurrentLocation, rest.Location);
        long duration = world.ActionCatalog.MaxDurationHours.GetValueOrDefault(ActionType.Sleep, 1);
        long elapsed = Math.Max(0, world.CurrentDate.TotalHours - npc.ActionStartedAtTick);
        long remaining = Math.Max(0, duration - elapsed);

        return new NpcRestStatusDto(rest.Kind, rest.RecoveryEfficiency, rest.Location, remaining, Blocked: !atPlace && !reachable);
    }

    public static ProcessVisualSnapshot ToProcess(WorldState world, Npc npc)
    {
        var status = Of(world, npc) ?? throw new InvalidOperationException($"npc {npc.Id}: sem descanso ativo");
        long duration = Math.Max(1, world.ActionCatalog.MaxDurationHours.GetValueOrDefault(ActionType.Sleep, 1));
        double progress = 1.0 - status.RemainingHours / (double)duration;
        string kindKey = status.Kind.ToString().ToLowerInvariant();
        return new ProcessVisualSnapshot(
            ProcessIdOffset + npc.Id.Value, npc.Id.Value, status, Math.Clamp(progress, 0, 1), $"sleep-{kindKey}");
    }
}

public sealed record ProcessVisualSnapshot(
    long Id, long ActorId, NpcRestStatusDto Status, double Progress, string DescriptorKey);
