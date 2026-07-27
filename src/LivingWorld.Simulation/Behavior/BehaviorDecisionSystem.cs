using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Escolhe a ação do tick de cada NPC vivo (Fase 4 — NEEDS-05/06/07/08/09/10/11/12/13):
/// rotina diária por padrão, utility AI só quando alguma necessidade supera o limiar de urgência
/// do cenário, com bônus de continuidade (histerese) e conclusão de ação ao atingir a duração
/// máxima declarada. Deslocamento/moradia (T14) ainda não plugam um refinamento real no teto de
/// passos — <see cref="ResolveWithStepCap"/> já existe e é exercitado hoje com refinamento
/// identidade (sempre converge no passo 0); T14 troca esse refinamento pela dependência real de
/// local.</summary>
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

            bool justCompleted = TryCompleteAction(npc, catalog, now);
            var continuityAction = justCompleted ? null : npc.CurrentAction;

            var stage = world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate));
            var routineAction = catalog.RoutineOf(NoneIfSentinel(npc.Profession), stage, world.CurrentDate.Hour);

            var candidate = npc.HasUrgentNeed(rules)
                ? SelectByUtility(npc, rules, continuityAction)
                : routineAction;

            // T14 substitui esta identidade pela dependência real de local (ação vencedora que
            // exige outro local vira Travel) — o teto/abort (NEEDS-09) já valem hoje.
            var chosen = ResolveWithStepCap(npc.Id.Value, candidate, static a => a, rules.MaxActionSelectionSteps);

            if (justCompleted || chosen != npc.CurrentAction)
                npc.SetCurrentAction(chosen, now);
        }
    }

    /// <summary>NEEDS-13: ao atingir a duração máxima declarada (<see cref="ActionCatalog.MaxDurationHours"/>),
    /// aplica o efeito da ação e libera o NPC pra nova seleção — nunca deixa a ação corrente
    /// ultrapassar a duração declarada.</summary>
    private static bool TryCompleteAction(Npc npc, ActionCatalog catalog, long now)
    {
        if (npc.CurrentAction is not { } action) return false;
        if (now - npc.ActionStartedAtTick < catalog.MaxDurationHours[action]) return false;

        ApplyActionEffect(npc, action);
        return true;
    }

    /// <summary>Eat restaura fome e sede (sem ação de "beber" dedicada, ver <see cref="UtilityBaseOf"/>);
    /// Socialize restaura o mínimo social. Sleep restaura sono pra 100 aqui — a penalidade de
    /// sem-teto (<c>HomelessSleepEfficiency</c>) é T14. Work/Travel/Idle não restauram
    /// necessidade nenhuma, só concluem e liberam o NPC.</summary>
    private static void ApplyActionEffect(Npc npc, ActionType action)
    {
        switch (action)
        {
            case ActionType.Eat:
                npc.SetHunger(100);
                npc.SetThirst(100);
                break;
            case ActionType.Sleep:
                npc.SetSleep(100);
                break;
            case ActionType.Socialize:
                npc.SetSocial(100);
                break;
        }
    }

    /// <summary>Ordena as 6 ações candidatas por nota (<c>utilidadeBase × pesoPersonalidade</c>,
    /// NEEDS-06); empate exato desempata por menor <c>ActionId</c> — <c>Enum.GetValues</c> já
    /// devolve os valores em ordem ascendente do valor binário subjacente (Eat=0..Idle=5), então
    /// a primeira ocorrência de uma nota máxima é sempre a de menor id. NEEDS-12 (histerese): a
    /// ação corrente ganha <see cref="NeedsRules.ContinuityBonus"/> antes da comparação quando
    /// <see cref="NeedsRules.HysteresisEnabled"/> — só troca se algum desafiante superar essa
    /// nota efetiva.</summary>
    private static ActionType SelectByUtility(Npc npc, NeedsRules rules, ActionType? continuityAction)
    {
        var best = ActionType.Eat;
        double bestScore = double.NegativeInfinity;

        foreach (var action in Enum.GetValues<ActionType>())
        {
            double score = UtilityBaseOf(action, npc) * PersonalityWeighting.WeightOf(npc.Personality, action);
            if (rules.HysteresisEnabled && continuityAction == action)
                score += rules.ContinuityBonus;

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

    /// <summary>NEEDS-09: reavalia <paramref name="initial"/> por <paramref name="refine"/> até
    /// convergir (mesmo valor duas vezes seguidas) ou estourar <paramref name="maxSteps"/> —
    /// mesmo padrão de teto do <see cref="WorldClock"/> (rules/simulation-determinism.md).
    /// Estourar aborta nomeando o NPC e as ações que ficaram empatadas em ciclo, reusando
    /// <see cref="TickBudgetExceededException"/> (mesma convenção de nomear o culpado, aqui o
    /// NPC em vez do sistema).</summary>
    internal static ActionType ResolveWithStepCap(long npcId, ActionType initial, Func<ActionType, ActionType> refine, int maxSteps)
    {
        var current = initial;
        for (int step = 0; step < maxSteps; step++)
        {
            var next = refine(current);
            if (next == current) return current;
            current = next;
        }

        var final = refine(current);
        throw new TickBudgetExceededException(
            $"npc {npcId}: seleção de ação não convergiu em {maxSteps} passos — ações empatadas em ciclo ({current} <-> {final})",
            maxSteps);
    }
}
