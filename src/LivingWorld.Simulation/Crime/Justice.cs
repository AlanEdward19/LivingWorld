using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Crime;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Crime;

/// <summary>O que a justiça soube no instante do crime: o caso (-1 = nenhum: ninguém viu nem suspeitou), quantos viram e quem
/// é apontado (-1 = ninguém).</summary>
internal readonly record struct CrimeOutcome(long CaseId, int Seers, long Accused);

/// <summary>Justiça conforme a época (AD-150): testemunha, boato, denúncia ou silêncio, julgamento, punição e dissuasão.
/// Tudo por evento (o crime) e por dia (boato, denúncia, corte), em ordem de id.</summary>
internal static class Justice
{
    private const double Ln2 = 0.6931471805599453;

    private static double Decay(double value, long fromTick, long now, int halfLifeDays, int hoursPerDay) =>
        value * DeterministicMath.Exp(-Ln2 * Math.Max(0, now - fromTick) / (double)hoursPerDay / halfLifeDays);

    public static double CityDeterrence(WorldState world, CityId city, long now)
    {
        if (!world.CrimeOn) return 0;
        foreach (var d in world.Crime!.Deterrence)
            if (d.City == city.Value)
                return Decay(d.Level, d.Tick, now, world.CrimeRules!.Justice.DeterrenceHalfLifeDays, world.Calendar.HoursPerDay);
        return 0;
    }

    /// <summary>Má fama atual (0..100) e a parte violenta dela.</summary>
    public static (double All, double Violent) InfamyOf(WorldState world, long npc, long now)
    {
        if (!world.CrimeOn || world.Crime!.RecordOf(npc) is not { } record) return (0, 0);
        int half = world.CrimeRules!.Justice.InfamyHalfLifeDays, hpd = world.Calendar.HoursPerDay;
        return (Decay(record.Infamy, record.InfamyTick, now, half, hpd), Decay(record.ViolentInfamy, record.InfamyTick, now, half, hpd));
    }

    /// <summary>AD-178: a fama como os outros a veem — a própria e, atenuada, a do pior da mesma linhagem (mesmo nome de família)
    /// na mesma cidade. Lida pela suspeita, pela troca de culpado na memória e pelo crédito; a própria fama é que se grava.
    /// AD-183: a boa fama vista (a própria e a da linhagem) desconta, na fração <c>HonorOffsetsInfamy</c>.</summary>
    public static double ReputationOf(WorldState world, long npc, long now)
    {
        double own = InfamyOf(world, npc, now).All;
        if (!world.CrimeOn || world.FindNpc(new NpcId(npc)) is not { } who) return own;
        var names = world.FamilyRules.Names;
        double seen = own;
        if (names.LineageInfamySpill > 0)
        {
            double worst = 0;
            foreach (var record in world.Crime!.Records)
                if (record.Npc != npc && world.FindNpc(new NpcId(record.Npc)) is { IsAlive: true } other && other.City == who.City
                    && LivingWorld.Simulation.Population.Naming.NameBonds.SameLineage(other, who))
                    worst = Math.Max(worst, InfamyOf(world, record.Npc, now).All);
            seen = Math.Min(100, own + names.LineageInfamySpill * worst);
        }
        if (names.HonorOffsetsInfamy > 0 && seen > 0)
            seen = Math.Max(0, seen - names.HonorOffsetsInfamy * LivingWorld.Simulation.Population.Naming.Honor.StandingOf(world, who, now));
        return seen;
    }

    /// <summary>AD-183: a fama do pior da linhagem pesou na suspeita (a dele sozinha não passava do que a linhagem somou).</summary>
    internal static bool LineageWeighed(WorldState world, long npc, long now) =>
        world.FamilyRules.Names.LineageInfamySpill > 0 && ReputationOf(world, npc, now) > InfamyOf(world, npc, now).All;

