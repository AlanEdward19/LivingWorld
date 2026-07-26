namespace LivingWorld.Domain;

/// <summary>Resolve a idade de morte de um NPC por antecipação (task 4): rola ano a ano contra
/// <see cref="LifeTable.AnnualMortality"/> até o primeiro sucesso ou o teto de longevidade.
/// Puro — o chamador agenda UM evento futuro com o resultado, nunca uma varredura por tick.</summary>
public static class MortalityPlanner
{
    public static int RollDeathAge(WorldRng rng, LifeTable table, int health)
    {
        for (int age = 0; age < table.MaxLongevityYears; age++)
        {
            double p = table.AnnualMortality(age, health);
            if (rng.NextDouble() < p)
                return age;
        }
        return table.MaxLongevityYears;
    }
}
