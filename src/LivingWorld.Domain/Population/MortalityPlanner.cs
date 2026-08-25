namespace LivingWorld.Domain;

/// <summary>Resolve a idade de morte de um NPC por antecipação (task 4): rola ano a ano contra
/// <see cref="LifeTable.AnnualMortality"/> até o primeiro sucesso ou o teto de longevidade.
/// Puro — o chamador agenda UM evento futuro com o resultado, nunca uma varredura por tick.</summary>
public static class MortalityPlanner
{
    /// <summary><paramref name="vitalityMultiplier"/> (Fase 7, T9) e
    /// <paramref name="senescenceRateMultiplier"/> (Fase 16.1, PWR-20) — defaults <c>1.0</c>
    /// preservam o comportamento anterior. Cada ano de calendário avança a idade biológica
    /// pelo multiplicador; o relógio do mundo não é alterado. Multiplicador 0 é recusado pelo
    /// chamador (<c>SchedulePlannedDeath</c>) e não entra neste laço.</summary>
    public static int RollDeathAge(
        WorldRng rng, LifeTable table, int health,
        double vitalityMultiplier = 1.0, double senescenceRateMultiplier = 1.0)
    {
        if (senescenceRateMultiplier <= 0.0)
            return table.MaxLongevityYears;

        double biologicalAge = 0;
        int calendarYears = 0;
        while (biologicalAge < table.MaxLongevityYears)
        {
            double p = table.AnnualMortality((int)biologicalAge, health, vitalityMultiplier);
            if (rng.NextDouble() < p)
                return calendarYears;
            biologicalAge += senescenceRateMultiplier;
            calendarYears++;
        }
        return calendarYears;
    }
}
