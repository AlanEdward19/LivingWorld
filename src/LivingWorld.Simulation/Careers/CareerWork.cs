using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Careers;

/// <summary>#263: a carreira de quem vive na casa, só pelo dado do catálogo <c>Careers</c>. O turno tem cinco passos: o aviso (quem o jogador conduz), a saída (o corpo sai da casa:
/// fora do mapa, como no Sims, e some das telas), o trabalho (o motor simula o turno: o desempenho sai de necessidades e habilidade, as horas dão o que o nível diz ao corpo), a volta
/// (o corpo reaparece na soleira) e o pagamento (o empregador, um <c>Workplace</c> com caixa, paga; a cidade retém a mesma alíquota da folha; a moeda só muda de mão). Faltar,
/// promover e rebaixar também são do dado. A economia de sempre (<c>WagePaymentSystem</c>, ofícios) não é tocada: este caminho é só de quem tem carreira no livro.</summary>
public static class CareerWork
{
    public const string SourceSystem = "CareerSystem";

    public static CareerCatalog? CatalogOf(WorldState world) => world.ObjectCatalog?.Careers;

    public static bool IsAway(WorldState world, Npc npc) => world.Careers?.JobOf(npc.Id.Value) is { ReturnMs: > 0 };

    private static long DayMs(WorldState world) => (long)world.Calendar.HoursPerDay * WorldMillis.PerHour;

    private static long DayOf(WorldState world, long ms) => Math.DivRem(ms, DayMs(world), out _);

    private static int HourOfDay(WorldState world, long ms) => (int)(ms % DayMs(world) / WorldMillis.PerHour);

    private static long CommuteMs(CareerDef def) => def.CommuteMin * 60_000L;

    /// <summary>Hora (inteira) em que sai de casa: a do começo menos a ida, arredondada para cima em horas, para chegar a tempo.</summary>
    public static int LeaveHour(CareerDef def, CareerLevel level) => Math.Max(0, level.StartHour - (int)Math.Ceiling(def.CommuteMin / 60.0));

    /// <summary>O turno do dia <paramref name="day"/> do nível: começo e fim do trabalho em ms.</summary>
    public static (long Start, long End) Shift(WorldState world, CareerLevel level, long day)
    {
        long start = day * DayMs(world) + level.StartHour * (long)WorldMillis.PerHour;
        return (start, start + level.Hours * (long)WorldMillis.PerHour);
    }

    public static bool IsWorkday(WorldState world, CareerCatalog catalog, CareerLevel level, long day) => level.Days.Contains((int)(day % catalog.Rules.WeekDays));

    private static (CareerDef Def, CareerLevel Level)? Resolve(CareerCatalog catalog, CareerJob job) =>
        catalog.Find(job.Career) is { } def ? (def, def.Levels[Math.Clamp(job.Level, 0, def.Levels.Count - 1)]) : null;

    /// <summary>O próximo turno da pessoa a partir de agora (ainda não cumprido): começo e hora de sair. Nulo sem dia de trabalho nos próximos 14 dias.</summary>
    public static (long Start, long Leave)? NextShift(WorldState world, CareerCatalog catalog, CareerJob job, long nowMs)
    {
        if (Resolve(catalog, job) is not { } r) return null;
        long today = DayOf(world, nowMs);
        for (long day = today; day < today + 15; day++)
        {
            if (!IsWorkday(world, catalog, r.Level, day) || day == job.ShiftDay && job.ReturnMs == 0) continue;
            var (start, end) = Shift(world, r.Level, day);
            if (end <= nowMs || nowMs >= start + catalog.Rules.GraceHours * (long)WorldMillis.PerHour) continue; // o turno que passou da tolerância é falta, não "próximo"
            return (start, day * DayMs(world) + LeaveHour(r.Def, r.Level) * (long)WorldMillis.PerHour);
        }

        return null;
    }

    // ---- emprego ----------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Emprega <paramref name="npc"/> na carreira (nível <paramref name="level"/>) no empregador. Abre as contas da casa dele, se ainda não tem.</summary>
    public static Result<CareerJob> Hire(WorldState world, Npc npc, string careerId, int level, Workplace employer, long nowMs, bool announce = false)
    {
        if (CatalogOf(world) is not { } catalog || catalog.Find(careerId) is not { } def) return Result<CareerJob>.Fail($"carreira '{careerId}' não existe");
        if (level < 0 || level >= def.Levels.Count) return Result<CareerJob>.Fail($"carreira '{careerId}': nível {level} não existe");
        var ledger = world.CareersLedger();
        if (ledger.JobOf(npc.Id.Value) is not null) return Result<CareerJob>.Fail("já tem emprego");
        var job = new CareerJob { Npc = npc.Id.Value, Career = careerId, Level = level, Employer = employer.Id.Value };
        ledger.AddJob(job);
        OpenAccounts(world, ledger, catalog, npc, nowMs);
        if (announce) Notify(world, ledger, catalog, nowMs, npc, "hired", $"Contratado: {def.Levels[level].Title} ({def.Name}), {def.Levels[level].WagePerHour} por hora.", 0, null);
        world.CareersChanged();
        return Result<CareerJob>.Ok(job);
    }

