namespace LivingWorld.Domain;

/// <summary>Todo parâmetro numérico de habilidade (Fase 6), cenário-driven (R3) — nenhum
/// literal em C#, mesmo padrão de <see cref="NeedsRules"/>/<see cref="EconomyRules"/>: teto
/// único compartilhado pelas 13 habilidades, taxa-base por fonte de ganho, e o mapeamento de
/// qual habilidade cada profissão pratica.</summary>
public sealed record SkillsRules(
    bool Enabled,
    double Cap,
    IReadOnlyDictionary<SkillGainSource, double> BaseRateBySource,
    IReadOnlyDictionary<int, SkillType> SkillByProfession)
{
    public static Result<SkillsRules> Create(
        double cap,
        IReadOnlyDictionary<SkillGainSource, double> baseRateBySource,
        IReadOnlyDictionary<int, SkillType> skillByProfession,
        bool enabled = true)
    {
        if (cap <= 0) return Result<SkillsRules>.Fail("Cap: deve ser > 0");

        foreach (var (source, rate) in baseRateBySource)
            if (rate < 0)
                return Result<SkillsRules>.Fail($"BaseRateBySource[{source}]: deve ser >= 0");

        if (skillByProfession.Count == 0)
            return Result<SkillsRules>.Fail("SkillByProfession: não pode ser vazio");

        return Result<SkillsRules>.Ok(new SkillsRules(enabled, cap, baseRateBySource, skillByProfession));
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
