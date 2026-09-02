using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population.Body;

/// <summary>Faixa etária com mortalidade anual base (task 5), dado de cenário.</summary>
public sealed record LifeTableBracket(int MinAgeYears, int MaxAgeYears, double BaseAnnualMortality);

/// <summary>Tabela de vida (task 5): mortalidade por faixa etária e longevidade máxima
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
    /// <paramref name="vitalityMultiplier"/> (Fase 7, T9) escala o resultado — default
    /// <c>1.0</c> preserva o comportamento anterior a T9 (AD-050); sempre clampado a
    /// <c>[0,1]</c>, nunca produz probabilidade fora de faixa.</summary>
    public double AnnualMortality(int ageYears, int health, double vitalityMultiplier = 1.0)
    {
        if (ageYears >= MaxLongevityYears) return 1.0;

        var bracket = Brackets.First(b => ageYears >= b.MinAgeYears && ageYears <= b.MaxAgeYears);
        double healthMultiplier = 1.0 + (100 - Math.Clamp(health, 0, 100)) / 100.0;
        return Math.Clamp(bracket.BaseAnnualMortality * healthMultiplier * vitalityMultiplier, 0.0, 1.0);
    }
}
