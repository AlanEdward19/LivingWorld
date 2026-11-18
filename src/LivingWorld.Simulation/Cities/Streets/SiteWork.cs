using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Works;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Streets;

/// <summary>Uma obra com turma (AD-179, AD-184): o trecho de calçamento (<see cref="Paving"/>), o prédio na fila da cidade
/// (<see cref="Project"/>, com <see cref="City"/>) ou o prédio encomendado por uma casa ou um fundador (<see cref="Build"/>).</summary>
internal readonly record struct WorkSite(PavingWork? Paving, City? City, ConstructionProject? Project, BuildWork? Build = null)
{
    public List<long> Crew => Paving?.Crew ?? Build?.Crew ?? Project!.Crew;
    public Guid CityId => Paving?.City ?? Build?.City ?? City!.Id.Value;
    public long Id => Paving?.Id ?? Build?.Id ?? Project!.WorkId ?? -1;
    public string Label => Paving is not null ? Paving.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : Build is not null ? Build.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : Project!.WorkId is { } id ? id.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : $"predio-{Project.BuildingTypeId}@{Project.SiteX}:{Project.SiteY}";
    public int OfferPm => Paving is not null ? Paving.OfferPm ?? 1000 : Build is not null ? Build.OfferPm : Project!.OfferPm ?? 1000;
    public long LastRaiseTick => Paving is not null ? Paving.LastRaiseTick ?? 0 : Build is not null ? Build.LastRaiseTick : Project!.LastRaiseTick ?? 0;
}

/// <summary>Termos da oferta da obra para um NPC (AD-184), o que a decisão de aceitar pesa: o jornal oferecido e o que ele ganha
/// hoje (por dia), a necessidade da casa, a parte da jornada que a ida deixa, o valor final e, se zero, o motivo.</summary>
public readonly record struct SiteTerms(double Value, double OfferDay, double WageDay, double Need, double Share, string Reason);

/// <summary>Trabalho na obra (AD-179, AD-184): toda obra é um lugar de trabalho de verdade. O diarista aceitou pela utilidade dele
/// (<see cref="Evaluate"/>: o jornal oferecido contra o que ganha hoje, a necessidade da casa e a ida pela rota); o desempregado
/// aceita primeiro, o empregado só larga o ofício se o jornal vale <see cref="StreetRules.QuitMargin"/> do salário. Quem encomenda
/// sobe a oferta toda semana sem braço bastante, até o que pode pagar. Vai e volta pela rota nas horas da rotina, e a obra só anda
/// pelas horas de quem está NELA quando a jornada acaba (<see cref="Clock"/>), paga na hora. Na obra de casa e de oficina, a
/// família, os parentes e os vizinhos de afeto ajudam nas horas livres sem jornal (<see cref="BuildWorks"/>).</summary>
public static class SiteWork
{
    public const string SourceSystem = "site-work";

    private sealed record ProjectRef(City City, ConstructionProject Project);

    /// <summary>Alguém entrou ou saiu de uma turma (ou a obra acabou): o índice de quem trabalha onde se refaz na próxima consulta.</summary>
    internal static void Touch(WorldState world)
    {
        if (world.Ground is { } ground) ground.SiteIndex = null;
    }

    private static Dictionary<long, object> Index(WorldState world)
    {
        var ground = world.Ground!;
        if (ground.SiteIndex is { } built) return built;
        var index = new Dictionary<long, object>();
        foreach (var work in ground.Works.OrderBy(w => w.Id))
            foreach (long id in work.Crew) index.TryAdd(id, work);
        foreach (var work in ground.Builds.OrderBy(w => w.Id))
            foreach (long id in work.Crew) index.TryAdd(id, work);
        if (world.StreetRules.BuildNeedsCrew && world.CityRules.Enabled)
            foreach (var city in world.ActiveCities().OrderBy(c => c.Id.Value))
                foreach (var project in city.ConstructionQueue)
                    foreach (long id in project.Crew) index.TryAdd(id, new ProjectRef(city, project));
        // ajudantes sem jornal, com a chave negativa (o diarista vem antes)
        foreach (var work in ground.Builds.OrderBy(w => w.Id))
            foreach (long id in work.Helpers) index.TryAdd(-id - 1, work);
        ground.SiteIndex = index;
        return index;
    }

