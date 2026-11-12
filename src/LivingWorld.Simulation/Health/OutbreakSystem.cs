using LivingWorld.Domain.Health;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Health;

/// <summary>Doença de época (disease-epidemics, AD-137), <c>Daily</c>. Cada infecção passa por incubação, doença (de cama ou
/// andando, com quem cuida) e desfecho decidido no sintoma: cura com imunidade (e às vezes sequela) ou morte agendada numa hora da
/// doença. O contágio sai só de quem está contagioso, pela convivência real: a casa, a turma de trabalho (se ainda trabalha) e os
/// encontros na vila (mercado, conversa; visita, se está de cama), sem varrer pares. Casos novos vêm também do ambiente (água,
/// frio), por idade e mês, e de fora (bexigas, sarampo, peste), abrindo um surto que entra na história. Custo por doente e por
/// caso, nunca por NPC; tudo em ordem de id e com streams nomeados.</summary>
[LivingCapability("OUTBREAK", Events = [WorldEventKind.OutbreakStarted, WorldEventKind.OutbreakEnded], Consumers = ["timeline.outbreak"])]
[RunsAfter(typeof(MortalitySystem))]
public sealed class OutbreakSystem : ISimulationSystem
{
    public const string SystemName = "health-outbreak";
    public const string DeathPayloadPrefix = "disease-death|";

    private readonly List<Infection> _work = [];
    private readonly List<Infection> _caught = [];
    private readonly List<Infection> _ended = [];

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.DiseaseRules;
        if (!rules.Enabled || rules.Diseases.Count == 0) return;

        long now = ctx.CurrentTick;
        long day = now / world.Calendar.HoursPerDay;
        int month = world.CurrentDate.Month;

        world.PurgeExpiredImmunities();
        if (world.Infections.Count > 0)
        {
            ProcessOnsets(world, ctx, rules, now);
            ResolveRecoveries(world, ctx, rules, now);
            Spread(world, ctx, rules, now, day, month);
        }

