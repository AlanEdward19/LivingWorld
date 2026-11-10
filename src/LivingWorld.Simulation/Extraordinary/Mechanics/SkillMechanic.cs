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

    /// <summary>Parser único de <c>skill.copy:&lt;skillId&gt;</c>.</summary>
    public static readonly ExtraordinaryPrimitive<int> Copy = new(
        CopyPrefix, "skill.copy:<skill>", false, "target",
        PrimitiveSyntax.From<int>(
            ParseCopy, static token => $"Effects: skill.copy exige um SkillType id inteiro '{token}'"));

    /// <summary>Parser único de <c>skill.learn-rate:&lt;m&gt;</c> (lido também por <see cref="ManifestedLearnRate"/>).</summary>
    public static readonly ExtraordinaryPrimitive<double> LearnRate = new(
        LearnRatePrefix, "skill.learn-rate:<n>", false, "carrier",
        PrimitiveSyntax.From<double>(
            ParseLearnRate,
            static token => $"Effects: skill.learn-rate exige um multiplicador não negativo '{token}'"));

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } = [Copy, LearnRate];

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
        var carrier = world.CarrierOf(npc.Id);
        if (carrier is not { IsManifested: true }) return 1;

        double rate = 1;
        foreach (var effect in world.Extraordinary.Descriptors
                     .Where(descriptor => carrier.PowerIds.Contains(descriptor.Id, StringComparer.Ordinal))
                     .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
                     .SelectMany(descriptor => descriptor.Effects))
        {
            if (!LearnRate.TryParse(effect, out double multiplier)) continue;
            // Satura a cada passo: fatores finitos não podem estourar em infinito, e 0 × infinito gravaria NaN na habilidade.
            rate = Math.Min(rate * multiplier, double.MaxValue);
        }

        return rate;
    }

    private static Result<PreparedMutation?> PrepareCopy(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var copy = Copy.Parse(declaration);
        if (!copy.IsSuccess)
            return Result<PreparedMutation?>.Fail(copy.Error!);
        int skillId = copy.Value;

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
        var rate = LearnRate.Parse(declaration);
        return rate.IsSuccess
            ? Result<PreparedMutation?>.Ok(null)
            : Result<PreparedMutation?>.Fail(rate.Error!);
    }

    private static bool ParseLearnRate(string declaration, out double multiplier)
    {
        multiplier = 0;
        if (!declaration.StartsWith(LearnRatePrefix, StringComparison.Ordinal)) return false;
        return double.TryParse(
                   declaration[LearnRatePrefix.Length..],
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out multiplier)
               && multiplier >= 0
               && double.IsFinite(multiplier);
    }

    private static bool ParseCopy(string declaration, out int skillId)
    {
        skillId = 0;
        return declaration.StartsWith(CopyPrefix, StringComparison.Ordinal)
            && int.TryParse(
                declaration[CopyPrefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out skillId)
            && skillId >= 0;
    }
}
