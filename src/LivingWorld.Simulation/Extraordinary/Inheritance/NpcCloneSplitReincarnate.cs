using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;

namespace LivingWorld.Simulation.Extraordinary.Inheritance;

/// <summary>Modo de transferência de vínculos sociais (REALISM-29) em instanciação.</summary>
public enum BondTransferMode
{
    /// <summary><c>npc.clone</c> — cópia independente dos vínculos do original.</summary>
    Copy,

    /// <summary><c>npc.split-on-death</c> — cada novo NPC preserva os vínculos do original.</summary>
    Preserve,

    /// <summary><c>npc.reincarnate</c> — vínculos não sobrevivem (NPC novo).</summary>
    None,
}

/// <summary>Herança de skill e vínculos para clone/split/reincarnate (REALISM-26..29).
/// Reusa a fórmula de <see cref="RateGene.Inherit"/>/<see cref="HeredityService.InheritVitality"/>
/// e <see cref="WorldState.Relationships"/> — sem stores paralelos.</summary>
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

    /// <summary>Transfere vínculos de <paramref name="source"/> para cada alvo conforme
    /// <paramref name="mode"/> — nunca omite por omissão (REALISM-29).</summary>
    public static void TransferBonds(
        WorldState world, Npc source, IReadOnlyList<NpcId> targets, BondTransferMode mode)
    {
        if (mode == BondTransferMode.None || targets.Count == 0)
            return;

        long now = world.CurrentDate.TotalHours;
        var partners = world.Relationships
            .Where(pair => pair.Key.From == source.Id || pair.Key.To == source.Id)
            .Select(pair => pair.Key.From == source.Id ? pair.Key.To : pair.Key.From)
            .Distinct()
            .OrderBy(id => id.Value)
            .ToList();

        foreach (var targetId in targets.OrderBy(id => id.Value))
        {
            foreach (var partnerId in partners)
            {
                if (partnerId == targetId)
                    continue;

                CopyDirectedBond(world, source.Id, partnerId, targetId, partnerId, now);
                CopyDirectedBond(world, partnerId, source.Id, partnerId, targetId, now);
            }
        }
    }

    private static void CopyDirectedBond(
        WorldState world, NpcId fromSource, NpcId toSource, NpcId fromDest, NpcId toDest, long now)
    {
        if (!world.Relationships.TryGetValue(new RelationshipKey(fromSource, toSource), out var origin))
            return;

        var copy = world.GetOrCreateRelationship(new RelationshipKey(fromDest, toDest), now);
        copy.CopyAxesFrom(origin);
        copy.MarkContact(now);
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
