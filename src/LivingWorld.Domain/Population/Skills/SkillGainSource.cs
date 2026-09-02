namespace LivingWorld.Domain.Population.Skills;

/// <summary>Catálogo fechado das 6 fontes de ganho de habilidade da Fase 6 (task 3) —
/// cada uma com taxa e requisito próprios (SKILL-03..08).</summary>
public enum SkillGainSource
{
    Practice = 0,
    DeliberateTraining = 1,
    School = 2,
    Parental = 3,
    Observation = 4,
    Tutoring = 5,
}
