using System.Globalization;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Crime;

/// <summary>Crime e justiça (AD-150), <c>Daily</c>: uma passada pelos vivos decide quem planeja furtar hoje (miséria ou
/// oportunidade, pelo temperamento, pelo corpo e pelo medo da punição) e quem arranja briga (ofensa, com a raiva do momento);
/// as mágoas guardadas podem virar agressão; o dia da justiça corre (boato, denúncia, corte). O ato acontece numa hora sorteada
/// do dia (evento agendado): quem estiver no lugar naquela hora testemunha. O furto por fome é decidido na hora, pela ação
/// Furtar (<c>TheftResolver</c>).</summary>
[LivingCapability("CRIME", Events = [WorldEventKind.TheftCommitted, WorldEventKind.Assault, WorldEventKind.Homicide,
    WorldEventKind.CrimeReported, WorldEventKind.Verdict, WorldEventKind.Executed, WorldEventKind.Banished,
    WorldEventKind.InheritanceDisputed, WorldEventKind.DomesticViolence, WorldEventKind.Kidnapped, WorldEventKind.KidnappingEnded,
    WorldEventKind.SexualAssault], Consumers = ["timeline.crime"])]
public sealed class CrimeSystem : ISimulationSystem
{
    public const string SystemName = "crime";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        var theft = world.TheftRules;
        bool crime = world.CrimeOn;
        if (!theft.Enabled && !crime) return;
        long now = ctx.CurrentTick;
        int hpd = world.Calendar.HoursPerDay;
        long day = now / hpd;
        double daysPerYear = world.Calendar.HoursPerYear / (double)hpd;
        var violence = crime ? world.CrimeRules!.Violence : null;
        bool fights = violence is { Enabled: true };
        var rng = ctx.OneShot($"crime-day-{day}");
        var guards = GuardsByCity(world);

        // um sorteio por pessoa e motivo; a conta (temperamento, corpo, medo) só para quem o sorteio cai abaixo do teto da
        // chance — o mesmo resultado de calcular sempre, sem o custo diário de ler o corpo de todo mundo
        double body = MaxBody(world);
        double theftCeiling = theft.OpportunityPerYear * Math.Max(1.0, theft.PovertyFactor) * MaxInclination(theft) * Pow(body, theft.SexAgeExponent) / daysPerYear;
        double quarrelCeiling = fights
            ? violence!.QuarrelPerYear * Pow(body, violence.SexAgeExponent) * 1.5 * 1.5 * (1 + violence.AngerGain) / daysPerYear : 0;
        // AD-154: crimes contra a pessoa, cada um com chave própria e stream próprio (desligados, o resto do dia não muda)
        var person = crime ? world.CrimeRules! : null;
        var domestic = person is { DomesticOn: true } ? person.Domestic : null;
        var kidnap = person is { KidnappingOn: true } ? person.Kidnapping : null;
        var sexual = person is { SexualViolenceOn: true } ? person.SexualViolence : null;
        var prng = domestic is not null || kidnap is not null || sexual is not null ? ctx.OneShot($"crime-person-day-{day}") : null;
        double bodyPow = person is null ? 1 : Pow(body, person.Violence.SexAgeExponent);
        double domesticCeiling = domestic is null ? 0 : DomesticViolence.CeilingPerYear(domestic, bodyPow) / daysPerYear;
        double kidnapCeiling = kidnap is null ? 0 : Kidnapping.CeilingPerYear(kidnap, bodyPow) / daysPerYear;
        double sexualCeiling = sexual is null ? 0 : SexualViolence.CeilingPerYear(sexual, bodyPow) / daysPerYear;
        int personMinAge = Math.Min(domestic?.MinAgeYears ?? int.MaxValue, Math.Min(kidnap?.MinAgeYears ?? int.MaxValue, sexual?.MinAgeYears ?? int.MaxValue));
        bool anyHeld = world.Crime?.Captives is not null;

