using LivingWorld.Domain;

namespace LivingWorld.Simulation.Behavior;

/// <summary>Projeção de alimentação (Fase 15.1, Stage 4, LWV-03.2/LWV-06): recurso, preparo cru vs
/// preparado, duração restante e bloqueio — só enquanto a ação canônica é <see cref="ActionType.Eat"/>.</summary>
public static class FoodPresentation
{
    public const long ProcessIdOffset = 5_000_000;

    public static NpcFoodStatusDto? Of(WorldState world, Npc npc)
    {
        if (!npc.IsAlive || npc.CurrentAction != ActionType.Eat) return null;

        long duration = world.ActionCatalog.MaxDurationHours.GetValueOrDefault(ActionType.Eat, 1);
        long elapsed = Math.Max(0, world.CurrentDate.TotalHours - npc.ActionStartedAtTick);
        long remaining = Math.Max(0, duration - elapsed);

        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household)
            return new NpcFoodStatusDto(0, PreparationState.Raw, remaining, Blocked: true);

        var meal = FoodResolver.ResolveMeal(world, household);
        if (meal.Id <= 0)
            return new NpcFoodStatusDto(0, PreparationState.Raw, remaining, Blocked: true);

        var preparation = world.ResourceCatalog.Specs.TryGetValue(meal.Id, out var spec)
            ? spec.Preparation
            : PreparationState.Prepared;

        return new NpcFoodStatusDto(meal.Id, preparation, remaining, Blocked: false);
    }

    public static FoodProcessSnapshot ToProcess(WorldState world, Npc npc)
    {
        var status = Of(world, npc) ?? throw new InvalidOperationException($"npc {npc.Id}: sem refeição ativa");
        long duration = Math.Max(1, world.ActionCatalog.MaxDurationHours.GetValueOrDefault(ActionType.Eat, 1));
        double progress = 1.0 - status.RemainingHours / (double)duration;
        string descriptor = status.Preparation == PreparationState.Raw ? "eat-raw" : "eat-prepared";
        return new FoodProcessSnapshot(
            ProcessIdOffset + npc.Id.Value, npc.Id.Value, status, Math.Clamp(progress, 0, 1), descriptor);
    }
}

public sealed record FoodProcessSnapshot(
    long Id, long ActorId, NpcFoodStatusDto Status, double Progress, string DescriptorKey);