    private static void AddInfamy(WorldState world, long npc, int amount, bool violent, long now)
    {
        if (npc < 0 || amount <= 0) return;
        var record = world.Crime!.EnsureRecord(npc);
        var (all, viol) = InfamyOf(world, npc, now);
        record.Infamy = (int)Math.Min(100, all + amount);
        record.ViolentInfamy = (int)Math.Min(100, viol + (violent ? amount : 0));
        record.InfamyTick = now;
    }

    /// <summary>O crime aconteceu: quem estava na célula vê (acordado ou dormindo) e aponta alguém (às vezes a pessoa errada);
    /// sem ninguém, a vítima suspeita de quem tem má fama; sem acusado, impunidade. Com acusado, o caso abre, o boato começa
    /// com quem viu e a má fama do apontado sobe.</summary>
    public static CrimeOutcome OnCrime(WorldState world, TickContext ctx, CrimeKind kind, Npc offender, Npc? victim, long victimId,
        CellCoord where, long value, string good, string motive, bool caught, WorldRng rng, bool victimKnows = false)
    {
        if (!world.CrimeOn || !world.CrimeRules!.Justice.Enabled) return new CrimeOutcome(-1, 0, -1);
        var rules = world.CrimeRules.Justice;
        long now = ctx.CurrentTick;
        var present = world.AliveNpcIndex.At(where).Where(n => n.Id != offender.Id).OrderBy(n => n.Id.Value).ToList();
        var named = new SortedDictionary<long, int>();
        var knowers = new List<CaseKnower>();
        int seers = 0;
        foreach (var witness in present)
        {
            bool awake = witness.CurrentAction != ActionType.Sleep;
            bool isVictim = victim is not null && witness.Id == victim.Id;
            if (victimKnows && isVictim) continue; // a vítima que conhece o autor entra abaixo, sem confundir quem foi
            bool sees = (caught && (isVictim || seers == 0)) || rng.NextDouble() < (awake ? rules.WitnessSeeChance : rules.SleepingSeeChance);
            if (!sees) continue;
            seers++;
            long pointed = offender.Id.Value;
            if (!caught && rng.NextDouble() < rules.MisidentifyChance)
                pointed = WrongPerson(world, witness, offender, present.Where(p => p != victim).ToList(), now);
            named[pointed] = named.GetValueOrDefault(pointed) + 1;
            knowers.Add(new CaseKnower { Npc = witness.Id.Value, Accused = pointed, Value = value });
        }
        // AD-154: no crime contra a pessoa a vítima viva sabe quem foi (o agressor é quem a prendeu, a feriu em casa, a violentou)
        bool victimNames = victimKnows && victim is { IsAlive: true };
        if (victimNames && !knowers.Exists(k => k.Npc == victim!.Id.Value))
            knowers.Add(new CaseKnower { Npc = victim!.Id.Value, Accused = offender.Id.Value, Value = value });

        long accused = -1;
        int confirm = 0;
        foreach (var (id, count) in named)
            if (count > confirm || (count == confirm && id == offender.Id.Value)) { accused = id; confirm = count; }
        if (victimNames) (accused, confirm) = (offender.Id.Value, named.GetValueOrDefault(offender.Id.Value)); // a vítima sabe quem foi

        // sem testemunha: a vítima (ou, na morte, a família) desconfia de quem tem má fama
        var suspector = victim is { IsAlive: true } ? victim : kind == CrimeKind.Homicide && victim is not null ? Violence.KinOf(world, victim).FirstOrDefault() : null;
        string? why = null;
        if (accused < 0 && suspector is not null && Suspect(world, suspector, offender, now) is { } suspect
            && rng.NextDouble() < rules.SuspicionPerInfamy * ReputationOf(world, suspect, now))
        {
            accused = suspect;
            if (LineageWeighed(world, suspect, now)) why = "fama-da-linhagem"; // AD-183: o motivo da suspeita fica no caso
            knowers.Add(new CaseKnower { Npc = suspector.Id.Value, Accused = suspect, Value = value });
        }

        if (accused < 0) return new CrimeOutcome(-1, seers, -1); // impunidade: ninguém viu
        if (suspector is not null && !knowers.Exists(k => k.Npc == suspector.Id.Value))
            knowers.Add(new CaseKnower { Npc = suspector.Id.Value, Accused = accused, Value = value }); // quem viu conta à vítima

        var ledger = world.Crime!;
        var crime = new CrimeCase
        {
            Id = ledger.NextCaseId++,
            Kind = kind,
            Offender = offender.Id.Value,
            Victim = victimId,
            Tick = now,
            City = offender.City.Value,
            Value = value,
            Good = good,
            Motive = motive,
            Why = why,
            Accused = accused,
            Witnesses = confirm,
            Caught = caught,
            Knowers = knowers,
        };
        ledger.Cases.Add(crime);
        // AD-154: o crime de dentro de casa ou às escondidas só mancha a fama quando alguém de fora viu ou quando é denunciado
        if (!IsPersonal(kind) || seers > 0) AddInfamy(world, accused, rules.InfamyPerCrime, kind != CrimeKind.Theft, now);
        return new CrimeOutcome(crime.Id, seers, accused);
    }