        var alive = world.AliveNpcIndex.Alive;
        for (int i = 0; i < alive.Count; i++)
        {
            var npc = alive[i];
            if (anyHeld && world.Crime!.CaptivityOf(npc.Id.Value) is not null) continue; // preso não planeja nada
            int age = npc.AgeYears(world.CurrentDate);
            if (prng is not null && age >= personMinAge)
                PlanAgainstPersons(world, ctx, npc, age, domestic, kidnap, sexual, domesticCeiling, kidnapCeiling, sexualCeiling,
                    daysPerYear, guards, prng);
            if (age < Math.Min(theft.MinAgeYears, fights ? violence!.MinAgeYears : int.MaxValue)) continue;
            if (theft.Enabled && age >= theft.MinAgeYears)
            {
                double u = rng.NextDouble();
                double hunger = FamilyNeed(world, npc, now, out int lacking);
                if (hunger >= 0) PlanFoodForFamily(world, ctx, npc, hunger, lacking, Deterrence(world, npc, now, guards), u, rng);
                else if (u < theftCeiling) PlanTheft(world, ctx, npc, Deterrence(world, npc, now, guards), daysPerYear, u, rng);
            }
            if (fights && age >= violence!.MinAgeYears)
            {
                double u = rng.NextDouble();
                if (u < quarrelCeiling) PlanQuarrel(world, ctx, npc, violence, Deterrence(world, npc, now, guards), daysPerYear, u, rng);
            }
        }

