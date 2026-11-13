using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cognition;

/// <summary>Memória da própria vida e emoção (AD-138). Só eventos escrevem; o tempo age por fórmula na leitura (decaimento
/// exponencial com meia-vida), então não há passo por tick nem por dia. Toda leitura é pura: o mundo reaberto de um snapshot
/// lê o mesmo valor que o que seguiu rodando.</summary>
public static class PsycheMechanic
{
    private const double Ln2 = 0.6931471805599453;
    private const double Permille = 1000.0;

    // ---------------- emoção ----------------

    /// <summary>Emoções atuais do NPC no <paramref name="tick"/>; calmo sem psique ou depois de <c>CalmFromTick</c>.</summary>
    public static Affect AffectOf(WorldState world, Npc npc, long tick)
    {
        if (npc.Psyche is not { } psyche || tick >= psyche.CalmFromTick) return Affect.Calm;
        var rules = world.NeedsRules.Psyche;
        if (!rules.Enabled) return Affect.Calm;
        int hpd = world.Calendar.HoursPerDay;
        long dt = Math.Max(0, tick - psyche.AffectTick);
        var p = npc.Personality;
        return new Affect(
            Decayed(psyche.Grief, dt, HalfLifeHours(rules, Emotion.Grief, p, hpd)),
            Decayed(psyche.Fear, dt, HalfLifeHours(rules, Emotion.Fear, p, hpd)),
            Decayed(psyche.Anger, dt, HalfLifeHours(rules, Emotion.Anger, p, hpd)),
            Decayed(psyche.Joy, dt, HalfLifeHours(rules, Emotion.Joy, p, hpd)),
            Decayed(psyche.Shame, dt, HalfLifeHours(rules, Emotion.Shame, p, hpd)),
            Decayed(psyche.Loneliness, dt, HalfLifeHours(rules, Emotion.Loneliness, p, hpd)));
    }

    /// <summary>Uma emoção só (leitura mais barata no caminho quente).</summary>
    public static double EmotionOf(WorldState world, Npc npc, Emotion emotion, long tick)
    {
        if (npc.Psyche is not { } psyche || tick >= psyche.CalmFromTick || !world.NeedsRules.Psyche.Enabled) return 0;
        int stored = emotion switch
        {
            Emotion.Grief => psyche.Grief,
            Emotion.Fear => psyche.Fear,
            Emotion.Anger => psyche.Anger,
            Emotion.Joy => psyche.Joy,
            Emotion.Shame => psyche.Shame,
            _ => psyche.Loneliness,
        };
        if (stored == 0) return 0;
        return Decayed(stored, Math.Max(0, tick - psyche.AffectTick),
            HalfLifeHours(world.NeedsRules.Psyche, emotion, npc.Personality, world.Calendar.HoursPerDay));
    }

    /// <summary>Meia-vida em horas. A emoção negativa dura mais em quem tem pouca estabilidade emocional (×0,7 a ×1,3).</summary>
    public static double HalfLifeHours(PsycheRules rules, Emotion emotion, Personality p, int hoursPerDay)
    {
        double days = rules.HalfLifeDaysOf(emotion);
        if (emotion != Emotion.Joy) days *= 0.7 + 0.6 * (1 - p.EmotionalStability / 100.0);
        return days * hoursPerDay;
    }

    private static double Decayed(int permille, long dtHours, double halfLifeHours) =>
        permille == 0 ? 0 : permille / Permille * DeterministicMath.Exp(-Ln2 * dtHours / halfLifeHours);

    /// <summary>Soma emoções ao estado atual (saturando em 1): <c>v + a·(1 − v)</c>. A reatividade vem da personalidade:
    /// pouca estabilidade emocional amplifica as negativas (×0,6 a ×1,4); a extroversão amplifica alegria e solidão.</summary>
    public static void Feel(WorldState world, Npc npc, long tick, LifeEventImpact impact, double scale = 1.0, double loneliness = 0)
    {
        var rules = world.NeedsRules.Psyche;
        if (!rules.Enabled) return;
        var p = npc.Personality;
        double negative = scale * (0.6 + 0.8 * (1 - p.EmotionalStability / 100.0));
        double positive = scale * (0.7 + 0.6 * p.Extroversion / 100.0);
        var now = AffectOf(world, npc, tick);
        double grief = Add(now.Grief, impact.Grief * negative);
        double fear = Add(now.Fear, impact.Fear * negative);
        double anger = Add(now.Anger, impact.Anger * negative);
        double joy = Add(now.Joy, impact.Joy * positive);
        double shame = Add(now.Shame, impact.Shame * negative);
        double lonely = loneliness >= 0
            ? Add(now.Loneliness, loneliness * (0.5 + p.Extroversion / 100.0))
            : Math.Max(0, now.Loneliness + loneliness);
        Store(world, npc, tick, grief, fear, anger, joy, shame, lonely);
    }

