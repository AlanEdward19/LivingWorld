using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>
/// Precognição probabilística (PWR-120..122): <c>foresight.preview:&lt;evento&gt;</c>
/// roda <see cref="Resolver.Resolve"/> no mesmo cálculo que o evento usaria agora,
/// num RNG forked — sem mutar <see cref="WorldState"/> e sem gravar <see cref="Fact"/>.
/// </summary>
public sealed class ForesightMechanic : ExtraordinaryMechanic
{
    public const string PreviewPrefix = "foresight.preview:";

    public override string Prefix => "foresight.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (!declaration.StartsWith(PreviewPrefix, StringComparison.Ordinal)
            || declaration.Length == PreviewPrefix.Length)
        {
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");
        }

        string evento = declaration[PreviewPrefix.Length..];
        var world = ctx.World;
        var carrier = ctx.Carrier;
        var target = ctx.Target;
        var tick = ctx.Tick;
        var invocation = ctx.Invocation;

        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
        {
            var resolution = PreviewResolve(world, carrier, target, tick, invocation, evento);
            tick.LogEvent(
                WorldEventKind.ExtraordinaryEffectApplied,
                $"{PreviewPrefix}{evento}|{resolution}", sourceSystem: "ForesightMechanic");
        }));
    }

    internal static ResolutionResult PreviewResolve(
        WorldState world, Npc carrier, Npc target, TickContext tick,
        ExtraordinaryInvocation invocation, string evento)
    {
        int largestMagnitude = ExtraordinaryMechanicSupport.ParseAmount(evento, "Effects", allowSigned: true)
            is { IsSuccess: true, Value: var parsed }
            ? Math.Abs(parsed.Amount)
            : 1;
        int difficulty = 10 + (int)Math.Ceiling(largestMagnitude / 10d)
            + Math.Clamp((100 - target.Health) / 20, 0, 5);
        int capacity = LuckMechanic.AdjustCapacity(
            world, carrier, tick.CurrentTick,
            (int)Math.Clamp(
                Math.Round(carrier.Vitality / 10d + carrier.RateGene.Value * 5d), 0, 20));
        capacity = Math.Max(0, capacity + LuckMechanic.ManifestedCapacityBonus(world, target));
        string stream = $"extraordinary-resolution-{carrier.Id.Value}-{evento}-{invocation.InvocationId}";
        return Resolver.Resolve(
            difficulty, capacity, VarianceProfile.Dramatico("extraordinary"), tick.Rng(stream).Fork());
    }
}
