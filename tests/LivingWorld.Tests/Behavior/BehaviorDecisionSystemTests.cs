using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 4, task 12: <see cref="BehaviorDecisionSystem"/> — rotina diária por padrão,
/// utility AI só quando alguma necessidade supera o limiar de urgência (NEEDS-05/06/10/11),
/// desempate por menor <c>ActionId</c> (NEEDS-06), fome vence trabalho com controle (NEEDS-07)
/// e tabela dos 10 traços de personalidade (NEEDS-08).</summary>
public class BehaviorDecisionSystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly LifeStageRules Stages = LifeStageRules.Create(childMaxAge: 14, adultMaxAge: 64).Value!;

    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    /// <summary>Traços cuja ação modulada é <see cref="ActionType.Socialize"/> (tabela de
    /// influência de <see cref="PersonalityWeighting"/>) — usados para decidir qual necessidade
    /// serve de gatilho de urgência sem interferir na comparação sob teste.</summary>
    private static readonly HashSet<string> SocializeTraits =
    [
        nameof(Personality.Extroversion), nameof(Personality.Openness),
        nameof(Personality.Altruism), nameof(Personality.Agreeableness),
    ];

    /// <summary>[Traço, ação prevista em 20, ação prevista em 80] — par de ações com a mesma
    /// utilidade base sob personalidade neutra (Work/Travel/Idle = 50 fixo; Socialize = déficit
    /// de Social ajustado para empatar em 50), de forma que só a personalidade decide o vencedor
    /// (NEEDS-08). Uma linha por trait de <see cref="Personality"/> — os dois papéis de
    /// Impulsivity (Idle positivo, Work negativo) produzem o mesmo par Work×Idle, cobertos numa
    /// linha só. Quando o traço sob teste empurra sua ação abaixo do empate de 50, o vencedor é
    /// o candidato baseline restante de menor <c>ActionId</c> (Work=2 &lt; Socialize=3 &lt;
    /// Travel=4 &lt; Idle=5) — não necessariamente "Idle": o desempate por menor id (NEEDS-06)
    /// vale aqui também.</summary>
    public static readonly TheoryData<string, ActionType, ActionType> TraitPredictedActionCases = new()
    {
        { nameof(Personality.Conscientiousness), ActionType.Travel, ActionType.Work },
        { nameof(Personality.Ambition), ActionType.Travel, ActionType.Work },
        { nameof(Personality.Loyalty), ActionType.Travel, ActionType.Work },
        { nameof(Personality.Impulsivity), ActionType.Work, ActionType.Idle },
        { nameof(Personality.RiskAversion), ActionType.Travel, ActionType.Work },
        { nameof(Personality.EmotionalStability), ActionType.Idle, ActionType.Work },
        { nameof(Personality.Extroversion), ActionType.Work, ActionType.Socialize },
        { nameof(Personality.Openness), ActionType.Work, ActionType.Socialize },
        { nameof(Personality.Altruism), ActionType.Work, ActionType.Socialize },
        { nameof(Personality.Agreeableness), ActionType.Work, ActionType.Socialize },
    };

    private static NeedsRules MakeRules(int urgencyThreshold) => NeedsRules.Create(
        hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
        urgencyThreshold, maxActionSelectionSteps: 10, hysteresisEnabled: false,
        continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;

    private static ActionCatalog MakeCatalogWithOpenWorkShift() => ActionCatalog.Create(
        maxDurationHours: new Dictionary<ActionType, int>
        {
            [ActionType.Eat] = 2,
            [ActionType.Sleep] = 8,
            [ActionType.Work] = 8,
            [ActionType.Socialize] = 3,
            [ActionType.Travel] = 4,
            [ActionType.Idle] = 2,
            [ActionType.Buy] = 2,
        },
        routineSlots: [new RoutineSlot(ProfessionId: null, Stage: LifeStage.Adult, HourStart: 0, HourEnd: 23, Action: ActionType.Work)],
        defaultAction: ActionType.Idle).Value!;

    private static Personality WithTrait(string traitName, int value)
    {
        var values = new Dictionary<string, int>
        {
            [nameof(Personality.Extroversion)] = 50,
            [nameof(Personality.Agreeableness)] = 50,
            [nameof(Personality.Conscientiousness)] = 50,
            [nameof(Personality.EmotionalStability)] = 50,
            [nameof(Personality.Openness)] = 50,
            [nameof(Personality.Ambition)] = 50,
            [nameof(Personality.Loyalty)] = 50,
            [nameof(Personality.Altruism)] = 50,
            [nameof(Personality.Impulsivity)] = 50,
            [nameof(Personality.RiskAversion)] = 50,
        };
        values[traitName] = value;

        return Personality.Create(
            values[nameof(Personality.Extroversion)], values[nameof(Personality.Agreeableness)],
            values[nameof(Personality.Conscientiousness)], values[nameof(Personality.EmotionalStability)],
            values[nameof(Personality.Openness)], values[nameof(Personality.Ambition)],
            values[nameof(Personality.Loyalty)], values[nameof(Personality.Altruism)],
            values[nameof(Personality.Impulsivity)], values[nameof(Personality.RiskAversion)]).Value!;
    }

    private static (WorldState World, TickContext Ctx, Npc Npc) BuildWorld(
        ulong seed, NeedsRules rules, ActionCatalog catalog, Personality personality,
        int hunger = 100, int thirst = 100, int sleep = 100, int social = 100)
    {
        var map = ScenarioRunner.DefaultMap(seed);
        var world = new WorldState(
            Calendar, seed, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            rules, catalog, Stages);
        var location = new CellCoord(1, 1);

        var npc = new Npc(
            new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: personality, profession: ProfessionType.None, currentLocation: location,
            hunger: hunger, thirst: thirst, sleep: sleep, social: social);

        world.AddNpc(npc);
        world.AdvanceNpcIdTo(2);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        return (world, ctx, npc);
    }

    [Fact]
    public void No_need_above_the_urgency_threshold_follows_the_declared_daily_routine()
    {
        var rules = MakeRules(urgencyThreshold: 70);
        var catalog = MakeCatalogWithOpenWorkShift();
        var (world, ctx, npc) = BuildWorld(seed: 1, rules, catalog, Neutral);

        new BehaviorDecisionSystem().Tick(world, ctx);

        Assert.Equal(ActionType.Work, npc.CurrentAction);
    }

    [Fact]
    public void A_need_above_the_urgency_threshold_overrides_the_routine()
    {
        var rules = MakeRules(urgencyThreshold: 70);
        var catalog = MakeCatalogWithOpenWorkShift();
        var (world, ctx, npc) = BuildWorld(seed: 1, rules, catalog, Neutral, hunger: 0);

        new BehaviorDecisionSystem().Tick(world, ctx);

        Assert.Equal(ActionType.Eat, npc.CurrentAction);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    [InlineData(4u)]
    [InlineData(5u)]
    [InlineData(6u)]
    [InlineData(7u)]
    [InlineData(8u)]
    [InlineData(9u)]
    [InlineData(10u)]
    public void Hunger_beats_the_open_work_shift_with_a_control_arm_in_10_of_10_seeds(ulong seed)
    {
        var rules = MakeRules(urgencyThreshold: 70);
        var catalog = MakeCatalogWithOpenWorkShift();
        var system = new BehaviorDecisionSystem();

        // Braço "fome=90" (bem faminto): satisfação de fome baixa (déficit 90, urgente).
        var (worldHungry, ctxHungry, npcHungry) = BuildWorld(seed, rules, catalog, Neutral, hunger: 10);
        system.Tick(worldHungry, ctxHungry);
        Assert.Equal(ActionType.Eat, npcHungry.CurrentAction);

        // Braço de controle "fome=10" (quase saciado): déficit 10, abaixo do limiar — segue rotina.
        var (worldFed, ctxFed, npcFed) = BuildWorld(seed, rules, catalog, Neutral, hunger: 90);
        system.Tick(worldFed, ctxFed);
        Assert.Equal(ActionType.Work, npcFed.CurrentAction);
    }

    [Fact]
    public void Exact_utility_tie_breaks_by_the_smaller_ActionId()
    {
        var rules = MakeRules(urgencyThreshold: 40);
        var catalog = MakeCatalogWithOpenWorkShift();
        // Déficit de Social = 50 (> limiar 40, urgente) empata com a utilidade base fixa de
        // Work/Travel/Idle (50) sob personalidade neutra — Work (ActionId=2) é o menor id entre
        // os quatro empatados (Work, Socialize, Travel, Idle).
        var (world, ctx, npc) = BuildWorld(seed: 1, rules, catalog, Neutral, social: 50);

        new BehaviorDecisionSystem().Tick(world, ctx);

        Assert.Equal(ActionType.Work, npc.CurrentAction);
    }

    [Fact]
    public void Every_personality_trait_has_a_predicted_action_case()
    {
        var covered = TraitPredictedActionCases.Select(row => (string)row[0]!).ToHashSet();
        Assert.Equal(10, PersonalityWeighting.AllTraitNames.Count);
        foreach (var trait in PersonalityWeighting.AllTraitNames)
            Assert.Contains(trait, covered);
    }

    [Theory]
    [MemberData(nameof(TraitPredictedActionCases))]
    public void Trait_at_20_vs_80_flips_the_predicted_action_in_10_of_10_seeds(
        string trait, ActionType lowAction, ActionType highAction)
    {
        bool isSocializeTrait = SocializeTraits.Contains(trait);
        var rules = MakeRules(urgencyThreshold: 40);
        var catalog = MakeCatalogWithOpenWorkShift();
        int social = isSocializeTrait ? 50 : 100; // déficit 50 (gatilho + concorrente de Socialize)
        int sleep = isSocializeTrait ? 100 : 55; // déficit 45 (gatilho que não interfere no par sob teste)

        for (ulong seed = 1; seed <= 10; seed++)
        {
            var low = WithTrait(trait, 20);
            var (worldLow, ctxLow, npcLow) = BuildWorld(seed, rules, catalog, low, social: social, sleep: sleep);
            new BehaviorDecisionSystem().Tick(worldLow, ctxLow);
            Assert.Equal(lowAction, npcLow.CurrentAction);

            var high = WithTrait(trait, 80);
            var (worldHigh, ctxHigh, npcHigh) = BuildWorld(seed, rules, catalog, high, social: social, sleep: sleep);
            new BehaviorDecisionSystem().Tick(worldHigh, ctxHigh);
            Assert.Equal(highAction, npcHigh.CurrentAction);
        }
    }
}
