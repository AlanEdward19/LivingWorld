using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 6, T14+: harness mínimo pros cenários pareados de habilidade — constrói um
/// <see cref="WorldState"/> isolado com NPCs de idade/genes/ação fixados por parâmetro (ação
/// sempre <see cref="ActionType.Work"/>, sem <c>BehaviorDecisionSystem</c>/<c>EmploymentSystem</c>
/// no meio), tickando só o(s) sistema(s) de habilidade que o critério precisa. Mesmo princípio de
/// <c>EconomyScenarioHarness</c> (Fase 5): controla exatamente o parâmetro que o critério pede,
/// sem duplicar o resto da montagem de <see cref="ScenarioRunner"/>. Determinístico por
/// construção (nenhum sistema de habilidade lê <see cref="WorldRng"/>) — a seed em cada teste
/// serve pra cumprir literalmente "N seeds" do critério do roadmap, não porque o resultado varie
/// com ela.</summary>
public static class SkillScenarioHarness
{
    public static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    public static readonly CellCoord SomeLocation = new(1, 1);

    public static WorldState CreateWorld(ulong seed) => new(
        ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.DefaultMap(seed), ScenarioRunner.DefaultPopulationCatalog,
        ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
        ScenarioRunner.DefaultLifeStageRules);

    /// <summary>NPC adulto (30 anos), sempre em <see cref="ActionType.Work"/> — nenhum sistema de
    /// decisão roda neste harness, então a ação corrente é um parâmetro fixado por construção, não
    /// uma decisão simulada.</summary>
    public static Npc MakeWorker(
        WorldState world, ProfessionType profession, CellCoord location, RateGene rateGene, SkillSet? skills = null)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), $"npc-{world.NextNpcId}", Sex.Male,
            WorldDate.Epoch(world.Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: profession, currentLocation: location,
            currentAction: ActionType.Work, rateGene: rateGene, skills: skills);
        world.AddNpc(npc);
        return npc;
    }

    public static Workplace MakeWorkplace(WorldState world, LocationType locationType, CellCoord location)
    {
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), locationType, location, maxVacancies: 10,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero,
            prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        return workplace;
    }

    public static void Hire(Npc npc, Workplace workplace)
    {
        workplace.Hire(npc.Id);
        npc.Hire(workplace.Id);
    }

    /// <summary><see cref="SkillsRules"/> só com a fonte <see cref="SkillGainSource.Practice"/>
    /// declarada, taxa por parâmetro — usado por T14 pra calibrar a curva num horizonte de 20
    /// anos sem tocar <see cref="ScenarioRunner.DefaultSkillsRules"/> (que satura antes de 20 anos
    /// nesse horizonte, escondendo a diferença especialista/trocador — mesmo achado de calibração
    /// de AD-046/048 na Fase 5, só que isolado neste teste em vez do cenário default).</summary>
    public static SkillsRules MakePracticeOnlyRules(double practiceRate, double cap = 100) => SkillsRules.Create(
        cap,
        baseRateBySource: new Dictionary<SkillGainSource, double> { [SkillGainSource.Practice] = practiceRate },
        skillByProfession: new Dictionary<int, SkillType> { [1] = SkillType.Agriculture, [2] = SkillType.Craft })
        .Value ?? throw new InvalidOperationException("skills rules de teste inválida");
}
