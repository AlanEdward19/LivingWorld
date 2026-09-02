using System.Globalization;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Skills;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// <c>skill.copy:&lt;skillId&gt;</c> copia o valor pontual do alvo no portador.
/// <c>skill.learn-rate:&lt;m&gt;</c> é modificador manifesto, lido a cada tick em
/// <see cref="SkillPracticeSystem"/> — sem resíduo quando o poder cessa.
/// </summary>
public sealed class SkillMechanic : ExtraordinaryMechanic
{
    public const string CopyPrefix = "skill.copy:";
    public const string LearnRatePrefix = "skill.learn-rate:";

    public override string Prefix => "skill.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (declaration.StartsWith(CopyPrefix, StringComparison.Ordinal))
            return PrepareCopy(ctx, declaration);
        if (declaration.StartsWith(LearnRatePrefix, StringComparison.Ordinal))
            return PrepareLearnRate(declaration);
        return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");
    }

    /// <summary>
    /// Produto dos <c>skill.learn-rate</c> dos descritores manifestos do NPC. Sempre recalculado
    /// do descritor (nunca armazenado). Combina com <see cref="RateGene"/>; não o substitui.
    /// </summary>
    public static double ManifestedLearnRate(WorldState world, Npc npc)
    {
        var carrier = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == npc.Id);
        if (carrier is not { IsManifested: true }) return 1;

        double rate = 1;
        foreach (var effect in world.Extraordinary.Descriptors
                     .Where(descriptor => carrier.PowerIds.Contains(descriptor.Id, StringComparer.Ordinal))
                     .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
                     .SelectMany(descriptor => descriptor.Effects))
        {
            if (!TryParseLearnRate(effect, out double multiplier)) continue;
            rate *= multiplier;
        }

        return rate;
    }

    private static Result<PreparedMutation?> PrepareCopy(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (!int.TryParse(
                declaration[CopyPrefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int skillId)
            || skillId < 0)
            return Result<PreparedMutation?>.Fail($"Effects: skill.copy exige um SkillType id inteiro '{declaration}'");

        var skillType = new SkillType(skillId);
        var target = ctx.Target;
        if (!target.Skills.Values.ContainsKey(skillId) || target.Skills.Get(skillType) == 0)
            return Result<PreparedMutation?>.Fail(
                $"Effects: skill.copy alvo não possui a habilidade {skillId}");

        double source = target.Skills.Get(skillType);
        double delta = source - ctx.Carrier.Skills.Get(skillType);
        double cap = Math.Max(ScenarioRunner.DefaultSkillsRules.Cap, source);
        var carrier = ctx.Carrier;
        return Result<PreparedMutation?>.Ok(new PreparedMutation(
            declaration, _ => carrier.GainSkill(skillType, delta, cap)));
    }

    private static Result<PreparedMutation?> PrepareLearnRate(string declaration)
    {
        if (!TryParseLearnRate(declaration, out _))
            return Result<PreparedMutation?>.Fail(
                $"Effects: skill.learn-rate exige um multiplicador não negativo '{declaration}'");
        return Result<PreparedMutation?>.Ok(null);
    }

    private static bool TryParseLearnRate(string declaration, out double multiplier)
    {
        multiplier = 0;
        if (!declaration.StartsWith(LearnRatePrefix, StringComparison.Ordinal)) return false;
        return double.TryParse(
                   declaration[LearnRatePrefix.Length..],
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out multiplier)
               && multiplier >= 0;
    }
}