    /// <summary>Quem a memória troca pelo culpado: o de pior fama entre os conhecidos de quem viu, senão outro presente.</summary>
    private static long WrongPerson(WorldState world, Npc witness, Npc offender, List<Npc> present, long now)
    {
        long best = -1;
        double worst = -1;
        foreach (var id in world.RelationshipTargetsOf(witness.Id))
        {
            if (id == offender.Id || world.FindNpc(id) is not { IsAlive: true }) continue;
            double infamy = ReputationOf(world, id.Value, now);
            if (infamy > worst) { worst = infamy; best = id.Value; }
        }
        if (best >= 0) return best;
        foreach (var other in present)
            if (other.Id != witness.Id) return other.Id.Value;
        return offender.Id.Value;
    }

    private static long? Suspect(WorldState world, Npc suspector, Npc offender, long now)
    {
        long? best = null;
        double worst = 0;
        foreach (var id in world.RelationshipTargetsOf(suspector.Id))
        {
            if (world.FindNpc(id) is not { IsAlive: true }) continue;
            double infamy = ReputationOf(world, id.Value, now);
            if (infamy > worst) { worst = infamy; best = id.Value; }
        }
        // quem tem má fama e foi mesmo o autor também entra (a fama ajuda a achar o culpado de verdade)
        double own = ReputationOf(world, offender.Id.Value, now);
        if (own > worst) best = offender.Id.Value;
        return best;
    }

    /// <summary>Um dia de justiça: o boato corre, quem sabe denuncia ou cala, o acusado de homicídio foge, a corte julga no seu
    /// dia e casos velhos prescrevem.</summary>
    public static void Daily(WorldState world, TickContext ctx, long day)
    {
        var rules = world.CrimeRules!.Justice;
        var ledger = world.Crime!;
        long now = ctx.CurrentTick;
        var rng = ctx.OneShot($"justice-day-{day}");
        bool courtDay = rules.Authority != JusticeAuthority.None && day % Math.Max(1, rules.CourtEveryDays) == 0;
        long maxAge = (long)rules.CaseLifeDays * world.Calendar.HoursPerDay;
        foreach (var crime in ledger.Cases.ToList())
        {
            if (crime.Closed || now - crime.Tick > maxAge) continue;
            // AD-154: o que aconteceu em casa ou às escondidas não corre de boca em boca enquanto ninguém denuncia
            bool quiet = IsPersonal(crime.Kind) && crime.ReportedBy < 0 && crime.Witnesses == 0;
            if (!quiet && now - crime.Tick <= (long)rules.RumorLifeDays * world.Calendar.HoursPerDay) Spread(world, ctx, crime, rng);
            if (crime.ReportedBy < 0) TryReport(world, ctx, crime, rng);
            else if (courtDay) Try(world, ctx, crime, rng);
        }
        ledger.Cases.RemoveAll(c => c.Closed || now - c.Tick > maxAge);
        ledger.Records.RemoveAll(r => r.Convictions == 0 && InfamyOf(world, r.Npc, now).All < 1);
    }