    /// <summary>As obras que contratam agora, em ordem: os trechos por id, o primeiro prédio por pagar de cada cidade (o que a fila
    /// está construindo) com o lote já escolhido, e as obras encomendadas por id.</summary>
    internal static IEnumerable<WorkSite> Sites(WorldState world)
    {
        if (!world.StreetsOn) yield break;
        foreach (var work in world.Ground!.Works.OrderBy(w => w.Id)) yield return new WorkSite(work, null, null);
        if (world.StreetRules.BuildNeedsCrew && world.CityRules.Enabled)
            foreach (var city in world.ActiveCities().OrderBy(c => c.Id.Value))
                if (city.ConstructionQueue.FirstOrDefault(p => p.TicksRemaining > 0) is { SiteX: >= 0 } project)
                    yield return new WorkSite(null, city, project);
        foreach (var build in world.Ground.Builds.OrderBy(w => w.Id)) yield return new WorkSite(null, null, null, build);
    }

    /// <summary>A obra de que o NPC é diarista; nula fora de obra.</summary>
    internal static WorkSite? WorkOf(WorldState world, Npc npc)
    {
        if (!world.StreetsOn || !Index(world).TryGetValue(npc.Id.Value, out var found)) return null;
        return found switch
        {
            PavingWork work => new WorkSite(work, null, null),
            BuildWork build => new WorkSite(null, null, null, build),
            ProjectRef p => new WorkSite(null, p.City, p.Project),
            _ => null,
        };
    }

    /// <summary>A obra em que o NPC ajuda sem jornal (família, parente ou vizinho); nula se não ajuda.</summary>
    internal static BuildWork? HelpOf(WorldState world, Npc npc) =>
        world.StreetsOn && world.Ground!.Builds.Count > 0 && Index(world).TryGetValue(-npc.Id.Value - 1, out var found) ? found as BuildWork : null;

    /// <summary>É ajudante de alguma obra (as horas dele na obra contam, sem jornal).</summary>
    internal static bool IsHelper(WorldState world, Npc npc) => HelpOf(world, npc) is not null;

