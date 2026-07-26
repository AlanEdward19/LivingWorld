namespace LivingWorld.Domain;

/// <summary>Regras demográficas do cenário (task 5/tasks fora-de-escopo da Fase 3): tabela de
/// vida e janela de fertilidade. Reprodução aqui é regra do cenário, não escolha do NPC — a
/// janela varia por espécie/cultura, por isso é dado, nunca constante de código (task 7).</summary>
public sealed record PopulationRules(
    LifeTable LifeTable,
    int FertilityMinAge,
    int FertilityMaxAge,
    double AnnualConceptionChance,
    int GestationDays)
{
    public static Result<PopulationRules> Create(
        LifeTable lifeTable, int fertilityMinAge, int fertilityMaxAge, double annualConceptionChance, int gestationDays)
    {
        if (fertilityMinAge < 0 || fertilityMaxAge < fertilityMinAge)
            return Result<PopulationRules>.Fail("FertilityMinAge/FertilityMaxAge: janela inválida");
        if (annualConceptionChance is < 0 or > 1)
            return Result<PopulationRules>.Fail("AnnualConceptionChance: fora de [0,1]");
        if (gestationDays <= 0)
            return Result<PopulationRules>.Fail("GestationDays: deve ser positivo");

        return Result<PopulationRules>.Ok(
            new PopulationRules(lifeTable, fertilityMinAge, fertilityMaxAge, annualConceptionChance, gestationDays));
    }

    public bool IsFertileAge(int ageYears) => ageYears >= FertilityMinAge && ageYears <= FertilityMaxAge;
}