    /// <summary>Boato: cada um que sabe conta, na chance do dia, a um conhecido; às vezes troca o acusado ou aumenta o valor.</summary>
    private static void Spread(WorldState world, TickContext ctx, CrimeCase crime, WorldRng rng)
    {
        var rules = world.CrimeRules!.Justice;
        long now = ctx.CurrentTick;
        int count = crime.Knowers.Count;
        for (int i = 0; i < count && crime.Knowers.Count < rules.RumorMaxKnowers; i++)
        {
            if (rng.NextDouble() >= rules.RumorTellsPerDay) continue;
            var teller = crime.Knowers[i];
            var contacts = world.RelationshipTargetsOf(new NpcId(teller.Npc));
            if (contacts.Count == 0) continue;
            var heard = contacts[Math.Min(contacts.Count - 1, (int)(rng.NextDouble() * contacts.Count))];
            if (heard.Value == crime.Offender && heard.Value == teller.Accused) continue; // não se conta ao próprio acusado
            if (crime.Knowers.Exists(k => k.Npc == heard.Value) || world.FindNpc(heard) is not { IsAlive: true } hearer) continue;
            long accused = teller.Accused, value = teller.Value;
            if (rng.NextDouble() < rules.RumorDistortChance)
            {
                if (rng.NextDouble() < 0.5 && world.FindNpc(new NpcId(teller.Npc)) is { } t && world.FindNpc(new NpcId(crime.Offender)) is { } off)
                    accused = WrongPerson(world, t, off, [], now);
                else value = value + value / 2 + 1;
            }
            crime.Knowers.Add(new CaseKnower { Npc = heard.Value, Accused = accused, Value = value });
            AddInfamy(world, accused, Math.Max(1, rules.InfamyPerCrime / 10), crime.Kind != CrimeKind.Theft, now);
            if (accused >= 0 && world.Relationships.TryGetValue(new RelationshipKey(heard, new NpcId(accused)), out var bond))
                bond.ApplyEvent(RelationshipEventType.Betrayal, world.FamilyRules); // ouvi dizer: confio menos nele
            if (heard.Value == crime.Victim && crime.Kind == CrimeKind.Theft && world.FindNpc(new NpcId(accused)) is { } who)
                Theft.Discovered(world, ctx, who, hearer, 0, crime.Value > 0);
        }
    }

