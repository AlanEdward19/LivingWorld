namespace LivingWorld.Domain.Population.Skills;

/// <summary>Curva de retornos decrescentes de ganho de habilidade (Fase 6, task 2, SKILL-02) —
/// função pura, sem estado, testável isolada sem <c>ScenarioRunner</c> e sem seed.</summary>
public static class SkillCurve
{
    /// <summary>Ganho aplicado a uma habilidade no nível <paramref name="currentSkill"/>: quanto
    /// mais perto do teto <paramref name="cap"/>, menor o ganho marginal. Nunca negativo — nível
    /// 0/negativo ou já no/acima do teto é defesa de fronteira (Edge Case da spec), não caminho
    /// esperado.</summary>
    public static double Gain(double currentSkill, double cap, double baseRate)
    {
        if (cap <= 0) return 0;

        double remaining = 1.0 - currentSkill / cap;
        double gain = baseRate * remaining;
        return gain < 0 ? 0 : gain;
    }
}
