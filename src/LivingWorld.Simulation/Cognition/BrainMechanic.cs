using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cognition;

/// <summary>Cérebro com ganhos sinápticos (ADR-0046, AD-142). Nada aqui sabe o nome de uma ação ou de um comportamento: o NPC
/// guarda quem escolheu a ação (participação de cada grupo de termos na nota), sente o que ela fez por ele na conclusão
/// (necessidades, despensa em perspectiva, humor) e reforça ou enfraquece os grupos na medida da surpresa contra o que essa ação
/// costuma lhe dar. Nasce com a média dos pais e segregação semeada; a criação puxa para quem criou. Só roda com
/// <see cref="WorldState.BrainOn"/>.</summary>
internal static class BrainMechanic
{
    private static readonly short[] NeutralGains = new short[BrainState.Actions * BrainState.Groups];

    /// <summary>O que o NPC sente agora, para medir o alívio de uma ação.</summary>
    internal readonly record struct Felt(int Needs, double Valence, double PantryDays, double SurvivalDeficit);

    /// <summary>Ganhos para a decisão: os do NPC, ou neutros (todos zero) se ele ainda não tem cérebro. Só leitura.</summary>
    internal static short[] GainsOf(Npc npc) => npc.Brain?.Gains ?? NeutralGains;

    /// <summary>Depois de uma escolha pela utilidade: guarda a participação (milésimos, com sinal) de cada grupo na nota da
    /// vencedora. Nota sem nenhum grupo (poder, furto) não deixa nada para aprender.</summary>
    internal static void Remember(Npc npc, ActionType action, ReadOnlySpan<double> groups)
    {
        double total = 0;
        foreach (double g in groups) total += Math.Abs(g);
        var brain = npc.Brain;
        if (total <= 0 || action is ActionType.UsePower or ActionType.Steal)
        {
            if (brain?.PendingAction is null) return;
            brain.Forget();
            npc.TouchBrain();
            return;
        }

        Span<short> shares = stackalloc short[BrainState.Groups];
        for (int i = 0; i < shares.Length; i++)
            shares[i] = (short)Math.Round(1000.0 * groups[i] / total, MidpointRounding.AwayFromZero);
        if (brain is null)
        {
            brain = BrainState.Neutral();
            npc.SetBrain(brain);
        }

        brain.Remember(action, shares);
        npc.TouchBrain();
    }

    /// <summary>Estado sentido: soma das quatro necessidades, valência do humor, dias de despensa por morador e o maior déficit
    /// de fome ou sede.</summary>
    internal static Felt Feel(WorldState world, Npc npc, long tick, int foodResourceId, int waterResourceId)
    {
        int hunger = npc.HungerAt(tick), thirst = npc.ThirstAt(tick);
        int needs = hunger + thirst + npc.SleepAt(tick) + npc.SocialAt(tick);
        double valence = npc.Psyche is null ? 0 : PsycheMechanic.AffectOf(world, npc, tick).Valence;
        double days = 0;
        if (npc.Household is { } id && world.FindHousehold(id) is { } home)
        {
            long scarcest = Math.Min(
                home.Stock.GetValueOrDefault(new ResourceType(foodResourceId)),
                home.Stock.GetValueOrDefault(new ResourceType(waterResourceId)));
            days = (double)scarcest / Math.Max(1, home.Members.Count);
        }

        return new Felt(needs, valence, days, Math.Max(100 - hunger, 100 - thirst));
    }

    /// <summary>Recompensa vivida de uma ação concluída: o que as necessidades subiram, a despensa que ela encheu valendo a
    /// fome ou sede que o NPC sentia (em perspectiva, até a meta de <paramref name="targetDays"/>) e a mudança de humor.</summary>
    internal static double Reward(Felt before, Felt after, double targetDays)
    {
        double pantry = targetDays <= 0 ? 0
            : Math.Clamp((after.PantryDays - before.PantryDays) / targetDays, 0, 1) * before.SurvivalDeficit;
        return after.Needs - before.Needs + pantry + 100 * (after.Valence - before.Valence);
    }

    /// <summary>Conclusão da ação guardada: cada grupo que a escolheu muda o ganho em <c>taxa(idade) × surpresa ×
    /// participação</c>; a surpresa é a recompensa menos o que esta ação costuma dar ao NPC, que então se atualiza.</summary>
    internal static void Learn(WorldState world, Npc npc, ActionType action, double reward)
    {
        var rules = world.BrainRules!;
        var brain = npc.Brain!;
        int a = (int)action;
        double surprise = reward - brain.ActionMeans[a];
        double rate = rules.LearningRate(npc.AgeYears(world.CurrentDate)) * rules.LearningPerPoint;
        var shares = brain.PendingShares!;
        int row = a * BrainState.Groups;
        for (int i = 0; i < BrainState.Groups; i++)
        {
            if (shares[i] == 0) continue;
            brain.Gains[row + i] = rules.Clamp(brain.Gains[row + i] + rate * surprise * shares[i] / 1000.0, i);
        }

        brain.ActionMeans[a] = (short)Math.Clamp(
            Math.Round(brain.ActionMeans[a] + surprise / rules.MeanWindow, MidpointRounding.AwayFromZero), short.MinValue, short.MaxValue);
        brain.Forget();
        npc.TouchBrain();
    }

    /// <summary>Nascimento: <c>h × média dos pais + √(1 − h²) × segregação</c> por ganho, como a personalidade
    /// (<see cref="Personality.Inherit"/>), com o stream próprio do bebê. Pais sem cérebro valem neutro.</summary>
    internal static BrainState Inherit(BrainRules rules, BrainState? mother, BrainState? father, WorldRng rng, double heritability)
    {
        double h = Math.Clamp(heritability, 0, 1), independent = Math.Sqrt(1 - h * h);
        var child = BrainState.Neutral();
        for (int i = 0; i < child.Gains.Length; i++)
        {
            double noise = (rng.NextDouble() * 2 - 1) * rules.InheritedNoiseMilli;
            double parents = ((mother?.Gains[i] ?? 0) + (father?.Gains[i] ?? 0)) / 2.0;
            child.Gains[i] = rules.Clamp(h * parents + independent * noise);
        }

        return child;
    }

    /// <summary>Uma amostra da criação: os ganhos da criança andam <see cref="BrainRules.RearingPullPerSample"/> na direção da
    /// média dos cuidadores (quem passa mais tempo junto puxa mais, por somar mais amostras).</summary>
    internal static void Rear(BrainRules rules, Npc child, IReadOnlyList<Npc> caregivers)
    {
        if (caregivers.Count == 0) return;
        var brain = child.Brain;
        if (brain is null)
        {
            brain = BrainState.Neutral();
            child.SetBrain(brain);
        }

        for (int i = 0; i < brain.Gains.Length; i++)
        {
            double sum = 0;
            for (int c = 0; c < caregivers.Count; c++) sum += caregivers[c].Brain?.Gains[i] ?? 0;
            double target = sum / caregivers.Count;
            brain.Gains[i] = rules.Clamp(brain.Gains[i] + (target - brain.Gains[i]) * rules.RearingPullPerSample);
        }

        child.TouchBrain();
    }
}
