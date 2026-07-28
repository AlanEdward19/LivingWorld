namespace LivingWorld.Domain;

/// <summary>Resolve a idade de morte de um NPC por antecipação (task 4): rola ano a ano contra
/// <see cref="LifeTable.AnnualMortality"/> até o primeiro sucesso ou o teto de longevidade.
/// Puro — o chamador agenda UM evento futuro com o resultado, nunca uma varredura por tick.</summary>
public static class MortalityPlanner
{
    /// <summary><paramref name="vitalityMultiplier"/> (Fase 7, T9) repassado direto a
    /// <see cref="LifeTable.AnnualMortality"/> — default <c>1.0</c> preserva o comportamento
    /// anterior a T9.</summary>
    public static int RollDeathAge(WorldRng rng, LifeTable table, int health, double vitalityMultiplier = 1.0)
    {
        for (int age = 0; age < table.MaxLongevityYears; age++)
        {
            double p = table.AnnualMortality(age, health, vitalityMultiplier);
            if (rng.NextDouble() < p)
                return age;
        }
        return table.MaxLongevityYears;
    }
}
