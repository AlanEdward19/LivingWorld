using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>Mutação física explícita de construtos; não toca moeda, estoque ou produção.</summary>
public static class ExtraordinaryConstructOperations
{
    public static Result<ExtraordinaryConstruct> Damage(
        WorldState world, TickContext ctx, long constructId, int amount)
    {
        if (amount <= 0)
            return Result<ExtraordinaryConstruct>.Fail("amount: deve ser positivo");
        var current = world.ExtraordinaryConstructs.FirstOrDefault(item => item.Id == constructId);
        if (current is null)
            return Result<ExtraordinaryConstruct>.Fail("constructId: ausente");

        int durability = Math.Max(0, current.Durability - amount);
        var updated = current with { Durability = durability };
        long damagedId = ctx.LogEvent(
            WorldEventKind.ExtraordinaryConstructDamaged,
            $"{current.CreatorId.Value}|{current.SourceInvocationId}|{current.Id}|{amount}|{durability}",
            sourceSystem: "ExtraordinaryConstructOperations");
        if (durability == 0)
        {
            world.RemoveExtraordinaryConstruct(current.Id);
            ctx.LogEvent(
                WorldEventKind.ExtraordinaryConstructRemoved,
                $"{current.CreatorId.Value}|{current.SourceInvocationId}|{current.Id}|destroyed",
                sourceSystem: "ExtraordinaryConstructOperations",
                causeEventId: damagedId);
        }
        else
        {
            world.ReplaceExtraordinaryConstruct(updated);
        }
        return Result<ExtraordinaryConstruct>.Ok(updated);
    }
}
