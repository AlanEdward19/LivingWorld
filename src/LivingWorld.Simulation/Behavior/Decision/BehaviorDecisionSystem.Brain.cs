using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Behavior.Decision;

// Parte de BehaviorDecisionSystem — o cérebro fora da escolha urgente (AD-160b): o que a ação concluída ensinou e o tempo livre.
public sealed partial class BehaviorDecisionSystem
{
    private static readonly ActionType[] LeisureChoices = [ActionType.Socialize, ActionType.Idle, ActionType.Sleep, ActionType.Work];
    private static readonly short[] NoGains = new short[BrainState.Actions * BrainState.Groups];

    /// <summary>Último valor do estado medido (AD-160b): o fim de uma ação é o começo da próxima, então o valor medido ao concluir
    /// serve à escolha seguinte do mesmo NPC no mesmo tick sem medir de novo. Cache de uma função pura do mundo naquele ponto:
    /// reaberto de um snapshot, o tick refaz a mesma sequência.</summary>
    [ThreadStatic] private static (WorldState? World, long Npc, long Tick, double Value) _lastValue;

    private static double BrainValueAt(WorldState world, Npc npc, long now)
    {
        var last = _lastValue;
        if (ReferenceEquals(last.World, world) && last.Npc == npc.Id.Value && last.Tick == now) return last.Value;
        double value = BrainMechanic.ValueOf(world, npc, now).Total;
        _lastValue = (world, npc.Id.Value, now, value);
        return value;
    }

    /// <summary>A ação guardada terminou: a recompensa é a variação do valor do estado entre o começo e o fim dela (AD-160b), mais o
    /// salário que a jornada rende quando é trabalho; no AD-142, o alívio medido antes e depois do efeito.</summary>
    private static void BrainLearnOnCompletion(
        WorldState world, Npc npc, ActionType action, BrainMechanic.Felt feltBefore, long startedAt, long now, TickContext ctx)
    {
        var rules = world.BrainRules!;
        double reward;
        if (rules.ValuesSustenance)
        {
            double end = BrainMechanic.ValueOf(world, npc, now).Total;
            _lastValue = (world, npc.Id.Value, now, end);
            reward = end - npc.Brain!.PendingValue
                + (action == ActionType.Work ? BrainMechanic.WageCredit(world, npc, now - startedAt) : 0);
        }
        else
        {
            reward = BrainMechanic.Reward(feltBefore, FeltNow(world, npc, now), ProvisionTargetDays);
        }

        BrainMechanic.Learn(world, npc, action, reward, stepped: rules.GainStep > 1);
    }

    /// <summary>Depois de uma escolha pela utilidade: guarda a participação dos grupos e o valor do estado no começo. AD-160b: só a
    /// escolha cujo desfecho vai ensinar — a que começa uma ação; quem decide continuar o que já faz mantém a que a começou (e não
    /// mede o estado de novo).</summary>
    private static void BrainRemember(
        WorldState world, Npc npc, ActionType chosen, ReadOnlySpan<double> groups, long now, bool justCompleted)
    {
        if (!justCompleted && chosen == npc.CurrentAction && npc.Brain?.PendingAction == chosen) return;
        BrainMechanic.Remember(npc, chosen, groups, world.BrainRules!.ValuesSustenance ? BrainValueAt(world, npc, now) : 0);
    }

