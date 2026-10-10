namespace LivingWorld.Domain.Population.Skills;

/// <summary>Catálogo fechado das 6 fontes de ganho de habilidade —
/// cada uma com taxa e requisito próprios.</summary>
public enum SkillGainSource
{
    Practice = 0,
    DeliberateTraining = 1,
    School = 2,
    Parental = 3,
    Observation = 4,
    Tutoring = 5,
}
