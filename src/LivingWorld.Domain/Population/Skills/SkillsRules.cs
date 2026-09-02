using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population.Skills;

/// <summary>Todo parâmetro numérico de habilidade (Fase 6), cenário-driven (R3) — nenhum
/// literal em C#, mesmo padrão de <see cref="NeedsRules"/>/<see cref="EconomyRules"/>: teto
/// compartilhado por todas as habilidades, taxa-base por fonte de ganho, o mapeamento de qual
/// habilidade cada profissão pratica, e (Fase 13, T11b) qual id de habilidade conta como
/// "ensino" pro multiplicador de tutoria — antes um literal (<c>SkillType.Teaching</c>) em
/// <see cref="SkillTeachingSystem"/>, agora declarado aqui, mesmo padrão de
/// <see cref="SkillByProfession"/>.</summary>
public sealed record SkillsRules(
    bool Enabled,
    double Cap,
    IReadOnlyDictionary<SkillGainSource, double> BaseRateBySource,
    IReadOnlyDictionary<int, SkillType> SkillByProfession,
    SkillType TeachingSkill)
{
    public static Result<SkillsRules> Create(
        double cap,
        IReadOnlyDictionary<SkillGainSource, double> baseRateBySource,
        IReadOnlyDictionary<int, SkillType> skillByProfession,
        SkillType teachingSkill,
        bool enabled = true)
    {
        if (cap <= 0) return Result<SkillsRules>.Fail("Cap: deve ser > 0");

        foreach (var (source, rate) in baseRateBySource)
            if (rate < 0)
                return Result<SkillsRules>.Fail($"BaseRateBySource[{source}]: deve ser >= 0");

        if (skillByProfession.Count == 0)
            return Result<SkillsRules>.Fail("SkillByProfession: não pode ser vazio");

        return Result<SkillsRules>.Ok(new SkillsRules(enabled, cap, baseRateBySource, skillByProfession, teachingSkill));
    }

    /// <summary>Ganho de habilidade para a fonte <paramref name="source"/>, combinando a curva
    /// pura de retornos decrescentes com o multiplicador de taxa genético — gene multiplica
    /// taxa, nunca valor (Assunção A1).</summary>
    public double Gain(double currentSkill, SkillGainSource source, double rateGene)
    {
        double baseRate = BaseRateBySource.TryGetValue(source, out var rate) ? rate : 0;
        return SkillCurve.Gain(currentSkill, Cap, baseRate) * rateGene;
    }
}
