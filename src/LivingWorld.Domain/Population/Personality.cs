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

    /// <summary>Herança de personalidade (personality-heredity), modelo infinitesimal que preserva a variância: o componente
    /// herdado de cada traço é a média dos pais mais uma segregação uniforme de ±<see cref="InheritanceNoise"/> pontos (variância
    /// da metade da população), e o filho é <c>50 + h × (herdado − 50) + √(1 − h²) × (sorteio − 50)</c>, cortado em [0,100], com
    /// <c>h</c> = <paramref name="heritability"/>. A variância da população fica a mesma de geração em geração (a média simples
    /// dos pais a reduzia pela metade por geração) e irmãos correlacionam em <c>h²/2</c>. Os 10 sorteios independentes saem
    /// sempre primeiro, do mesmo stream e na mesma ordem de <see cref="RollFrom"/>; com <c>h = 0</c> nenhum outro número é
    /// sorteado e o resultado é byte a byte o do sorteio independente (o comportamento de antes).</summary>
    public static Personality Inherit(Personality mother, Personality father, WorldRng rng, double heritability)
    {
        var rolled = RollFrom(rng);
        if (heritability <= 0) return rolled;

        double h = Math.Min(1.0, heritability);
        double independent = Math.Sqrt(1 - h * h);
        return Create(
            Mix(h, independent, mother.Extroversion, father.Extroversion, rolled.Extroversion, rng),
            Mix(h, independent, mother.Agreeableness, father.Agreeableness, rolled.Agreeableness, rng),
            Mix(h, independent, mother.Conscientiousness, father.Conscientiousness, rolled.Conscientiousness, rng),
            Mix(h, independent, mother.EmotionalStability, father.EmotionalStability, rolled.EmotionalStability, rng),
            Mix(h, independent, mother.Openness, father.Openness, rolled.Openness, rng),
            Mix(h, independent, mother.Ambition, father.Ambition, rolled.Ambition, rng),
            Mix(h, independent, mother.Loyalty, father.Loyalty, rolled.Loyalty, rng),
            Mix(h, independent, mother.Altruism, father.Altruism, rolled.Altruism, rng),
            Mix(h, independent, mother.Impulsivity, father.Impulsivity, rolled.Impulsivity, rng),
            Mix(h, independent, mother.RiskAversion, father.RiskAversion, rolled.RiskAversion, rng)).Value!;
    }

    /// <summary>Meia-largura, em pontos, da segregação uniforme somada à média dos pais: variância de metade da de um traço
    /// sorteado em [0,100] (≈ 850), ou seja √(3 × 850 / 2).</summary>
    public const double InheritanceNoise = 35.7;

    private const double Midpoint = 50.0;

    private static int Mix(double h, double independent, int mother, int father, int rolled, WorldRng rng)
    {
        double noise = (rng.NextDouble() * 2 - 1) * InheritanceNoise;
        double inherited = (mother + father) / 2.0 + noise;
        double value = Midpoint + h * (inherited - Midpoint) + independent * (rolled - Midpoint);
        return Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 0, 100);
    }

    private static int RollTrait(WorldRng rng) => Math.Min((int)(rng.NextDouble() * 101), 100);

    private static bool IsValidTrait(int value) => value is >= 0 and <= 100;
}
