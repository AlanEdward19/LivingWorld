namespace LivingWorld.Domain;

/// <summary>Personalidade do NPC (Fase 4, task 1): os 10 traços de <c>docs/domain/npc.md</c>,
/// cada um em <c>[0,100]</c>, imutáveis após o nascimento (sem herança genética ainda —
/// Fase 7). Modula profissão, relações e decisão momento a momento sempre como peso, nunca
/// como trava.</summary>
public sealed record Personality(
    int Extroversion,
    int Agreeableness,
    int Conscientiousness,
    int EmotionalStability,
    int Openness,
    int Ambition,
    int Loyalty,
    int Altruism,
    int Impulsivity,
    int RiskAversion)
{
    public static Result<Personality> Create(
        int extroversion, int agreeableness, int conscientiousness, int emotionalStability, int openness,
        int ambition, int loyalty, int altruism, int impulsivity, int riskAversion)
    {
        if (!IsValidTrait(extroversion)) return Result<Personality>.Fail("Extroversion: fora de [0,100]");
        if (!IsValidTrait(agreeableness)) return Result<Personality>.Fail("Agreeableness: fora de [0,100]");
        if (!IsValidTrait(conscientiousness)) return Result<Personality>.Fail("Conscientiousness: fora de [0,100]");
        if (!IsValidTrait(emotionalStability)) return Result<Personality>.Fail("EmotionalStability: fora de [0,100]");
        if (!IsValidTrait(openness)) return Result<Personality>.Fail("Openness: fora de [0,100]");
        if (!IsValidTrait(ambition)) return Result<Personality>.Fail("Ambition: fora de [0,100]");
        if (!IsValidTrait(loyalty)) return Result<Personality>.Fail("Loyalty: fora de [0,100]");
        if (!IsValidTrait(altruism)) return Result<Personality>.Fail("Altruism: fora de [0,100]");
        if (!IsValidTrait(impulsivity)) return Result<Personality>.Fail("Impulsivity: fora de [0,100]");
        if (!IsValidTrait(riskAversion)) return Result<Personality>.Fail("RiskAversion: fora de [0,100]");

        return Result<Personality>.Ok(new Personality(
            extroversion, agreeableness, conscientiousness, emotionalStability, openness,
            ambition, loyalty, altruism, impulsivity, riskAversion));
    }

    private static bool IsValidTrait(int value) => value is >= 0 and <= 100;
}
