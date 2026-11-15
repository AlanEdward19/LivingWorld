using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Crime;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Crime;

/// <summary>Violência sexual (AD-154) como evento abstrato do motor: autor, vítima, caso e consequências, sem cena, sem
/// descrição do ato e sem campo que o detalhe. Sai do temperamento do autor (a propensão da violência vezes pouca amabilidade e
/// pouca consciência), da oportunidade (quem ele conhece e é mais fraco; o ato só acontece sem ninguém acordado olhando) e da
/// impunidade percebida (dissuasão). Consequências: trauma e medo duradouros, gravidez possível em vítima adulta fértil (regras
/// de concepção), revelação a parentes (vingança), denúncia rara, julgamento e má fama do autor quando se sabe.</summary>
internal static class SexualViolence
{
    /// <summary>Insensibilidade (pessoa comum = 1): pouca amabilidade e pouca consciência.</summary>
    private static double Callousness(Personality p) => (1.5 - p.Agreeableness / 100.0) * (1.5 - p.Conscientiousness / 100.0);

    public static double CeilingPerYear(SexualViolenceRules rules, double maxBodyPow) => rules.PerYear * maxBodyPow * 1.5 * 1.5 * 2.25;

    public static double ChancePerYear(WorldState world, SexualViolenceRules rules, ViolenceRules violence, Npc npc, long now, double deterrence) =>
        rules.PerYear * Violence.Propensity(world, violence, npc, now) * Callousness(npc.Personality) * Math.Max(0, 1 - deterrence);

    /// <summary>A oportunidade: entre conhecidos e moradores da casa na mesma cidade (na idade mínima), sorteio pesado pela fraqueza
    /// relativa (força do autor contra a da vítima) e pelo sexo (o do mesmo sexo pesa pouco). Nulo sem ninguém.</summary>
    public static Npc? Target(WorldState world, SexualViolenceRules rules, Npc offender, WorldRng rng)
    {
        var pool = new SortedDictionary<long, Npc>();
        void Consider(Npc? other)
        {
            if (other is not { IsAlive: true } || other.Id == offender.Id || other.City != offender.City) return;
            if (other.AgeYears(world.CurrentDate) < rules.MinVictimAgeYears) return;
            if (world.Crime!.CaptivityOf(other.Id.Value) is not null) return;
            pool[other.Id.Value] = other;
        }
        foreach (var id in world.RelationshipTargetsOf(offender.Id)) Consider(world.FindNpc(id));
        if (offender.Household is { } h && world.FindHousehold(h) is { } home)
            foreach (var id in home.Members) Consider(world.FindNpc(id));
        if (pool.Count == 0) return null;
        double total = 0;
        var weights = new List<(Npc Npc, double W)>(pool.Count);
        foreach (var other in pool.Values)
        {
            double weakness = Math.Clamp((offender.MuscleMass + 5) / Math.Max(5.0, other.MuscleMass + 5), 0.4, 2.5);
            double w = weakness * (other.Sex == offender.Sex ? rules.SameSexWeight : 1.0);
            if (w <= 0) continue;
            total += w;
            weights.Add((other, w));
        }
        double pick = rng.NextDouble() * total;
        foreach (var (npc, w) in weights)
        {
            if (pick < w) return npc;
            pick -= w;
        }
        return weights.Count > 0 ? weights[^1].Npc : null;
    }

    /// <summary>Sem oportunidade, não acontece: alguém acordado no lugar (fora os dois) faz o autor desistir.</summary>
    public static bool Watched(WorldState world, Npc offender, Npc victim)
    {
        foreach (var other in world.AliveNpcIndex.At(victim.CurrentLocation))
            if (other.Id != offender.Id && other.Id != victim.Id && other.CurrentAction != ActionType.Sleep) return true;
        return false;
    }

    /// <summary>O fato e as consequências. Nada aqui descreve o ato.</summary>
    public static void Commit(WorldState world, TickContext ctx, Npc offender, Npc victim, WorldRng rng)
    {
        var crime = world.CrimeRules!;
        var rules = crime.SexualViolence!;
        long now = ctx.CurrentTick;
        bool child = world.LifeStageRules.LifeStageOf(victim.AgeYears(world.CurrentDate)) == LifeStage.Child;
        string category = child ? "crianca" : "adulto";
        bool conceived = !child && NatalitySystem.TryConceiveByAssault(world, ctx, victim, offender, rules.SingleActFecundability, rng);
        var outcome = Justice.OnCrime(world, ctx, CrimeKind.SexualAssault, offender, victim, victim.Id.Value, victim.CurrentLocation,
            0, category, "temperamento", caught: false, rng, victimKnows: true);
        ctx.LogEvent(WorldEventKind.SexualAssault,
            $"{offender.Id.Value}|{victim.Id.Value}|{category}|{outcome.Seers}|{outcome.CaseId}|{(conceived ? 1 : 0)}", Theft.SourceName);

        var tie = world.GetOrCreateRelationship(new RelationshipKey(victim.Id, offender.Id), now);
        tie.ApplyEvent(RelationshipEventType.Betrayal, world.FamilyRules);
        tie.ApplyEvent(RelationshipEventType.Betrayal, world.FamilyRules);
        tie.MarkContact(now);
        Violence.AddGrudge(world, victim.Id.Value, offender.Id.Value, "violencia_sexual", crime.Violence.AssaultGrudge, now);
        bool psyche = world.NeedsRules.Psyche.Enabled;
        if (psyche) PsycheMechanic.Experience(world, victim, LifeEventKind.SexualAssault, offender.Id.Value, now);

        // a vítima conta (ou não) a cada parente próximo: quem sabe sente, quer vingança e pode denunciar por ela
        var known = outcome.CaseId >= 0 ? world.Crime!.Cases.Find(c => c.Id == outcome.CaseId) : null;
        foreach (var kin in Violence.KinOf(world, victim))
        {
            if (kin.Id == offender.Id || rng.NextDouble() >= rules.DisclosureChance) continue;
            Violence.AddGrudge(world, kin.Id.Value, offender.Id.Value, "vinganca", rules.KinGrudge, now);
            if (psyche) PsycheMechanic.Experience(world, kin, LifeEventKind.KinAssaulted, offender.Id.Value, now);
            if (known is not null && !known.Knowers.Exists(k => k.Npc == kin.Id.Value))
                known.Knowers.Add(new CaseKnower { Npc = kin.Id.Value, Accused = offender.Id.Value, Value = 0 });
        }
    }
}
