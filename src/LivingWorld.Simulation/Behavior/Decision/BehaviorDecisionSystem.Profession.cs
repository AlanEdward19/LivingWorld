using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;

namespace LivingWorld.Simulation.Behavior.Decision;

// Parte de BehaviorDecisionSystem — troca de profissão por pontuação e vagas.
public sealed partial class BehaviorDecisionSystem
{
    private static ProfessionType? NoneIfSentinel(ProfessionType profession) =>
        profession == ProfessionType.None ? null : profession;

    private void EvaluateProfessionSwitch(WorldState world, Npc npc, VacancyWeights vacancyIndex, LifeStage stage)
    {
        if (_skillsRules is not { } rules) return;
        if (stage != LifeStage.Adult) return;

        var catalog = world.EconomyCatalog;
        if (catalog.LocationTypeByProfession.Count == 0) return;

        ProfessionType? best = null;
        double bestScore = double.NegativeInfinity;
        // Igual para todas as profissões: calculado uma vez por avaliação, não por profissão.
        double personalityWeight = PersonalityWeighting.WeightOf(npc.Personality, ActionType.Work);
        foreach (var professionId in SortedProfessionIds(catalog))
        {
            double score = ProfessionScoreOf(npc, rules, catalog, professionId, vacancyIndex, personalityWeight);
            if (score > bestScore)
            {
                bestScore = score;
                best = new ProfessionType(professionId);
            }
        }

        if (best is { } candidate && candidate != npc.Profession)
            npc.SwitchProfession(candidate);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EconomyCatalog, int[]> SortedProfessions = new();

    /// <summary>Ids de profissão do catálogo em ordem crescente (o catálogo é imutável: ordena uma vez).</summary>
    private static int[] SortedProfessionIds(EconomyCatalog catalog) =>
        SortedProfessions.GetValue(catalog, c => c.LocationTypeByProfession.Keys.OrderBy(id => id).ToArray());

    private static double ProfessionScoreOf(
        Npc npc, SkillsRules rules, EconomyCatalog catalog, int professionId, VacancyWeights vacancyIndex,
        double personalityWeight)
    {
        double skillWeight = rules.SkillByProfession.TryGetValue(professionId, out var skillType)
            ? 1.0 + npc.Skills.Get(skillType) / rules.Cap
            : 1.0;
        double vacancyWeight = VacancyWeightOf(catalog, professionId, vacancyIndex);

        return skillWeight * personalityWeight * vacancyWeight;
    }

    private static double VacancyWeightOf(EconomyCatalog catalog, int professionId, VacancyWeights vacancyIndex)
    {
        if (!catalog.LocationTypeByProfession.TryGetValue(professionId, out var locationTypeId)) return 1.0;
        return vacancyIndex.VacancyWeightForLocationType(locationTypeId);
    }
}