    private static void OpenAccounts(WorldState world, CareerLedger ledger, CareerCatalog catalog, Npc npc, long nowMs)
    {
        if (npc.Household is not { } household || ledger.HasAccount(household.Value)) return;
        long day = DayOf(world, nowMs);
        foreach (var bill in catalog.Bills)
            ledger.AddBill(new BillState { Household = household.Value, Bill = bill.Id, NextIssueDay = day + bill.FirstIssueDay });
    }

    private static void Notify(WorldState world, CareerLedger ledger, CareerCatalog catalog, long ms, Npc npc, string kind, string text, long amount, string? verb) =>
        ledger.Notify(ms, npc.Id.Value, npc.Household?.Value ?? 0, kind, text, amount, verb, catalog.Rules.NoticeKeep);

    // ---- o turno ----------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Por que o corpo não pode sair agora para o turno de hoje (nulo = pode): sem turno hoje, já foi, longe da hora, tarde demais.</summary>
    public static string? CannotGo(WorldState world, Npc npc, long nowMs)
    {
        if (CatalogOf(world) is not { } catalog || world.Careers?.JobOf(npc.Id.Value) is not { } job || Resolve(catalog, job) is not { } r) return "sem emprego";
        if (job.ReturnMs > 0) return "já está no trabalho";
        long day = DayOf(world, nowMs);
        if (!IsWorkday(world, catalog, r.Level, day)) return "hoje não é dia de trabalho";
        if (job.ShiftDay == day) return "o turno de hoje já passou";
        var (start, end) = Shift(world, r.Level, day);
        if (nowMs < day * DayMs(world) + LeaveHour(r.Def, r.Level) * (long)WorldMillis.PerHour - catalog.Rules.NoticeLeadHours * (long)WorldMillis.PerHour) return "ainda não é hora de ir";
        if (nowMs + CommuteMs(r.Def) >= end) return "já passou do horário";
        return null;
    }

