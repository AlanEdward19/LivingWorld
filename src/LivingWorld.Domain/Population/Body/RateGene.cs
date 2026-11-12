using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population.Body;

/// <summary>Multiplicador de <b>taxa</b> de ganho de habilidade — nunca de valor inicial
/// (Assunção A1: modelo simplificado de gene único, pode ser expandido depois). Sempre
/// positivo: nunca 0 nem negativo, mesmo depois de mutação extrema na herança.</summary>
public sealed record RateGene(double Value)
{
    /// <summary>Piso positivo — nunca 0 nem negativo, mesmo com mutação extrema em
    /// <see cref="Inherit"/> (spec: "nunca 0 nem negativo").</summary>
    private const double MinValue = 0.01;

    /// <summary>Meia-largura da faixa de sorteio/mutação em torno do centro — mesmo espírito de
    /// <see cref="Personality.RollFrom"/> (constante de algoritmo, não conteúdo de cenário).</summary>
    private const double Spread = 0.3;

    public static Result<RateGene> Create(double value) =>
        value > 0
            ? Result<RateGene>.Ok(new RateGene(value))
            : Result<RateGene>.Fail("Value: deve ser > 0");

    /// <summary>Sorteia o gene de um NPC sem pais conhecidos (população seed inicial) — stream de
    /// RNG próprio do NPC (mesmo padrão de <see cref="Personality.RollFrom"/>), distribuição em
    /// torno de 1.0.</summary>
    public static RateGene RollInitial(WorldRng rng)
    {
        double value = 1.0 + (rng.NextDouble() * 2 - 1) * Spread;
        return new RateGene(Math.Max(value, MinValue));
    }

    /// <summary>Gene do recém-nascido: <c>mãe*0,5 + pai*0,5 + mutação</c> (Assunção A1),
    /// clampado a um piso positivo — nunca herda o valor de habilidade, só a predisposição de
    /// taxa.</summary>
    /// <summary>Gene do filho com segregação mendeliana (AD-134): média dos pais + N(0, <paramref name="segregationStdDev"/>),
    /// que mantém a variância da população entre gerações; piso positivo.</summary>
    public static RateGene Inherit(RateGene mother, RateGene father, WorldRng rng, QuantitativeGeneticsRules genetics)
    {
        if (!genetics.Mendelian) return Inherit(mother, father, rng);
        double value = QuantitativeGenetics.Inherit(mother.Value, father.Value, genetics.RateGeneSegregationStdDev, rng);
        return new RateGene(Math.Max(value, MinValue));
    }

    public static RateGene Inherit(RateGene mother, RateGene father, WorldRng rng)
    {
        double blended = mother.Value * 0.5 + father.Value * 0.5;
        double mutation = (rng.NextDouble() * 2 - 1) * Spread;
        return new RateGene(Math.Max(blended + mutation, MinValue));
    }
}