    /// <summary>Denúncia ou silêncio: a vítima (ou, na morte dela, quem sabe) denuncia o acusado em que acredita, salvo se é
    /// parente, gosta muito dele ou tem medo. Sem autoridade, ninguém julga: fica a mágoa.</summary>
    private static void TryReport(WorldState world, TickContext ctx, CrimeCase crime, WorldRng rng)
    {
        var rules = world.CrimeRules!.Justice;
        long now = ctx.CurrentTick;
        CaseKnower? reporter = null;
        // AD-154: a criança não vai à corte; quem sabe por ela (o pai, a mãe) denuncia
        bool victimGone = crime.Victim < 0 || world.FindNpc(new NpcId(crime.Victim)) is not { IsAlive: true } v
            || (IsPersonal(crime.Kind) && v.AgeYears(world.CurrentDate) < world.CrimeRules!.Violence.MinAgeYears);
        foreach (var k in crime.Knowers)
        {
            if (k.Npc == crime.Victim && !victimGone) { reporter = k; break; }
            // vítima morta ou sem dono (a oficina da cidade): denuncia quem sabe, o primeiro vivo
            if (victimGone && reporter is null && k.Npc != crime.Victim && k.Npc != crime.Offender && world.FindNpc(new NpcId(k.Npc)) is { IsAlive: true }) reporter = k;
        }
        if (reporter is null || reporter.Accused < 0) return;
        if (world.FindNpc(new NpcId(reporter.Npc)) is not { IsAlive: true } who || world.FindNpc(new NpcId(reporter.Accused)) is not { } accused)
            return;
        if (!accused.IsAlive) { crime.Closed = true; return; }

        if (rules.Authority == JusticeAuthority.None)
        {
            Violence.AddGrudge(world, who.Id.Value, accused.Id.Value, crime.Kind == CrimeKind.Theft ? "furto" : "agressao",
                world.CrimeRules.Violence.AssaultGrudge, now);
            crime.Closed = true;
            return;
        }

        // a vítima de dentro de casa não cala por ser da família (a norma e a dependência já cortam a chance, abaixo)
        bool kin = (!IsPersonal(crime.Kind) || who.Id.Value != crime.Victim) && Violence.KinOf(world, accused).Exists(n => n.Id == who.Id);
        double affection = world.Relationships.TryGetValue(new RelationshipKey(who.Id, accused.Id), out var bond) ? bond.Affection : 0;
        if (kin || affection >= rules.SilenceAffection) { crime.Closed = true; return; } // silêncio de quem é chegado
        double fear = Math.Max(PsycheMechanic.EmotionOf(world, who, Emotion.Fear, now), InfamyOf(world, accused.Id.Value, now).Violent / 100.0);
        double petty = crime.Kind == CrimeKind.Theft && crime.Value < rules.PettyTheftValue ? rules.PettyReportFactor : 1.0;
        petty *= ReportFactor(world.CrimeRules, crime.Kind);
        if (rng.NextDouble() >= rules.ReportChance * petty * (1 - rules.FearSilence * fear))
        {
            if (IsPersonal(crime.Kind)) crime.Closed = true; // AD-154: quem decide calar sobre o que sofreu cala de vez
            return; // hoje não (pode amanhã)
        }

        crime.ReportedBy = who.Id.Value;
        if (IsPersonal(crime.Kind)) AddInfamy(world, accused.Id.Value, rules.InfamyPerCrime, true, now);
        ctx.LogEvent(WorldEventKind.CrimeReported,
            $"{who.Id.Value}|{accused.Id.Value}|{crime.Id}|{KindName(crime.Kind)}|{(accused.Id.Value == crime.Offender ? 1 : 0)}"
                + (crime.Why is { } why ? $"|{why}" : ""),
            Theft.SourceName);
        if (crime.Kind == CrimeKind.Homicide && rng.NextDouble() < rules.HomicideFleeChance)
        {
            Banish(world, ctx, accused, crime, "fuga");
            crime.Closed = true;
        }
    }

    /// <summary>Corte: condena pela prova (testemunhas que confirmam, flagrante) e pelo preconceito (má fama); pune conforme o
    /// crime e a época; a punição dissuade a cidade e marca o punido; o condenado guarda mágoa de quem o denunciou.</summary>
    private static void Try(WorldState world, TickContext ctx, CrimeCase crime, WorldRng rng)
    {
        var rules = world.CrimeRules!.Justice;
        long now = ctx.CurrentTick;
        crime.Closed = true;
        if (world.FindNpc(new NpcId(crime.Accused)) is not { IsAlive: true } accused) return;
        double infamy = InfamyOf(world, accused.Id.Value, now).All / 100.0;
        double p = (rules.ConvictBase + rules.ConvictPerWitness * crime.Witnesses + (crime.Caught ? rules.ConvictCaught : 0)
            + rules.ConvictInfamy * infamy + (IsPersonal(crime.Kind) ? rules.ConvictPerWitness / 2 : 0)) // a palavra da vítima
            * ConvictFactor(world.CrimeRules, crime.Kind);
        bool guilty = rng.NextDouble() < Math.Clamp(p, 0, 1);
        bool truly = accused.Id.Value == crime.Offender;
        string punishment = "nenhuma";
        long amount = 0;
        if (guilty) (punishment, amount) = Punish(world, ctx, accused, crime, rng);
        else Violence.AddGrudge(world, accused.Id.Value, crime.ReportedBy, "denuncia", world.CrimeRules.Violence.DenunciationGrudge / 2, now);

        ctx.LogEvent(WorldEventKind.Verdict,
            $"{accused.Id.Value}|{crime.Victim}|{crime.Id}|{KindName(crime.Kind)}|{(guilty ? "culpado" : "inocentado")}|{punishment}|{amount}|{(truly ? 1 : 0)}",
            Theft.SourceName);
        if (punishment == "banimento") Banish(world, ctx, accused, crime, "sentenca");
        else if (punishment == "forca") NpcDeath.Apply(world, ctx, accused, WorldEventKind.Executed);
    }

