using LivingWorld.Domain;

namespace LivingWorld.Simulation;

public sealed record PersonalityValues(
    int Extroversion, int Agreeableness, int Conscientiousness, int EmotionalStability,
    int Openness, int Ambition, int Loyalty, int Altruism, int Impulsivity, int RiskAversion);

/// <summary>Borda autoritativa única para intervenções do operador. Todos os argumentos são
/// validados antes da primeira escrita e cada aceite entra no log causal do mundo.</summary>
public static class WorldAuthoringCommands
{
    public static Result<Unit> RewritePersonality(
        WorldState world, TickContext ctx, NpcId npcId, PersonalityValues values)
    {
        var npc = world.FindNpc(npcId);
        if (npc is null || !npc.IsAlive) return Result<Unit>.Fail("NpcId: NPC ausente ou morto");
        var personality = Personality.Create(
            values.Extroversion, values.Agreeableness, values.Conscientiousness,
            values.EmotionalStability, values.Openness, values.Ambition, values.Loyalty,
            values.Altruism, values.Impulsivity, values.RiskAversion);
        if (!personality.IsSuccess) return Result<Unit>.Fail(personality.Error!);
        npc.RewritePersonality(personality.Value!);
        ctx.LogEvent(WorldEventKind.AuthoringCommandApplied, $"personality|{npcId.Value}");
        return Result<Unit>.Ok(Unit.Value);
    }

    public static Result<int> BreakRelationships(
        WorldState world, TickContext ctx, NpcId first, NpcId second)
    {
        if (first == second) return Result<int>.Fail("OtherNpcId: deve ser diferente");
        if (world.FindNpc(first) is null || world.FindNpc(second) is null)
            return Result<int>.Fail("NpcId: NPC ausente");
        int removed = world.RemoveRelationshipsBetween(first, second);
        ctx.LogEvent(WorldEventKind.AuthoringCommandApplied, $"relationships.break|{first.Value}|{second.Value}|{removed}");
        return Result<int>.Ok(removed);
    }

    public static Result<Unit> ForceAction(
        WorldState world, TickContext ctx, NpcId npcId, ActionType action)
    {
        if (!Enum.IsDefined(action)) return Result<Unit>.Fail("Action: ação fora do catálogo");
        var npc = world.FindNpc(npcId);
        if (npc is null || !npc.IsAlive) return Result<Unit>.Fail("NpcId: NPC ausente ou morto");
        npc.SetCurrentAction(action, ctx.CurrentTick);
        ctx.LogEvent(WorldEventKind.AuthoringCommandApplied, $"action|{npcId.Value}|{action}");
        return Result<Unit>.Ok(Unit.Value);
    }
}

public sealed class WorldAuthoringService(IWorldEventSink sink)
{
    private static TickContext Context(WorldState world, IWorldEventSink sink) =>
        new(world, world.Rng, world.Scheduler, sink);

    private Result<T> Run<T>(WorldState world, string operation, Func<TickContext, Result<T>> command)
    {
        var ctx = Context(world, sink);
        var result = command(ctx);
        if (!result.IsSuccess)
            ctx.LogEvent(WorldEventKind.AuthoringCommandRejected, $"{operation}|{result.Error}");
        return result;
    }

    public Result<ExtraordinaryCarrierState> Grant(WorldState world, NpcId npcId, string powerId) =>
        Run(world, "power.grant", ctx => ExtraordinaryStateSystem.GrantAuthored(world, ctx, npcId, powerId));

    public Result<ExtraordinaryCarrierState?> Revoke(WorldState world, NpcId npcId, string powerId) =>
        Run(world, "power.revoke", ctx => ExtraordinaryStateSystem.RevokeAuthored(world, ctx, npcId, powerId));

    public Result<ExtraordinaryInvocationResult> Invoke(
        WorldState world, NpcId carrierId, string powerId, NpcId targetId,
        CellCoord? targetCell, ResolutionResult? resolution) =>
        Run(world, "power.invoke", ctx => ExtraordinaryInvocationEngine.InvokeAuthored(
            world, ctx, carrierId, powerId, targetId, targetCell, resolution));

    public Result<Unit> RewritePersonality(WorldState world, NpcId npcId, PersonalityValues values) =>
        Run(world, "personality", ctx => WorldAuthoringCommands.RewritePersonality(world, ctx, npcId, values));

    public Result<int> BreakRelationships(WorldState world, NpcId first, NpcId second) =>
        Run(world, "relationships.break", ctx => WorldAuthoringCommands.BreakRelationships(world, ctx, first, second));

    public Result<Unit> ForceAction(WorldState world, NpcId npcId, ActionType action) =>
        Run(world, "action", ctx => WorldAuthoringCommands.ForceAction(world, ctx, npcId, action));
}