        if (fights) GrudgeFights(world, ctx, violence!, daysPerYear, guards, rng);
        if (kidnap is not null || world.Crime?.Captives is not null) Kidnapping.Daily(world, ctx, day);
        if (crime && world.CrimeRules!.Justice.Enabled) Justice.Daily(world, ctx, day);
    }

    /// <summary>AD-154: um sorteio por pessoa e crime contra a pessoa (doméstica, sequestro, sexual); a conta (temperamento,
    /// estresse, motivo, dissuasão) só para quem o sorteio cai abaixo do teto da chance. O ato vai a uma hora do dia.</summary>
    private static void PlanAgainstPersons(WorldState world, TickContext ctx, Npc npc, int age,
        LivingWorld.Domain.Crime.DomesticViolenceRules? domestic, LivingWorld.Domain.Crime.KidnappingRules? kidnap,
        LivingWorld.Domain.Crime.SexualViolenceRules? sexual, double domesticCeiling, double kidnapCeiling, double sexualCeiling,
        double daysPerYear, Dictionary<Guid, int>? guards, WorldRng rng)
    {
        long now = ctx.CurrentTick;
        var violence = world.CrimeRules!.Violence;
        int hpd = world.Calendar.HoursPerDay;
        if (domestic is not null && age >= domestic.MinAgeYears)
        {
            double u = rng.NextDouble();
            if (u < domesticCeiling && npc.Household is not null
                && u < DomesticViolence.ChancePerYear(world, domestic, violence, npc, now, Deterrence(world, npc, now, guards), out string motive) / daysPerYear
                && DomesticViolence.Target(world, domestic, npc, motive) is { } victim)
                ctx.ScheduleEvent(now + hpd * 17 / 24 + (long)(rng.NextDouble() * (hpd * 6 / 24)), SystemName,
                    $"D|{npc.Id.Value}|{victim.Id.Value}|{motive}");
        }
        if (kidnap is not null && age >= kidnap.MinAgeYears)
        {
            double u = rng.NextDouble();
            if (u < kidnapCeiling && npc.Household is not null
                && u < Kidnapping.ChancePerYear(world, kidnap, violence, npc, now, Deterrence(world, npc, now, guards),
                    out string motive, out var victim, out long ransom) / daysPerYear && victim is not null)
                ctx.ScheduleEvent(now + 1 + (long)(rng.NextDouble() * (hpd - 1)), SystemName,
                    $"K|{npc.Id.Value}|{victim.Id.Value}|{motive}|{ransom}");
        }
        if (sexual is not null && age >= sexual.MinAgeYears)
        {
            double u = rng.NextDouble();
            if (u < sexualCeiling
                && u < SexualViolence.ChancePerYear(world, sexual, violence, npc, now, Deterrence(world, npc, now, guards)) / daysPerYear
                && SexualViolence.Target(world, sexual, npc, rng) is { } victim)
                ctx.ScheduleEvent(now + 1 + (long)(rng.NextDouble() * (hpd - 1)), SystemName, $"S|{npc.Id.Value}|{victim.Id.Value}");
        }
    }

    private static bool Held(WorldState world, Npc npc) => world.Crime?.CaptivityOf(npc.Id.Value) is not null;

    /// <summary>Maior agressividade do corpo que o cenário permite (testosterona plena); 1 sem hormônios.</summary>
    private static double MaxBody(WorldState world)
    {
        var r = LivingWorld.Simulation.Population.Body.Endocrine.Rules(world);
        return r.Enabled ? Math.Max(1.0, r.AggressionBase + Math.Max(0, r.AggressionGain)) : 1.0;
    }

    /// <summary>Maior inclinação possível: o peso de Furtar vai a no máximo 3 (todos os traços contra a moral).</summary>
    private static double MaxInclination(LivingWorld.Domain.Crime.TheftRules rules) =>
        Math.Max(1.0, rules.Inclination(3.0, rules.PlannedBarrier(0)));

    private static double Pow(double x, double exponent)
    {
        double pow = 1.0;
        for (int k = 0; k < (int)exponent; k++) pow *= x;
        return pow;
    }

    private static Dictionary<Guid, int>? GuardsByCity(WorldState world)
    {
        int guardId = world.TheftRules.GuardProfessionId;
        if (guardId < 0) return null;
        var guards = new Dictionary<Guid, int>();
        foreach (var npc in world.AliveNpcIndex.Alive)
            if (npc.Profession.Id == guardId) guards[npc.City.Value] = guards.GetValueOrDefault(npc.City.Value) + 1;
        return guards;
    }

    private static double Deterrence(WorldState world, Npc npc, long now, Dictionary<Guid, int>? guards)
    {
        double fromGuards = Math.Min(1.0, (guards?.GetValueOrDefault(npc.City.Value) ?? 0) * world.TheftRules.GuardEffect);
        if (!world.CrimeOn || !world.CrimeRules!.Justice.Enabled) return 0.5 * fromGuards;
        double city = Justice.CityDeterrence(world, npc.City, now);
        double own = PsycheMechanic.Strongest(world, npc, LifeEventKind.Punished, now) / 100.0;
        return world.CrimeRules.Justice.DeterrenceWeight * Math.Max(Math.Max(city, own), fromGuards);
    }

    /// <summary>Furto planejado: só quem o temperamento leva além da barreira moral (mais a dissuasão); a miséria multiplica.</summary>
    private static void PlanTheft(WorldState world, TickContext ctx, Npc npc, double det, double daysPerYear, double u, WorldRng rng)
    {
        double chance = PlannedTheftPerYear(world, npc, det, ctx.CurrentTick, out bool poor) / daysPerYear;
        if (u >= chance) return;
        var motive = poor ? TheftMotive.Poverty : TheftMotive.Opportunity;
        long at = ctx.CurrentTick + 1 + (long)(rng.NextDouble() * (world.Calendar.HoursPerDay - 1));
        ctx.ScheduleEvent(at, SystemName, $"T|{npc.Id.Value}|{(int)motive}");
    }

    /// <summary>Fome ou sede da casa (0..1, o pior de quem mora junto; -1 = não há): falta em casa, sem como comprar e
    /// alguém da casa no limite do desespero. A checagem barata (despensa) vem primeiro.</summary>
    internal static double FamilyHunger(WorldState world, Npc npc, long now) => FamilyNeed(world, npc, now, out _);

    /// <summary>A fome ou sede da casa e o que falta para ela (comida ou água).</summary>
    internal static double FamilyNeed(WorldState world, Npc npc, long now, out int resource)
    {
        resource = -1;
        if (npc.Household is not { } id || world.FindHousehold(id) is not { } home) return -1;
        var economy = world.EconomyRules;
        if (home.Stock.GetValueOrDefault(new ResourceType(economy.FoodResourceId)) >= 1
            && home.Stock.GetValueOrDefault(new ResourceType(economy.WaterResourceId)) >= 1) return -1; // nada falta: sem olhar ninguém
        double worst = -1;
        foreach (var memberId in home.Members)
        {
            if (world.FindNpc(memberId) is not { IsAlive: true } member) continue;
            int missing = Theft.MissingAtHome(world, member, now);
            if (missing < 0) continue;
            double need = Theft.NeedOf(world.TheftRules, member, now);
            if (need > worst) { worst = need; resource = missing; }
        }
        return worst < 0 || Theft.CanBuy(world, npc, resource) ? -1 : worst;
    }

    /// <summary>Quem tem fome em casa (filho, cônjuge ou a si) e nada para dar vai buscar comida onde houver: a chance do dia
    /// sai do temperamento contra a barreira que a fome rebaixa.</summary>
    private static void PlanFoodForFamily(WorldState world, TickContext ctx, Npc npc, double hunger, int resource, double det, double u, WorldRng rng)
    {
        var rules = world.TheftRules;
        double inclination = rules.Inclination(PersonalityWeighting.WeightOf(npc.Personality, ActionType.Steal), rules.NeedBarrier(hunger, det));
        if (u >= rules.FamilyHungerPerDay * Math.Min(1.0, inclination)) return;
        long at = ctx.CurrentTick + 1 + (long)(rng.NextDouble() * (world.Calendar.HoursPerDay - 1));
        ctx.ScheduleEvent(at, SystemName, $"T|{npc.Id.Value}|{(int)TheftMotive.Hunger}|{resource}");
    }

    /// <summary>Chance por ano de planejar um furto: inclinação além da barreira moral e da dissuasão, vezes o corpo (idade e
    /// sexo) e a miséria. Zero para quem o temperamento não leva ao furto (a conta para cedo, sem ler o corpo).</summary>
    internal static double PlannedTheftPerYear(WorldState world, Npc npc, double deterrence, long now, out bool poor)
    {
        var rules = world.TheftRules;
        poor = false;
        double inclination = rules.Inclination(PersonalityWeighting.WeightOf(npc.Personality, ActionType.Steal), rules.PlannedBarrier(deterrence));
        if (inclination <= 0) return 0;
        poor = IsPoor(world, npc);
        double body = LivingWorld.Simulation.Population.Body.Endocrine.AggressionFactor(world, npc, now);
        double pow = 1.0;
        for (int k = 0; k < (int)rules.SexAgeExponent; k++) pow *= body;
        return rules.OpportunityPerYear * inclination * pow * (poor ? rules.PovertyFactor : 1.0);
    }

    /// <summary>Medo da punição de quem pensa em furtar ou brigar (ver <see cref="Deterrence(WorldState, Npc, long, Dictionary{Guid, int}?)"/>).</summary>
    internal static double DeterrenceOf(WorldState world, Npc npc, long now) => Deterrence(world, npc, now, GuardsByCity(world));

    /// <summary>Pobre: pouca moeda e despensa curta para a casa.</summary>
    internal static bool IsPoor(WorldState world, Npc npc)
    {
        var rules = world.TheftRules;
        if (npc.Wallet.Amount >= rules.PoorBelowCoin) return false;
        if (npc.Household is not { } id || world.FindHousehold(id) is not { } home) return true;
        long food = home.Stock.GetValueOrDefault(new ResourceType(world.EconomyRules.FoodResourceId));
        return food < rules.PoorPantryDays * Math.Max(1, home.Members.Count);
    }

    /// <summary>Briga por ofensa: a propensão e a raiva do momento; o alvo é o conhecido de quem menos gosta (fora de casa).</summary>
    private static void PlanQuarrel(WorldState world, TickContext ctx, Npc npc, LivingWorld.Domain.Crime.ViolenceRules rules,
        double det, double daysPerYear, double u, WorldRng rng)
    {
        long now = ctx.CurrentTick;
        double anger = PsycheMechanic.EmotionOf(world, npc, Emotion.Anger, now);
        double chance = rules.QuarrelPerYear * Violence.Propensity(world, rules, npc, now) * (1 + rules.AngerGain * anger)
            * Math.Max(0, 1 - det) / daysPerYear;
        if (u >= chance) return;
        var targets = world.RelationshipTargetsOf(npc.Id);
        var bonds = world.RelationshipsFrom(npc.Id);
        if (bonds is null) return;
        Npc? target = null;
        double lowest = double.MaxValue;
        for (int i = 0; i < targets.Count && i < bonds.Count; i++)
        {
            if (world.FindNpc(targets[i]) is not { IsAlive: true } other || other.City != npc.City || Violence.Domestic(npc, other)) continue;
            if (other.AgeYears(world.CurrentDate) < rules.MinAgeYears) continue;
            if (bonds[i].Affection < lowest) { lowest = bonds[i].Affection; target = other; }
        }
        if (target is null) return;
        ScheduleFight(world, ctx, npc, target, "ofensa", rng);
    }

    private static void ScheduleFight(WorldState world, TickContext ctx, Npc aggressor, Npc victim, string motive, WorldRng rng)
    {
        // briga é de fim de tarde e de noite (convívio, volta do trabalho): entre as 17h e as 22h
        int hpd = world.Calendar.HoursPerDay;
        long at = ctx.CurrentTick + hpd * 17 / 24 + (long)(rng.NextDouble() * (hpd * 5 / 24));
        ctx.ScheduleEvent(at, SystemName, $"F|{aggressor.Id.Value}|{victim.Id.Value}|{motive}");
    }

    /// <summary>Mágoa que vira agressão (vingança, ciúme, herança, denúncia), pela intensidade que resta e pela propensão.</summary>
    private static void GrudgeFights(WorldState world, TickContext ctx, LivingWorld.Domain.Crime.ViolenceRules rules,
        double daysPerYear, Dictionary<Guid, int>? guards, WorldRng rng)
    {
        long now = ctx.CurrentTick;
        int hpd = world.Calendar.HoursPerDay;
        var grudges = world.Crime!.Grudges;
        grudges.RemoveAll(g => Violence.Intensity(rules, g, now, hpd) < rules.GrudgeFloor
            || world.FindNpc(new NpcId(g.Holder)) is not { IsAlive: true } || world.FindNpc(new NpcId(g.Target)) is not { IsAlive: true });
        foreach (var grudge in grudges.ToList())
        {
            var holder = world.FindNpc(new NpcId(grudge.Holder))!;
            var target = world.FindNpc(new NpcId(grudge.Target))!;
            if (holder.City != target.City || Violence.Domestic(holder, target) || holder.AgeYears(world.CurrentDate) < rules.MinAgeYears) continue;
            double chance = rules.GrudgeFightPerYear * Violence.Intensity(rules, grudge, now, hpd) / 100.0
                * Violence.Propensity(world, rules, holder, now) * Math.Max(0, 1 - Deterrence(world, holder, now, guards)) / daysPerYear;
            if (rng.NextDouble() < chance) ScheduleFight(world, ctx, holder, target, grudge.Motive, rng);
        }
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        var parts = (evt.Payload ?? "").Split('|');
        if (parts.Length < 3) return;
        var rng = ctx.OneShot($"crime-act-{evt.Id}");
        if (parts[0] == "T" && world.TheftRules.Enabled)
        {
            if (Find(world, parts[1]) is not { IsAlive: true } thief) return;
            var motive = (TheftMotive)int.Parse(parts[2], CultureInfo.InvariantCulture);
            int resource = parts.Length > 3 && int.TryParse(parts[3], CultureInfo.InvariantCulture, out int r) ? r : -1;
            if (Theft.FindTarget(world, thief, motive, rng: rng, resourceId: resource) is { } target && (motive == TheftMotive.Hunger || !Watched(world, thief, target, rng)))
                Theft.Commit(world, ctx, thief, motive, target, rng);
        }
        else if (parts[0] == "F" && parts.Length >= 4 && world.CrimeOn && world.CrimeRules!.Violence.Enabled)
        {
            if (Find(world, parts[1]) is not { IsAlive: true } aggressor || Find(world, parts[2]) is not { IsAlive: true } victim) return;
            if (aggressor.City != victim.City || Violence.Domestic(aggressor, victim) || Held(world, aggressor) || Held(world, victim)) return;
            Violence.Fight(world, ctx, aggressor, victim, parts[3], rng);
        }
        else if (world.CrimeOn)
            HandlePersonCrime(world, ctx, parts, rng);
    }

    /// <summary>AD-154: o ato agendado de um crime contra a pessoa, se ainda cabe (os dois vivos, ninguém preso, a mesma casa na
    /// doméstica, sem ninguém olhando na violência sexual).</summary>
    private static void HandlePersonCrime(WorldState world, TickContext ctx, string[] parts, WorldRng rng)
    {
        var rules = world.CrimeRules!;
        if (Find(world, parts[1]) is not { IsAlive: true } offender || Find(world, parts[2]) is not { IsAlive: true } victim) return;
        if (Held(world, offender) || Held(world, victim)) return;
        switch (parts[0])
        {
            case "D" when rules.DomesticOn && Violence.Domestic(offender, victim) && offender.Household == victim.Household:
                DomesticViolence.Assault(world, ctx, offender, victim, parts[3], rng);
                return;
            case "K" when rules.KidnappingOn && parts.Length >= 5:
                Kidnapping.Abduct(world, ctx, offender, victim, parts[3], long.Parse(parts[4], CultureInfo.InvariantCulture), rng);
                return;
            case "S" when rules.SexualViolenceOn && offender.City == victim.City && !SexualViolence.Watched(world, offender, victim):
                SexualViolence.Commit(world, ctx, offender, victim, rng);
                return;
        }
    }

    /// <summary>Quem planejou o furto (fora a fome, que não espera) desiste se há gente acordada olhando: cada uma pesa pela aversão a risco do
    /// ladrão (o ousado arrisca, o cauteloso espera outra vez). A vítima do batedor de carteira não conta.</summary>
    private static bool Watched(WorldState world, Npc thief, TheftTarget target, WorldRng rng)
    {
        double caution = thief.Personality.RiskAversion / 100.0;
        foreach (var other in world.AliveNpcIndex.At(target.Where).OrderBy(n => n.Id.Value))
        {
            if (other.Id == thief.Id || other.CurrentAction == ActionType.Sleep) continue;
            if (target.Kind == TheftTargetKind.Person && other.Id == target.Person!.Id) continue;
            if (rng.NextDouble() < 0.5 + 0.5 * caution) return true;
        }
        return false;
    }

    private static Npc? Find(WorldState world, string id) =>
        long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? world.FindNpc(new NpcId(value)) : null;

    /// <summary>Consumidor do log (chamado por <c>TickContext.LogEvent</c>): o traído guarda ciúme do amante do cônjuge.</summary>
    public static void OnEvent(WorldState world, WorldEventKind kind, string payload, long tick)
    {
        if (kind != WorldEventKind.AffairDiscovered || !world.CrimeRules!.Violence.Enabled) return;
        var parts = payload.Split('|');
        if (parts.Length < 3 || !long.TryParse(parts[0], CultureInfo.InvariantCulture, out long betrayed)
            || !long.TryParse(parts[2], CultureInfo.InvariantCulture, out long lover)) return;
        Violence.AddGrudge(world, betrayed, lover, "ciume", world.CrimeRules.Violence.JealousyGrudge, tick);
    }
}
