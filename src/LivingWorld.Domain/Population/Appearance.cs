using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population;

/// <summary>Genes de aparência do NPC (#46 fatia 2), cada um em <c>[0,100]</c>: tom de pele, tom do cabelo
/// (claro → escuro), ondulação do cabelo (liso → crespo) e porte (esguio → robusto). Altura e peso seguem em
/// <c>Npc.Height</c>/<c>Weight</c>. Herdados pelo mesmo modelo de <see cref="Personality.Inherit"/> (ADR-0014: genes
/// que alimentam atração são canônicos). O sprite/material do cliente é função pura destes valores.</summary>
public sealed record Appearance(int SkinTone, int HairTone, int HairCurl, int Build)
{
    public const int Genes = 4;

    public static Result<Appearance> Create(int skinTone, int hairTone, int hairCurl, int build)
    {
        if (!IsValid(skinTone)) return Result<Appearance>.Fail("SkinTone: fora de [0,100]");
        if (!IsValid(hairTone)) return Result<Appearance>.Fail("HairTone: fora de [0,100]");
        if (!IsValid(hairCurl)) return Result<Appearance>.Fail("HairCurl: fora de [0,100]");
        if (!IsValid(build)) return Result<Appearance>.Fail("Build: fora de [0,100]");
        return Result<Appearance>.Ok(new Appearance(skinTone, hairTone, hairCurl, build));
    }

    /// <summary>Sorteio independente dos 4 genes, uma chamada de <see cref="WorldRng.NextDouble"/> por gene.</summary>
    public static Appearance RollFrom(WorldRng rng) =>
        new(Roll(rng), Roll(rng), Roll(rng), Roll(rng));

    /// <summary>Mesmo modelo infinitesimal de <see cref="Personality.Inherit"/>: sorteio independente primeiro,
    /// depois média dos pais + segregação, misturados por <paramref name="heritability"/> (variância preservada).
    /// Com <c>h = 0</c> nada além do sorteio independente é consumido.</summary>
    public static Appearance Inherit(Appearance mother, Appearance father, WorldRng rng, double heritability)
    {
        var rolled = RollFrom(rng);
        if (heritability <= 0) return rolled;

        double h = Math.Min(1.0, heritability);
        double independent = Math.Sqrt(1 - h * h);
        return new Appearance(
            Mix(h, independent, mother.SkinTone, father.SkinTone, rolled.SkinTone, rng),
            Mix(h, independent, mother.HairTone, father.HairTone, rolled.HairTone, rng),
            Mix(h, independent, mother.HairCurl, father.HairCurl, rolled.HairCurl, rng),
            Mix(h, independent, mother.Build, father.Build, rolled.Build, rng));
    }

    /// <summary>Semelhança em <c>[0,1]</c>: 1 = genes idênticos, 0 = extremos opostos em todos os genes.</summary>
    public static double Similarity(Appearance a, Appearance b) =>
        1.0 - (Math.Abs(a.SkinTone - b.SkinTone) + Math.Abs(a.HairTone - b.HairTone)
            + Math.Abs(a.HairCurl - b.HairCurl) + Math.Abs(a.Build - b.Build)) / (Genes * 100.0);

    private static int Mix(double h, double independent, int mother, int father, int rolled, WorldRng rng)
    {
        double noise = (rng.NextDouble() * 2 - 1) * Personality.InheritanceNoise;
        double inherited = (mother + father) / 2.0 + noise;
        double value = 50.0 + h * (inherited - 50.0) + independent * (rolled - 50.0);
        return Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 0, 100);
    }

    private static int Roll(WorldRng rng) => Math.Min((int)(rng.NextDouble() * 101), 100);

    private static bool IsValid(int value) => value is >= 0 and <= 100;
}

/// <summary>Regras de aparência do período. Ausente (<c>null</c> no mundo) = ninguém tem genes de aparência, o hash do
/// mundo é o de antes e o cliente sorteia a aparência como sempre. <see cref="Heritability"/> em <c>[0,1]</c>.
/// <see cref="AttractionWeight"/> (≥ 0, 0 desliga) é o peso de <c>AttractionFactor.Appearance</c> no cortejo, aplicado
/// pelo mundo só quando o cenário já tem pesos de atração declarados.</summary>
public sealed record AppearanceRules(double Heritability, double AttractionWeight = 1.0)
{
    public static Result<AppearanceRules> Create(double heritability, double attractionWeight = 1.0)
    {
        if (!(heritability >= 0 && heritability <= 1))
            return Result<AppearanceRules>.Fail("Appearance.Heritability: deve estar entre 0 e 1");
        if (!(attractionWeight >= 0))
            return Result<AppearanceRules>.Fail("Appearance.AttractionWeight: deve ser >= 0");
        return Result<AppearanceRules>.Ok(new AppearanceRules(heritability, attractionWeight));
    }
}