    private static (string Punishment, long Amount) Punish(WorldState world, TickContext ctx, Npc convict, CrimeCase crime, WorldRng rng)
    {
        var rules = world.CrimeRules!.Justice;
        long now = ctx.CurrentTick;
        var record = world.Crime!.EnsureRecord(convict.Id.Value);
        record.Convictions++;
        AddInfamy(world, convict.Id.Value, rules.InfamyPerCrime, crime.Kind != CrimeKind.Theft, now);
        var deterrence = world.Crime.EnsureDeterrence(crime.City);
        double level = Decay(deterrence.Level, deterrence.Tick, now, rules.DeterrenceHalfLifeDays, world.Calendar.HoursPerDay);
        deterrence.Level = level + (1 - level) * rules.PunishmentDeterrence;
        deterrence.Tick = now;
        if (world.NeedsRules.Psyche.Enabled)
            PsycheMechanic.Experience(world, convict, convict.Id.Value == crime.Offender ? LifeEventKind.Punished : LifeEventKind.WronglyPunished,
                crime.ReportedBy, now);
        Violence.AddGrudge(world, convict.Id.Value, crime.ReportedBy, "denuncia", world.CrimeRules.Violence.DenunciationGrudge, now);

        if (crime.Kind == CrimeKind.Homicide)
            return (rng.NextDouble() < rules.ExecutionShare ? "forca" : "banimento", 0);
        if (crime.Kind == CrimeKind.Kidnapping)
            return (rng.NextDouble() < world.CrimeRules.Kidnapping!.ExecutionShare ? "forca" : "banimento", 0);
        if (crime.Kind == CrimeKind.SexualAssault)
        {
            var sexual = world.CrimeRules.SexualViolence!;
            double roll = rng.NextDouble();
            if (roll < sexual.ExecutionShare) return ("forca", 0);
            if (roll < sexual.ExecutionShare + sexual.BanishShare || crime.Good == "crianca") return ("banimento", 0);
        }

        long owed = crime.Kind == CrimeKind.Theft ? crime.Value + (long)(rules.FineMultiple * crime.Value)
            : crime.Kind == CrimeKind.SexualAssault ? world.CrimeRules.SexualViolence!.Fine : rules.AssaultFine;
        long paid = Math.Min(owed, convict.Wallet.Amount);
        if (paid > 0 && convict.TryDebitWallet(new Money(paid)).IsSuccess)
        {
            // restituição à vítima primeiro; o resto (multa) à cidade
            long restitution = crime.Kind == CrimeKind.Theft ? Math.Min(paid, crime.Value) : paid;
            long fine = paid - restitution;
            if (world.FindNpc(new NpcId(crime.Victim)) is { IsAlive: true } victim) victim.CreditWallet(new Money(restitution));
            else fine += restitution;
            if (fine > 0 && world.FindCity(new CityId(crime.City)) is { } city)
            {
                city.CreditTreasury(new Money(fine));
                world.RecordTreasury(city.Id, now, revenue: fine, spending: 0);
            }
            else if (fine > 0) convict.CreditWallet(new Money(fine));
        }

        bool grave = crime.Kind == CrimeKind.Theft && crime.Value >= rules.GraveTheftValue;
        if (record.Convictions >= rules.BanishAfterConvictions && grave) // só o ladrão grave reincidente; o briguento paga ou apanha
            return ("banimento", paid);
        if (grave || paid < owed)
        {
            // pelourinho, açoite: fere, mas o castigo não mata nem deixa no piso de perigo (≤ 1), de onde a vizinhança fugiria
            convict.SetHealth(Math.Max(Math.Min(convict.Health, AliveNpcIndex.EndangeredHealth + 1), convict.Health - rules.CorporalHarm));
            return (paid > 0 ? "multa_castigo" : "castigo", paid);
        }
        return ("multa", paid);
    }

