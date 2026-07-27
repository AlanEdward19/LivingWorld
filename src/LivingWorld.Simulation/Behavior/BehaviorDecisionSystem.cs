using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Escolhe a ação do tick de cada NPC vivo (Fase 4 — NEEDS-05..16): rotina diária por
/// padrão, utility AI só quando alguma necessidade supera o limiar de urgência do cenário, com
/// bônus de continuidade (histerese), conclusão de ação ao atingir a duração máxima declarada, e
/// deslocamento com custo real quando a ação vencedora exige um local diferente do atual (hoje,
/// só <c>Sleep</c> tem exigência de local — dormir em <see cref="Household"/>.Location; sem-teto
/// dorme onde está, NEEDS-15).</summary>
public sealed class BehaviorDecisionSystem : ISimulationSystem
{
    public const string SystemName = "behavior-decision";

    /// <summary>Utilidade base de ações não ligadas a uma das 4 necessidades modeladas (Work,
    /// Travel, Idle) — parte do modelo de decisão em si (mesmo status que a fórmula de
    /// <see cref="PersonalityWeighting"/>: constante de algoritmo, não dado de cenário). Mesma
    /// escala 0-100 do déficit de necessidade, para que personalidade dispute em pé de igualdade
    /// quando nada mais está em jogo.</summary>
    private const double NonNeedBaselineUtility = 50.0;

    /// <summary>Cache de <c>Enum.GetValues&lt;ActionType&gt;()</c> — o método aloca um array novo
    /// a cada chamada (cópia defensiva do cache interno do runtime); chamado por NPC por tick
    /// Hourly, isso era a maior fonte de alocação do sistema em população grande. Mesma ordem
    /// ascendente do valor binário (Eat=0..Idle=5) que o desempate por <c>ActionId</c> exige.</summary>
    private static readonly ActionType[] AllActions = Enum.GetValues<ActionType>();

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

            bool justCompleted = TryCompleteAction(world, npc, rules, catalog, now);
            var continuityAction = justCompleted ? null : npc.CurrentAction;

            var stage = world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate));
            var routineAction = catalog.RoutineOf(NoneIfSentinel(npc.Profession), stage, world.CurrentDate.Hour);

            var candidate = npc.HasUrgentNeed(rules)
                ? SelectByUtility(npc, rules, continuityAction)
                : routineAction;

            // NEEDS-14: ação vencedora que exige outro local (hoje, só Sleep) vira Travel até lá;
            // reavaliado em loop com teto MaxActionSelectionSteps (NEEDS-09).
            var chosen = ResolveWithStepCap(npc.Id.Value, candidate, c => RefineForLocation(world, npc, c), rules.MaxActionSelectionSteps);

            if (justCompleted || chosen != npc.CurrentAction)
                npc.SetCurrentAction(chosen, now);
        }
    }

    /// <summary>NEEDS-13/14: ao atingir a duração máxima declarada (<see cref="ActionCatalog.MaxDurationHours"/>)
    /// aplica o efeito da ação e libera o NPC pra nova seleção. <c>Travel</c> rumo ao local de
    /// sono (<see cref="RefineForLocation"/>) é exceção: sua duração não é a do catálogo, é
    /// <see cref="TravelResolution.TicksBetween"/> até o destino — chegar move o NPC
    /// (<see cref="Npc.MoveTo"/>) e libera pra nova seleção, sem aplicar efeito de Sleep no tick
    /// de chegada (NEEDS-14: ação de destino nunca executa no tick em que decidiu ir).</summary>
    private static bool TryCompleteAction(WorldState world, Npc npc, NeedsRules rules, ActionCatalog catalog, long now)
    {
        if (npc.CurrentAction is not { } action) return false;

        if (action == ActionType.Travel && SleepDestinationOf(world, npc) is { } destination && destination != npc.CurrentLocation)
        {
            long ticksNeeded = TravelResolution.TicksBetween(world.Map, npc.CurrentLocation, destination);
            if (now - npc.ActionStartedAtTick < ticksNeeded) return false; // ainda em trânsito

            npc.MoveTo(destination, now);
            return true;
        }

        if (now - npc.ActionStartedAtTick < catalog.MaxDurationHours[action]) return false;

        ApplyActionEffect(npc, rules, action);
        return true;
    }

    /// <summary>Eat restaura fome e sede (sem ação de "beber" dedicada, ver <see cref="UtilityBaseOf"/>);
    /// Socialize restaura o mínimo social. Sleep restaura sono pra 100, ou, sem-teto
    /// (<see cref="Npc.HomelessSince"/> não nulo), pra <see cref="NeedsRules.HomelessSleepEfficiency"/>
    /// × 100 (NEEDS-15) — nunca lança exceção por falta de residência. Work/Travel/Idle não
    /// restauram necessidade nenhuma, só concluem e liberam o NPC.</summary>
    private static void ApplyActionEffect(Npc npc, NeedsRules rules, ActionType action)
    {
        switch (action)
        {
            case ActionType.Eat:
                npc.SetHunger(100);
                npc.SetThirst(100);
                break;
            case ActionType.Sleep:
                npc.SetSleep(npc.HomelessSince is null ? 100 : (int)(100 * rules.HomelessSleepEfficiency));
                break;
            case ActionType.Socialize:
                npc.SetSocial(100);
                break;
        }
    }

    /// <summary>NEEDS-14: se a ação candidata for <c>Sleep</c> e exigir um local diferente do
    /// atual (residência existe e não é onde o NPC está), a ação efetiva do tick vira
    /// <c>Travel</c> — sem-teto (<see cref="SleepDestinationOf"/> retorna <c>null</c>) nunca
    /// precisa viajar pra dormir (NEEDS-15).</summary>
    private static ActionType RefineForLocation(WorldState world, Npc npc, ActionType candidate)
    {
        if (candidate != ActionType.Sleep) return candidate;
        return SleepDestinationOf(world, npc) is { } destination && destination != npc.CurrentLocation
            ? ActionType.Travel
            : candidate;
    }

    /// <summary>Local onde o NPC dorme de fato: <see cref="Household"/>.Location se tiver
    /// residência; <c>null</c> se sem-teto (dorme onde está, sem viagem — Tech Decision:
    /// Residence reusa <see cref="Npc.Household"/>, nenhum campo novo).</summary>
    private static CellCoord? SleepDestinationOf(WorldState world, Npc npc) =>
        npc.Household is { } householdId ? world.FindHousehold(householdId)?.Location : null;

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

        foreach (var action in AllActions)
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
