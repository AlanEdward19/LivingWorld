using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

public sealed class TeleportMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "npc.teleport";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var parsed = ExtraordinaryMechanicSupport.ParseAmount(declaration, "Effects", allowSigned: false);
        if (!parsed.IsSuccess) return Result<PreparedMutation?>.Fail(parsed.Error!);
        int maxDistance = parsed.Value.Amount;
        var invocation = ctx.Invocation;
        var world = ctx.World;
        var target = ctx.Target;

        if (invocation.TargetCell is not { } destination)
            return Result<PreparedMutation?>.Fail("Effects: npc.teleport exige uma célula alvo (TargetCell)");

        int distance = Math.Max(
            Math.Abs(destination.X - target.CurrentLocation.X),
            Math.Abs(destination.Y - target.CurrentLocation.Y));
        if (distance > maxDistance)
            return Result<PreparedMutation?>.Fail(
                $"Effects: npc.teleport excede o alcance ({distance} > {maxDistance})");

        var blocked = DestinationError(world, destination);
        if (blocked is not null) return Result<PreparedMutation?>.Fail(blocked);

        return Result<PreparedMutation?>.Ok(new PreparedMutation(
            declaration, _ => Move(target, destination, ctx.Tick.CurrentTick)));
    }

    internal static string? DestinationError(WorldState world, CellCoord destination, Npc? occupantIgnore = null)
    {
        if (!world.Map.TryGetCell(destination, out _))
            return "Effects: npc.teleport exige célula dentro do mapa";
        if (ExtraordinaryMechanicSupport.IsBuildingCell(world, destination))
            return "Effects: npc.teleport não pode mirar em prédio";
        if (world.ExtraordinaryConstructs.SelectMany(item => item.Footprint).Contains(destination))
            return "Effects: npc.teleport não pode mirar em constructo";
        if (world.Npcs.Any(npc => npc.IsAlive && npc != occupantIgnore && npc.CurrentLocation == destination))
            return "Effects: npc.teleport não pode mirar em célula ocupada";
        return null;
    }

    internal static void Move(Npc npc, CellCoord destination, long tick) => npc.MoveTo(destination, tick);
}
