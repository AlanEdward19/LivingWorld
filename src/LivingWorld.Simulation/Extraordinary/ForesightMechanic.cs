using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>
/// Precognição probabilística (PWR-120..122): <c>foresight.preview:&lt;evento&gt;</c>
/// roda <see cref="Resolver.Resolve"/> no mesmo cálculo que o evento usaria agora,
/// num RNG forked — sem mutar <see cref="WorldState"/> e sem gravar <see cref="Fact"/>.
/// O resultado fica disponível no tick corrente do portador (REALISM-30).
/// </summary>
public sealed class ForesightMechanic : ExtraordinaryMechanic
{
    public const string PreviewPrefix = "foresight.preview:";

    internal static readonly IReadOnlyDictionary<ActionType, ResolutionResult> EmptyPreviews =
        new Dictionary<ActionType, ResolutionResult>();

    private static readonly Dictionary<NpcId, Dictionary<ActionType, ResolutionResult>> PreviewsByCarrier = new();
    private static long _storedTick = -1;

    public override string Prefix => "foresight.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    internal static void EnsureTick(long tick)
    {
        if (_storedTick == tick) return;
        _storedTick = tick;
        PreviewsByCarrier.Clear();
    }

    internal static IReadOnlyDictionary<ActionType, ResolutionResult> PreviewsFor(NpcId carrier, long tick)
    {
        EnsureTick(tick);
        return PreviewsByCarrier.TryGetValue(carrier, out var previews) ? previews : EmptyPreviews;
    }

    internal static void StorePreview(NpcId carrier, long tick, string evento, ResolutionResult resolution)
    {
        EnsureTick(tick);
        if (!Enum.TryParse(evento, ignoreCase: true, out ActionType action)) return;

        if (!PreviewsByCarrier.TryGetValue(carrier, out var previews))
        {
            previews = new Dictionary<ActionType, ResolutionResult>();
            PreviewsByCarrier[carrier] = previews;
        }

        previews[action] = resolution;
    }

    internal static double UtilityFactor(ResolutionResult resolution) => resolution switch
    {
        ResolutionResult.CriticalSuccess => 1.5,
        ResolutionResult.Success => 1.25,
        ResolutionResult.PartialSuccess => 1.0,
        ResolutionResult.Failure => 0.5,
        ResolutionResult.CriticalFailure => 0.25,
        _ => 1.0,
    };

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
            StorePreview(carrier.Id, tick.CurrentTick, evento, resolution);
            tick.LogEvent(
                WorldEventKind.ExtraordinaryEffectApplied,
                $"{PreviewPrefix}{evento}|{resolution}");
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
