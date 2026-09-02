using System.Globalization;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

public sealed record EnvironmentTemperatureAdjustment(RegionId Region, float Delta, long UntilTick);

/// <summary>
/// Clima local: <c>environment.temperature:&lt;região&gt;:&lt;delta&gt;:&lt;duração&gt;</c>
/// ajusta células da região até o tick de expiração, depois o valor volta ao base gerado.
/// </summary>
public sealed class EnvironmentTemperatureMechanic : ExtraordinaryMechanic
{
    public const string TokenPrefix = "environment.temperature:";

    public override string Prefix => "environment.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (!TryParse(declaration, out var region, out float delta, out long durationTicks))
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");

        if (ctx.World.Map.Regions.All(item => item.Id != region))
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");

        var world = ctx.World;
        long untilTick = checked(ctx.Tick.CurrentTick + durationTicks);
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
            world.AddEnvironmentTemperatureAdjustment(
                new EnvironmentTemperatureAdjustment(region, delta, untilTick))));
    }

    public static float EffectiveTemperature(WorldState world, CellCoord coord, long currentTick)
    {
        if (!world.Map.TryGetCell(coord, out var cell))
            return 0;

        var region = world.Map.RegionOf(coord);
        float delta = 0;
        foreach (var adjustment in world.EnvironmentTemperatureAdjustments)
        {
            if (adjustment.Region != region || adjustment.UntilTick <= currentTick)
                continue;
            delta += adjustment.Delta;
        }

        return cell.Temperature + delta;
    }

    internal static bool TryParse(
        string declaration, out RegionId region, out float delta, out long durationTicks)
    {
        region = default;
        delta = 0;
        durationTicks = 0;
        if (!declaration.StartsWith(TokenPrefix, StringComparison.Ordinal))
            return false;

        var parts = declaration[TokenPrefix.Length..].Split(':');
        if (parts.Length != 3)
            return false;
        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int regionValue)
            || !parts[0].Equals(regionValue.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            return false;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out delta))
            return false;
        if (!long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out durationTicks)
            || durationTicks <= 0
            || !parts[2].Equals(durationTicks.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            return false;

        region = new RegionId(regionValue);
        return true;
    }
}
