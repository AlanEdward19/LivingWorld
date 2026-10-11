using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population;

/// <summary>Regras demográficas do cenário: tabela de
/// vida e janela de fertilidade. Reprodução aqui é regra do cenário, não escolha do NPC — a
/// janela varia por espécie/cultura, por isso é dado, nunca constante de código.</summary>
public sealed record PopulationRules(
    LifeTable LifeTable,
    int FertilityMinAge,
    int FertilityMaxAge,
    double AnnualConceptionChance,
    int GestationDays,
    /// <summary>Teto de NPCs vivos (natalidade + split-on-death). Default
    /// <see cref="int.MaxValue"/> = sem teto (comportamento legado).</summary>
    int MaxAliveNpcs = int.MaxValue,
    /// <summary>Quando ligado, a idade de morte planejada de um NPC que já nasceu (população inicial,
    /// fundação, clone) é sorteada <em>condicionada a ele estar vivo na idade atual</em>. Desligado
    /// (default, comportamento legado) o sorteio parte da idade 0 e um adulto pode receber idade de
    /// morte menor que a atual — e morre uma hora depois.</summary>
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool ConditionalInitialMortality = false)
{
    public static Result<PopulationRules> Create(
        LifeTable lifeTable,
        int fertilityMinAge,
        int fertilityMaxAge,
        double annualConceptionChance,
        int gestationDays,
        int maxAliveNpcs = int.MaxValue,
        bool conditionalInitialMortality = false)
    {
        if (fertilityMinAge < 0 || fertilityMaxAge < fertilityMinAge)
            return Result<PopulationRules>.Fail("FertilityMinAge/FertilityMaxAge: janela inválida");
        if (annualConceptionChance is < 0 or > 1)
            return Result<PopulationRules>.Fail("AnnualConceptionChance: fora de [0,1]");
        if (gestationDays <= 0)
            return Result<PopulationRules>.Fail("GestationDays: deve ser positivo");
        if (maxAliveNpcs <= 0)
            return Result<PopulationRules>.Fail("MaxAliveNpcs: deve ser > 0");

        return Result<PopulationRules>.Ok(
            new PopulationRules(
                lifeTable, fertilityMinAge, fertilityMaxAge, annualConceptionChance, gestationDays, maxAliveNpcs,
                conditionalInitialMortality));
    }

    public bool IsFertileAge(int ageYears) => ageYears >= FertilityMinAge && ageYears <= FertilityMaxAge;

    /// <summary>Quantos novos vivos ainda cabem sob o teto, dado o número atual de vivos.</summary>
    public int RemainingAliveSlots(int currentAlive) => Math.Max(0, MaxAliveNpcs - currentAlive);
}
