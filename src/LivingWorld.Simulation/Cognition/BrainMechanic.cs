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

    /// <summary>Ganhos para uma decisão agora (AD-160): com fome ou sede crítica o corpo decide (neutros, a escolha é a da utilidade
    /// pura e os grupos ainda são guardados para aprender); fora disso, os do NPC.</summary>
    internal static short[] GainsFor(BrainRules rules, Npc npc, long tick) =>
        npc.Brain is null || rules.BodyDecides(npc.HungerAt(tick), npc.ThirstAt(tick)) ? NeutralGains : npc.Brain.Gains;

    /// <summary>Depois de uma escolha pela utilidade: guarda a participação (milésimos, com sinal) de cada grupo na nota da
    /// vencedora. Nota sem nenhum grupo (poder, furto) não deixa nada para aprender.</summary>
    internal static void Remember(Npc npc, ActionType action, ReadOnlySpan<double> groups, double valueNow = 0)
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

        brain.Remember(action, shares, valueNow);
        npc.TouchBrain();
    }

    /// <summary>Preço de referência de uma unidade de comida: o piso do cenário (nunca menos de 1). Fixo, para a moeda valer o
    /// mesmo em toda medida e a conta não depender de achar o mercado.</summary>
    internal static long FoodReferencePrice(WorldState world) =>
        Math.Max(1, world.EconomyRules.PriceFloor.GetValueOrDefault(world.EconomyRules.FoodResourceId));

    /// <summary>AD-160b: o estado do NPC agora em pontos (<see cref="BrainRules.ValuesSustenance"/>).</summary>
    internal static BrainValue ValueOf(WorldState world, Npc npc, long tick)
    {
        var rules = world.BrainRules ?? BrainRules.Default;
        double body = npc.HungerAt(tick) + npc.ThirstAt(tick) + npc.SleepAt(tick) + npc.SocialAt(tick);
        double mood = npc.Psyche is null ? 0 : 100 * PsycheMechanic.AffectOf(world, npc, tick).Valence;
        double pantryDays = 0;
        int members = 1;
        if (npc.Household is { } id && world.FindHousehold(id) is { } home)
        {
            members = Math.Max(1, home.Members.Count);
            var economy = world.EconomyRules;
            long scarcest = Math.Min(
                home.Stock.GetValueOrDefault(new ResourceType(economy.FoodResourceId)),
                home.Stock.GetValueOrDefault(new ResourceType(economy.WaterResourceId)));
            pantryDays = (double)scarcest / members;
        }

        double moneyDays = (double)npc.Wallet.Amount / (FoodReferencePrice(world) * members);
        double horizon = rules.ValueHorizonDays;
        return new BrainValue(body, rules.PantryDayValue * Math.Min(pantryDays, horizon),
            rules.MoneyDayValue * Math.Min(moneyDays, horizon), mood);
    }

    /// <summary>AD-160b: o salário que uma jornada de <paramref name="hours"/> horas rende ao NPC, em pontos (dias de comida que ele
    /// compra × <see cref="BrainRules.MoneyDayValue"/>, até o horizonte). O salário cai no fim do mês; a parte desta jornada é
    /// creditada ao trabalho feito, no fim dele.</summary>
    internal static double WageCredit(WorldState world, Npc npc, double hours)
    {
        var rules = world.BrainRules!;
        if (rules.WageHoursPerMonth <= 0 || npc.Employer is null) return 0;
        if (!world.EconomyRules.WageByProfession.TryGetValue(npc.Profession.Id, out long wage) || wage <= 0) return 0;
        int members = npc.Household is { } id && world.FindHousehold(id) is { } home ? Math.Max(1, home.Members.Count) : 1;
        double days = wage * hours / rules.WageHoursPerMonth / (FoodReferencePrice(world) * members);
        return rules.MoneyDayValue * Math.Min(days, rules.ValueHorizonDays);
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
    internal static void Learn(WorldState world, Npc npc, ActionType action, double reward, bool stepped = false)
    {
        ulong draw = stepped ? DrawState(world, "brain-learn", npc, world.CurrentDate.TotalHours) : 0;
        var rules = world.BrainRules!;
        var brain = npc.Brain!;
        int a = (int)action;
        short known = brain.ActionMeans[a];
        // AD-160: sem expectativa ainda, a primeira vez só a forma (surpresa zero); no AD-142 a expectativa começava em zero
        double expected = known == BrainState.Unknown && rules.FirstExperienceSetsExpectation ? reward : known;
        double surprise = reward - expected;
        double rate = rules.LearningRate(npc.AgeYears(world.CurrentDate)) * rules.LearningPerPoint;
        int row = a * BrainState.Groups;
        for (int i = 0; i < BrainState.Groups; i++)
        {
            int share = brain.ShareOf(i);
            if (share == 0) continue;
            // AD-160b: em degraus (a média aprendida é a mesma; o ganho cabe num byte no snapshot)
            double delta = Stepped(rules, rate * surprise * share / 1000.0, ref draw, stepped);
            if (delta == 0) continue;
            brain.Gains[row + i] = rules.Clamp(brain.Gains[row + i] + delta, i);
        }

        short mean = (short)Math.Clamp(
            Math.Round(expected + surprise / rules.MeanWindow, MidpointRounding.AwayFromZero), short.MinValue, short.MaxValue);
        if (mean == BrainState.Unknown && rules.FirstExperienceSetsExpectation) mean = (short)(expected >= 0 ? 1 : -1); // vivida
        brain.ActionMeans[a] = mean;
        brain.Forget();
        npc.TouchBrain();
    }

    /// <summary>AD-160b: uma mudança de ganho em degraus de <see cref="BrainRules.GainStep"/>; o que sobra de um degrau move com
    /// chance proporcional (sem viés). Sem sorteio (ou degrau 1), a mudança como veio.</summary>
    internal static double Stepped(BrainRules rules, double delta, ref ulong draw, bool drawn = true)
    {
        if (rules.GainStep <= 1 || !drawn) return delta;
        double steps = delta / rules.GainStep, whole = Math.Floor(steps);
        return (whole + (WorldRng.NextDouble(ref draw) < steps - whole ? 1 : 0)) * rules.GainStep;
    }

    /// <summary>Sorteio do degrau (AD-160b): stream do mundo por (propósito, NPC, tick), sem alocar. O tick entra na chave: o stream
    /// por NPC sozinho daria o mesmo primeiro número a cada vez.</summary>
    internal static ulong DrawState(WorldState world, string purpose, Npc npc, long tick) =>
        world.Rng.StreamStateFor(purpose, (npc.Id.Value << 24) ^ (tick & 0xFFFFFF));

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
            double inherited = h * parents + independent * noise;
            if (rules.GainStep > 1) inherited = Math.Round(inherited / rules.GainStep, MidpointRounding.AwayFromZero) * rules.GainStep;
            child.Gains[i] = rules.Clamp(inherited, i % BrainState.Groups);
        }

        // AD-160b: o filho já sabe pelo que viu em casa o que cada ação costuma render (média do que os pais esperam)
        if (rules.InheritExpectation)
            for (int a = 0; a < child.ActionMeans.Length; a++)
            {
                int m = mother?.ActionMeans[a] ?? 0, f = father?.ActionMeans[a] ?? 0;
                int known = (m != BrainState.Unknown ? 1 : 0) + (f != BrainState.Unknown ? 1 : 0);
                if (known > 0) child.ActionMeans[a] = (short)Math.Round((m + f) / (double)known, MidpointRounding.AwayFromZero) is var e and not 0 ? e : (short)1;
            }

        return child;
    }

    /// <summary>Uma amostra da criação: os ganhos da criança andam <see cref="BrainRules.RearingPullPerSample"/> na direção da
    /// média dos cuidadores (quem passa mais tempo junto puxa mais, por somar mais amostras).</summary>
    internal static void Rear(BrainRules rules, Npc child, IReadOnlyList<Npc> caregivers, WorldState? world = null)
    {
        if (caregivers.Count == 0) return;
        var brain = child.Brain;
        if (brain is null)
        {
            brain = BrainState.Neutral();
            child.SetBrain(brain);
        }

        ulong draw = world is null ? 0 : DrawState(world, "brain-rear", child, world.CurrentDate.TotalHours);
        for (int i = 0; i < brain.Gains.Length; i++)
        {
            double sum = 0;
            for (int c = 0; c < caregivers.Count; c++) sum += caregivers[c].Brain?.Gains[i] ?? 0;
            double target = sum / caregivers.Count;
            double pull = Stepped(rules, (target - brain.Gains[i]) * rules.RearingPullPerSample, ref draw, world is not null);
            if (pull != 0) brain.Gains[i] = rules.Clamp(brain.Gains[i] + pull, i % BrainState.Groups);
        }

        child.TouchBrain();
    }
}