    private static double Add(double current, double amount) =>
        amount <= 0 ? current : Math.Clamp(current + amount * (1 - current), 0, 1);

    private static void Store(WorldState world, Npc npc, long tick,
        double grief, double fear, double anger, double joy, double shame, double loneliness)
    {
        var rules = world.NeedsRules.Psyche;
        int hpd = world.Calendar.HoursPerDay;
        var p = npc.Personality;
        int g = ToPermille(grief), f = ToPermille(fear), a = ToPermille(anger), j = ToPermille(joy), s = ToPermille(shame), l = ToPermille(loneliness);
        long calm = tick;
        calm = Math.Max(calm, CalmTick(tick, g, HalfLifeHours(rules, Emotion.Grief, p, hpd)));
        calm = Math.Max(calm, CalmTick(tick, f, HalfLifeHours(rules, Emotion.Fear, p, hpd)));
        calm = Math.Max(calm, CalmTick(tick, a, HalfLifeHours(rules, Emotion.Anger, p, hpd)));
        calm = Math.Max(calm, CalmTick(tick, j, HalfLifeHours(rules, Emotion.Joy, p, hpd)));
        calm = Math.Max(calm, CalmTick(tick, s, HalfLifeHours(rules, Emotion.Shame, p, hpd)));
        calm = Math.Max(calm, CalmTick(tick, l, HalfLifeHours(rules, Emotion.Loneliness, p, hpd)));
        var psyche = npc.Psyche ?? PsycheState.Empty;
        npc.SetPsyche(psyche with
        {
            AffectTick = tick,
            CalmFromTick = calm,
            Grief = g,
            Fear = f,
            Anger = a,
            Joy = j,
            Shame = s,
            Loneliness = l,
        });
    }

    private static int ToPermille(double v) => (int)Math.Round(Math.Clamp(v, 0, 1) * Permille, MidpointRounding.AwayFromZero);

    /// <summary>Emoção abaixo disto (milésimos) já não se sente: a leitura devolve calma.</summary>
    private const int FeltFromPermille = 20;

    /// <summary>Tick em que a emoção guardada cai abaixo de <see cref="FeltFromPermille"/>: <c>meia-vida × log2(v / limiar)</c>.</summary>
    private static long CalmTick(long tick, int permille, double halfLifeHours) =>
        permille <= FeltFromPermille ? tick
            : tick + (long)Math.Ceiling(halfLifeHours * DeterministicMath.Log(permille / (double)FeltFromPermille) / Ln2);

    // ---------------- memória ----------------

    /// <summary>Força atual (0..100) de uma lembrança: saliência que decai desde o último reforço, com meia-vida maior para o que
    /// foi marcante.</summary>
    public static double StrengthOf(PsycheRules rules, in EpisodicMemory memory, long tick, int hoursPerDay)
    {
        double halfLife = rules.MemoryHalfLifeDays * (1 + rules.SalienceHalfLifeGain * memory.Salience) * hoursPerDay;
        long dt = Math.Max(0, tick - memory.RehearsedTick);
        return memory.Salience * DeterministicMath.Exp(-Ln2 * dt / halfLife);
    }

    /// <summary>Grava uma lembrança e desperta as emoções do acontecimento (escala por proximidade do vínculo).</summary>
    public static void Experience(WorldState world, Npc npc, LifeEventKind kind, long other, long tick, double scale = 1.0,
        LifeEventImpact? impactOverride = null)
    {
        var rules = world.NeedsRules.Psyche;
        if (!rules.Enabled || !npc.IsAlive) return;
        var impact = impactOverride ?? rules.ImpactOf(kind);
        int salience = (int)Math.Round(Math.Clamp(impact.Salience * scale, 0, 100));
        Remember(world, npc, kind, other, tick, salience);
        Feel(world, npc, tick, impact, scale);
        Imprint(rules, npc, kind, impact, salience);
    }

