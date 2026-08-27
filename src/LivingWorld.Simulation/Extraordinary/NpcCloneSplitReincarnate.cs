using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Herança de skill para clone/split/reincarnate (REALISM-26..28).
/// Reusa a fórmula de <see cref="RateGene.Inherit"/>/<see cref="HeredityService.InheritVitality"/>
/// — blend ponderado + mutação RNG + clamp — sem segunda regra genética.</summary>
public static class NpcInstantiationHeredity
{
    /// <summary>Meia-largura de mutação de <b>nível</b> de skill. Zero: skill é nível acumulado
    /// (não predisposição de taxa como <see cref="RateGene"/>); o termo de mutação existe na
    /// fórmula (parity com <see cref="RateGene.Inherit"/>) e o stream dedicado ainda é consumido.</summary>
    private const double SkillMutationSpread = 0.0;

    private const double SkillCap = 100.0;

    /// <summary>Aplica <c>source * weight + mutação</c> (mesmo molde de
    /// <see cref="RateGene.Inherit"/>) a cada skill, clampado via <see cref="SkillSet.WithGain"/>.</summary>
    public static SkillSet InheritSkills(SkillSet source, double weight, WorldRng rng)
    {
        SkillSet result = SkillSet.Empty;
        foreach (var (skillId, value) in source.Values.OrderBy(pair => pair.Key))
        {
            double blended = value * weight;
            double mutation = (rng.NextDouble() * 2 - 1) * SkillMutationSpread;
            double inherited = Math.Clamp(blended + mutation, 0.0, SkillCap);
            result = result.WithGain(new SkillType(skillId), inherited, SkillCap);
        }

        return result;
    }
}

public sealed class NpcCloneMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "npc.clone";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var parsed = ExtraordinaryMechanicSupport.ParseAmount(declaration, "Effects", allowSigned: false);
        if (!parsed.IsSuccess) return Result<PreparedMutation?>.Fail(parsed.Error!);
        if (parsed.Value.Key != Prefix)
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{parsed.Value.Key}'");

        int count = parsed.Value.Amount;
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, resolution =>
        {
            int copies = ExtraordinaryMechanicSupport.ScaledAmount(count, resolution);
            for (int i = 0; i < copies; i++)
                NpcInstantiationMechanic.InstantiateCopy(ctx.World, ctx.Tick, ctx.Carrier, "clone");
        }));
    }
}

public sealed class NpcSplitOnDeathMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "npc.split-on-death";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var parsed = ExtraordinaryMechanicSupport.ParseAmount(declaration, "Effects", allowSigned: false);
        if (!parsed.IsSuccess) return Result<PreparedMutation?>.Fail(parsed.Error!);
        if (parsed.Value.Key != Prefix)
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{parsed.Value.Key}'");
        // Efeito passivo: dispara em NpcDeath via NpcInstantiationMechanic.OnCarrierDeath.
        return Result<PreparedMutation?>.Ok(null);
    }
}

public sealed class NpcReincarnateMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "npc.reincarnate";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var parsed = ExtraordinaryMechanicSupport.ParseAmount(declaration, "Effects", allowSigned: false);
        if (!parsed.IsSuccess) return Result<PreparedMutation?>.Fail(parsed.Error!);
        if (parsed.Value.Key != Prefix || parsed.Value.Amount is < 1 or > 100)
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");
        // Efeito passivo: fila PendingReincarnation em OnCarrierDeath; aplica no próximo nascimento.
        return Result<PreparedMutation?>.Ok(null);
    }
}