    /// <summary>Agora é hora livre de obra para o ajudante: de dia (antes de <see cref="StreetRules.BuildDaylightEndHour"/>) e, se tem
    /// emprego, fora das horas de trabalho da rotina dele.</summary>
    internal static bool HelpingNow(WorldState world, Npc npc)
    {
        if (HelpOf(world, npc) is not { } work || npc.City.Value != work.City) return false;
        int hpd = world.Calendar.HoursPerDay, hour = world.CurrentDate.Hour;
        if (hour >= world.StreetRules.BuildDaylightEndHour || hour < hpd / 4) return false;
        // com fome ou sede abaixo do piso a hora livre é de comer e beber em casa, não de levantar parede
        long now = world.CurrentDate.TotalHours;
        if (npc.HungerAt(now) < world.StreetRules.HelperNeedFloor || npc.ThirstAt(now) < world.StreetRules.HelperNeedFloor) return false;
        // AD-185: a casa sem água bastante manda primeiro à fonte (quem ajuda na obra é quem iria buscá-la)
        if (LivingWorld.Simulation.Economy.Supply.WaterFetch.HomeNeedsWater(world, npc)) return false;
        if (npc.Employer is null) return true;
        var stage = world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate));
        var profession = npc.Profession == ProfessionType.None ? (ProfessionType?)null : npc.Profession;
        return LivingWorld.Simulation.Behavior.Needs.RoutineBoundaries.RoutineAt(world.ActionCatalog, profession, stage, hour, hpd)
            is not (LivingWorld.Domain.Behavior.ActionType.Work or LivingWorld.Domain.Behavior.ActionType.Sleep);
    }

    /// <summary>A célula onde a turma trabalha: a primeira do trecho, ou o lote do prédio.</summary>
    internal static CellCoord SiteOf(WorldState world, WorkSite site) =>
        site.Paving is { } work ? world.Ground!.CellOf(work.Cells[0])
        : site.Build is { } build ? new CellCoord(build.X, build.Y)
        : new CellCoord(site.Project!.SiteX, site.Project.SiteY);

    internal static CellCoord SiteOf(WorldState world, PavingWork work) => SiteOf(world, new WorkSite(work, null, null));

    /// <summary>Para onde o diarista vai trabalhar (ou o ajudante, na hora livre); nulo se não trabalha em obra.
    /// AD-180: com chuva forte, neve ou chão congelado ninguém vai (não trabalha nem recebe no dia).</summary>
    internal static CellCoord? DestinationOf(WorldState world, Npc npc)
    {
        CellCoord? cell = WorkOf(world, npc) is { } site ? SiteOf(world, site)
            : HelpingNow(world, npc) && HelpOf(world, npc) is { } build ? new CellCoord(build.X, build.Y) : null;
        return cell is { } c && LivingWorld.Simulation.Climate.WeatherNow.OutdoorWorkable(world, c) ? c : null;
    }

    /// <summary>Jornal de uma hora: o jornal do dia (<see cref="PavingDecision.DayWage"/>) pelas horas da jornada.</summary>
    internal static long HourWage(WorldState world) =>
        Math.Max(1, PavingDecision.DayWage(world) / Math.Max(1, world.StreetRules.CrewHoursPerDay));

    /// <summary>O que a jornada de <paramref name="hours"/> horas paga ao diarista: com a oferta da AD-184, o jornal do dia pela oferta
    /// e pelas horas (arredondado, pelo menos 1); sem ela, a hora cheia de antes.</summary>
    internal static long DueFor(WorldState world, WorkSite site, long hours)
    {
        if (!world.StreetRules.WageBidding) return hours * HourWage(world);
        double day = PavingDecision.DayWage(world) * site.OfferPm / 1000.0;
        return Math.Max(1, (long)Math.Round(hours * day / Math.Max(1, world.StreetRules.CrewHoursPerDay), MidpointRounding.AwayFromZero));
    }

    public static double OfferValue(WorldState world, Npc npc, PavingWork work) => OfferValue(world, npc, new WorkSite(work, null, null));

    internal static double OfferValue(WorldState world, Npc npc, WorkSite site) => Evaluate(world, npc, site).Value;

    /// <summary>Quanto a oferta de trabalho na obra pesa para o NPC (termo <c>SiteWork</c> da utilidade de trabalhar) e por quê. Zero
    /// para quem não é adulto, mora fora da cidade, é da casa que encomenda (essa ajuda sem jornal) ou para quem a ida come o dia.
    /// Sem emprego: <see cref="StreetRules.SiteOfferMax"/> × o maior entre a necessidade (o que falta de moeda na casa para a reserva,
    /// ou na despensa) e o atrativo do jornal (metade no jornal do dia, cheio no dobro) × a parte da jornada que sobra da ida e
    /// volta. Com emprego (AD-184): só se o jornal da obra passa o salário pela margem da troca, na proporção do que passa, mais
    /// forte com a necessidade; o dono da oficina e quem trocou de emprego há pouco não largam o ofício.</summary>
    internal static SiteTerms Evaluate(WorldState world, Npc npc, WorkSite site)
    {
        var rules = world.StreetRules;
        long dayWage = PavingDecision.DayWage(world);
        double offerDay = rules.WageBidding ? dayWage * site.OfferPm / 1000.0 : dayWage;
        double wageDay = WageDayOf(world, npc);
        if (npc.Employer is not null && !rules.WageBidding) return new(0, offerDay, wageDay, 0, 0, "emprego");
        if (npc.City.Value != site.CityId) return new(0, offerDay, wageDay, 0, 0, "outra-cidade");
        if (world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) != LifeStage.Adult) return new(0, offerDay, wageDay, 0, 0, "idade");
        if (site.Build is { } own && npc.Household is { } mine && mine.Value == own.Household) return new(0, offerDay, wageDay, 0, 0, "da-casa");
        var home = npc.Household is { } h ? world.FindHousehold(h) : null;
        var from = home?.Location ?? npc.CurrentLocation;
        double commute = StreetRoutes.CostOf(world, from, SiteOf(world, site)).Cost;
        double hours = rules.CrewHoursPerDay;
        double share = (hours - 2 * commute) / hours;
        if (!(share > 0)) return new(0, offerDay, wageDay, 0, 0, "longe");
        long reserve = dayWage * rules.SiteReserveDays;
        long means = home is null ? npc.Wallet.Amount : PavingDecision.MeansOf(world, home.Id.Value);
        double money = reserve <= 0 ? 0 : Math.Clamp(1.0 - (double)means / reserve, 0, 1);
        double pantry = home is null ? 0 : PantryShortfall(world, home);
        double need = Math.Max(money, pantry);
        if (npc.Employer is { } employerId)
        {
            var employer = world.FindWorkplace(employerId);
            if (employer?.Owner == npc.Id) return new(0, offerDay, wageDay, need, share, "dono");
            long cooldown = 30L * world.Calendar.HoursPerDay;
            if (npc.LastJobChangeTick > 0 && world.CurrentDate.TotalHours - npc.LastJobChangeTick < cooldown)
                return new(0, offerDay, wageDay, need, share, "carencia");
            double gain = offerDay <= 0 ? 0 : Math.Clamp((offerDay - wageDay * MarginFor(world, npc)) / offerDay, 0, 1);
            if (gain <= 0) return new(0, offerDay, wageDay, need, share, "salario");
            return new(rules.SiteOfferMax * gain * (0.5 + 0.5 * need) * share, offerDay, wageDay, need, share, "aceita");
        }
        // o jornal atrai quem não tem trabalho, menos quanto mais a casa tem guardado (quem tem muitas reservas não vai à obra)
        double comfort = reserve <= 0 ? 0 : Math.Clamp((double)means / (reserve * Math.Max(1, rules.SiteComfortReserves)), 0, 1);
        double pull = rules.WageBidding && dayWage > 0 ? 0.5 * Math.Min(2.0, offerDay / dayWage) * (1 - comfort) : 0;
        double value = rules.SiteOfferMax * Math.Max(need, pull) * share;
        return new(value, offerDay, wageDay, need, share, value > 0 ? "aceita" : "sem-necessidade");
    }

    /// <summary>Quanto o jornal da obra tem de passar o salário para o empregado largar o ofício: a margem da troca de emprego; para
    /// quem lavra, a da entressafra fora da semeadura e da ceifa (a fazenda toma todo braço de volta na ceifa, então largar custa
    /// pouco) e a do pico nelas (ninguém larga a foice no meio da colheita).</summary>
    internal static double MarginFor(WorldState world, Npc npc)
    {
        var rules = world.StreetRules;
        var agri = world.AgricultureRules;
        if (!agri.Enabled || npc.Employer is not { } id || world.FindWorkplace(id) is not { } place || world.FindCropAt(place.Location) is null)
            return rules.QuitMargin;
        int month = world.CurrentDate.Month;
        bool peak = agri.IsSowingMonth(month) || (month >= agri.EarliestRipeningMonth && month <= agri.ExpectedHarvestMonth + 1);
        return peak ? rules.QuitMarginPeak : rules.QuitMarginSlack;
    }

    /// <summary>O que o NPC ganha hoje por dia no ofício (o salário do mês pelos dias); zero sem emprego ou com a folha atrasada.</summary>
    internal static double WageDayOf(WorldState world, Npc npc)
    {
        if (npc.Employer is not { } id || world.FindWorkplace(id) is not { } place || place.PayrollFailed) return 0;
        return world.EconomyRules.WageByProfession.TryGetValue(npc.Profession.Id, out long wage)
            ? wage / (double)Math.Max(1, world.Calendar.DaysPerMonth) : 0;
    }

    /// <summary>A oferta da obra em que o NPC trabalha, ou a melhor das que contratam na cidade dele, para o contexto da decisão.</summary>
    internal static double OfferFor(WorldState world, Npc npc)
    {
        if (npc.Employer is not null || !world.StreetsOn) return 0;
        var ground = world.Ground!;
        if (ground.Works.Count == 0 && ground.Builds.Count == 0 && !(world.StreetRules.BuildNeedsCrew && world.CityRules.Enabled)) return 0;
        if (WorkOf(world, npc) is { } mine) return OfferValue(world, npc, mine);
        // AD-184: quem não está na turma não tem para onde ir trabalhar; entrar é a decisão diária da turma (Staff), que já pesa a
        // oferta de cada obra. Varrer todas as obras a cada decisão custava o triplo do tick com as casas em obra.
        if (world.StreetRules.WageBidding) return 0;
        double best = 0;
        foreach (var site in Sites(world))
            if (site.Crew.Count < CrewTarget(world, site)) best = Math.Max(best, OfferValue(world, npc, site));
        return best;
    }

    /// <summary>A melhor obra para o NPC e os termos (painel do cérebro): a dele, se trabalha ou ajuda numa; senão a de maior valor
    /// entre as que contratam na cidade dele. Nula sem obra.</summary>
    public static (long WorkId, string Role, CellCoord Site, bool Joined, SiteTerms Terms)? Explain(WorldState world, Npc npc)
    {
        if (!world.StreetsOn) return null;
        if (WorkOf(world, npc) is { } mine) return (mine.Id, "diarista", SiteOf(world, mine), true, Evaluate(world, npc, mine));
        if (HelpOf(world, npc) is { } help)
        {
            var site = new WorkSite(null, null, null, help);
            string role = npc.Household is { } h && h.Value == help.Household ? "familia" : BuildWorks.IsKin(world, npc, help) ? "parente" : "mutirao";
            return (help.Id, role, SiteOf(world, site), true, Evaluate(world, npc, site) with { Reason = role });
        }
        (WorkSite Site, SiteTerms Terms)? best = null;
        foreach (var site in Sites(world))
        {
            if (site.CityId != npc.City.Value || CrewTarget(world, site) == 0) continue;
            var terms = Evaluate(world, npc, site);
            if (best is null || terms.Value > best.Value.Terms.Value) best = (site, terms);
        }
        if (best is not { } chosen) return null;
        bool full = chosen.Site.Crew.Count >= CrewTarget(world, chosen.Site);
        var reason = chosen.Terms.Value >= world.StreetRules.SiteJoinMin ? full ? "turma-cheia" : "aceita"
            : chosen.Terms.Reason == "aceita" ? "abaixo-do-minimo" : chosen.Terms.Reason;
        return (chosen.Site.Id, "candidato", SiteOf(world, chosen.Site), false, chosen.Terms with { Reason = reason });
    }

    private static double PantryShortfall(WorldState world, LivingWorld.Domain.Population.Family.Household home)
    {
        var economy = world.EconomyRules;
        if (!economy.Enabled || home.Members.Count == 0) return 0;
        long scarcest = Math.Min(home.Stock.GetValueOrDefault(new ResourceType(economy.FoodResourceId)),
            home.Stock.GetValueOrDefault(new ResourceType(economy.WaterResourceId)));
        double days = (double)scarcest / home.Members.Count;
        return Math.Clamp(1.0 - days / Math.Max(1, world.StreetRules.SiteReserveDays / 3.0), 0, 1);
    }

    /// <summary>Moeda que quem encomenda tem para pagar jornal agora: o tesouro (cidade, ou trecho sem casas de frente), o que as casas
    /// de frente têm, a casa da família menos a reserva dela, ou o que o fundador guardou para a obra.</summary>
    internal static long Budget(WorldState world, WorkSite site)
    {
        if (site.Build is { } build)
        {
            if (build.Kind == BuildKind.Workshop) return Math.Max(0, build.Escrow - build.Reserve);
            // a casa guarda a reserva de cada morador e põe na obra metade do que passa dela (o resto é a vida que segue)
            int members = world.FindHousehold(new HouseholdId(build.Household))?.Members.Count ?? 1;
            long reserve = PavingDecision.DayWage(world) * world.StreetRules.SiteReserveDays * Math.Max(1, members);
            return Math.Max(0, PavingDecision.MeansOf(world, build.Household) - reserve) / 2;
        }
        if (site.Paving is { Payers.Count: > 0 } work)
        {
            long least = long.MaxValue;
            foreach (long payer in work.Payers) least = Math.Min(least, PavingDecision.MeansOf(world, payer));
            return least == long.MaxValue ? 0 : least * work.Payers.Count;
        }
        return world.FindActiveCity(new CityId(site.CityId)) is { } city ? city.Treasury.Amount : 0;
    }

    /// <summary>Horas que ainda faltam na obra (o teto da oferta se conta sobre elas).</summary>
    private static long HoursLeftOf(WorldState world, WorkSite site) =>
        site.Paving is { } w ? w.HoursLeft
        : site.Build is { } b ? b.HoursLeft
        : site.Project!.TicksRemaining + site.Project.HaulHoursLeft - site.Project.HoursBanked;

    /// <summary>Diaristas que quem encomenda quer: a turma cheia na obra pública; na encomendada, quantos o dinheiro paga (a casa que só
    /// tem os próprios braços não contrata ninguém).</summary>
    internal static int CrewTarget(WorldState world, WorkSite site)
    {
        int max = world.StreetRules.CrewMax;
        if (site.Build is null || !world.StreetRules.WageBidding) return site.Build is null ? max : 0;
        long hours = Math.Max(1, HoursLeftOf(world, site));
        double hourCost = PavingDecision.DayWage(world) * site.OfferPm / 1000.0 / Math.Max(1, world.StreetRules.CrewHoursPerDay);
        double affordable = Budget(world, site) / Math.Max(1e-9, hours * hourCost);
        return (int)Math.Clamp(Math.Floor(max * affordable), 0, max);
    }

    internal static void Staff(WorldState world, TickContext ctx, PavingWork work) => Staff(world, ctx, new WorkSite(work, null, null));

    /// <summary>Diária: a turma de uma obra. Sai quem morreu, arrumou emprego, mudou de cidade ou já não quer (valor abaixo da metade
    /// do mínimo); entra, até o que quem encomenda quer, quem aceita (valor a partir de <see cref="StreetRules.SiteJoinMin"/>), quem
    /// mais quer primeiro (empate por id). O empregado que aceita larga o ofício. Quem já está noutra obra não entra nesta. Sem braço
    /// bastante, a oferta sobe uma vez por semana até o que quem encomenda pode pagar.</summary>
    internal static void Staff(WorldState world, TickContext ctx, WorkSite site)
    {
        var rules = world.StreetRules;
        var crew = site.Crew;
        bool changed = false;
        for (int i = crew.Count - 1; i >= 0; i--)
        {
            var npc = world.FindNpc(new NpcId(crew[i]));
            if (npc is { IsAlive: true } && npc.Employer is null && OfferValue(world, npc, site) >= rules.SiteJoinMin / 2.0) continue;
            string why = npc is not { IsAlive: true } ? "morto" : npc.Employer is not null ? "emprego" : "saiu";
            ctx.LogEvent(WorldEventKind.SiteWorkLeft, $"{crew[i]}|{site.Label}|{why}", sourceSystem: SourceSystem);
            crew.RemoveAt(i);
            changed = true;
        }
        int target = CrewTarget(world, site);
        if (crew.Count < target)
        {
            var cityId = new CityId(site.CityId);
            var index = Index(world);
            List<(double Value, Npc Npc)>? willing = null;
            double offerDay = rules.WageBidding ? PavingDecision.DayWage(world) * site.OfferPm / 1000.0 : 0;
            foreach (var npc in world.AliveNpcIndex.Alive)
            {
                if (npc.City != cityId || index.ContainsKey(npc.Id.Value) || index.ContainsKey(-npc.Id.Value - 1)) continue;
                if (npc.Employer is not null && (!rules.WageBidding || WageDayOf(world, npc) * MarginFor(world, npc) >= offerDay)) continue;
                double value = OfferValue(world, npc, site);
                if (value >= rules.SiteJoinMin) (willing ??= []).Add((value, npc));
            }
            if (willing is not null)
            {
                willing.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : a.Npc.Id.Value.CompareTo(b.Npc.Id.Value));
                foreach (var (value, npc) in willing)
                {
                    if (crew.Count >= target) break;
                    if (npc.Employer is { } employerId) Quit(world, ctx, npc, employerId, site);
                    crew.Add(npc.Id.Value);
                    changed = true;
                    ctx.LogEvent(WorldEventKind.SiteWorkJoined, $"{npc.Id.Value}|{site.Label}|{(long)Math.Round(value * 100)}", sourceSystem: SourceSystem);
                }
            }
            if (crew.Count < target) Raise(world, ctx, site);
        }
        if (changed) Touch(world);
    }

    /// <summary>O empregado larga o ofício pela obra (saiu, não foi demitido): a vaga fica livre e a carência de troca começa.</summary>
    private static void Quit(WorldState world, TickContext ctx, Npc npc, WorkplaceId employerId, WorkSite site)
    {
        world.FindWorkplace(employerId)?.Fire(npc.Id);
        npc.Fire();
        npc.MarkJobChange(ctx.CurrentTick);
        world.Ground!.OnLeave.Add([npc.Id.Value, employerId.Value]); // o lugar dele: volta quando a obra acaba, se houver vaga
        long? cause = site.Paving?.StartEventId ?? site.Build?.StartEventId ?? site.Project!.StartEventId;
        ctx.LogEvent(WorldEventKind.Fired, $"{npc.Id.Value}|{employerId.Value}|obra-{site.Label}", sourceSystem: SourceSystem,
            causeEventId: cause is > 0 ? cause : null);
    }

    /// <summary>Sem braço bastante: a oferta sobe <see cref="StreetRules.OfferRaisePm"/> por semana, até o teto e até o que quem
    /// encomenda pode pagar pelas horas que faltam. O calçamento não sobe: ele só abriu porque o tempo poupado paga a obra no jornal
    /// do dia (<see cref="PavingDecision"/>); pagar o triplo tirava o ferreiro da forja para assentar cascalho e a metrópole ficava
    /// sem ferramenta (medido: seed 7, 20 anos, de 507 para 227 moradores).</summary>
    private static void Raise(WorldState world, TickContext ctx, WorkSite site)
    {
        var rules = world.StreetRules;
        if (!rules.WageBidding || site.Paving is not null) return;
        long now = ctx.CurrentTick, week = 7L * world.Calendar.HoursPerDay;
        long last = site.LastRaiseTick;
        if (last > 0 && now - last < week) return;
        long hours = Math.Max(1, HoursLeftOf(world, site));
        long dayWage = Math.Max(1, PavingDecision.DayWage(world));
        double cap = (double)Budget(world, site) * 1000 * Math.Max(1, rules.CrewHoursPerDay) / (hours * (double)dayWage);
        int next = (int)Math.Min(Math.Min(rules.OfferMaxPm, cap), site.OfferPm + rules.OfferRaisePm);
        if (next <= site.OfferPm)
        {
            if (last == 0) SetRaise(site, site.OfferPm, now);
            return;
        }
        SetRaise(site, next, now);
        world.CanonicalHashCache.MarkPropertyDirty(site.Project is not null ? nameof(WorldState.Cities) : nameof(WorldState.Ground));
    }

    private static void SetRaise(WorkSite site, int offerPm, long now)
    {
        if (site.Paving is { } w) { w.OfferPm = offerPm; w.LastRaiseTick = now; }
        else if (site.Build is { } b) { b.OfferPm = offerPm; b.LastRaiseTick = now; }
        else { site.Project!.OfferPm = offerPm; site.Project.LastRaiseTick = now; }
    }

    /// <summary>A jornada acabou ou foi interrompida: as horas em que o diarista (ou o ajudante) esteve NA obra trabalhando contam para
    /// ela. O diarista é pago agora; quem paga não tem: ele sai sem receber e a hora não conta (ninguém trabalha de graça a quem
    /// devia pagar). O ajudante não recebe. No trecho e na obra encomendada, as horas descontam a obra; no prédio da fila, ficam
    /// guardadas para o passo da fila (<c>ConstructionSystem</c>).</summary>
    internal static void Clock(WorldState world, TickContext ctx, Npc npc, long startedAt, long now)
    {
        if (WorkOf(world, npc) is not { } site)
        {
            if (HelpOf(world, npc) is { } help && npc.CurrentLocation == new CellCoord(help.X, help.Y)
                && LivingWorld.Simulation.Climate.WeatherNow.OutdoorWorkable(world, npc.CurrentLocation)) // AD-180: dia perdido
                BuildWorks.Progress(world, ctx, help, npc, Math.Min(now - startedAt, world.StreetRules.CrewHoursPerDay), paid: 0, now);
            return;
        }
        if (npc.CurrentLocation != SiteOf(world, site)) return; // não estava lá: a ida não é jornada
        if (!LivingWorld.Simulation.Climate.WeatherNow.OutdoorWorkable(world, npc.CurrentLocation)) return; // AD-180: dia perdido
        long hours = Math.Min(now - startedAt, world.StreetRules.CrewHoursPerDay);
        if (hours <= 0) return;
        if (world.FindActiveCity(new CityId(site.CityId)) is not { } city) return;
        long due = DueFor(world, site, hours);
        if (!Pay(world, ctx, city, site, due))
        {
            site.Crew.Remove(npc.Id.Value);
            Touch(world);
            ctx.LogEvent(WorldEventKind.SiteWorkLeft, $"{npc.Id.Value}|{site.Label}|sem-pagamento", sourceSystem: SourceSystem);
            world.CanonicalHashCache.MarkPropertyDirty(site.Project is not null ? nameof(WorldState.Cities) : nameof(WorldState.Ground));
            return;
        }
        npc.CreditWallet(new Money(due));
        if (site.Paving is { } work)
        {
            work.Paid += due;
            work.HoursWorked += hours;
            work.HoursLeft -= hours;
            if (work.Stalled == true)
            {
                work.Stalled = null;
                var at = SiteOf(world, work);
                WorkEvents.Log(ctx, WorldEventKind.WorkResumed, work.Id, work.City, "calcamento", at.X, at.Y,
                    ((now - work.LastProgressTick) / world.Calendar.HoursPerDay).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    PavingWorksSystem.SystemName, work.StartEventId);
            }
            work.LastProgressTick = now;
            if (work.HoursLeft <= 0) PavingWorksSystem.Finish(world, ctx, work);
            world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Ground));
            return;
        }
        if (site.Build is { } build)
        {
            BuildWorks.Progress(world, ctx, build, npc, hours, due, now);
            return;
        }
        // a força de quem trabalha rende mais horas de obra (o poder modula a base, AD-084), como na fila sem turma
        long done = (long)Math.Round(hours * LivingWorld.Simulation.Extraordinary.Mechanics.AttributeMechanic.StrengthMultiplier(world, npc), MidpointRounding.AwayFromZero);
        site.Project!.HoursBanked += done;
        site.Project.HoursWorked += hours;
        site.Project.Paid += due;
        LivingWorld.Simulation.Cities.Construction.ConstructionWorks.Progressed(world, ctx, city, site.Project, now);
        world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Cities));
    }

    /// <summary>Quem paga a jornada: as casas de frente ou o tesouro (trecho), o tesouro (fila), a casa da família (casa) ou o que o
    /// fundador guardou (oficina). Falso sem mexer em nada se não tem.</summary>
    private static bool Pay(WorldState world, TickContext ctx, City city, WorkSite site, long due)
    {
        if (site.Build is { Kind: BuildKind.Workshop } shop)
        {
            if (shop.Escrow < due) return false;
            shop.Escrow -= due;
            return true;
        }
        if (site.Build is { } house) return PavingDecision.PayFrom(world, ctx, city, [house.Household], due);
        return PavingDecision.PayFrom(world, ctx, city, site.Paving?.Payers ?? [], due);
    }
}