    /// <summary>O que devasta deixa marca na estabilidade emocional; o casamento a firma um pouco (investimento social).</summary>
    private static void Imprint(PsycheRules rules, Npc npc, LifeEventKind kind, LifeEventImpact impact, int salience)
    {
        if (rules.ImprintPoints == 0) return;
        var p = npc.Personality;
        int delta = 0;
        if (salience >= rules.ImprintSalience && (impact.Grief > 0 || impact.Anger > 0 || impact.Shame > 0)) delta = -rules.ImprintPoints;
        else if (kind == LifeEventKind.Married) delta = rules.ImprintPoints / 2;
        if (delta == 0) return;
        npc.ImprintPersonality(p with { EmotionalStability = Math.Clamp(p.EmotionalStability + delta, 0, 100) });
    }

    /// <summary>Lembrança nova, ou reforço da que já existe do mesmo tipo com a mesma pessoa. Lembranças com a mesma pessoa
    /// são reavivadas. A capacidade é limitada: as fracas somem e, cheia, a mais fraca dá lugar à nova se a nova for mais forte.</summary>
    public static void Remember(WorldState world, Npc npc, LifeEventKind kind, long other, long tick, int salience)
    {
        var rules = world.NeedsRules.Psyche;
        if (!rules.Enabled || rules.MemoryCapacity == 0 || salience <= 0) return;
        int hpd = world.Calendar.HoursPerDay;
        var psyche = npc.Psyche ?? PsycheState.Empty;
        var kept = new List<EpisodicMemory>(Math.Min(rules.MemoryCapacity, psyche.Memories.Count + 1));
        bool merged = false;
        foreach (var memory in psyche.Memories)
        {
            double strength = StrengthOf(rules, memory, tick, hpd);
            if (strength < rules.ForgetBelow) continue;
            if (!merged && memory.Kind == kind && memory.Other == other)
            {
                // reforço: a lembrança volta à força de agora mais o reforço, sem passar de 100
                int boosted = (int)Math.Min(100, Math.Round(Math.Max(strength, salience) + rules.RehearsalBoost));
                kept.Add(memory with { Salience = boosted, RehearsedTick = tick });
                merged = true;
                continue;
            }

            if (other != 0 && memory.Other == other)
            {
                int boosted = (int)Math.Min(100, Math.Round(strength + rules.RehearsalBoost / 2.0));
                kept.Add(memory with { Salience = Math.Max(memory.Salience, boosted), RehearsedTick = tick });
                continue;
            }

            kept.Add(memory);
        }

        if (!merged)
        {
            var fresh = new EpisodicMemory(kind, other, tick, salience, tick);
            if (kept.Count < rules.MemoryCapacity)
                kept.Add(fresh);
            else
            {
                int weakest = 0;
                double weakestStrength = double.MaxValue;
                for (int i = 0; i < kept.Count; i++)
                {
                    double s = StrengthOf(rules, kept[i], tick, hpd);
                    if (s < weakestStrength) { weakestStrength = s; weakest = i; }
                }
                if (salience > weakestStrength) kept[weakest] = fresh;
            }
        }

        npc.SetPsyche(psyche with { Memories = kept.ToArray() });
    }

    /// <summary>Força (0..100) da lembrança mais forte do tipo dado (e com a pessoa dada, se <paramref name="other"/> ≠ 0).</summary>
    public static double Strongest(WorldState world, Npc npc, LifeEventKind kind, long tick, long other = 0)
    {
        if (npc.Psyche is not { Memories.Count: > 0 } psyche) return 0;
        var rules = world.NeedsRules.Psyche;
        if (!rules.Enabled) return 0;
        double best = 0;
        foreach (var memory in psyche.Memories)
            if (memory.Kind == kind && (other == 0 || memory.Other == other))
                best = Math.Max(best, StrengthOf(rules, memory, tick, world.Calendar.HoursPerDay));
        return best;
    }

    /// <summary>Mágoa (0..1) com uma pessoa: a lembrança mais forte de mal sofrido por ela (traição, furto, violência, abandono).</summary>
    public static double GrudgeAgainst(WorldState world, Npc npc, long other, long tick)
    {
        if (other == 0 || npc.Psyche is not { Memories.Count: > 0 } psyche || !world.NeedsRules.Psyche.Enabled) return 0;
        var rules = world.NeedsRules.Psyche;
        double best = 0;
        foreach (var memory in psyche.Memories)
            if (memory.Other == other && memory.Kind is LifeEventKind.Betrayed or LifeEventKind.Robbed
                    or LifeEventKind.Violence or LifeEventKind.Abandoned)
                best = Math.Max(best, StrengthOf(rules, memory, tick, world.Calendar.HoursPerDay));
        return best / 100.0;
    }

