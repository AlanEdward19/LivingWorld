using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population.Body;

/// <summary>Faixa etária com mortalidade anual base, dado de cenário.</summary>
public sealed record LifeTableBracket(int MinAgeYears, int MaxAgeYears, double BaseAnnualMortality);

/// <summary>Tabela de vida: mortalidade por faixa etária e longevidade máxima
/// explícita. Entrada de cenário deve passar por <see cref="Create"/>, que valida cobertura
/// contígua das faixas antes de construir.</summary>
public sealed class LifeTable
{
    public int MaxLongevityYears { get; }
    public IReadOnlyList<LifeTableBracket> Brackets { get; }

    /// <summary>Construtor direto, sem validação — para reconstrução a partir de dado já
    /// validado (rehidratação do snapshot). Entrada de cenário (borda) passa por
    /// <see cref="Create"/>, mesmo padrão de <c>WorldMap</c>.</summary>
    public LifeTable(int maxLongevityYears, IReadOnlyList<LifeTableBracket> brackets)
    {
        MaxLongevityYears = maxLongevityYears;
        Brackets = brackets;
    }

    /// <summary>Valida cobertura contígua de 0 até <paramref name="maxLongevityYears"/> — sem
    /// lacuna nem sobreposição (critério "a tabela de vida não trunca cedo").</summary>
    public static Result<LifeTable> Create(int maxLongevityYears, IReadOnlyList<LifeTableBracket> brackets)
    {
        if (maxLongevityYears <= 0)
            return Result<LifeTable>.Fail("MaxLongevityYears: deve ser positivo");
        if (brackets.Count == 0)
            return Result<LifeTable>.Fail("Brackets: precisa de ao menos uma faixa");

        var ordered = brackets.OrderBy(b => b.MinAgeYears).ToList();
        int expectedStart = 0;
        foreach (var bracket in ordered)
        {
            if (bracket.MinAgeYears != expectedStart)
                return Result<LifeTable>.Fail($"Brackets: lacuna ou sobreposição em {bracket.MinAgeYears}");
            if (bracket.MaxAgeYears < bracket.MinAgeYears)
                return Result<LifeTable>.Fail($"Brackets[{bracket.MinAgeYears}]: MaxAgeYears < MinAgeYears");
            if (bracket.BaseAnnualMortality is < 0 or > 1)
                return Result<LifeTable>.Fail($"Brackets[{bracket.MinAgeYears}]: BaseAnnualMortality fora de [0,1]");
            expectedStart = bracket.MaxAgeYears + 1;
        }
        if (expectedStart < maxLongevityYears)
            return Result<LifeTable>.Fail("Brackets: não cobre até MaxLongevityYears");

        return Result<LifeTable>.Ok(new LifeTable(maxLongevityYears, ordered));
    }

    /// <summary>Probabilidade de morte no ano corrente. Saúde pior (menor) aumenta a mortalidade
    /// base; ao atingir <see cref="MaxLongevityYears"/> a morte é certa (100%) — sem isso a
    /// tabela poderia "truncar cedo" sem nunca garantir o teto de vida do cenário.
    /// <paramref name="vitalityMultiplier"/> escala o resultado — default
    /// <c>1.0</c> preserva o comportamento anterior à introdução deste fator; sempre clampado a
    /// <c>[0,1]</c>, nunca produz probabilidade fora de faixa.</summary>
    public double AnnualMortality(int ageYears, int health, double vitalityMultiplier = 1.0)
    {
        if (ageYears >= MaxLongevityYears) return 1.0;

        double healthMultiplier = 1.0 + (100 - Math.Clamp(health, 0, 100)) / 100.0;
        return Math.Clamp(BaseAnnualMortality(ageYears) * healthMultiplier * vitalityMultiplier, 0.0, 1.0);
    }

    /// <summary>Mortalidade anual da faixa (sem modulação), laço direto sem alocar.</summary>
    public double BaseAnnualMortality(int ageYears)
    {
        if (ageYears >= MaxLongevityYears) return 1.0;
        var brackets = Brackets;
        for (int i = 0; i < brackets.Count; i++)
            if (ageYears >= brackets[i].MinAgeYears && ageYears <= brackets[i].MaxAgeYears)
                return brackets[i].BaseAnnualMortality;
        return brackets[^1].BaseAnnualMortality;
    }

    /// <summary>Probabilidade de morrer num intervalo de <paramref name="years"/> anos (risco contínuo, AD-130): a anual
    /// <see cref="AnnualMortality"/> vira taxa constante no intervalo, 1 − (1 − q)^anos.</summary>
    public double PeriodMortality(int ageYears, int health, double multiplier, double years)
    {
        double q = AnnualMortality(ageYears, health, multiplier);
        if (q >= 1.0) return 1.0;
        if (q <= 0.0 || years <= 0) return 0.0;
        return 1.0 - DeterministicMath.Exp(years * DeterministicMath.Log(1.0 - q));
    }

    /// <summary>Tábua de época (vila medieval, AD-130), próxima de uma tábua-modelo pré-industrial com e0 ≈ 30: ~200‰ no
    /// 1º ano, perto de 60% chegando aos 15, adulto ~1%/ano subindo na velhice; o teto de 100 anos é só limite técnico.
    /// Valores aproximados, escolhidos no meio das faixas aceitas para populações pré-industriais: precisam de conferência.</summary>
    public static LifeTable PreIndustrial { get; } = new(100,
    [
        new LifeTableBracket(0, 0, 0.20),
        new LifeTableBracket(1, 4, 0.05),
        new LifeTableBracket(5, 9, 0.016),
        new LifeTableBracket(10, 14, 0.008),
        new LifeTableBracket(15, 19, 0.009),
        new LifeTableBracket(20, 29, 0.011),
        new LifeTableBracket(30, 39, 0.013),
        new LifeTableBracket(40, 49, 0.017),
        new LifeTableBracket(50, 54, 0.023),
        new LifeTableBracket(55, 59, 0.032),
        new LifeTableBracket(60, 64, 0.045),
        new LifeTableBracket(65, 69, 0.065),
        new LifeTableBracket(70, 74, 0.095),
        new LifeTableBracket(75, 79, 0.14),
        new LifeTableBracket(80, 84, 0.20),
        new LifeTableBracket(85, 89, 0.29),
        new LifeTableBracket(90, 94, 0.40),
        new LifeTableBracket(95, 99, 0.55),
    ]);
}
