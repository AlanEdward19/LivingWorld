namespace LivingWorld.Domain.Population.Family;

/// <summary>Como a convivência vira vínculo (AD-128). Sem este bloco (<c>null</c> em <see cref="FamilyRules.Bonds"/>) vale o
/// modelo antigo: todo par presente ganha o mesmo todo dia até 100. Com ele: cada um convive de verdade com poucos por dia,
/// o quanto gosta e confia tem teto dado pela personalidade dos dois, o ganho diminui perto do teto, e quem deixa de se ver
/// esfria aos poucos até ser esquecido (parentes nunca abaixo de um piso).</summary>
public sealed record SocialBondRules(
    int WorkContactsPerDay,
    int WorkEncountersPerDay,
    int WorkCrewSize,
    double AffectionCeilingFloor,
    double HouseholdCeilingFloor,
    double TrustCeilingFloor,
    double KinBondFloor,
    int FadeEveryDays,
    int SocializeRadius,
    int CommunitySize,
    double IntroductionAffection)
{
    /// <summary>Largura de cada eixo abaixo da qual o par, sem contato, é esquecido.</summary>
    public const double ForgottenBelow = 1.0;

    public static readonly SocialBondRules Default = new(
        WorkContactsPerDay: 3,
        WorkEncountersPerDay: 8,
        WorkCrewSize: 16,
        AffectionCeilingFloor: 30,
        HouseholdCeilingFloor: 70,
        TrustCeilingFloor: 40,
        KinBondFloor: 40,
        FadeEveryDays: 7,
        SocializeRadius: 3,
        CommunitySize: 150,
        IntroductionAffection: 40);

    public string? Validate()
    {
        if (WorkContactsPerDay < 0) return "Bonds.WorkContactsPerDay: deve ser >= 0";
        if (WorkEncountersPerDay < WorkContactsPerDay) return "Bonds.WorkEncountersPerDay: deve ser >= WorkContactsPerDay";
        if (WorkCrewSize < WorkEncountersPerDay) return "Bonds.WorkCrewSize: deve ser >= WorkEncountersPerDay";
        if (!InScale(AffectionCeilingFloor)) return "Bonds.AffectionCeilingFloor: fora de [0,100]";
        if (!InScale(HouseholdCeilingFloor)) return "Bonds.HouseholdCeilingFloor: fora de [0,100]";
        if (!InScale(TrustCeilingFloor)) return "Bonds.TrustCeilingFloor: fora de [0,100]";
        if (!InScale(KinBondFloor)) return "Bonds.KinBondFloor: fora de [0,100]";
        if (FadeEveryDays <= 0) return "Bonds.FadeEveryDays: deve ser > 0";
        if (SocializeRadius < 0) return "Bonds.SocializeRadius: deve ser >= 0";
        if (CommunitySize < 0) return "Bonds.CommunitySize: deve ser >= 0";
        if (!InScale(IntroductionAffection)) return "Bonds.IntroductionAffection: fora de [0,100]";
        return null;
    }

    private static bool InScale(double value) => value >= 0 && value <= 100;

    /// <summary>O quanto <paramref name="from"/> tende a gostar de <paramref name="to"/>, em [0,1]: semelhança de
    /// temperamento (quem se parece se entende) e o quanto o outro é agradável. Assimétrico de propósito.</summary>
    public static double Likability(Personality from, Personality to)
    {
        double distance =
            Math.Abs(from.Extroversion - to.Extroversion) + Math.Abs(from.Agreeableness - to.Agreeableness)
            + Math.Abs(from.Conscientiousness - to.Conscientiousness) + Math.Abs(from.EmotionalStability - to.EmotionalStability)
            + Math.Abs(from.Openness - to.Openness) + Math.Abs(from.Ambition - to.Ambition)
            + Math.Abs(from.Loyalty - to.Loyalty) + Math.Abs(from.Altruism - to.Altruism)
            + Math.Abs(from.Impulsivity - to.Impulsivity) + Math.Abs(from.RiskAversion - to.RiskAversion);
        // Dois temperamentos ao acaso ficam perto de 0,67 de semelhança bruta; a faixa útil vai de 0,50 a 0,85.
        double similarity = Math.Clamp((1.0 - distance / 1000.0 - 0.50) / 0.35, 0.0, 1.0);
        return 0.7 * similarity + 0.3 * (to.Agreeableness / 100.0);
    }

    /// <summary>Teto do afeto de <paramref name="from"/> por <paramref name="to"/>; quem mora junto nunca abaixo do piso da casa.</summary>
    public double AffectionCeiling(Personality from, Personality to, bool sameHousehold)
    {
        double ceiling = AffectionCeilingFloor + (100.0 - AffectionCeilingFloor) * Likability(from, to);
        return sameHousehold ? Math.Max(ceiling, HouseholdCeilingFloor) : ceiling;
    }

    /// <summary>Teto da confiança em <paramref name="to"/>: depende de quão leal e cumpridor ele é, não de quem confia.</summary>
    public double TrustCeiling(Personality to) =>
        TrustCeilingFloor + (100.0 - TrustCeilingFloor) * ((to.Loyalty + to.Conscientiousness) / 200.0);
}