    /// <summary>Sai do mundo pela justiça (sentença ou fuga): a moeda fica com quem fica na casa (senão os herdeiros), o caso
    /// amoroso acaba, a vaga na casa se fecha. A moeda do mundo não muda.</summary>
    public static void Banish(WorldState world, TickContext ctx, Npc npc, CrimeCase crime, string mode)
    {
        if (!npc.IsAlive) return;
        long savings = npc.Wallet.Amount;
        if (savings > 0 && Keeper(world, npc) is { } keeper && npc.TryDebitWallet(new Money(savings)).IsSuccess)
            keeper.CreditWallet(new Money(savings));
        if (npc.Union?.AffairWith is { } lover && world.FindNpc(new NpcId(lover)) is { Union: { } theirs } other && theirs.AffairWith == npc.Id.Value)
            other.SetUnion(theirs with { AffairWith = null });
        npc.LeaveWorld(world.CurrentDate);
        world.AliveNpcIndex.OnDied(npc);
        world.ForgetHealthOf(npc.Id);
        ctx.LogEvent(WorldEventKind.Banished, $"{npc.Id.Value}|{crime.Id}|{KindName(crime.Kind)}|{mode}", Theft.SourceName);
        if (npc.Household is { } homeId && world.FindHousehold(homeId) is { } home)
        {
            home.RemoveMember(npc.Id);
            HouseholdCleanup.DissolveIfEmpty(world, ctx, home);
        }
    }

    private static Npc? Keeper(WorldState world, Npc npc)
    {
        if (npc.Household is { } homeId && world.FindHousehold(homeId) is { } home)
            foreach (var memberId in home.Members.OrderBy(id => id.Value))
                if (memberId != npc.Id && world.FindNpc(memberId) is { IsAlive: true } member
                    && world.LifeStageRules.LifeStageOf(member.AgeYears(world.CurrentDate)) != LivingWorld.Domain.Population.Body.LifeStage.Child)
                    return member;
        return EstateSystem.HeirsOf(world, npc).FirstOrDefault();
    }

    public static string KindName(CrimeKind kind) => kind switch
    {
        CrimeKind.Theft => "furto",
        CrimeKind.Assault => "agressao",
        CrimeKind.DomesticAssault => "violencia_domestica",
        CrimeKind.Kidnapping => "sequestro",
        CrimeKind.SexualAssault => "violencia_sexual",
        _ => "homicidio",
    };

    /// <summary>Crime contra a pessoa em que a vítima conhece o autor e decide ela mesma (ou quem fala por ela) se denuncia.</summary>
    public static bool IsPersonal(CrimeKind kind) => kind is CrimeKind.DomesticAssault or CrimeKind.Kidnapping or CrimeKind.SexualAssault;

    private static double ReportFactor(CrimeRules rules, CrimeKind kind) => kind switch
    {
        CrimeKind.DomesticAssault => rules.Domestic?.ReportFactor ?? 1.0,
        CrimeKind.SexualAssault => rules.SexualViolence?.ReportFactor ?? 1.0,
        _ => 1.0,
    };

    private static double ConvictFactor(CrimeRules rules, CrimeKind kind) => kind switch
    {
        CrimeKind.DomesticAssault => rules.Domestic?.ConvictFactor ?? 1.0,
        CrimeKind.SexualAssault => rules.SexualViolence?.ConvictFactor ?? 1.0,
        _ => 1.0,
    };
}
