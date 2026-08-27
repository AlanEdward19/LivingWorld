using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>
/// Confronto NPC-vs-NPC. AD-010 (<c>.specs/STATE.md</c>): <c>combat.strike:&lt;magnitude&gt;</c>
/// permanece resolução imediata single-shot; multi-round entra por <c>combat.engage:</c>
/// criando <see cref="CombatEncounter"/> persistente via <see cref="CombatEncounterSystem"/>.
/// </summary>
public sealed class CombatMechanic : ExtraordinaryMechanic
{
    public const string StrikePrefix = "combat.strike:";

    /// <summary>Token multi-round (AD-010) — não reusa <see cref="StrikePrefix"/>.</summary>
    public const string EngagePrefix = "combat.engage:";

    public override string Prefix => "combat.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (declaration.StartsWith(EngagePrefix, StringComparison.Ordinal))
            return PrepareEngage(ctx, declaration);

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
                $"{attacker.Id.Value}|{target.Id.Value}|{resolution}", sourceSystem: "CombatMechanic");
        }));
    }

    /// <summary>AD-010: inicia encontro persistente — sem dano imediato de strike.</summary>
    private static Result<PreparedMutation?> PrepareEngage(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var parsed = ExtraordinaryMechanicSupport.ParseAmount(declaration, "Effects", allowSigned: false);
        if (!parsed.IsSuccess) return Result<PreparedMutation?>.Fail(parsed.Error!);

        int magnitude = parsed.Value.Amount;
        var world = ctx.World;
        var attacker = ctx.Carrier;
        var target = ctx.Target;
        var tick = ctx.Tick;

        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
            CombatEncounterSystem.StartEncounter(world, attacker.Id, target.Id, magnitude, tick)));
    }

    internal static int DamageOf(int magnitude, ResolutionResult resolution) => resolution switch
    {
        ResolutionResult.CriticalSuccess => magnitude + ExtraordinaryMechanicSupport.HalfAwayFromZero(magnitude),
        ResolutionResult.Success => magnitude,
        ResolutionResult.PartialSuccess => ExtraordinaryMechanicSupport.HalfAwayFromZero(magnitude),
        _ => 0,
    };
}