    /// <summary>O corpo sai para o turno: para o que fazia, deixa a casa e some do mapa (fora do mapa, ida e volta pelo tempo da carreira). O motor simula o turno e o
    /// agenda a volta; o desempenho já sai medido (necessidades e habilidade de agora, menos o atraso).</summary>
    public static Result<Unit> Depart(WorldState world, TickContext ctx, Npc npc, long nowMs)
    {
        if (CannotGo(world, npc, nowMs) is { } why) return Result<Unit>.Fail($"{PlayerCommands.Conflict} trabalho: {why}");
        var catalog = CatalogOf(world)!;
        var ledger = world.Careers!;
        var job = ledger.JobOf(npc.Id.Value)!;
        var (def, level) = Resolve(catalog, job)!.Value;
        long day = DayOf(world, nowMs);
        var (start, end) = Shift(world, level, day);
        long arrive = Math.Max(start, nowMs + CommuteMs(def));
        long late = arrive - start;
        double lateFraction = Math.Clamp(late / (double)(end - start), 0, 1);
        job.PendingScore = (int)Math.Round(Performance(world, npc, def, nowMs) * (1 - lateFraction));
        job.DepartMs = nowMs;
        job.ArriveMs = arrive;
        job.WorkEndMs = end;
        job.ReturnMs = end + CommuteMs(def);
        job.ShiftDay = day;

        if (npc.BodyTask is not null || npc.UseQueue is { Count: > 0 })
        {
            if (ObjectUsage.Active(npc)) ObjectUsage.Stop(world, ctx, npc);
            npc.SetBodyTask(null);
            npc.SetUseQueue(null);
        }

        PlayerCommands.StopOnPossession(world, npc);
        var door = npc.Household is { } h && world.FindHousehold(h)?.Dwelling is { } dwelling && world.FindBuilding(dwelling) is { } building ? PlayerGround.DoorOf(building) : null;
        npc.ExitBuilding();
        npc.SetFine(null);
        if (door is { } spot) npc.MoveTo(spot.Doorstep, ctx.CurrentTick);
        npc.SetCurrentActionAtMs(ActionType.Work, nowMs);
        ctx.ScheduleEventAtMs(job.ReturnMs, CareerSystem.SystemName, $"r|{npc.Id.Value}|{job.ReturnMs}");
        Notify(world, ledger, catalog, nowMs, npc, "departed", $"{npc.GivenName} saiu para o trabalho ({level.Title}). Volta {Clock(world, job.ReturnMs)}.", 0, null);
        world.CareersChanged();
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>O fim do turno: o corpo volta à soleira de casa, o turno dá o que o nível diz (refeição, café), o desempenho conta (promoção, rebaixamento, demissão),
    /// a habilidade pratica e o empregador paga as horas trabalhadas.</summary>
    public static void Return(WorldState world, TickContext ctx, Npc npc, long nowMs)
    {
        if (CatalogOf(world) is not { } catalog || world.Careers is not { } ledger || ledger.JobOf(npc.Id.Value) is not { ReturnMs: > 0 } job || Resolve(catalog, job) is not { } r) return;
        var (def, level) = r;
        double hours = Math.Max(0, job.WorkEndMs - job.ArriveMs) / (double)WorldMillis.PerHour;
        long gross = (long)Math.Floor(level.WagePerHour * hours);
        int score = job.PendingScore;
        foreach (var effect in level.AwayEffects) HomeLife.Add(world, npc, effect.Need, effect.PerHour * hours, nowMs);
        if (def.SkillId is { } skill && def.Performance.Practice > 0) npc.GainSkill(new SkillType(skill), def.Performance.Practice * hours, 100);
        job.DepartMs = job.ArriveMs = job.WorkEndMs = job.ReturnMs = 0;
        job.PendingScore = 0;
        job.Score = score;
        job.Shifts++;
        job.Absences = 0;
        npc.SetCurrentActionAtMs(ActionType.Idle, nowMs);
        PlayerCommands.WakeNow(world, npc);
        EnterHome(world, ctx, npc);

        long net = Pay(world, ctx, npc, job, gross, nowMs);
        Notify(world, ledger, catalog, nowMs, npc, "returned", $"Voltou do trabalho. Desempenho do dia: {score}%.", 0, null);
        Evaluate(world, ctx, ledger, catalog, npc, job, def, level, score, nowMs);
        world.CareersChanged();
    }

    /// <summary>#267: a volta do turno é para dentro de casa, pela porta (como o Sims: quem chega do trabalho entra sozinho), não na soleira olhando a fachada: o corpo conduzido fica
    /// na célula logo depois da porta; quem ninguém conduz entra no prédio. Casa sem posição ou sem planta deixa o corpo onde estava.</summary>
    private static void EnterHome(WorldState world, TickContext ctx, Npc npc)
    {
        if (npc.Household is not { } h || world.FindHousehold(h)?.Dwelling is not { } dwelling || world.FindBuilding(dwelling) is not { Position: { } origin } home
            || PlayerGround.DoorOf(home) is not { } door) return;
        var inner = new CellCoord(door.Door.X + (door.Door.X - door.Doorstep.X), door.Door.Y + (door.Door.Y - door.Doorstep.Y));
        var cell = PlayerGround.At(world, inner) is { } spot && spot.Building.Id == home.Id ? inner : door.Door;
        if (npc.ControllerId is not null)
        {
            npc.SetFine(null);
            npc.MoveTo(cell, ctx.CurrentTick);
        }
        else Cities.InteriorEntry.TryEnter(world, npc, home.Id, new FloorLevel(0), new CellCoord(cell.X - origin.X, cell.Y - origin.Y));
    }

    /// <summary>#267: quem assume o corpo depois da hora do aviso também o recebe: com a janela do turno aberta e sem o aviso desta janela no livro, avisa agora.</summary>
    public static void NoticeOnTakeover(WorldState world, Npc npc, long nowMs)
    {
        if (CatalogOf(world) is not { } catalog || world.Careers is not { } ledger || ledger.JobOf(npc.Id.Value) is not { ReturnMs: 0 } job || Resolve(catalog, job) is not { } r) return;
        if (CannotGo(world, npc, nowMs) is not null) return;
        long windowStart = DayOf(world, nowMs) * DayMs(world) + (LeaveHour(r.Def, r.Level) - catalog.Rules.NoticeLeadHours) * (long)WorldMillis.PerHour;
        foreach (var n in ledger.Notices)
            if (n.Npc == npc.Id.Value && n.Kind == "shift-soon" && n.Ms >= windowStart) return;
        Notify(world, ledger, catalog, nowMs, npc, "shift-soon", $"Hora do trabalho: {r.Level.Title}, às {r.Level.StartHour:00}:00.", 0, "go");
        world.CareersChanged();
    }

    /// <summary>#267: quando volta quem da casa de <paramref name="npc"/> (ele mesmo ou os outros) está no trabalho: o primeiro retorno, ou nulo se ninguém está fora.</summary>
    public static long? NextReturnMs(WorldState world, Npc npc)
    {
        if (world.Careers is not { } ledger) return null;
        long? best = null;
        foreach (var job in ledger.Jobs)
        {
            if (job.ReturnMs <= 0 || world.FindNpc(new NpcId(job.Npc)) is not { } who) continue;
            if (who.Id != npc.Id && (npc.Household is null || who.Household != npc.Household)) continue;
            if (best is null || job.ReturnMs < best) best = job.ReturnMs;
        }

        return best;
    }

    /// <summary>Paga do caixa do empregador para a carteira; devolve o líquido. Caixa curto paga o que há e deixa o atraso nos avisos.</summary>
    private static long Pay(WorldState world, TickContext ctx, Npc npc, CareerJob job, long gross, long nowMs)
    {
        var ledger = world.Careers!;
        var catalog = CatalogOf(world)!;
        if (gross <= 0 || world.FindWorkplace(new WorkplaceId(job.Employer)) is not { } employer) return 0;
        long pay = Math.Min(gross, employer.Treasury.Amount);
        if (pay < gross)
        {
            ctx.LogEvent(WorldEventKind.WageUnpaid, $"{npc.Id.Value}|{employer.Id.Value}|{gross - pay}", sourceSystem: SourceSystem);
            Notify(world, ledger, catalog, nowMs, npc, "wage-short", $"O empregador pagou só {pay} de {gross}: caixa curto.", pay, null);
        }

        if (pay <= 0 || !employer.TryDebitTreasury(new Money(pay)).IsSuccess) return 0;
        long tax = world.EconomyRules.WageTaxRate > 0 && world.FindCity(employer.City) is { } city ? (long)Math.Floor(pay * world.EconomyRules.WageTaxRate) : 0;
        if (tax > 0)
        {
            world.FindCity(employer.City)!.CreditTreasury(new Money(tax));
            world.RecordTreasury(employer.City, ctx.CurrentTick, revenue: tax, spending: 0);
        }

        npc.CreditWallet(new Money(pay - tax));
        job.Earned += pay - tax;
        Notify(world, ledger, catalog, nowMs, npc, "paid", $"+{pay - tax} salário", pay - tax, null);
        return pay - tax;
    }

    /// <summary>Promoção, rebaixamento e demissão pelo desempenho do turno (e pela falta), como o nível diz.</summary>
    private static void Evaluate(WorldState world, TickContext ctx, CareerLedger ledger, CareerCatalog catalog, Npc npc, CareerJob job, CareerDef def, CareerLevel level, int score, long nowMs)
    {
        if (score >= level.PromoteAt) { job.Good++; job.Bad = 0; }
        else if (score < level.DemoteBelow) { job.Bad++; job.Good = 0; }
        else { job.Good = 0; job.Bad = 0; }

        double skill = def.SkillId is { } sid ? npc.Skills.Get(new SkillType(sid)) : 0;
        if (job.Good >= level.PromoteAfter && job.Level + 1 < def.Levels.Count && skill >= def.Levels[job.Level + 1].MinSkill)
        {
            job.Level++;
            job.Good = job.Bad = 0;
            var next = def.Levels[job.Level];
            Notify(world, ledger, catalog, nowMs, npc, "promoted", $"Promoção! Agora: {next.Title}, {next.WagePerHour} por hora.", next.WagePerHour, null);
        }
        else if (job.Bad >= level.DemoteAfter)
        {
            job.Good = job.Bad = 0;
            if (job.Level > 0)
            {
                job.Level--;
                var prev = def.Levels[job.Level];
                Notify(world, ledger, catalog, nowMs, npc, "demoted", $"Rebaixado a {prev.Title} pelo desempenho.", prev.WagePerHour, null);
            }
            else Fire(world, ctx, ledger, catalog, npc, job, "Demitido pelo desempenho.", nowMs);
        }
    }

    private static void Fire(WorldState world, TickContext ctx, CareerLedger ledger, CareerCatalog catalog, Npc npc, CareerJob job, string text, long nowMs)
    {
        ledger.RemoveJob(npc.Id.Value);
        Notify(world, ledger, catalog, nowMs, npc, "fired", text, 0, null);
        ctx.LogEvent(WorldEventKind.Fired, $"{npc.Id.Value}|{job.Employer}", sourceSystem: SourceSystem);
    }

    /// <summary>A falta do dia: o corpo não saiu a tempo. Desempenho zero conta como turno ruim; faltas seguidas demitem.</summary>
    public static void Absent(WorldState world, TickContext ctx, Npc npc, long nowMs)
    {
        if (CatalogOf(world) is not { } catalog || world.Careers is not { } ledger || ledger.JobOf(npc.Id.Value) is not { ReturnMs: 0 } job || Resolve(catalog, job) is not { } r) return;
        job.ShiftDay = DayOf(world, nowMs);
        job.Absences++;
        job.Score = 0;
        if (job.Absences >= catalog.Rules.AbsencesToFire)
        {
            Fire(world, ctx, ledger, catalog, npc, job, $"Demitido: {job.Absences} faltas seguidas.", nowMs);
        }
        else
        {
            Notify(world, ledger, catalog, nowMs, npc, "absent", $"Faltou ao trabalho ({job.Absences} de {catalog.Rules.AbsencesToFire}). Sem pagamento.", 0, null);
            Evaluate(world, ctx, ledger, catalog, npc, job, r.Def, r.Level, 0, nowMs);
        }

        world.CareersChanged();
    }

    /// <summary>Desempenho (0–100) como a pessoa está: a média ponderada do nível das necessidades que a carreira cita e da habilidade (pesos do dado).</summary>
    public static double Performance(WorldState world, Npc npc, CareerDef def, long nowMs)
    {
        double sum = 0, weights = 0;
        foreach (var (need, weight) in def.Performance.Needs.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (weight <= 0) continue;
            sum += weight * (HomeLife.Value(world, npc, need, nowMs) ?? 50);
            weights += weight;
        }

        if (def.SkillId is { } skill && def.Performance.Skill > 0)
        {
            sum += def.Performance.Skill * Math.Clamp(npc.Skills.Get(new SkillType(skill)), 0, 100);
            weights += def.Performance.Skill;
        }

        return weights > 0 ? Math.Clamp(sum / weights, 0, 100) : 50;
    }

    // ---- procurar emprego ---------------------------------------------------------------------------------------------------------------------------------------------

    public sealed record Vacancy(CareerDef Career, Workplace Employer, int Free, CareerLevel Entry, double Chance);

    /// <summary>As vagas reais do mundo para a pessoa: empregadores vivos (com caixa para pagar o turno) do tipo que a carreira pede e com lugar livre no livro.</summary>
    public static IReadOnlyList<Vacancy> Vacancies(WorldState world, Npc npc)
    {
        var list = new List<Vacancy>();
        if (CatalogOf(world) is not { } catalog || npc.Household is null) return list;
        var ledger = world.Careers;
        foreach (var def in catalog.Careers)
        {
            var entry = def.Levels[0];
            foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
            {
                if (workplace.Closed || workplace.LocationType.Id != def.EmployerType) continue;
                int used = ledger is null ? 0 : ledger.Jobs.Count(j => j.Employer == workplace.Id.Value);
                if (used >= workplace.MaxVacancies || workplace.Treasury.Amount < entry.WagePerHour * entry.Hours * 3) continue;
                double skill = def.SkillId is { } sid ? npc.Skills.Get(new SkillType(sid)) : 0;
                if (skill < entry.MinSkill) continue;
                list.Add(new Vacancy(def, workplace, workplace.MaxVacancies - used, entry, Math.Clamp(def.Search.BaseChance + def.Search.PerSkill * skill, 0, 1)));
            }
        }

        return list;
    }

    /// <summary>O fim do uso de procurar emprego (jornal, computador): lista as vagas do mundo e vale por <c>searchValidHours</c>.</summary>
    public static void Search(WorldState world, Npc npc, long nowMs)
    {
        if (CatalogOf(world) is not { } catalog) return;
        var ledger = world.CareersLedger();
        ledger.Searches.RemoveAll(s => s.Npc == npc.Id.Value);
        ledger.Searches.Add(new JobSearched { Npc = npc.Id.Value, AtMs = nowMs });
        ledger.Searches.Sort((a, b) => a.Npc.CompareTo(b.Npc));
        var found = Vacancies(world, npc);
        string text = ledger.JobOf(npc.Id.Value) is not null ? "Você já tem emprego; as vagas ficam para consulta."
            : found.Count == 0 ? "Nenhuma vaga aberta agora."
            : $"{found.Count} vaga(s): {string.Join("; ", found.Select(v => $"{v.Entry.Title} ({v.Career.Name}, {v.Entry.WagePerHour}/h)"))}. Candidate-se pela roda do E: o corpo vai até o empregador.";
        Notify(world, ledger, catalog, nowMs, npc, "search", text, found.Count, null);
        world.CareersChanged();
    }

    public static bool SearchIsValid(WorldState world, CareerCatalog catalog, Npc npc, long nowMs) =>
        world.Careers?.SearchOf(npc.Id.Value) is { } s && nowMs - s.AtMs <= catalog.Rules.SearchValidHours * (long)WorldMillis.PerHour;

    /// <summary>Candidata-se à vaga: a chance (base + habilidade) é sorteada pelo RNG semeado do mundo; contratado entra no nível de entrada. Devolve se foi contratado e o texto (o motivo, se não).</summary>
    public static Result<(bool Hired, string Note)> Apply(WorldState world, Npc npc, string careerId, long workplaceId, long nowMs)
    {
        if (CatalogOf(world) is not { } catalog) return Result<(bool, string)>.Fail($"{PlayerCommands.Conflict} sem carreiras neste mundo");
        if (world.Careers?.JobOf(npc.Id.Value) is not null) return Result<(bool, string)>.Fail($"{PlayerCommands.Conflict} candidatura: você já tem emprego");
        if (!SearchIsValid(world, catalog, npc, nowMs)) return Result<(bool, string)>.Fail($"{PlayerCommands.Conflict} candidatura: procure as vagas antes (jornal ou computador)");
        var vacancy = Vacancies(world, npc).FirstOrDefault(v => v.Career.Id == careerId && v.Employer.Id.Value == workplaceId);
        if (vacancy is null) return Result<(bool, string)>.Fail($"{PlayerCommands.Conflict} candidatura: a vaga não existe mais");
        var ledger = world.CareersLedger();
        ledger.Searches.RemoveAll(s => s.Npc == npc.Id.Value);
        bool lucky = world.Rng.OneShot($"job-{npc.Id.Value}-{careerId}-{nowMs}").NextDouble() < vacancy.Chance;
        if (!lucky)
        {
            string no = $"Não foi dessa vez: {vacancy.Entry.Title} escolheu outra pessoa (sua chance era de {(int)Math.Round(vacancy.Chance * 100)}%).";
            Notify(world, ledger, catalog, nowMs, npc, "rejected", no, 0, null);
            world.CareersChanged();
            return Result<(bool, string)>.Ok((false, no));
        }

        var hired = Hire(world, npc, careerId, 0, vacancy.Employer, nowMs, announce: true);
        return hired.IsSuccess ? Result<(bool, string)>.Ok((true, $"Contratado: {vacancy.Entry.Title}.")) : Result<(bool, string)>.Fail($"{PlayerCommands.Conflict} candidatura: {hired.Error}");
    }

    // ---- candidatura guiada (#268) -----------------------------------------------------------------------------------------------------------------------------------

    /// <summary>A célula onde se candidata: a soleira do prédio do empregador (ou o ponto do local, sem prédio).</summary>
    private static CellCoord ApplyCell(WorldState world, Workplace employer) =>
        employer.Building is { } id && world.FindBuilding(id) is { } place && PlayerGround.DoorOf(place) is { } door ? door.Doorstep : employer.Location;

    /// <summary>#268: o jogador pede a candidatura. O motor valida (busca válida, vaga viva) e, se o cenário pede estar no empregador e o corpo está longe, põe o corpo a caminho
    /// (o mesmo "ir até" do jogo) com a candidatura guardada: ao chegar, ela corre (<see cref="OnArrived"/>). Já lá, a candidatura corre agora: contratado, recusado com o motivo ou, fora do
    /// atendimento, entrevista marcada para a próxima abertura. Devolve o desfecho (<c>done</c>, <c>refused</c>, <c>going</c>, <c>later</c>) e o texto.</summary>
    public static Result<(string Outcome, string Note)> BeginApply(WorldState world, TickContext ctx, Npc npc, string careerId, long workplaceId, long nowMs)
    {
        if (CatalogOf(world) is not { } catalog) return Result<(string, string)>.Fail($"{PlayerCommands.Conflict} sem carreiras neste mundo");
        if (world.Careers?.JobOf(npc.Id.Value) is not null) return Result<(string, string)>.Fail($"{PlayerCommands.Conflict} candidatura: você já tem emprego");
        if (!SearchIsValid(world, catalog, npc, nowMs)) return Result<(string, string)>.Fail($"{PlayerCommands.Conflict} candidatura: procure as vagas antes (jornal ou computador)");
        var vacancy = Vacancies(world, npc).FirstOrDefault(v => v.Career.Id == careerId && v.Employer.Id.Value == workplaceId);
        if (vacancy is null) return Result<(string, string)>.Fail($"{PlayerCommands.Conflict} candidatura: a vaga não existe mais");
        var ledger = world.CareersLedger();
        if (catalog.Rules.ApplyAtEmployer && world.FineTime)
        {
            var cell = ApplyCell(world, vacancy.Employer);
            var here = PlayerGround.PoseOf(world, npc);
            int reachCells = Math.Max(1, (int)Math.Ceiling(catalog.Rules.ApplyReachM / Math.Max(0.1, world.Players.CellM)));
            if (Math.Max(Math.Abs(here.X - cell.X), Math.Abs(here.Y - cell.Y)) > reachCells)
            {
                var begun = PlayerGo.Begin(world, ctx, npc, new PlayerGo.Target(cell, null), $"Candidatar-se: {vacancy.Career.Name}");
                if (!begun.IsSuccess) return Result<(string, string)>.Fail(begun.Error!);
                if (ledger.SearchOf(npc.Id.Value) is { } search) { search.PendingCareer = careerId; search.PendingEmployer = workplaceId; }
                world.CareersChanged();
                return Result<(string, string)>.Ok(("going", $"Indo ao {vacancy.Career.Name} para se candidatar a {vacancy.Entry.Title}."));
            }
        }

        return Decide(world, npc, catalog, vacancy, careerId, workplaceId, nowMs);
    }

    /// <summary>A candidatura já no empregador: fora do horário de atendimento vira entrevista (a busca segue valendo); dentro, o sorteio decide.</summary>
    private static Result<(string Outcome, string Note)> Decide(WorldState world, Npc npc, CareerCatalog catalog, Vacancy vacancy, string careerId, long workplaceId, long nowMs)
    {
        var rules = catalog.Rules;
        int hour = HourOfDay(world, nowMs);
        if (rules.ApplyAtEmployer && (hour < rules.ApplyOpenHour || hour >= rules.ApplyCloseHour))
        {
            long opens = DayOf(world, nowMs) * DayMs(world) + rules.ApplyOpenHour * (long)WorldMillis.PerHour;
            if (hour >= rules.ApplyOpenHour) opens += DayMs(world);
            string text = $"{vacancy.Career.Name} está fechado: entrevista marcada {When(world, opens)} para {vacancy.Entry.Title}.";
            var ledger = world.CareersLedger();
            if (ledger.SearchOf(npc.Id.Value) is { } search) { search.PendingCareer = null; search.PendingEmployer = 0; }
            Notify(world, ledger, catalog, nowMs, npc, "interview", text, opens, null);
            world.CareersChanged();
            return Result<(string, string)>.Ok(("later", text));
        }

        var applied = Apply(world, npc, careerId, workplaceId, nowMs);
        return applied.IsSuccess ? Result<(string, string)>.Ok((applied.Value.Hired ? "done" : "refused", applied.Value.Note)) : Result<(string, string)>.Fail(applied.Error!);
    }

    /// <summary>#268: o "ir até" terminou na célula <paramref name="cell"/>: se era a candidatura guardada (o corpo chegou ao empregador), ela corre agora e o resultado vai aos avisos.</summary>
    public static void OnArrived(WorldState world, TickContext ctx, Npc npc, CellCoord cell)
    {
        if (CatalogOf(world) is not { } catalog || world.Careers?.SearchOf(npc.Id.Value) is not { PendingCareer: { } careerId } search) return;
        long workplaceId = search.PendingEmployer;
        search.PendingCareer = null;
        search.PendingEmployer = 0;
        world.CareersChanged();
        if (world.FindWorkplace(new WorkplaceId(workplaceId)) is not { } employer || ApplyCell(world, employer) != cell) return;
        long nowMs = world.CurrentDate.TotalMilliseconds;
        var vacancy = Vacancies(world, npc).FirstOrDefault(v => v.Career.Id == careerId && v.Employer.Id.Value == workplaceId);
        if (vacancy is null) { Notify(world, world.CareersLedger(), catalog, nowMs, npc, "rejected", "A vaga não existe mais.", 0, null); world.CareersChanged(); return; }
        Decide(world, npc, catalog, vacancy, careerId, workplaceId, nowMs);
    }

    // ---- contas da casa ---------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>O uso mede a conta: soma as horas de uso do objeto às contas da casa que o citam (água do chuveiro, da pia, do vaso).</summary>
    public static void Meter(WorldState world, Npc npc, ObjectUseDef use, double hours)
    {
        if (world.Careers is not { Bills.Count: > 0 } ledger || CatalogOf(world) is not { } catalog || npc.Household is not { } household) return;
        foreach (var bill in catalog.Bills)
            if (bill.PerHour > 0 && (bill.MeterUses.Contains(use.Id) || use.Toggle is { } t && bill.MeterUses.Contains(t)) && ledger.BillOf(household.Value, bill.Id) is { } state)
                state.Usage += hours;
        world.CareersChanged();
    }

    /// <summary>O uso está cortado por conta atrasada? Devolve o motivo (a conta e o que falta) ou nulo.</summary>
    public static string? BlockedUse(WorldState world, Npc npc, ObjectUseDef use)
    {
        if (world.Careers is not { Bills.Count: > 0 } ledger || CatalogOf(world) is not { } catalog || npc.Household is not { } household) return null;
        foreach (var state in ledger.Bills)
        {
            if (state.Household != household.Value || !state.Cut || catalog.FindBill(state.Bill) is not { } bill) continue;
            if (bill.Blocks.Contains(use.Id) || use.Toggle is { } t && bill.Blocks.Contains(t)) return $"{bill.Name} cortada: conta em atraso (pague nas Contas)";
        }

        return null;
    }

    /// <summary>Paga a conta aberta (com a multa): a carteira de quem paga primeiro, depois a dos outros moradores em ordem de id; o valor vai ao tesouro da cidade. Paga, o serviço volta.</summary>
    public static Result<string> PayBill(WorldState world, TickContext ctx, Npc payer, string billId, long nowMs)
    {
        if (CatalogOf(world) is not { } catalog || world.Careers is not { } ledger || payer.Household is not { } hid || world.FindHousehold(hid) is not { } household
            || catalog.FindBill(billId) is not { } bill || ledger.BillOf(hid.Value, billId) is not { Open: > 0 } state)
            return Result<string>.Fail($"{PlayerCommands.Conflict} contas: não há essa conta em aberto");
        long total = state.Open + state.Fee;
        var payers = new List<Npc> { payer };
        payers.AddRange(household.Members.OrderBy(m => m.Value).Where(m => m != payer.Id).Select(world.FindNpc).OfType<Npc>().Where(n => n.IsAlive));
        if (payers.Sum(n => n.Wallet.Amount) < total) return Result<string>.Fail($"{PlayerCommands.Conflict} contas: faltam {total - payers.Sum(n => n.Wallet.Amount)} para pagar {bill.Name} ({total})");
        long left = total;
        foreach (var n in payers)
        {
            long take = Math.Min(left, n.Wallet.Amount);
            if (take > 0 && n.TryDebitWallet(new Money(take)).IsSuccess) left -= take;
            if (left == 0) break;
        }

        if (world.FindCity(household.City) is { } city)
        {
            city.CreditTreasury(new Money(total));
            world.RecordTreasury(city.Id, ctx.CurrentTick, revenue: total, spending: 0);
        }

        state.Paid += total;
        state.Open = state.Fee = 0;
        state.Warned = false;
        bool restored = state.Cut;
        state.Cut = false;
        Notify(world, ledger, catalog, nowMs, payer, "bill-paid", $"{bill.Name} paga: -{total}.", total, null);
        if (restored) Notify(world, ledger, catalog, nowMs, payer, "bill-restored", $"{bill.Name}: serviço religado.", 0, null);
        world.CareersChanged();
        return Result<string>.Ok($"{bill.Name} paga: {total}");
    }

    /// <summary>A rotina diária das contas (na hora do dia que o dado diz): emite a conta do período (fixo + uso medido), avisa um dia antes do vencimento, cobra multa uma vez e
    /// corta o serviço depois dos dias de atraso. Casa em que ninguém é conduzido por jogador paga sozinha ao vencer, se tem com que pagar; a do jogador espera o pagamento dele.</summary>
    public static void TickBills(WorldState world, TickContext ctx, long nowMs)
    {
        if (world.Careers is not { Bills.Count: > 0 } ledger || CatalogOf(world) is not { } catalog) return;
        long day = DayOf(world, nowMs);
        foreach (var state in ledger.Bills.ToList())
        {
            if (catalog.FindBill(state.Bill) is not { } bill || world.FindHousehold(new HouseholdId(state.Household)) is not { } household) continue;
            var members = household.Members.Select(world.FindNpc).OfType<Npc>().Where(n => n.IsAlive).ToList();
            if (members.Count == 0) continue;
            var voice = members.FirstOrDefault(n => n.ControllerId is not null) ?? members[0];
            bool human = members.Any(n => n.ControllerId is not null);
            if (state.Open == 0 && day >= state.NextIssueDay)
            {
                long amount = bill.Fixed + (long)Math.Round(state.Usage * bill.PerHour, MidpointRounding.AwayFromZero);
                state.Usage = 0;
                state.NextIssueDay += bill.EveryDays;
                if (amount > 0)
                {
                    state.Open = amount;
                    state.Fee = 0;
                    state.Warned = false;
                    state.DueMs = (day + bill.DueDays) * DayMs(world) + catalog.Rules.BillHour * (long)WorldMillis.PerHour;
                    Notify(world, ledger, catalog, nowMs, voice, "bill-issued", $"Conta de {bill.Name}: {amount}, vence {Clock(world, state.DueMs)}.", amount, "pay");
                }
            }

            if (state.Open == 0) continue;
            if (!human && nowMs >= state.DueMs - DayMs(world) && PayBill(world, ctx, voice, bill.Id, nowMs).IsSuccess) continue; // quem ninguém conduz paga a conta antes do vencimento, se pode
            if (!state.Warned && nowMs >= state.DueMs - DayMs(world) && nowMs < state.DueMs)
            {
                state.Warned = true;
                Notify(world, ledger, catalog, nowMs, voice, "bill-due", $"A conta de {bill.Name} vence amanhã: {state.Open}.", state.Open, "pay");
            }

            if (nowMs < state.DueMs) continue;
            if (state.Fee == 0 && bill.LateFeeRate > 0)
            {
                state.Fee = (long)Math.Ceiling(state.Open * bill.LateFeeRate);
                Notify(world, ledger, catalog, nowMs, voice, "bill-late", $"Conta de {bill.Name} atrasada: multa de {state.Fee}.", state.Fee, "pay");
            }

            if (!state.Cut && (nowMs - state.DueMs) / DayMs(world) >= bill.CutAfterDays && bill.Blocks.Count > 0)
            {
                state.Cut = true;
                Notify(world, ledger, catalog, nowMs, voice, "bill-cut", $"{bill.Name} cortada por falta de pagamento.", state.Open + state.Fee, "pay");
            }
        }

        world.CareersChanged();
    }

    /// <summary>A luz acesa na casa mede as contas que citam <c>light:on</c>: uma hora de cada lâmpada ligada (amostra horária).</summary>
    public static void MeterLights(WorldState world)
    {
        if (world.Lights is not { On.Count: > 0 } lights || world.Careers is not { Bills.Count: > 0 } ledger || CatalogOf(world) is not { } catalog) return;
        foreach (var bill in catalog.Bills)
        {
            if (bill.PerHour <= 0 || !bill.MeterUses.Contains(ObjectToggles.LightOn)) continue;
            foreach (var state in ledger.Bills)
                if (state.Bill == bill.Id && world.FindHousehold(new HouseholdId(state.Household)) is { Dwelling: { } dwelling })
                    state.Usage += lights.On.Count(l => l.Building == dwelling.Value);
        }
    }

    public static string Clock(WorldState world, long ms)
    {
        int minutes = (int)(ms % DayMs(world) / 60_000);
        long today = DayOf(world, world.CurrentDate.TotalMilliseconds), day = DayOf(world, ms);
        string when = day == today ? "" : day == today + 1 ? "amanhã " : $"dia {day} ";
        return $"{when}{minutes / 60:00}:{minutes % 60:00}";
    }

    /// <summary>#268: a hora do jogo por extenso: "hoje às 14:30", "amanhã às 08:12", "dia 5 às 09:00" (a hora é a do relógio do mundo, calculada pelo motor).</summary>
    public static string When(WorldState world, long ms)
    {
        int minutes = (int)(ms % DayMs(world) / 60_000);
        long today = DayOf(world, world.CurrentDate.TotalMilliseconds), day = DayOf(world, ms);
        string when = day == today ? "hoje" : day == today + 1 ? "amanhã" : $"dia {day}";
        return $"{when} às {minutes / 60:00}:{minutes % 60:00}";
    }
}
