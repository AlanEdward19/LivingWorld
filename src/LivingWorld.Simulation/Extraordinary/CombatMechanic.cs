using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>
/// Confronto NPC-vs-NPC: <c>combat.strike:&lt;magnitude-base&gt;</c> resolve via
/// <see cref="Resolver.Resolve"/> (capacidade inclui <c>attribute.strength</c>), aplica dano
/// no caminho <c>npc.health</c> e loga <see cref="WorldEventKind.CombatResolved"/>.
/// </summary>
public sealed class CombatMechanic : ExtraordinaryMechanic
{
    public const string StrikePrefix = "combat.strike:";

    public override string Prefix => "combat.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (!declaration.StartsWith(StrikePrefix, StringComparison.Ordinal))
        {
            int separator = declaration.LastIndexOf(':');
            string key = separator > 0 ? declaration[..separator] : declaration;
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{key}'");
        }

        var parsed = ExtraordinaryMechanicSupport.ParseAmount(declaration, "Effects", allowSigned: false);
        if (!parsed.IsSuccess) return Result<PreparedMutation?>.Fail(parsed.Error!);

        int magnitude = parsed.Value.Amount;
        var world = ctx.World;
        var attacker = ctx.Carrier;
        var target = ctx.Target;
        var tick = ctx.Tick;
        var invocation = ctx.Invocation;

        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
        {
            int difficulty = 10 + Math.Clamp((100 - target.Health) / 20, 0, 5);
            int baseCapacity = (int)Math.Clamp(
                Math.Round(attacker.Vitality / 10d + attacker.RateGene.Value * 5d), 0, 20);
            int strengthBonus = (int)Math.Round((AttributeMechanic.StrengthMultiplier(world, attacker) - 1) * 10);
            int capacity = LuckMechanic.AdjustCapacity(
                world, attacker, tick.CurrentTick, baseCapacity + strengthBonus);
            string stream =
                $"combat-strike-{attacker.Id.Value}-{target.Id.Value}-{invocation.InvocationId}";
            var resolution = Resolver.Resolve(
                difficulty, capacity, VarianceProfile.Dramatico("extraordinary"), tick.Rng(stream));
            int damage = DamageOf(magnitude, resolution);
            target.SetHealth(ExtraordinaryMechanicSupport.ClampNeed((long)target.Health - damage));
            tick.LogEvent(
                WorldEventKind.CombatResolved,
                $"{attacker.Id.Value}|{target.Id.Value}|{resolution}");
        }));
    }

    internal static int DamageOf(int magnitude, ResolutionResult resolution) => resolution switch
    {
        ResolutionResult.CriticalSuccess => magnitude + ExtraordinaryMechanicSupport.HalfAwayFromZero(magnitude),
        ResolutionResult.Success => magnitude,
        ResolutionResult.PartialSuccess => ExtraordinaryMechanicSupport.HalfAwayFromZero(magnitude),
        _ => 0,
    };
}
