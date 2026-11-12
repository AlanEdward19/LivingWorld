using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population.Skills;

/// <summary>Ponto da curva de aptidão: na idade <paramref name="AgeYears"/> a taxa de aprendizado de ofício vale
/// <paramref name="Factor"/> da taxa-base (interpolação linear entre pontos).</summary>
public sealed record AptitudePoint(double AgeYears, double Factor);

/// <summary>Como a idade, a casa e o uso mexem na habilidade de ofício (AD-132). Valores padrão aproximados para uma vila
/// medieval, a conferir: bebê não aprende ofício; a criança ajuda em casa e no campo por volta dos 5–7 anos; aprendiz aos
/// 12–14; adulto aprende com plasticidade plena e um pouco menos com a idade. O que a criança aprende em casa tem teto em
/// uma fração do que os adultos da casa sabem (ninguém ensina o que não sabe; a maestria vem da prática). Habilidade sem
/// uso cai devagar e a habilidade manual cai com a velhice.</summary>
public sealed record SkillLearningRules(
    IReadOnlyList<AptitudePoint> AptitudeByAge,
    double HouseholdCeilingFraction,
    double ApprenticeAgeYears,
    double DisuseLossPerYear,
    double PhysicalDeclineStartAgeYears,
    double PhysicalDeclineLossPerYear,
    int MaintenanceEveryDays,
    IReadOnlyList<int>? PhysicalSkillIds = null)
{
    /// <summary>Vila medieval (aproximado): aptidão 0 até 5 anos, 0,4 aos 12, plena dos 16 aos 30, 0,6 aos 60; a criança
    /// chega em casa a até metade do que o adulto da casa sabe; aprendiz a partir de 12; 4%/ano de perda sem uso (5 anos
    /// fora do ofício ≈ −18%); 1,5%/ano de perda manual depois dos 55; manutenção mensal. <see cref="PhysicalSkillIds"/>
    /// nulo = toda habilidade praticada por alguma profissão é manual.</summary>
    public static SkillLearningRules Default { get; } = new(
        [new(0, 0), new(5, 0), new(12, 0.4), new(16, 1.0), new(30, 1.0), new(60, 0.6), new(90, 0.4)],
        HouseholdCeilingFraction: 0.5,
        ApprenticeAgeYears: 12,
        DisuseLossPerYear: 0.04,
        PhysicalDeclineStartAgeYears: 55,
        PhysicalDeclineLossPerYear: 0.015,
        MaintenanceEveryDays: 30);

    /// <summary>Sem efeito de idade nem de uso: aptidão plena em qualquer idade, nenhuma perda (o comportamento de antes).</summary>
    public static SkillLearningRules Ageless { get; } = new(
        [new(0, 1.0)], HouseholdCeilingFraction: 1.0, ApprenticeAgeYears: double.MaxValue,
        DisuseLossPerYear: 0, PhysicalDeclineStartAgeYears: double.MaxValue, PhysicalDeclineLossPerYear: 0,
        MaintenanceEveryDays: 30);

    public static Result<SkillLearningRules> Create(
        IReadOnlyList<AptitudePoint> aptitudeByAge, double householdCeilingFraction, double apprenticeAgeYears,
        double disuseLossPerYear, double physicalDeclineStartAgeYears, double physicalDeclineLossPerYear,
        int maintenanceEveryDays, IReadOnlyList<int>? physicalSkillIds = null)
    {
        if (aptitudeByAge.Count == 0) return Result<SkillLearningRules>.Fail("AptitudeByAge: não pode ser vazio");
        for (int i = 0; i < aptitudeByAge.Count; i++)
        {
            if (aptitudeByAge[i].Factor < 0) return Result<SkillLearningRules>.Fail("AptitudeByAge: fator deve ser >= 0");
            if (i > 0 && aptitudeByAge[i].AgeYears <= aptitudeByAge[i - 1].AgeYears)
                return Result<SkillLearningRules>.Fail("AptitudeByAge: idades devem crescer");
        }
        if (householdCeilingFraction is < 0 or > 1) return Result<SkillLearningRules>.Fail("HouseholdCeilingFraction: fora de [0,1]");
        if (disuseLossPerYear is < 0 or >= 1) return Result<SkillLearningRules>.Fail("DisuseLossPerYear: fora de [0,1)");
        if (physicalDeclineLossPerYear is < 0 or >= 1) return Result<SkillLearningRules>.Fail("PhysicalDeclineLossPerYear: fora de [0,1)");
        if (maintenanceEveryDays <= 0) return Result<SkillLearningRules>.Fail("MaintenanceEveryDays: deve ser > 0");
        return Result<SkillLearningRules>.Ok(new SkillLearningRules(
            aptitudeByAge, householdCeilingFraction, apprenticeAgeYears, disuseLossPerYear,
            physicalDeclineStartAgeYears, physicalDeclineLossPerYear, maintenanceEveryDays, physicalSkillIds));
    }

    /// <summary>Fração da taxa-base que a pessoa rende na idade dada (interpolação linear; fora da tabela, o extremo).</summary>
    public double Aptitude(double ageYears)
    {
        var points = AptitudeByAge;
        if (ageYears <= points[0].AgeYears) return points[0].Factor;
        for (int i = 1; i < points.Count; i++)
        {
            if (ageYears > points[i].AgeYears) continue;
            var (a, b) = (points[i - 1], points[i]);
            return a.Factor + (b.Factor - a.Factor) * (ageYears - a.AgeYears) / (b.AgeYears - a.AgeYears);
        }
        return points[^1].Factor;
    }

    /// <summary>Fração perdida num período de <paramref name="days"/> dias: sem uso, mais a perda manual da velhice.</summary>
    public double LossFraction(double ageYears, bool practiced, bool physical, int days, int daysPerYear)
    {
        double perYear = (practiced ? 0 : DisuseLossPerYear)
            + (physical && ageYears >= PhysicalDeclineStartAgeYears ? PhysicalDeclineLossPerYear : 0);
        return Math.Clamp(perYear * days / daysPerYear, 0.0, 1.0);
    }

    /// <summary>Habilidade esperada, na idade dada, de quem praticou o próprio ofício desde criança sob estas regras: soma
    /// mês a mês o ganho de prática (aptidão da idade × curva de retorno decrescente) e a perda manual da velhice. Usada
    /// para quem já existe quando o mundo começa, cuja história não foi simulada.</summary>
    public double ExpectedPracticedSkill(double ageYears, double practiceRatePerDay, double cap, int daysPerMonth, int monthsPerYear)
    {
        double skill = 0;
        int months = (int)(ageYears * monthsPerYear);
        int daysPerYear = daysPerMonth * monthsPerYear;
        for (int m = 0; m < months; m++)
        {
            double age = (double)m / monthsPerYear;
            skill += SkillCurve.Gain(skill, cap, practiceRatePerDay) * Aptitude(age) * daysPerMonth;
            skill -= skill * LossFraction(age, practiced: true, physical: true, daysPerMonth, daysPerYear);
        }
        return Math.Clamp(skill, 0.0, cap);
    }
}