    /// <summary>Tempo livre pesado pelo cérebro (AD-160b): na hora em que a rotina diz Conversar ou Ficar à toa, o NPC escolhe entre
    /// conversar, ficar à toa, deitar mais cedo (se a rotina manda dormir nas próximas <see cref="EarlyBedHours"/> horas) e ficar
    /// mais no trabalho (se ainda está nele), com a mesma forma da utilidade (necessidade × temperamento + hábito da rotina), cada
    /// grupo com o ganho que ele herdou e aprendeu: dois vizinhos do mesmo ofício passam a ter tardes diferentes. Com fome ou sede crítica não chega aqui (é urgência); o corpo vem antes. Uma escolha que
    /// muda a ação fica guardada para ensinar pelo que rendeu; a rotina que não passa pelo cérebro esquece a escolha anterior.</summary>
    private static ActionType BrainWeighsRoutine(
        WorldState world, Npc npc, ActionType routine, long now, bool justCompleted, ActionCatalog catalog, LifeStage stage)
    {
        var rules = world.BrainRules!;
        if (!rules.WeighsLeisure || routine is not (ActionType.Idle or ActionType.Socialize))
        {
            if (npc.Brain?.PendingAction is not null && (justCompleted || routine != npc.CurrentAction))
            {
                npc.Brain.Forget(); // a rotina escolheu: o desfecho não ensina os grupos da escolha anterior
                npc.TouchBrain();
            }
            return routine;
        }

        // o corpo antes do gosto: com fome ou sede abaixo do piso o tempo livre é o da rotina (quem escolhia conversar ou passear
        // com sede saía de perto da água: +34 h de sede crítica longe de casa por pessoa-ano, AD-160b)
        if (npc.HungerAt(now) < rules.LeisureNeedFloor || npc.ThirstAt(now) < rules.LeisureNeedFloor) return routine;

        // AD-160b: decide na fronteira da rotina (ao entrar no tempo livre ou quando a rotina muda de faixa), não a cada despertar
        // nem a cada ação concluída: quem escolheu conversar ou ficar à toa segue nisso até a faixa acabar (era o maior custo do
        // cérebro por NPC-tick)
        if (npc.CurrentAction is ActionType.Socialize or ActionType.Idle && !RoutineSlotStarts(world, npc, catalog, stage))
            return npc.CurrentAction.Value;

        var gains = npc.Brain?.Gains ?? NoGains;
        double continuity = world.NeedsRules.ContinuityBonus;
        var best = routine;
        double bestScore = double.NegativeInfinity;
        Span<double> bestGroups = stackalloc double[BrainState.Groups];
        bool bedSoon = rules.LeisureEarlyBed && BedtimeWithin(world, npc, catalog, stage, EarlyBedHours);
        // fica mais no trabalho só quem não está com fome nem sede (longe da despensa a sede chega ao crítico antes de voltar)
        bool atWork = rules.LeisureStaysAtWork && npc.CurrentAction == ActionType.Work && npc.Employer is not null
            && npc.HungerAt(now) >= StayAwayNeedFloor && npc.ThirstAt(now) >= StayAwayNeedFloor;
        foreach (var action in LeisureChoices)
        {
            if (action == ActionType.Sleep && !bedSoon || action == ActionType.Work && !atWork) continue;
            double baseValue = action switch
            {
                ActionType.Socialize => Deficit(npc.SocialAt(now)),
                ActionType.Sleep => SurvivalDeficit(npc.SleepAt(now)),
                _ => NonNeedBaselineUtility,
            };
            double temperament = baseValue * (LivingWorld.Domain.Behavior.PersonalityWeighting.WeightOf(npc.Personality, action) - 1);
            double habit = action == routine ? continuity : 0;
            int row = (int)action * BrainState.Groups;
            double b = baseValue * (1 + gains[row + (int)UtilityGroup.Base] * 0.001);
            double p = temperament * (1 + gains[row + (int)UtilityGroup.Personality] * 0.001);
            double h = habit * (1 + gains[row + (int)UtilityGroup.Continuity] * 0.001);
            double score = b + p + h;
            if (score <= bestScore) continue;
            bestScore = score;
            best = action;
            bestGroups.Clear();
            bestGroups[(int)UtilityGroup.Base] = b;
            bestGroups[(int)UtilityGroup.Personality] = p;
            bestGroups[(int)UtilityGroup.Continuity] = h;
        }

        if (justCompleted || best != npc.CurrentAction)
            BrainRemember(world, npc, best, bestGroups, now, justCompleted);
        return best;
    }

    /// <summary>Quantas horas antes da hora de dormir da rotina o NPC pode escolher deitar (AD-160b).</summary>
    private const int EarlyBedHours = 2;

    /// <summary>Fome e sede mínimas para ficar além do turno (AD-160b).</summary>
    private const int StayAwayNeedFloor = 50;

    /// <summary>Esta hora começa uma faixa nova da rotina do NPC (a ação da rotina difere da hora anterior).</summary>
    private static bool RoutineSlotStarts(WorldState world, Npc npc, ActionCatalog catalog, LifeStage stage)
    {
        var profession = NoneIfSentinel(npc.Profession);
        int hpd = world.Calendar.HoursPerDay, hour = world.CurrentDate.Hour;
        return Needs.RoutineBoundaries.RoutineAt(catalog, profession, stage, hour, hpd)
            != Needs.RoutineBoundaries.RoutineAt(catalog, profession, stage, (hour + hpd - 1) % hpd, hpd);
    }

    /// <summary>A rotina do NPC manda dormir em até <paramref name="hours"/> horas.</summary>
    private static bool BedtimeWithin(WorldState world, Npc npc, ActionCatalog catalog, LifeStage stage, int hours)
    {
        var profession = NoneIfSentinel(npc.Profession);
        int hpd = world.Calendar.HoursPerDay, hour = world.CurrentDate.Hour;
        for (int k = 1; k <= hours; k++)
            if (Needs.RoutineBoundaries.RoutineAt(catalog, profession, stage, (hour + k) % hpd, hpd) == ActionType.Sleep) return true;
        return false;
    }
}