    /// <summary>Cautela (0..1) de quem foi traído, roubado ou abandonado: a lembrança mais forte desses males, com qualquer um.</summary>
    public static double Wariness(WorldState world, Npc npc, long tick)
    {
        if (npc.Psyche is not { Memories.Count: > 0 } psyche || !world.NeedsRules.Psyche.Enabled) return 0;
        var rules = world.NeedsRules.Psyche;
        double best = 0;
        foreach (var memory in psyche.Memories)
            if (memory.Kind is LifeEventKind.Betrayed or LifeEventKind.Robbed or LifeEventKind.Abandoned)
                best = Math.Max(best, StrengthOf(rules, memory, tick, world.Calendar.HoursPerDay));
        return best / 100.0;
    }

    /// <summary>Tetos de afeto e confiança que a convivência pode refazer, depois da cautela (traição, furto, abandono sofridos:
    /// confia menos em todos) e da mágoa com esta pessoa (confia e gosta menos dela).</summary>
    public static (double Affection, double Trust) BondCeilings(
        WorldState world, Npc from, Npc to, long tick, double affectionCeiling, double trustCeiling)
    {
        var rules = world.NeedsRules.Psyche;
        if (!rules.Enabled) return (affectionCeiling, trustCeiling);
        double wariness = Wariness(world, from, tick);
        double grudge = GrudgeAgainst(world, from, to.Id.Value, tick);
        trustCeiling *= (1 - rules.BetrayalWariness * wariness) * (1 - grudge);
        affectionCeiling *= 1 - grudge;
        return (affectionCeiling, trustCeiling);
    }

    /// <summary>Gratidão (0..1) de quem pode ajudar para com uma casa: a lembrança mais forte de ter sido ajudado por um dos
    /// moradores, e uma parte da gratidão por qualquer ajuda recebida (quem foi ajudado ajuda adiante).</summary>
    public static double Gratitude(WorldState world, Npc helper, IReadOnlyList<Npc> household, long tick)
    {
        if (helper.Psyche is not { Memories.Count: > 0 } psyche || !world.NeedsRules.Psyche.Enabled) return 0;
        var rules = world.NeedsRules.Psyche;
        double direct = 0, any = 0;
        foreach (var memory in psyche.Memories)
        {
            if (memory.Kind != LifeEventKind.AidReceived) continue;
            double strength = StrengthOf(rules, memory, tick, world.Calendar.HoursPerDay);
            any = Math.Max(any, strength);
            foreach (var member in household)
                if (member.Id.Value == memory.Other) direct = Math.Max(direct, strength);
        }
        return Math.Max(direct, 0.3 * any) / 100.0;
    }

    /// <summary>Meta de despensa multiplicada pela lembrança de fome: quem passou fome guarda mais.</summary>
    public static double HoardFactor(WorldState world, Npc npc, long tick)
    {
        double famine = Strongest(world, npc, LifeEventKind.Famine, tick);
        return famine <= 0 ? 1.0 : 1.0 + world.NeedsRules.Psyche.HoardGain * famine / 100.0;
    }

    /// <summary>Recolhimento (0..0,9): fração do trabalho que luto e vergonha tiram.</summary>
    public static double WorkWithdrawal(WorldState world, Npc npc, long tick)
    {
        if (npc.Psyche is not { } psyche || tick >= psyche.CalmFromTick || (psyche.Grief == 0 && psyche.Shame == 0)) return 0;
        var rules = world.NeedsRules.Psyche;
        double grief = EmotionOf(world, npc, Emotion.Grief, tick);
        double shame = EmotionOf(world, npc, Emotion.Shame, tick);
        return Math.Clamp(rules.WorkWithdrawalAtFullGrief * grief + rules.ShameWithdrawal * rules.WorkWithdrawalAtFullGrief * shame, 0, 0.9);
    }

    /// <summary>Recolhimento social (0..0,9): fração das horas de convívio que luto e vergonha tiram.</summary>
    public static double SocialWithdrawal(WorldState world, Npc npc, long tick)
    {
        if (npc.Psyche is not { } psyche || tick >= psyche.CalmFromTick || (psyche.Grief == 0 && psyche.Shame == 0)) return 0;
        var rules = world.NeedsRules.Psyche;
        double grief = EmotionOf(world, npc, Emotion.Grief, tick);
        double shame = EmotionOf(world, npc, Emotion.Shame, tick);
        return Math.Clamp(rules.SocialWithdrawalAtFullGrief * grief + rules.ShameWithdrawal * shame, 0, 0.9);
    }
}
