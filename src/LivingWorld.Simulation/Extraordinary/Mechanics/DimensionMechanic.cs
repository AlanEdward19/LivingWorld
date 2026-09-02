using System.Globalization;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// Bolso dimensional e portal bidirecional (<c>dimension.pocket-store</c>,
/// <c>dimension.portal:&lt;xA&gt;,&lt;yA&gt;:&lt;xB&gt;,&lt;yB&gt;</c>).
/// </summary>
public sealed class DimensionMechanic : ExtraordinaryMechanic
{
    public const string PocketStoreToken = "dimension.pocket-store";
    public const string PortalPrefix = "dimension.portal:";

    public override string Prefix => "dimension.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (string.Equals(declaration, PocketStoreToken, StringComparison.Ordinal)
            || declaration.StartsWith(PocketStoreToken + ":", StringComparison.Ordinal))
            return PreparePocketStore(ctx, declaration);
        if (declaration.StartsWith(PortalPrefix, StringComparison.Ordinal))
            return PreparePortal(ctx, declaration);
        return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");
    }

    internal static bool PortalsStillActive(IReadOnlyList<PowerDescriptor> active) =>
        active.Any(descriptor => descriptor.Effects.Any(effect =>
            effect.StartsWith(PortalPrefix, StringComparison.Ordinal)));

    internal static void ApplyPortals(WorldState world, TickContext ctx)
    {
        var portals = world.ExtraordinaryCarriers
            .Where(carrier => carrier.IsManifested && carrier.DimensionalPortals is { Count: > 0 })
            .SelectMany(carrier => carrier.DimensionalPortals!.Select(portal => (carrier.CarrierId, portal)))
            .OrderBy(item => item.CarrierId.Value)
            .ThenBy(item => item.portal.CellA.X)
            .ThenBy(item => item.portal.CellA.Y)
            .ThenBy(item => item.portal.CellB.X)
            .ThenBy(item => item.portal.CellB.Y)
            .ToList();
        if (portals.Count == 0) return;

        var start = world.Npcs
            .Where(npc => npc.IsAlive)
            .OrderBy(npc => npc.Id.Value)
            .ToDictionary(npc => npc.Id, npc => npc.CurrentLocation);

        foreach (var (_, portal) in portals)
        {
            foreach (var npc in world.Npcs.Where(item => item.IsAlive).OrderBy(item => item.Id.Value))
            {
                if (!start.TryGetValue(npc.Id, out var origin)) continue;
                CellCoord? destination = origin == portal.CellA
                    ? portal.CellB
                    : origin == portal.CellB
                        ? portal.CellA
                        : null;
                if (destination is not { } dest) continue;
                var blocked = TeleportMechanic.DestinationError(world, dest, npc);
                if (blocked is not null && !blocked.Contains("ocupada", StringComparison.Ordinal))
                    continue;
                bool occupied = world.Npcs.Any(other =>
                    other.IsAlive
                    && other.Id != npc.Id
                    && other.CurrentLocation == dest
                    && start.GetValueOrDefault(other.Id) != dest);
                if (occupied) continue;
                TeleportMechanic.Move(npc, dest, ctx.CurrentTick);
            }
        }
    }

    private static Result<PreparedMutation?> PreparePocketStore(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var home = ctx.Carrier.Household is { } householdId
            ? ctx.World.FindHousehold(householdId)
            : null;
        if (home is null)
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");

        var world = ctx.World;
        var carrierId = ctx.Carrier.Id;
        var existing = world.ExtraordinaryCarriers.First(item => item.CarrierId == carrierId);
        bool retrieving = existing.DimensionalPocket is { Count: > 0 };
        ResourceType? origin = retrieving
            ? null
            : home.Stock
                .Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Key.Id)
                .Select(pair => (ResourceType?)pair.Key)
                .FirstOrDefault();
        if (!retrieving && origin is null)
            return Result<PreparedMutation?>.Fail($"Effects: saldo insuficiente para '{declaration}'");

        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
        {
            var carrier = world.ExtraordinaryCarriers.First(item => item.CarrierId == carrierId);
            var pocket = (carrier.DimensionalPocket ?? []).ToList();
            if (pocket.Count > 0)
            {
                foreach (var entry in pocket.OrderBy(item => item.ResourceId))
                    home.Deposit(new ResourceType(entry.ResourceId), entry.Quantity);
                world.UpsertExtraordinaryCarrier(carrier with { DimensionalPocket = [] });
                return;
            }

            var stored = origin!.Value;
            int qty = 1;
            if (declaration.Length > PocketStoreToken.Length + 1
                && TryParseInt(declaration[(PocketStoreToken.Length + 1)..], out int parsedQty)
                && parsedQty > 0)
                qty = parsedQty;
            var withdrawn = home.Withdraw(stored, qty);
            if (!withdrawn.IsSuccess) return;
            world.UpsertExtraordinaryCarrier(carrier with
            {
                DimensionalPocket = [new DimensionalPocketEntry(stored.Id, qty)],
            });
        }));
    }

    private static Result<PreparedMutation?> PreparePortal(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (!TryParsePortalCells(declaration, out var cellA, out var cellB))
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");
        if (cellA == cellB)
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");
        if (!ctx.World.Map.TryGetCell(cellA, out _) || !ctx.World.Map.TryGetCell(cellB, out _))
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");

        var world = ctx.World;
        var invocation = ctx.Invocation;
        var carrierId = ctx.Carrier.Id;
        var portal = new DimensionalPortal(cellA, cellB, invocation.PowerId, invocation.InvocationId);
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
        {
            var existing = world.ExtraordinaryCarriers.First(item => item.CarrierId == carrierId);
            var next = (existing.DimensionalPortals ?? [])
                .Where(item => item.PowerId != invocation.PowerId)
                .Append(portal)
                .OrderBy(item => item.CellA.X)
                .ThenBy(item => item.CellA.Y)
                .ThenBy(item => item.CellB.X)
                .ThenBy(item => item.CellB.Y)
                .ToList();
            world.UpsertExtraordinaryCarrier(existing with { DimensionalPortals = next });
        }));
    }

    private static bool TryParsePortalCells(string declaration, out CellCoord cellA, out CellCoord cellB)
    {
        cellA = default;
        cellB = default;
        if (!declaration.StartsWith(PortalPrefix, StringComparison.Ordinal)) return false;
        string rest = declaration[PortalPrefix.Length..];
        var compact = rest.Split(':', StringSplitOptions.TrimEntries);
        if (compact.Length == 4
            && TryParseInt(compact[0], out int xA)
            && TryParseInt(compact[1], out int yA)
            && TryParseInt(compact[2], out int xB)
            && TryParseInt(compact[3], out int yB))
        {
            cellA = new CellCoord(xA, yA);
            cellB = new CellCoord(xB, yB);
            return true;
        }

        var cells = rest.Split(':', 2, StringSplitOptions.TrimEntries);
        return cells.Length == 2
            && TryParseCell(cells[0], out cellA)
            && TryParseCell(cells[1], out cellB);
    }

    private static bool TryParseCell(string token, out CellCoord cell)
    {
        cell = default;
        var parts = token.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !TryParseInt(parts[0], out int x)
            || !TryParseInt(parts[1], out int y))
            return false;
        cell = new CellCoord(x, y);
        return true;
    }

    private static bool TryParseInt(string token, out int value) =>
        int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
        && token.Equals(value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
}

/// <summary>Detecta NPCs nas células do portal a cada hora e reusa o teleporte existente.</summary>
public sealed class DimensionPortalSystem : ISimulationSystem
{
    public const string SystemName = "DimensionPortal";
    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.Extraordinary.Enabled) return;
        DimensionMechanic.ApplyPortals(world, ctx);
    }
}
