using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Escolhe a ação do tick de cada NPC vivo (Fase 4, task 12 — NEEDS-05/06/07/08/10/11):
/// rotina diária por padrão, utility AI só quando alguma necessidade supera o limiar de urgência
/// do cenário. Histerese/teto de passos (T13) e deslocamento/moradia (T14) chegam nas próximas
/// tasks — esta fatia cobre só rotina + pontuação.</summary>
public sealed class BehaviorDecisionSystem : ISimulationSystem
{
    public const string SystemName = "behavior-decision";

    /// <summary>Utilidade base de ações não ligadas a uma das 4 necessidades modeladas (Work,
    /// Travel, Idle) — parte do modelo de decisão em si (mesmo status que a fórmula de
    /// <see cref="PersonalityWeighting"/>: constante de algoritmo, não dado de cenário). Mesma
    /// escala 0-100 do déficit de necessidade, para que personalidade dispute em pé de igualdade
    /// quando nada mais está em jogo.</summary>
    private const double NonNeedBaselineUtility = 50.0;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.NeedsRules;
        var catalog = world.ActionCatalog;
        long now = ctx.CurrentTick;

        foreach (var npc in world.Npcs)
        {
            if (!npc.IsAlive) continue;

            var stage = world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate));
            var routineAction = catalog.RoutineOf(NoneIfSentinel(npc.Profession), stage, world.CurrentDate.Hour);

            var chosen = npc.HasUrgentNeed(rules) ? SelectByUtility(npc) : routineAction;

            npc.SetCurrentAction(chosen, now);
        }
    }

    /// <summary>Ordena as 6 ações candidatas por nota (<c>utilidadeBase × pesoPersonalidade</c>,
    /// NEEDS-06); empate exato desempata por menor <c>ActionId</c> — <c>Enum.GetValues</c> já
    /// devolve os valores em ordem ascendente do valor binário subjacente (Eat=0..Idle=5), então
    /// a primeira ocorrência de uma nota máxima é sempre a de menor id.</summary>
    private static ActionType SelectByUtility(Npc npc)
    {
        var best = ActionType.Eat;
        double bestScore = double.NegativeInfinity;

        foreach (var action in Enum.GetValues<ActionType>())
        {
            double score = UtilityBaseOf(action, npc) * PersonalityWeighting.WeightOf(npc.Personality, action);
            if (score > bestScore)
            {
                bestScore = score;
                best = action;
            }
        }

        return best;
    }

    /// <summary>Eat cobre fome e sede (a fase não tem ação de "beber" dedicada — catálogo
    /// fechado em 6 ações — então o déficit mais urgente entre as duas rege a nota de Eat).
    /// Work/Travel/Idle usam a mesma utilidade base fixa (<see cref="NonNeedBaselineUtility"/>):
    /// nenhuma necessidade modelada os direciona, só personalidade os distingue entre si.</summary>
    private static double UtilityBaseOf(ActionType action, Npc npc) => action switch
    {
        ActionType.Eat => Math.Max(Deficit(npc.Hunger), Deficit(npc.Thirst)),
        ActionType.Sleep => Deficit(npc.Sleep),
        ActionType.Socialize => Deficit(npc.Social),
        ActionType.Work or ActionType.Travel or ActionType.Idle => NonNeedBaselineUtility,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "ActionType desconhecido"),
    };

    private static int Deficit(int need) => 100 - need;

    /// <summary><see cref="ProfessionType.None"/> (catálogo vazio no sorteio, task 7) não é uma
    /// profissão de rotina válida — resolve como "any" (<c>null</c>), mesma convenção de
    /// <see cref="ActionCatalog.RoutineOf"/> para slot sem profissão específica.</summary>
    private static ProfessionType? NoneIfSentinel(ProfessionType profession) =>
        profession == ProfessionType.None ? null : profession;
}
