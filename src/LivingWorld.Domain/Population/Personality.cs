using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population;

/// <summary>Personalidade do NPC: os 10 traços de <c>docs/domain/npc.md</c>,
/// cada um em <c>[0,100]</c>. Sistemas ordinários não a alteram após o nascimento; a única
/// exceção é uma intervenção explícita de autoria que substitui o conjunto validado inteiro.
/// Modula profissão, relações e decisão momento a momento sempre como peso, nunca como trava.</summary>
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

    /// <summary>Sorteia os 10 traços por 10 rolls independentes do stream de RNG recebido
    /// — cada roll é uma chamada sequencial de <see cref="WorldRng.NextDouble"/>, mesma
    /// convenção de <c>PopulationGenerator.RollAge</c>. Sempre em faixa válida, então
    /// <c>Create</c> nunca falha aqui.</summary>
    public static Personality RollFrom(WorldRng rng) => Create(
        RollTrait(rng), RollTrait(rng), RollTrait(rng), RollTrait(rng), RollTrait(rng),
        RollTrait(rng), RollTrait(rng), RollTrait(rng), RollTrait(rng), RollTrait(rng)).Value!;

    /// <summary>Herança de personalidade (personality-heredity): cada traço do filho é
    /// <c>h × (média dos pais + ruído) + (1 − h) × sorteio independente</c>, cortado em [0,100], com
    /// <c>h</c> = <paramref name="heritability"/> e ruído uniforme de ±<see cref="InheritanceNoise"/> pontos. Os 10 sorteios
    /// independentes saem sempre primeiro, do mesmo stream e na mesma ordem de <see cref="RollFrom"/>; com <c>h = 0</c> nenhum
    /// outro número é sorteado e o resultado é byte a byte o do sorteio independente (o comportamento de antes).</summary>
    public static Personality Inherit(Personality mother, Personality father, WorldRng rng, double heritability)
    {
        var rolled = RollFrom(rng);
        if (heritability <= 0) return rolled;

        double h = Math.Min(1.0, heritability);
        return Create(
            Mix(h, mother.Extroversion, father.Extroversion, rolled.Extroversion, rng),
            Mix(h, mother.Agreeableness, father.Agreeableness, rolled.Agreeableness, rng),
            Mix(h, mother.Conscientiousness, father.Conscientiousness, rolled.Conscientiousness, rng),
            Mix(h, mother.EmotionalStability, father.EmotionalStability, rolled.EmotionalStability, rng),
            Mix(h, mother.Openness, father.Openness, rolled.Openness, rng),
            Mix(h, mother.Ambition, father.Ambition, rolled.Ambition, rng),
            Mix(h, mother.Loyalty, father.Loyalty, rolled.Loyalty, rng),
            Mix(h, mother.Altruism, father.Altruism, rolled.Altruism, rng),
            Mix(h, mother.Impulsivity, father.Impulsivity, rolled.Impulsivity, rng),
            Mix(h, mother.RiskAversion, father.RiskAversion, rolled.RiskAversion, rng)).Value!;
    }

    /// <summary>Pontos de ruído (para cima ou para baixo) somados à média dos pais antes da mistura.</summary>
    public const double InheritanceNoise = 10.0;

    private static int Mix(double h, int mother, int father, int rolled, WorldRng rng)
    {
        double noise = (rng.NextDouble() * 2 - 1) * InheritanceNoise;
        double inherited = (mother + father) / 2.0 + noise;
        return Math.Clamp((int)Math.Round(h * inherited + (1 - h) * rolled), 0, 100);
    }

    private static int RollTrait(WorldRng rng) => Math.Min((int)(rng.NextDouble() * 101), 100);

    private static bool IsValidTrait(int value) => value is >= 0 and <= 100;
}
