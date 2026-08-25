using LivingWorld.Domain;

namespace LivingWorld.Simulation;

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
        return Result<PreparedMutation?>.Ok(null);
    }
}
