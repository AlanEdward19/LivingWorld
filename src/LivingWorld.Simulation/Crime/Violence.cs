using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Crime;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Crime;

/// <summary>Violência entre pessoas (AD-150): propensão, mágoa e briga. A briga fere (a saúde dinâmica do AD-134 cuida da
/// recuperação e do risco de morte depois), às vezes mata na hora, deixa mágoa em quem apanhou e chama a vingança dos
/// parentes. Violência doméstica fica fora (fatia seguinte): ninguém briga com quem mora na mesma casa nem com o cônjuge.</summary>
internal static class Violence
{
    /// <summary>Propensão à violência (pessoa comum ≈ 1): agressividade do corpo (testosterona por idade e sexo, AD-139) na
    /// potência do cenário, vezes impulsividade e instabilidade emocional.</summary>
    public static double Propensity(WorldState world, ViolenceRules rules, Npc npc, long now)
    {
        double body = LivingWorld.Simulation.Population.Body.Endocrine.AggressionFactor(world, npc, now);
        double pow = 1.0;
        for (int i = 0; i < (int)rules.SexAgeExponent; i++) pow *= body;
        var p = npc.Personality;
        return pow * (0.5 + p.Impulsivity / 100.0) * (1.5 - p.EmotionalStability / 100.0);
    }

    /// <summary>Duas pessoas que não brigam entre si nesta fatia: o casal e quem mora junto (doméstica fica para a próxima).</summary>
    public static bool Domestic(Npc a, Npc b) =>
        a.Spouse == b.Id || b.Spouse == a.Id || (a.Household is { } home && b.Household == home);

    public static int Intensity(ViolenceRules rules, Grudge grudge, long now, int hoursPerDay)
    {
        double days = Math.Max(0, now - grudge.Tick) / (double)hoursPerDay;
        return (int)(grudge.Intensity * DeterministicMath.Exp(-0.6931471805599453 * days / rules.GrudgeHalfLifeDays));
    }

    /// <summary>Soma mágoa (a mais forte vence; reforço soma até 100). Teto de mágoas: a mais fraca sai.</summary>
    public static void AddGrudge(WorldState world, long holder, long target, string motive, int intensity, long now)
    {
        if (!world.CrimeOn || holder == target || holder < 0 || target < 0) return;
        var rules = world.CrimeRules!.Violence;
        var ledger = world.Crime!;
        int hpd = world.Calendar.HoursPerDay;
        if (ledger.GrudgeOf(holder, target) is { } had)
        {
            int current = Intensity(rules, had, now, hpd);
            had.Intensity = Math.Min(100, Math.Max(current, intensity) + Math.Min(current, intensity) / 2);
            had.Tick = now;
            if (intensity >= current) had.Motive = motive;
            return;
        }
        ledger.InsertGrudge(new Grudge { Holder = holder, Target = target, Motive = motive, Intensity = Math.Clamp(intensity, 0, 100), Tick = now });
        if (ledger.Grudges.Count <= rules.MaxGrudges) return;
        int weakest = 0, weakestValue = int.MaxValue;
        for (int i = 0; i < ledger.Grudges.Count; i++)
        {
            int v = Intensity(rules, ledger.Grudges[i], now, hpd);
            if (v < weakestValue) { weakest = i; weakestValue = v; }
        }
        ledger.Grudges.RemoveAt(weakest);
    }

    /// <summary>Uma agressão: dano pela força relativa (músculo), golpe mortal raro, ferimento ou morte, mágoa em quem apanhou,
    /// vingança dos parentes e o caso para a justiça.</summary>
    public static void Fight(WorldState world, TickContext ctx, Npc aggressor, Npc victim, string motive, WorldRng rng)
    {
        if (!aggressor.IsAlive || !victim.IsAlive || aggressor.Id == victim.Id) return;
        var rules = world.CrimeRules!.Violence;
        long now = ctx.CurrentTick;
        double strength = Math.Clamp((aggressor.MuscleMass + 5) / Math.Max(5.0, victim.MuscleMass + 5), 0.4, 2.5);
        int harm = Math.Max(1, (int)Math.Round(rules.HarmMean * strength * (1 + rules.HarmSpread * (2 * rng.NextDouble() - 1))));
        bool lethal = rng.NextDouble() < rules.LethalChance * strength || victim.Health - harm <= 0;
        var where = victim.CurrentLocation;
        var outcome = Justice.OnCrime(world, ctx, lethal ? CrimeKind.Homicide : CrimeKind.Assault, aggressor, victim,
            victim.Id.Value, where, harm, "corpo", motive, caught: false, rng);
        ctx.LogEvent(WorldEventKind.Assault,
            $"{aggressor.Id.Value}|{victim.Id.Value}|{motive}|{harm}|{(lethal ? "morto" : "ferido")}|{outcome.Seers}|{outcome.CaseId}",
            Theft.SourceName);

        // quem bateu desabafou: a mágoa dele cai à metade
        if (world.Crime!.GrudgeOf(aggressor.Id.Value, victim.Id.Value) is { } vented)
        {
            vented.Intensity = Intensity(rules, vented, now, world.Calendar.HoursPerDay) / 2;
            vented.Tick = now;
        }

        if (lethal)
        {
            NpcDeath.Apply(world, ctx, victim, WorldEventKind.Homicide);
            foreach (var kin in KinOf(world, victim))
            {
                AddGrudge(world, kin.Id.Value, aggressor.Id.Value, "vinganca", rules.KinKilledGrudge, now);
                if (world.NeedsRules.Psyche.Enabled) PsycheMechanic.Experience(world, kin, LifeEventKind.KinMurdered, aggressor.Id.Value, now);
            }
            return;
        }

        victim.SetHealth(victim.Health - harm);
        var bond = world.GetOrCreateRelationship(new RelationshipKey(victim.Id, aggressor.Id), now);
        bond.ApplyEvent(RelationshipEventType.Betrayal, world.FamilyRules);
        bond.MarkContact(now);
        if (world.NeedsRules.Psyche.Enabled) PsycheMechanic.Experience(world, victim, LifeEventKind.Violence, aggressor.Id.Value, now);
        AddGrudge(world, victim.Id.Value, aggressor.Id.Value, "agressao", rules.AssaultGrudge, now);
        if (harm >= 2 * rules.HarmMean)
            foreach (var kin in KinOf(world, victim))
                AddGrudge(world, kin.Id.Value, aggressor.Id.Value, "vinganca", rules.KinWoundedGrudge, now);
    }

    /// <summary>Parentes vivos próximos (cônjuge, pais, filhos, irmãos), em ordem de id. Uma passada pelos vivos: só em briga.</summary>
    public static List<Npc> KinOf(WorldState world, Npc npc)
    {
        var kin = new SortedDictionary<long, Npc>();
        void Add(Npc? who) { if (who is { IsAlive: true } && who.Id != npc.Id) kin[who.Id.Value] = who; }
        if (npc.Spouse is { } s) Add(world.FindNpc(s));
        if (npc.MotherId is { } m) Add(world.FindNpc(m));
        if (npc.FatherId is { } f) Add(world.FindNpc(f));
        foreach (var other in world.AliveNpcIndex.Alive)
            if (other.MotherId == npc.Id || other.FatherId == npc.Id
                || (npc.MotherId is { } om && other.MotherId == om) || (npc.FatherId is { } of && other.FatherId == of))
                Add(other);
        return [.. kin.Values];
    }
}