        Environmental(world, ctx, rules, now, day, month);
        Import(world, ctx, rules, now, day, month);
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        if (evt.Payload is not { } payload || !payload.StartsWith(DeathPayloadPrefix, StringComparison.Ordinal)) return;
        var parts = payload[DeathPayloadPrefix.Length..].Split('|');
        var id = new NpcId(long.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture));
        int disease = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
        int index = world.FirstInfectionIndex(id);
        Infection? infection = null;
        for (int i = index; i >= 0 && i < world.Infections.Count && world.Infections[i].Npc == id; i++)
            if (world.Infections[i].Disease == disease) infection = world.Infections[i];
        if (infection is not { DeathTick: { } due } || due != ctx.CurrentTick) return; // já sarou de outro jeito ou morreu antes
        if (world.FindNpc(id) is not { IsAlive: true } npc) return;

        world.RemoveInfection(id, disease);
        ctx.LogEvent(WorldEventKind.Disease, $"{id.Value}|{disease}", sourceSystem: SystemName);
        NpcDeath.Apply(world, ctx, npc, WorldEventKind.Disease);
        _ended.Clear();
        _ended.Add(infection);
        CloseFinishedOutbreaks(world, ctx);
    }

    /// <summary>Quanto a doença em curso tira do teto de saúde do NPC (lido pela fisiologia diária): o caso de cama tira tudo, o
    /// leve uma fração; quem ainda incuba não sente nada.</summary>
    public static double HealthPenalty(WorldState world, Npc npc, double ageYears)
    {
        _ = ageYears;
        if (world.Infections.Count == 0) return 0;
        double penalty = 0;
        var infections = world.Infections;
        for (int i = world.FirstInfectionIndex(npc.Id); i >= 0 && i < infections.Count && infections[i].Npc == npc.Id; i++)
        {
            var infection = infections[i];
            if (!infection.Symptomatic || world.DiseaseRules.Find(infection.Disease) is not { } profile) continue;
            penalty = Math.Max(penalty, profile.HealthPenalty * (infection.Bedridden ? 1.0 : profile.MildSeverity));
        }

        return penalty;
    }

    /// <summary>Nova infecção pelo perfil: incubação sorteada, fim pela duração (menor com medicina).</summary>
    public static Infection NewInfection(WorldState world, DiseaseRules rules, DiseaseProfile profile, NpcId npc, long now, WorldRng rng,
        long outbreakTick = -1, CityId outbreakCity = default)
    {
        long hoursPerDay = world.Calendar.HoursPerDay;
        int incubation = profile.IncubationDays + (int)(rng.NextDouble() * (profile.IncubationJitterDays + 1));
        long onset = now + Math.Max(0, incubation) * hoursPerDay;
        double duration = Math.Max(1, profile.DurationDays * (1 - Math.Clamp(rules.CareLevel * profile.DurationReductionAtFullCare, 0, 0.9)));
        return new Infection(npc, profile.Id, onset + (long)(duration * hoursPerDay))
        {
            InfectedTick = now,
            OnsetTick = onset,
            OutbreakTick = outbreakTick,
            OutbreakCity = outbreakCity,
        };
    }

    private void ProcessOnsets(WorldState world, TickContext ctx, DiseaseRules rules, long now)
    {
        _work.Clear();
        foreach (var infection in world.Infections)
            if (!infection.Symptomatic && infection.OnsetTick <= now) _work.Add(infection);

        foreach (var infection in _work)
        {
            if (world.FindNpc(infection.Npc) is not { IsAlive: true } npc || rules.Find(infection.Disease) is not { } profile)
            {
                world.RemoveInfection(infection.Npc, infection.Disease);
                continue;
            }

            var rng = ctx.StreamFor("disease-onset", unchecked((infection.Npc.Value * 1_000_003L + infection.OnsetTick) * 64 + infection.Disease));
            double age = Math.Max(0, now - npc.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear;
            bool bedridden = rng.NextDouble() < profile.BedriddenShare;
            double lethality = profile.LethalityFor(age, npc.Undernutrition)
                * (1 - Math.Clamp(rules.CareLevel * profile.LethalityReductionAtFullCare, 0, 1));
            NpcId? caregiver = SickBehavior.ChooseCaregiver(world, rules, npc, now);
            if (caregiver is null) lethality = Math.Min(1, lethality * rules.UncaredLethalityMultiplier);
            bool fatal = rng.NextDouble() < lethality;
            long end = Math.Max(infection.EndTick, now + 1);
            long? death = null;
            if (fatal)
            {
                bedridden = true;
                long span = Math.Max(1, end - now);
                death = now + 1 + Math.Min(span - 1, (long)((0.3 + 0.7 * rng.NextDouble()) * span));
                ctx.ScheduleEvent(death.Value, SystemName, $"{DeathPayloadPrefix}{infection.Npc.Value}|{infection.Disease}");
            }

            world.ReplaceInfection(infection with
            {
                Symptomatic = true,
                Bedridden = bedridden,
                DeathTick = death,
                EndTick = end,
                Caregiver = bedridden ? caregiver : null,
            });
        }
    }

    private void ResolveRecoveries(WorldState world, TickContext ctx, DiseaseRules rules, long now)
    {
        _ended.Clear();
        foreach (var infection in world.Infections)
            if (infection.Symptomatic && infection.DeathTick is null && infection.EndTick <= now) _ended.Add(infection);
        if (_ended.Count == 0) return;

        foreach (var infection in _ended)
        {
            world.RemoveInfection(infection.Npc, infection.Disease);
            if (world.FindNpc(infection.Npc) is not { IsAlive: true } npc || rules.Find(infection.Disease) is not { } profile) continue;
            if (profile.ImmunityDays > 0)
                world.SetImmunity(npc.Id, profile.Id, now + (long)profile.ImmunityDays * world.Calendar.HoursPerDay);
            if (profile.SequelaChance > 0
                && ctx.StreamFor("disease-sequela", unchecked(npc.Id.Value * 64 + profile.Id)).NextDouble() < profile.SequelaChance)
                npc.ShiftLifespanOffset((long)(profile.SequelaFrailtyYears * world.Calendar.HoursPerYear));
        }

        CloseFinishedOutbreaks(world, ctx);
    }

    /// <summary>Surto importado que ficou sem nenhum caso termina com evento (doença, cidade, início) para a história.</summary>
    private void CloseFinishedOutbreaks(WorldState world, TickContext ctx)
    {
        foreach (var gone in _ended)
        {
            if (gone.OutbreakTick < 0) continue;
            bool open = false;
            foreach (var infection in world.Infections)
                if (infection.Disease == gone.Disease && infection.OutbreakTick == gone.OutbreakTick) { open = true; break; }
            if (open) continue;
            bool logged = false;
            foreach (var earlier in _ended)
            {
                if (ReferenceEquals(earlier, gone)) break;
                if (earlier.Disease == gone.Disease && earlier.OutbreakTick == gone.OutbreakTick) { logged = true; break; }
            }

            if (!logged)
                ctx.LogEvent(WorldEventKind.OutbreakEnded, $"{gone.Disease}|{gone.OutbreakCity.Value}|{gone.OutbreakTick}", sourceSystem: SystemName);
        }

        _ended.Clear();
    }

    private void Spread(WorldState world, TickContext ctx, DiseaseRules rules, long now, long day, int month)
    {
        long hoursPerDay = world.Calendar.HoursPerDay;
        _work.Clear();
        foreach (var infection in world.Infections)
        {
            if (rules.Find(infection.Disease) is not { } profile) continue;
            long from = infection.OnsetTick - profile.ContagiousBeforeOnsetDays * hoursPerDay;
            long until = profile.ContagiousDays is { } days ? infection.OnsetTick + days * hoursPerDay : infection.EndTick;
            if (now >= from && now < until) _work.Add(infection);
        }

        _caught.Clear();
        foreach (var source in _work)
        {
            var profile = rules.Find(source.Disease)!;
            if (world.FindNpc(source.Npc) is not { IsAlive: true } sick) continue;
            double season = profile.Season(month);
            bool inBed = source.Symptomatic && source.Bedridden;
            var rng = ctx.StreamFor("disease-spread", unchecked((source.Npc.Value * 1_000_003L + day) * 64 + source.Disease));

            if (sick.Household is { } householdId && world.FindHousehold(householdId) is { } household)
                foreach (var member in household.Members)
                {
                    if (member == sick.Id) continue;
                    double rate = profile.Household * season * (source.Caregiver == member ? rules.CaregiverExposureMultiplier : 1.0);
                    TryInfect(world, rules, profile, source, member, rate, rng, now);
                }

            if (!inBed && profile.Crew > 0 && sick.Employer is { } employer && world.FindWorkplace(employer) is { } workplace)
            {
                var employees = workplace.Employees;
                int self = -1;
                for (int i = 0; i < employees.Count; i++)
                    if (employees[i] == sick.Id) { self = i; break; }
                int crew = Math.Min(rules.WorkCrewSize, employees.Count - 1);
                for (int step = 0, met = 0; self >= 0 && met < crew && step <= crew + 1; step++)
                {
                    int offset = step / 2 + 1;
                    int j = step % 2 == 0 ? self + offset : self - offset;
                    j = ((j % employees.Count) + employees.Count) % employees.Count;
                    if (j == self) continue;
                    met++;
                    TryInfect(world, rules, profile, source, employees[j], profile.Crew * season, rng, now);
                }
            }

            double contacts = rules.CommunityContactsPerDay * (inBed ? rules.BedriddenVisitFactor : 1.0);
            if (profile.Avoidance > 0 && KnownInCity(world, source, sick)) contacts *= 1 - profile.Avoidance;
            int count = (int)contacts + (rng.NextDouble() < contacts - (int)contacts ? 1 : 0);
            var alive = world.AliveNpcIndex.Alive;
            for (int c = 0; c < count && profile.Community > 0 && alive.Count > 1; c++)
            {
                var other = alive[Math.Min(alive.Count - 1, (int)(rng.NextDouble() * alive.Count))];
                if (other.Id == sick.Id || other.City != sick.City) continue;
                TryInfect(world, rules, profile, source, other.Id, profile.Community * season, rng, now);
            }
        }

        _caught.Sort((a, b) => a.Npc.Value != b.Npc.Value ? a.Npc.Value.CompareTo(b.Npc.Value) : a.Disease.CompareTo(b.Disease));
        foreach (var infection in _caught) world.AddInfection(infection);
    }

    /// <summary>O surto é conhecido na cidade: ao menos dois doentes com sintoma da mesma doença.</summary>
    private static bool KnownInCity(WorldState world, Infection source, Npc sick)
    {
        int cases = 0;
        foreach (var infection in world.Infections)
            if (infection.Disease == source.Disease && infection.Symptomatic && world.FindNpc(infection.Npc) is { } other
                && other.City == sick.City && ++cases >= 2) return true;
        return false;
    }

    private void TryInfect(WorldState world, DiseaseRules rules, DiseaseProfile profile, Infection source, NpcId target, double rate,
        WorldRng rng, long now)
    {
        double roll = rng.NextDouble(); // sempre sorteia: a sequência não depende de quem já é imune
        if (rate <= 0 || roll >= rate) return;
        if (!IsSusceptible(world, profile, target, now)) return;
        foreach (var pending in _caught)
            if (pending.Npc == target && pending.Disease == profile.Id) return;
        _caught.Add(NewInfection(world, rules, profile, target, now, rng, source.OutbreakTick, source.OutbreakCity));
    }

    /// <summary>Vivo, sem a doença agora, sem imunidade vigente e sem a imunidade de infância de quem já existia no começo do mundo.</summary>
    public static bool IsSusceptible(WorldState world, DiseaseProfile profile, NpcId target, long now)
    {
        if (world.IsInfected(target, profile.Id) || world.IsImmune(target, profile.Id)) return false;
        if (world.FindNpc(target) is not { IsAlive: true } npc) return false;
        if (profile.FounderImmunityByAge is not { } founder || npc.GestationAtBirthDays is not null) return true;
        long hoursPerYear = world.Calendar.HoursPerYear;
        long reference = npc.BirthDate.TotalHours < 0 ? 0 : now;
        double age = Math.Max(0, reference - npc.BirthDate.TotalHours) / (double)hoursPerYear;
        return world.Rng.StreamFor("disease-founder-immunity", unchecked(npc.Id.Value * 64 + profile.Id)).NextDouble() >= founder.At(age);
    }

    private void Environmental(WorldState world, TickContext ctx, DiseaseRules rules, long now, long day, int month)
    {
        var alive = world.AliveNpcIndex.Alive;
        if (alive.Count == 0) return;
        double daysPerYear = world.Calendar.DaysPerMonth * (double)world.Calendar.MonthsPerYear;
        _caught.Clear();
        foreach (var profile in rules.Diseases)
        {
            if (profile.EnvironmentalIncidenceByAge is not { } incidence) continue;
            double max = incidence.Max();
            if (max <= 0) continue;
            var rng = ctx.StreamFor("disease-environment", unchecked(day * 64 + profile.Id));
            int cases = Poisson(alive.Count * max * profile.Season(month) / daysPerYear, rng);
            for (int i = 0; i < cases; i++)
            {
                var npc = alive[Math.Min(alive.Count - 1, (int)(rng.NextDouble() * alive.Count))];
                double age = Math.Max(0, now - npc.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear;
                if (rng.NextDouble() * max >= incidence.At(age) || !IsSusceptible(world, profile, npc.Id, now)) continue;
                bool pending = false;
                foreach (var p in _caught) pending |= p.Npc == npc.Id && p.Disease == profile.Id;
                if (!pending) _caught.Add(NewInfection(world, rules, profile, npc.Id, now, rng));
            }
        }

        _caught.Sort((a, b) => a.Npc.Value != b.Npc.Value ? a.Npc.Value.CompareTo(b.Npc.Value) : a.Disease.CompareTo(b.Disease));
        foreach (var infection in _caught) world.AddInfection(infection);
    }

    private static void Import(WorldState world, TickContext ctx, DiseaseRules rules, long now, long day, int month)
    {
        double daysPerYear = world.Calendar.DaysPerMonth * (double)world.Calendar.MonthsPerYear;
        WorldRng? rng = null;
        foreach (var profile in rules.Diseases)
        {
            if (profile.ImportAnnualChance is not { } annual)
            {
                LegacyOutbreak(world, ctx, rules, profile, now, day);
                continue;
            }

            if (annual <= 0) continue;
            rng ??= ctx.StreamFor("disease-import", day);
            foreach (var city in world.Cities)
            {
                if (city.MergedIntoCityId is not null) continue;
                if (!(rng.NextDouble() < annual * profile.Season(month) / daysPerYear)) continue;
                if (HasCaseIn(world, profile.Id, city.Id)) continue;
                var alive = world.AliveNpcIndex.Alive;
                for (int attempt = 0; attempt < 20 && alive.Count > 0; attempt++)
                {
                    var zero = alive[Math.Min(alive.Count - 1, (int)(rng.NextDouble() * alive.Count))];
                    if (zero.City != city.Id || !IsSusceptible(world, profile, zero.Id, now)) continue;
                    world.AddInfection(NewInfection(world, rules, profile, zero.Id, now, rng, now, city.Id));
                    ctx.LogEvent(WorldEventKind.OutbreakStarted, $"{profile.Id}|{zero.Id.Value}|{city.Id.Value}", sourceSystem: SystemName);
                    break;
                }
            }
        }
    }

    private static bool HasCaseIn(WorldState world, int disease, CityId city)
    {
        foreach (var infection in world.Infections)
            if (infection.Disease == disease && world.FindNpc(infection.Npc) is { } npc && npc.City == city) return true;
        return false;
    }

    /// <summary>O surto do modelo antigo: sem caso no mundo, chance diária na maior cidade acima do limiar de população.</summary>
    private static void LegacyOutbreak(WorldState world, TickContext ctx, DiseaseRules rules, DiseaseProfile profile, long now, long day)
    {
        foreach (var infection in world.Infections)
            if (infection.Disease == profile.Id) return;
        if (!(ctx.OneShot($"outbreak-{profile.Id}-{day}").NextDouble() < rules.OutbreakDailyChance)) return; // NaN nunca dispara

        var crowded = world.Npcs.Where(n => n.IsAlive).GroupBy(n => n.City)
            .Select(g => (City: g.Key, Members: g.OrderBy(n => n.Id.Value).ToList()))
            .Where(g => g.Members.Count >= rules.OutbreakPopulationThreshold)
            .OrderByDescending(g => g.Members.Count).ThenBy(g => g.City.Value)
            .FirstOrDefault();
        if (crowded.Members is null) return;

        var rng = ctx.OneShot($"outbreak-zero-{profile.Id}-{day}");
        var patientZero = crowded.Members[Math.Min((int)(rng.NextDouble() * crowded.Members.Count), crowded.Members.Count - 1)];
        if (!IsSusceptible(world, profile, patientZero.Id, now)) return;

        world.AddInfection(NewInfection(world, rules, profile, patientZero.Id, now, rng, now, crowded.City));
        ctx.LogEvent(WorldEventKind.OutbreakStarted, $"{profile.Id}|{patientZero.Id.Value}|{crowded.City.Value}", sourceSystem: SystemName);
    }

    /// <summary>Sorteio de Poisson por inversão (λ pequeno: casos do dia).</summary>
    internal static int Poisson(double lambda, WorldRng rng)
    {
        if (lambda <= 0) return 0;
        double limit = DeterministicMath.Exp(-Math.Min(lambda, 50)), product = rng.NextDouble();
        int k = 0;
        while (product > limit && k < 1000) { k++; product *= rng.NextDouble(); }
        return k;
    }
}
