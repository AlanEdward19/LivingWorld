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

/// <summary>Violência doméstica (AD-154): quem bate em quem mora junto. A chance sai da propensão da violência (corpo,
/// impulsividade, instabilidade), do estresse da casa (fome, miséria, desemprego), do ciúme (mágoa conjugal) e do histórico
/// (lembrança de violência sofrida, inclusive na infância); o medo da punição corta. O alvo é quem a pessoa menos estima em
/// casa. Fere (ou mata), deixa medo e mágoa, pesa na separação (mágoa conjugal) e marca as crianças da casa.</summary>
internal static class DomesticViolence
{
    /// <summary>Teto da chance por ano (temperamento e estresse no máximo): só quem o sorteio do dia cai abaixo dele faz a conta.</summary>
    public static double CeilingPerYear(DomesticViolenceRules rules, double maxBodyPow) =>
        rules.PerYear * maxBodyPow * 1.5 * 1.5 * (1 + rules.HungerStress + rules.PovertyStress + rules.UnemployedStress)
        * (1 + rules.JealousyGain) * (1 + rules.HistoryGain);

    /// <summary>Chance por ano de agredir alguém da casa e o motivo que mais pesa (temperamento, fome, miseria, desemprego, ciume).</summary>
    public static double ChancePerYear(WorldState world, DomesticViolenceRules rules, ViolenceRules violence, Npc npc, long now,
        double deterrence, out string motive)
    {
        string cause = "temperamento";
        double top = 0, stress = 1;
        void Weigh(bool on, double weight, string name)
        {
            if (!on || weight <= 0) return;
            stress += weight;
            if (weight > top) { top = weight; cause = name; }
        }
        Weigh(CrimeSystem.FamilyHunger(world, npc, now) >= 0, rules.HungerStress, "fome");
        Weigh(CrimeSystem.IsPoor(world, npc), rules.PovertyStress, "miseria");
        Weigh(npc.Employer is null && world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) == LifeStage.Adult,
            rules.UnemployedStress, "desemprego");
        motive = cause;
        double jealousy = rules.JealousyGain * Math.Clamp((npc.Union?.Grievance ?? 0) / 100.0, 0, 1);
        if (npc.Spouse is null) jealousy = 0;
        if (jealousy > top) motive = "ciume";
        double history = world.NeedsRules.Psyche.Enabled
            ? rules.HistoryGain * PsycheMechanic.Strongest(world, npc, LifeEventKind.Violence, now) / 100.0 : 0;
        return rules.PerYear * Violence.Propensity(world, violence, npc, now) * stress * (1 + jealousy) * (1 + history)
            * Math.Max(0, 1 - deterrence);
    }

    /// <summary>Quem apanha: o morador (na idade mínima) que a pessoa menos estima; no ciúme, o cônjuge. Nulo se mora só.</summary>
    public static Npc? Target(WorldState world, DomesticViolenceRules rules, Npc npc, string motive)
    {
        if (motive == "ciume" && npc.Spouse is { } s && world.FindNpc(s) is { IsAlive: true } spouse && spouse.Household == npc.Household)
            return spouse;
        if (npc.Household is not { } id || world.FindHousehold(id) is not { } home) return null;
        Npc? target = null;
        double lowest = double.MaxValue;
        foreach (var memberId in home.Members.OrderBy(m => m.Value))
        {
            if (memberId == npc.Id || world.FindNpc(memberId) is not { IsAlive: true } member) continue;
            if (member.AgeYears(world.CurrentDate) < rules.MinVictimAgeYears) continue;
            double affection = world.Relationships.TryGetValue(new RelationshipKey(npc.Id, member.Id), out var bond) ? bond.Affection : 50;
            if (affection < lowest) { lowest = affection; target = member; }
        }
        return target;
    }

    /// <summary>Quem é a vítima para quem bate: cônjuge, filho, pai (o velho da casa) ou outro morador.</summary>
    public static string Bond(Npc aggressor, Npc victim) =>
        aggressor.Spouse == victim.Id || victim.Spouse == aggressor.Id ? "conjuge"
        : victim.MotherId == aggressor.Id || victim.FatherId == aggressor.Id ? "filho"
        : aggressor.MotherId == victim.Id || aggressor.FatherId == victim.Id ? "pai"
        : "outro";

    /// <summary>A agressão: dano menor que o da rua (pela força relativa), golpe mortal raro; a vítima sabe quem foi (caso com ela
    /// como conhecedora; denúncia rara pela norma); medo, mágoa, mágoa conjugal (separação) e a marca nas crianças da casa.</summary>
    public static void Assault(WorldState world, TickContext ctx, Npc aggressor, Npc victim, string motive, WorldRng rng)
    {
        if (!aggressor.IsAlive || !victim.IsAlive || aggressor.Id == victim.Id) return;
        var crime = world.CrimeRules!;
        var rules = crime.Domestic!;
        var violence = crime.Violence;
        long now = ctx.CurrentTick;
        double strength = Math.Clamp((aggressor.MuscleMass + 5) / Math.Max(5.0, victim.MuscleMass + 5), 0.4, 2.5);
        int harm = Math.Max(1, (int)Math.Round(violence.HarmMean * rules.HarmScale * strength * (1 + violence.HarmSpread * (2 * rng.NextDouble() - 1))));
        // mesmo piso da briga: ferido vivo nunca fica com saúde ≤ 1 (a percepção trata como perigo e a casa fugiria dele)
        bool lethal = rng.NextDouble() < rules.LethalChance * strength || victim.Health - harm <= AliveNpcIndex.EndangeredHealth;
        string bond = Bond(aggressor, victim);
        var outcome = Justice.OnCrime(world, ctx, lethal ? CrimeKind.Homicide : CrimeKind.DomesticAssault, aggressor, victim,
            victim.Id.Value, victim.CurrentLocation, harm, bond, motive, caught: false, rng, victimKnows: true);
        ctx.LogEvent(WorldEventKind.DomesticViolence,
            $"{aggressor.Id.Value}|{victim.Id.Value}|{bond}|{motive}|{harm}|{(lethal ? "morto" : "ferido")}|{outcome.Seers}|{outcome.CaseId}",
            Theft.SourceName);

        var children = ChildrenAtHome(world, aggressor, victim);
        if (lethal)
        {
            NpcDeath.Apply(world, ctx, victim, WorldEventKind.Homicide);
            foreach (var kin in Violence.KinOf(world, victim))
            {
                if (kin.Id == aggressor.Id) continue;
                Violence.AddGrudge(world, kin.Id.Value, aggressor.Id.Value, "vinganca", violence.KinKilledGrudge, now);
                if (world.NeedsRules.Psyche.Enabled) PsycheMechanic.Experience(world, kin, LifeEventKind.KinMurdered, aggressor.Id.Value, now);
            }
            return;
        }

        victim.SetHealth(victim.Health - harm);
        var tie = world.GetOrCreateRelationship(new RelationshipKey(victim.Id, aggressor.Id), now);
        tie.ApplyEvent(RelationshipEventType.Betrayal, world.FamilyRules);
        tie.MarkContact(now);
        // AD-162: só a agressão grave ou repetida pesa inteira na mágoa conjugal (a que leva à separação)
        bool repeated = world.Crime!.GrudgeOf(victim.Id.Value, aggressor.Id.Value) is { } before
            && Violence.Intensity(violence, before, now, world.Calendar.HoursPerDay) >= rules.RepeatGrudgeMin;
        Violence.AddGrudge(world, victim.Id.Value, aggressor.Id.Value, "agressao", violence.AssaultGrudge / 2, now);
        if (bond == "conjuge" && victim.Union is { } union)
        {
            double grievance = rules.SpouseGrievance * (harm >= rules.SevereHarm || repeated ? 1.0 : rules.ToleratedGrievanceShare);
            victim.SetUnion(union with { Grievance = Math.Min(100, union.Grievance + grievance) });
        }
        if (!world.NeedsRules.Psyche.Enabled) return;
        PsycheMechanic.Experience(world, victim, LifeEventKind.Violence, aggressor.Id.Value, now);
        foreach (var child in children)
            if (child.Id != victim.Id)
                PsycheMechanic.Experience(world, child, LifeEventKind.Violence, aggressor.Id.Value, now, rules.WitnessChildImpact);
    }

    /// <summary>Crianças da casa de quem bate (as que crescem vendo), em ordem de id.</summary>
    private static List<Npc> ChildrenAtHome(WorldState world, Npc aggressor, Npc victim)
    {
        var list = new List<Npc>();
        if (aggressor.Household is not { } id || world.FindHousehold(id) is not { } home) return list;
        foreach (var memberId in home.Members.OrderBy(m => m.Value))
            if (memberId != aggressor.Id && world.FindNpc(memberId) is { IsAlive: true } member
                && world.LifeStageRules.LifeStageOf(member.AgeYears(world.CurrentDate)) == LifeStage.Child)
                list.Add(member);
        return list;
    }
}
