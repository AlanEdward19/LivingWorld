using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

public sealed class ConstructMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "construct.create";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } =
    [
        new("construct.create", "construct.create:<L>x<A>:<durabilidade>:<horas>:<aparência>", false, "carrier", static token => TryParse(token, out _, out _, out _, out _, out _), LooseHead: true),
    ];

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var world = ctx.World;
        var invocation = ctx.Invocation;
        var target = ctx.Target;
        var tickCtx = ctx.Tick;
        if (!TryParse(declaration, out int width, out int height, out int durability,
                out long durationHours, out string appearanceToken))
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
                tickCtx.CurrentTick, checked(tickCtx.CurrentTick + durationHours), appearanceToken);
            world.AddExtraordinaryConstruct(construct);
            tickCtx.LogEvent(
                WorldEventKind.ExtraordinaryConstructCreated,
                $"{invocation.CarrierId.Value}|{invocation.InvocationId}|{invocation.PowerId}|{id}", sourceSystem: "ConstructMechanic");
        }));
    }

    private static bool TryParse(
        string declaration, out int width, out int height, out int durability,
        out long durationHours, out string appearance)
    {
        width = height = durability = 0;
        durationHours = 0;
        appearance = "";
        var parts = declaration.Split(':', StringSplitOptions.TrimEntries);
        var dimensions = parts.Length == 5 ? parts[1].Split('x', StringSplitOptions.TrimEntries) : [];
        if (dimensions.Length != 2
            || !int.TryParse(dimensions[0], out width) || width is < 1 or > 8
            || !int.TryParse(dimensions[1], out height) || height is < 1 or > 8
            || !int.TryParse(parts[2], out durability) || durability <= 0
            || !long.TryParse(parts[3], out durationHours) || durationHours <= 0
            || string.IsNullOrWhiteSpace(parts[4]))
            return false;
        appearance = parts[4];
        return true;
    }
}
