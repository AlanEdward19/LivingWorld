using LivingWorld.Domain;

namespace LivingWorld.Simulation;

public sealed class ConstructMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "construct.create";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var world = ctx.World;
        var invocation = ctx.Invocation;
        var target = ctx.Target;
        var tickCtx = ctx.Tick;
        var parts = declaration.Split(':', StringSplitOptions.TrimEntries);
        var dimensions = parts.Length == 5 ? parts[1].Split('x', StringSplitOptions.TrimEntries) : [];
        if (dimensions.Length != 2
            || !int.TryParse(dimensions[0], out int width) || width is < 1 or > 8
            || !int.TryParse(dimensions[1], out int height) || height is < 1 or > 8
            || !int.TryParse(parts[2], out int durability) || durability <= 0
            || !long.TryParse(parts[3], out long durationHours) || durationHours <= 0
            || string.IsNullOrWhiteSpace(parts[4]))
            return Result<PreparedMutation?>.Fail(
                "Effects: construct.create exige 'LxA:durabilidade:horas:aparência' válida");

        var carrier = ctx.Carrier;
        int directionX = Math.Sign(target.CurrentLocation.X - carrier.CurrentLocation.X);
        int directionY = Math.Sign(target.CurrentLocation.Y - carrier.CurrentLocation.Y);
        if (directionX == 0 && directionY == 0) directionX = 1;
        var origin = invocation.TargetCell ?? new CellCoord(
            target.CurrentLocation.X + directionX,
            target.CurrentLocation.Y + directionY);
        var footprint = Enumerable.Range(0, height)
            .SelectMany(y => Enumerable.Range(0, width)
                .Select(x => new CellCoord(origin.X + x, origin.Y + y)))
            .ToList();
        if (footprint.Any(cell => !world.Map.TryGetCell(cell, out _)))
            return Result<PreparedMutation?>.Fail("Effects: footprint do construto fora do mapa");
        if (world.ExtraordinaryConstructs.SelectMany(item => item.Footprint).Any(footprint.Contains))
            return Result<PreparedMutation?>.Fail("Effects: footprint do construto já ocupado");
        if (footprint.Any(cell => ExtraordinaryMechanicSupport.IsBuildingCell(world, cell)))
            return Result<PreparedMutation?>.Fail("Effects: footprint do construto ocupado por prédio");
        if (world.Npcs.Any(npc => npc.IsAlive && footprint.Contains(npc.CurrentLocation)))
            return Result<PreparedMutation?>.Fail("Effects: footprint do construto ocupado por NPC");

        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
        {
            long id = world.NextExtraordinaryConstructIdAndAdvance();
            var construct = new ExtraordinaryConstruct(
                id, invocation.CarrierId, invocation.PowerId, invocation.InvocationId,
                origin, footprint, durability, durability,
                tickCtx.CurrentTick, checked(tickCtx.CurrentTick + durationHours), parts[4]);
            world.AddExtraordinaryConstruct(construct);
            tickCtx.LogEvent(
                WorldEventKind.ExtraordinaryConstructCreated,
                $"{invocation.CarrierId.Value}|{invocation.InvocationId}|{invocation.PowerId}|{id}");
        }));
    }
}
