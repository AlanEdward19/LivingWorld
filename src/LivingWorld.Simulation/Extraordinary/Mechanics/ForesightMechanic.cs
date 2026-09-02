using System.Runtime.CompilerServices;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// Precognição probabilística (PWR-120..122 / REALISM-30): <c>foresight.preview:&lt;evento&gt;</c>
/// roda <see cref="Resolver.Resolve"/> no mesmo cálculo que o evento usaria agora,
/// num RNG forked — sem mutar estado canônico e sem gravar <see cref="Fact"/>.
/// Quando <c>evento</c> é um <see cref="ActionType"/>, o <see cref="ResolutionResult"/>
/// fica disponível no tick corrente (armazenamento volátil por mundo) para a utility AI.
/// </summary>
public sealed class ForesightMechanic : ExtraordinaryMechanic
{
    public const string PreviewPrefix = "foresight.preview:";

    /// <summary>Dicionário vazio compartilhado — caminho comum sem foresight (sem alocação).
    /// REALISM-32 / Risk design hot-path.</summary>
    public static readonly IReadOnlyDictionary<ActionType, ResolutionResult> EmptyPreviews =
        new Dictionary<ActionType, ResolutionResult>();

    private sealed class PreviewBucket
    {
        public long Tick = long.MinValue;
        public readonly Dictionary<(long CarrierId, ActionType Action), ResolutionResult> ByCarrierAction = new();
    }

    private static readonly ConditionalWeakTable<WorldState, PreviewBucket> PreviewStores = new();

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

    /// <summary>Previews do portador no tick corrente (REALISM-30). Volátil — não entra no
    /// hash canônico. Tick diferente ou sem entradas → <see cref="EmptyPreviews"/>.</summary>
    public static IReadOnlyDictionary<ActionType, ResolutionResult> PreviewsFor(
        WorldState world, NpcId carrier, long tick)
    {
        if (!PreviewStores.TryGetValue(world, out var bucket)
            || bucket.Tick != tick
            || bucket.ByCarrierAction.Count == 0)
        {
            return EmptyPreviews;
        }

        Dictionary<ActionType, ResolutionResult>? owned = null;
        foreach (var (key, result) in bucket.ByCarrierAction)
        {
            if (key.CarrierId != carrier.Value) continue;
            owned ??= new Dictionary<ActionType, ResolutionResult>();
            owned[key.Action] = result;
        }

        return owned is null ? EmptyPreviews : owned;
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
        var resolution = Resolver.Resolve(
            difficulty, capacity, VarianceProfile.Dramatico("extraordinary"), tick.Rng(stream).Fork());

        if (Enum.TryParse<ActionType>(evento, ignoreCase: true, out var action))
            StorePreview(world, carrier.Id, tick.CurrentTick, action, resolution);

        return resolution;
    }

    private static void StorePreview(
        WorldState world, NpcId carrier, long tick, ActionType action, ResolutionResult result)
    {
        var bucket = PreviewStores.GetOrCreateValue(world);
        if (bucket.Tick != tick)
        {
            bucket.ByCarrierAction.Clear();
            bucket.Tick = tick;
        }

        bucket.ByCarrierAction[(carrier.Value, action)] = result;
    }
}
