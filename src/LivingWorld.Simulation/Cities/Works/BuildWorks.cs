using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Layout;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Works;

/// <summary>Obras encomendadas (AD-184), <c>Daily</c>: a casa de quem precisa de moradia e a oficina de quem empreende. Toda casa sem
/// moradia própria (sem casa, ou o casal morando de favor com outra família, ou casa cheia além da conta) abre uma obra num lote do
/// traçado; enquanto isso mora onde está. Se há na cidade uma casa vazia, muda para ela sem obra. A obra só anda pelas horas de
/// quem está nela (<see cref="SiteWork.Clock"/>): os da casa e os parentes nas horas livres, os vizinhos de afeto em mutirão e os
/// diaristas que a casa pode pagar. Sem hora por uma semana a obra para (evento); sem hora por meses, é abandonada e o lote volta a
/// ser livre.</summary>
[LivingCapability("BUILD_WORKS", Events = [WorldEventKind.BuildWorkStarted, WorldEventKind.BuildWorkDone, WorldEventKind.WorkStalled,
    WorldEventKind.WorkResumed, WorldEventKind.WorkAbandoned], Consumers = ["population.dwelling", "economy.wallet", "economy.venture",
    "behavior.work-destination"])]
public sealed class BuildWorksSystem : ISimulationSystem
{
    public const string SystemName = "build-works";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.StreetsOn) return;
        BuildWorks.Tick(world, ctx);
    }
}

public static class BuildWorks
{
    public static void Tick(WorldState world, TickContext ctx)
    {
        var ground = world.Ground!;
        var rules = world.StreetRules;
        long now = ctx.CurrentTick;
        if (ground.Builds.Count > 0)
        {
            foreach (var work in ground.Builds.OrderBy(w => w.Id).ToList())
            {
                var city = world.FindActiveCity(new CityId(work.City));
                if (city is null) { Abandon(world, ctx, work, "cidade"); continue; }
                work.City = city.Id.Value;
                if (OwnerGone(world, work)) { Abandon(world, ctx, work, "sem-dono"); continue; }
                long idle = now - work.LastProgressTick;
                if (idle > rules.SiteStaleMonths * world.Calendar.HoursPerMonth) { Abandon(world, ctx, work, "sem-mao"); continue; }
                if (!work.Stalled && idle > rules.StallDays * (long)world.Calendar.HoursPerDay)
                {
                    work.Stalled = true;
                    WorkEvents.Log(ctx, WorldEventKind.WorkStalled, work.Id, work.City, TypeOf(work), work.X, work.Y,
                        (idle / world.Calendar.HoursPerDay).ToString(System.Globalization.CultureInfo.InvariantCulture), cause: work.StartEventId);
                }
                if (rules.HelpersBuild && (work.Id + ctx.CurrentTick / world.Calendar.HoursPerDay) % 7 == 0) RefreshHelpers(world, work); // quem ajuda se acerta por semana
                SiteWork.Staff(world, ctx, new WorkSite(null, null, null, work));
            }
        }
        // quem saiu do favor (casa própria, mudou, se desfez) deixa a lista
        ground.Lodgers.RemoveAll(id => world.FindHousehold(new HouseholdId(id)) is not { IsEmpty: false } h
            || h.Dwelling is not { } d || !SharesDwelling(world, h, d));
        if (rules.HouseIsWork) StartHouses(world, ctx);
        ReturnFromLeave(world, ctx);
        world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Ground));
    }

    /// <summary>Quem largou o ofício pela obra e já saiu da turma (a obra acabou, foi desfeita, ele saiu) volta ao lugar de antes se a
    /// oficina ainda existe, está aberta e tem vaga; senão procura trabalho como qualquer um. Quem já arrumou emprego sai da lista.</summary>
    private static void ReturnFromLeave(WorldState world, TickContext ctx)
    {
        var leave = world.Ground!.OnLeave;
        for (int i = 0; i < leave.Count;)
        {
            var npc = world.FindNpc(new NpcId(leave[i][0]));
            if (npc is { IsAlive: true, Employer: null } && SiteWork.WorkOf(world, npc) is not null) { i++; continue; } // ainda na obra
            var shopId = new LivingWorld.Domain.Shared.WorkplaceId(leave[i][1]);
            leave.RemoveAt(i);
            if (npc is not { IsAlive: true, Employer: null } || world.FindWorkplace(shopId) is not { Closed: false } shop
                || shop.Employees.Count >= shop.MaxVacancies) continue;
            shop.Hire(npc.Id);
            npc.Hire(shop.Id);
            ctx.LogEvent(WorldEventKind.Hired, $"{npc.Id.Value}|{shop.Id.Value}", sourceSystem: SiteWork.SourceSystem);
        }
    }

    internal static string TypeOf(BuildWork work) => work.Kind == BuildKind.House ? "casa" : "oficina";

    private static bool OwnerGone(WorldState world, BuildWork work)
    {
        if (work.Kind == BuildKind.Workshop) return world.FindNpc(new NpcId(work.Owner)) is not { IsAlive: true };
        if (world.FindHousehold(new HouseholdId(work.Household)) is not { IsEmpty: false } home) return true;
        foreach (var id in home.Members)
            if (world.FindNpc(id) is { IsAlive: true } npc && IsAdult(world, npc)) return false;
        return true;
    }

    private static bool IsAdult(WorldState world, Npc npc) =>
        world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) == LifeStage.Adult;

    // ---------------------------------------------------------------- casa nova

    /// <summary>Casas que precisam de moradia, em ordem de id, uma vez por semana cada (pelo id e pelo dia): muda para uma casa vazia
    /// da cidade se houver; senão abre a obra.</summary>
    private static void StartHouses(WorldState world, TickContext ctx)
    {
        long day = ctx.CurrentTick / world.Calendar.HoursPerDay;
        Dictionary<BuildingId, int>? tenants = null;
        Dictionary<BuildingId, long>? host = null;
        HashSet<long>? building = null;
        foreach (var household in world.Households)
        {
            if ((household.Id.Value + day) % 7 != 0 || household.IsEmpty || household.PendingRelocationCity is not null) continue;
            if (tenants is null)
            {
                tenants = DwellingCapacity.TenantsByBuilding(world);
                host = [];
                foreach (var h in world.Households)
                    if (h.Dwelling is { } d && !h.IsEmpty && (!host.TryGetValue(d, out long first) || h.Id.Value < first)) host[d] = h.Id.Value;
                building = [];
                foreach (var w in world.Ground!.Builds) if (w.Kind == BuildKind.House) building.Add(w.Household);
            }
            if (building!.Contains(household.Id.Value) || !NeedsHouse(world, household, tenants, host!)) continue;
            if (world.FindActiveCity(household.City) is not { } city) continue;
            var members = new List<Npc>();
            bool adult = false;
            foreach (var id in household.Members.OrderBy(m => m.Value))
                if (world.FindNpc(id) is { IsAlive: true } npc)
                {
                    members.Add(npc);
                    adult |= IsAdult(world, npc);
                }
            if (!adult) continue;
            if (TakeVacant(world, city, household, tenants, members)) continue;
            if (StartHouse(world, ctx, city, household, members)) building.Add(household.Id.Value);
        }
    }

    /// <summary>Precisa de casa: não tem (mora na célula, ou foi despejada), mora numa casa cheia além do que ela abriga, ou é um casal
    /// morando de favor (com <see cref="StreetRules.NeolocalCouples"/>). A família mais antiga da casa é a dona: ela não sai.</summary>
    internal static bool NeedsHouse(WorldState world, Household household, Dictionary<BuildingId, int> tenants, Dictionary<BuildingId, long> host)
    {
        if (household.Dwelling is not { } dwelling || world.FindBuilding(dwelling) is not { } house) return true;
        if (host.GetValueOrDefault(dwelling, household.Id.Value) == household.Id.Value) return false;
        int count = tenants.GetValueOrDefault(dwelling);
        if (count > DwellingCapacity.Of(world, house)) return true;
        // o casal que foi morar de favor com a família de onde saiu (não quem já morava no seu andar de uma casa de vários)
        return world.StreetRules.NeolocalCouples && count > 1 && world.Ground!.Lodgers.Contains(household.Id.Value) && IsCouple(world, household);
    }

    private static bool SharesDwelling(WorldState world, Household household, BuildingId dwelling)
    {
        foreach (var other in world.Households)
            if (other != household && !other.IsEmpty && other.Dwelling == dwelling) return true;
        return false;
    }

    private static bool IsCouple(WorldState world, Household household) =>
        world.FindNpc(household.Head) is { IsAlive: true, Spouse: { } spouse } && household.Members.Contains(spouse);

    /// <summary>Uma casa pronta e vazia da cidade (a do falecido, a que alguém deixou): a família muda para ela sem obra.</summary>
    private static bool TakeVacant(WorldState world, City city, Household household, Dictionary<BuildingId, int> tenants, List<Npc> members)
    {
        // a mais conveniente das vazias para quem vai morar (água, mercado, trabalho de cada um), não a primeira
        Building? best = null;
        double bestCost = double.MaxValue;
        HomePlaces? places = null;
        double? quarter = null;
        // só as de dentro da cidade de agora: morar na casa largada fora dela vira aglomerado e funda cidade-filha longe do trabalho
        var bounds = CityOccupancy.ResolveGrownBounds(world, city, CityPopulationQuery.Population(world, city.Id)).Bounds;
        foreach (var b in world.Buildings.OrderBy(b => b.Id.Value))
        {
            if (b.City != city.Id || b.UnderWork is not null || b.Position is not { } at || (world.CityRules.Enabled && !bounds.Contains(at))
                || !IsHouseType(world, b.BuildingTypeId)) continue;
            if (tenants.GetValueOrDefault(b.Id) > 0) continue;
            places ??= HomePlaces.Of(world, city.Id, members);
            quarter ??= QuarterYearly(world, city, bounds, members);
            double cost = places.YearlyCost(world, b.Position.Value) + quarter.Value * Quarter.PlaceSqualor(world, b);
            if (cost >= bestCost) continue;
            best = b;
            bestCost = cost;
        }
        if (best is null || !WorthMoving(world, household, places!, bestCost, quarter ?? 0)) return false;
        if (household.Dwelling is { } old) tenants[old] = Math.Max(0, tenants.GetValueOrDefault(old) - 1);
        household.MoveIn(best.Id, DwellingCapacity.InteriorCellOf(best, best.Position!.Value));
        tenants[best.Id] = 1;
        return true;
    }

    private static bool IsHouseType(WorldState world, int typeId) =>
        typeId == LivingWorld.Simulation.Population.PopulationSeeder.InitialHouseBuildingTypeId
        || (world.CityCatalog.BuildingRecipes.TryGetValue(typeId, out var r) && r.HousingCapacityProvided > 0 && r.Workplace is null
            && r.Lodging is null);

    private static int HouseTypeOf(WorldState world)
    {
        foreach (var (id, recipe) in world.CityCatalog.BuildingRecipes.OrderBy(kv => kv.Key))
            if (recipe.HousingCapacityProvided > 0 && recipe.Workplace is null && recipe.Lodging is null) return id;
        return LivingWorld.Simulation.Population.PopulationSeeder.InitialHouseBuildingTypeId;
    }

    /// <summary>Abre a obra da casa: o lote pelo perfil da família (ofício, tamanho, meios), o quintal só se ela pode pagar o foro da
    /// terra dele (senão o lote sem quintal), o material comprado se alguém vende e ela pode pagar (senão buscado na natureza) e as
    /// horas = casa + quintal + material. O canteiro ocupa o lote desde já.</summary>
    internal static bool StartHouse(WorldState world, TickContext ctx, City city, Household household, List<Npc> members)
    {
        var rules = world.StreetRules;
        long now = ctx.CurrentTick;
        int typeId = HouseTypeOf(world);
        var candidate = new Building(new BuildingId(world.NextBuildingId), city.Id, typeId, now);
        var profile = LotProfiles.ForHousehold(world, city.Id, members);
        var bounds = CityOccupancy.ResolveGrownBounds(world, city, CityPopulationQuery.Population(world, city.Id)).Bounds;
        long dayWage = PavingDecision.DayWage(world);
        long budget = Math.Max(0, PavingDecision.MeansOf(world, household.Id.Value) - dayWage * rules.SiteReserveDays);
        // a família escolhe o lote entre os possíveis pelo que ele custa a ela: as idas de cada dia (água, mercado, o trabalho de
        // cada um) pelo valor da hora nos anos da casa, mais o foro do quintal; o quintal que ela não pode pagar fica de fora
        var places = HomePlaces.Of(world, city.Id, members);
        double quarter = QuarterYearly(world, city, bounds, members); // AD-192: o que a sujeira do lugar pesa para esta família, por ano
        (CellCoord Position, int Orientation, int Croft)? chosen = null;
        int croftArea = 0;
        long fee = 0;
        double best = double.MaxValue;
        for (int level = 0; level <= 1; level++)
            foreach (var lot in CityOccupancy.CandidateLots(world, city, bounds, candidate.Id, typeId, 0, profile, level, rules.LotChoices, places.Anchors(world)))
            {
                int area = CroftArea(candidate, lot.Orientation, lot.Croft);
                long lotFee = rules.CroftFeeDaysPerCell * dayWage * area;
                if (lotFee > budget) continue;
                double cost = places.YearlyCost(world, lot.Origin) * rules.PaybackYears + lotFee;
                if (quarter > 0)
                    cost += quarter * rules.PaybackYears * Quarter.SiteSqualor(world, lot.Origin,
                        BuildingFootprintGenerator.Generate(candidate.Id, typeId, lot.Orientation), lot.Croft);
                if (cost >= best) continue;
                best = cost;
                chosen = (lot.Origin, lot.Orientation, lot.Croft);
                croftArea = area;
                fee = lotFee;
            }
        if (chosen is null) return false; // sem lote de rua que a casa possa ter: segue apertada
        if (!WorthMoving(world, household, places, (best - fee) / Math.Max(1, rules.PaybackYears), quarter)) return false; // longe demais: vale mais seguir apertada onde está
        if (fee > 0 && PavingDecision.PayFrom(world, ctx, city, [household.Id.Value], fee))
        {
            city.CreditTreasury(new Money(fee));
            world.RecordTreasury(city.Id, now, revenue: fee, spending: 0);
        }
        else fee = 0;
        var site = chosen.Value;
        var (materialHours, spent) = Material(world, ctx, city, household, site.Position, budget - fee);
        long hours = rules.HouseHours + (long)croftArea * rules.CroftHoursPerCell + materialHours;
        Open(world, ctx, city, BuildKind.House, typeId, site.Position, site.Orientation, site.Croft, household.Id.Value,
            household.Head.Value, hours, spent + fee, $"{household.Id.Value}|{hours}|{croftArea}|{spent + fee}");
        return true;
    }

    /// <summary>Mudar vale a pena? Quem não tem casa muda sempre. Quem mora (de favor, apertado) compara: as idas de cada dia do lugar
    /// novo contra as de onde está, mais o que vale ter a casa própria (<see cref="StreetRules.OwnHomeDaysPerYear"/> dias de jornal
    /// por ano por morador). Uma casa a horas do trabalho e da água não compensa: a família segue onde está.</summary>
    private static bool WorthMoving(WorldState world, Household household, HomePlaces places, double newCost, double quarter)
    {
        if (household.Dwelling is null) return true;
        double own = world.StreetRules.OwnHomeDaysPerYear * (double)PavingDecision.DayWage(world) * Math.Max(1, household.Members.Count);
        // AD-192: a sujeira de onde mora agora (apinhada de favor, colada, sem água boa) também pesa contra ficar
        double here = quarter > 0 ? quarter * Math.Min(1, Quarter.HomeSqualor(world, household)) : 0;
        return newCost <= places.YearlyCost(world, household.Location) + here + own;
    }

    /// <summary>AD-192: quanto a sujeira pesa, por ano e por ponto de sujeira, na escolha de onde morar desta família: dias de jornal por
    /// morador × a aversão média dos adultos ao bairro sujo (sem a curiosidade); 0 com a chave desligada.</summary>
    private static double QuarterYearly(WorldState world, City city, LivingWorld.Domain.Cities.Spatial.CityBounds bounds, List<Npc> members)
    {
        var rules = world.StreetRules;
        if (!rules.QuarterInLotChoice) return 0;
        double aversion = Quarter.HouseholdAversion(world, members, city, bounds);
        return aversion * rules.QuarterDaysPerYear * PavingDecision.DayWage(world) * Math.Max(1, members.Count);
    }

    private static int CroftArea(Building candidate, int orientation, int croft)
    {
        if (croft <= 0) return 0;
        var shape = BuildingFootprintGenerator.Generate(candidate.Id, candidate.BuildingTypeId, orientation);
        int spanX = shape.Max(c => c.Cell.X) - shape.Min(c => c.Cell.X) + 1;
        int spanY = shape.Max(c => c.Cell.Y) - shape.Min(c => c.Cell.Y) + 1;
        return croft * (orientation % 2 == 0 ? spanX : spanY);
    }

    /// <summary>Material da casa: comprado de quem vende na cidade (o mais barato, desempate por id) se a casa pode pagar tudo, e então
    /// só as viagens de carregar até o lote; senão as horas de buscar na natureza.</summary>
    private static (long Hours, long Spent) Material(WorldState world, TickContext ctx, City city, Household household, CellCoord site, long budget)
    {
        var rules = world.StreetRules;
        var resource = new ResourceType(rules.HouseMaterialResourceId);
        long units = rules.HouseMaterialUnits;
        LivingWorld.Domain.Economy.Workplace? seller = null;
        long price = 0;
        foreach (var w in world.Workplaces)
        {
            if (w.City != city.Id || w.Closed || w.Stock.GetValueOrDefault(resource) < units || !w.Prices.TryGetValue(resource, out long p) || p <= 0) continue;
            if (seller is null || p < price || (p == price && w.Id.Value < seller.Id.Value)) { seller = w; price = p; }
        }
        if (seller is null || units <= 0 || units * price > budget
            || !PavingDecision.PayFrom(world, ctx, city, [household.Id.Value], units * price)) return (rules.HouseGatherHours, 0);
        seller.Withdraw(resource, units);
        seller.CreditTreasury(new Money(units * price));
        world.RecordResourceConsumed(resource, units);
        long loads = (units + Math.Max(1, rules.BuildUnitsPerLoad) - 1) / Math.Max(1, rules.BuildUnitsPerLoad);
        double trip = StreetRoutes.CostOf(world, seller.Location, site).Cost
            * LivingWorld.Simulation.Emergence.Mobility.LoadFactorOfKg(rules, rules.SiteLoadKg)
            + StreetRoutes.CostOf(world, site, seller.Location).Cost;
        return ((long)Math.Ceiling(loads * trip), units * price);
    }

    /// <summary>Abre uma obra encomendada: o canteiro no lote (o prédio com <c>UnderWork</c>), a obra na lista e o evento.</summary>
    internal static BuildWork Open(WorldState world, TickContext ctx, City city, BuildKind kind, int typeId, CellCoord position, int orientation,
        int croft, long household, long owner, long hours, long spent, string payload)
    {
        var ground = world.Ground!;
        long id = ground.NextWorkId++;
        long now = ctx.CurrentTick;
        var canteiro = new Building(world.NextBuildingIdAndAdvance(), city.Id, typeId, now, position, orientation, croftCells: croft) { UnderWork = id };
        world.AddBuilding(canteiro);
        Streets.StreetGroundSystem.Occupied(world, now); // AD-192 (08d 182): o canteiro ocupa o chão já
        var work = new BuildWork
        {
            Id = id,
            City = city.Id.Value,
            Kind = kind,
            Building = canteiro.Id.Value,
            X = position.X,
            Y = position.Y,
            Croft = croft,
            Household = household,
            Owner = owner,
            HoursTotal = Math.Max(1, hours),
            HoursLeft = Math.Max(1, hours),
            Spent = spent,
            StartTick = now,
            LastProgressTick = now,
        };
        ground.Builds.Add(work);
        work.StartEventId = WorkEvents.Log(ctx, WorldEventKind.BuildWorkStarted, id, work.City, TypeOf(work), work.X, work.Y, payload);
        if (world.StreetRules.HelpersBuild) RefreshHelpers(world, work);
        SiteWork.Touch(world);
        world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Ground));
        return work;
    }

    // ---------------------------------------------------------------- quem trabalha

    /// <summary>Os ajudantes sem jornal da obra: os adultos da casa que encomenda (sempre), e até <see cref="StreetRules.HelpersMax"/>
    /// parentes (pais, filhos, irmãos) e vizinhos de afeto da cidade que querem: vontade = (1 o parente, afeto/100 o vizinho) ×
    /// (meio + metade da generosidade) × a parte das horas livres que a ida deixa − metade da necessidade da própria casa (quem
    /// precisa de jornal vai à obra paga), a partir de <see cref="StreetRules.HelpMin"/>. Quem já trabalha noutra obra, não.</summary>
    internal static void RefreshHelpers(WorldState world, BuildWork work)
    {
        var rules = world.StreetRules;
        var cityId = new CityId(work.City);
        var helpers = new List<long>();
        var adults = new List<Npc>();
        if (world.FindHousehold(new HouseholdId(work.Household)) is { } home)
            foreach (var id in home.Members.OrderBy(m => m.Value))
                if (world.FindNpc(id) is { IsAlive: true } npc && npc.City == cityId && IsAdult(world, npc))
                {
                    adults.Add(npc);
                    if (Free(world, npc, work) && !Nursing(world, npc)) helpers.Add(npc.Id.Value);
                }
        if (adults.Count > 0 && rules.HelpersMax > 0)
        {
            var site = new CellCoord(work.X, work.Y);
            var willing = new List<(double Value, long Id)>();
            var seen = new HashSet<long>(adults.Select(a => a.Id.Value));
            foreach (var npc in world.AliveNpcIndex.Alive)
            {
                if (npc.City != cityId || seen.Contains(npc.Id.Value) || !IsAdult(world, npc) || !Kin(adults, npc)) continue;
                seen.Add(npc.Id.Value);
                Consider(world, work, npc, 1.0, site, willing);
            }
            foreach (var member in adults)
            {
                var targets = world.RelationshipTargetsOf(member.Id);
                var relations = world.RelationshipsFrom(member.Id);
                if (relations is null) continue;
                for (int i = 0; i < targets.Count; i++)
                {
                    if (relations[i].Affection < rules.MutiraoAffection || !seen.Add(targets[i].Value)) continue;
                    if (world.FindNpc(targets[i]) is not { IsAlive: true } friend || friend.City != cityId || !IsAdult(world, friend)) continue;
                    Consider(world, work, friend, relations[i].Affection / 100.0, site, willing);
                }
            }
            willing.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : a.Id.CompareTo(b.Id));
            for (int i = 0; i < willing.Count && i < rules.HelpersMax; i++) helpers.Add(willing[i].Id);
        }
        if (!helpers.SequenceEqual(work.Helpers))
        {
            work.Helpers = helpers;
            SiteWork.Touch(world);
        }
    }

    /// <summary>Não está na turma paga de nenhuma obra nem ajudando outra (a primeira que o pegou fica com ele).</summary>
    private static bool Free(WorldState world, Npc npc, BuildWork work)
    {
        if (SiteWork.WorkOf(world, npc) is not null) return false;
        var other = SiteWork.HelpOf(world, npc);
        return other is null || ReferenceEquals(other, work);
    }

    private static void Consider(WorldState world, BuildWork work, Npc npc, double bond, CellCoord site, List<(double, long)> willing)
    {
        if (!Free(world, npc, work) || Nursing(world, npc)) return;
        // quem levanta a própria casa (ou oficina) trabalha nela, não na dos outros
        foreach (var other in world.Ground!.Builds)
            if (!ReferenceEquals(other, work) && npc.Household is { } own && other.Household == own.Value) return;
        var rules = world.StreetRules;
        var from = npc.Household is { } h && world.FindHousehold(h) is { } home ? home.Location : npc.CurrentLocation;
        double free = Math.Max(1, rules.CrewHoursPerDay); // o dia de mutirão (domingo, dia santo, a entressafra)
        double share = (free - 2 * StreetRoutes.CostOf(world, from, site).Cost) / free;
        if (!(share > 0)) return;
        double need = NeedOf(world, npc);
        double value = bond * (0.5 + npc.Personality.Altruism / 200.0) * share - 0.5 * need;
        if (value >= rules.HelpMin) willing.Add((value, npc.Id.Value));
    }

    /// <summary>Mãe com filho de colo (menos de um ano) na casa: amamenta e cuida, não vai à obra nas horas livres.</summary>
    private static bool Nursing(WorldState world, Npc npc)
    {
        if (npc.Sex != Sex.Female || npc.Household is not { } h || world.FindHousehold(h) is not { } home) return false;
        foreach (var id in home.Members)
            if (world.FindNpc(id) is { IsAlive: true } kid && kid.MotherId == npc.Id && kid.AgeYears(world.CurrentDate) < 1) return true;
        return false;
    }

    /// <summary>Necessidade de moeda da casa do NPC (0–1): o que falta para a reserva de jornal.</summary>
    private static double NeedOf(WorldState world, Npc npc)
    {
        long reserve = PavingDecision.DayWage(world) * world.StreetRules.SiteReserveDays;
        if (reserve <= 0) return 0;
        long means = npc.Household is { } h ? PavingDecision.MeansOf(world, h.Value) : npc.Wallet.Amount;
        return Math.Clamp(1.0 - (double)means / reserve, 0, 1);
    }

    private static bool Kin(List<Npc> adults, Npc npc)
    {
        foreach (var a in adults)
        {
            if (a.Id == npc.Id) continue;
            if (a.MotherId == npc.Id || a.FatherId == npc.Id || npc.MotherId == a.Id || npc.FatherId == a.Id) return true;
            if ((a.MotherId is { } m && npc.MotherId == m) || (a.FatherId is { } f && npc.FatherId == f)) return true;
        }
        return false;
    }

    /// <summary>É parente (pai, mãe, filho, irmão) de algum adulto da casa que encomenda.</summary>
    internal static bool IsKin(WorldState world, Npc npc, BuildWork work)
    {
        if (world.FindHousehold(new HouseholdId(work.Household)) is not { } home) return false;
        var adults = new List<Npc>();
        foreach (var id in home.Members)
            if (world.FindNpc(id) is { IsAlive: true } m && m.Id != npc.Id) adults.Add(m);
        return Kin(adults, npc);
    }

    // ---------------------------------------------------------------- andamento, fim e abandono

    /// <summary>Horas de alguém na obra: descontam o que falta (a força de quem tem rende mais), contam para a turma paga ou para a ajuda,
    /// retomam a obra parada e, no fim, a terminam.</summary>
    internal static void Progress(WorldState world, TickContext ctx, BuildWork work, Npc npc, long hours, long paid, long now)
    {
        if (hours <= 0 || !world.Ground!.Builds.Contains(work)) return;
        long done = (long)Math.Round(hours * LivingWorld.Simulation.Extraordinary.Mechanics.AttributeMechanic.StrengthMultiplier(world, npc), MidpointRounding.AwayFromZero);
        work.HoursLeft -= done;
        if (paid > 0) { work.HoursByCrew += hours; work.Paid += paid; }
        else work.HoursByHelpers += hours;
        if (work.Stalled)
        {
            work.Stalled = false;
            WorkEvents.Log(ctx, WorldEventKind.WorkResumed, work.Id, work.City, TypeOf(work), work.X, work.Y,
                ((now - work.LastProgressTick) / world.Calendar.HoursPerDay).ToString(System.Globalization.CultureInfo.InvariantCulture), cause: work.StartEventId);
        }
        work.LastProgressTick = now;
        if (work.HoursLeft <= 0) Finish(world, ctx, work);
        world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Ground));
    }

    /// <summary>A obra acabou: o prédio passa a existir; a família muda para a casa nova, ou a oficina abre com o fundador nela.</summary>
    internal static void Finish(WorldState world, TickContext ctx, BuildWork work)
    {
        var ground = world.Ground!;
        long now = ctx.CurrentTick;
        ground.Builds.Remove(work);
        ground.BuildsDone++;
        SiteWork.Touch(world);
        if (world.FindBuilding(new BuildingId(work.Building)) is not { } building) return;
        building.Complete(now);
        Streets.StreetGroundSystem.Occupied(world, now); // AD-192 (08d 182)
        if (work.Kind == BuildKind.House)
        {
            if (world.FindHousehold(new HouseholdId(work.Household)) is { IsEmpty: false } home)
                home.MoveIn(building.Id, DwellingCapacity.InteriorCellOf(building, building.Position!.Value));
        }
        else LivingWorld.Simulation.Economy.Venture.VentureFounding.OpenBuilt(world, ctx, work, building);
        WorkEvents.Log(ctx, WorldEventKind.BuildWorkDone, work.Id, work.City, TypeOf(work), work.X, work.Y,
            $"{building.Id.Value}|{work.HoursByCrew}|{work.HoursByHelpers}|{work.Paid}|{(now - work.StartTick) / world.Calendar.HoursPerDay}",
            cause: work.StartEventId);
    }

    /// <summary>Obra abandonada (sem mão por meses, sem dono, cidade desfeita): o canteiro sai e o lote volta a ser livre; o que o
    /// fundador guardou volta a ele (ou à casa dele, ou ao tesouro); o material comprado fica gasto.</summary>
    internal static void Abandon(WorldState world, TickContext ctx, BuildWork work, string reason)
    {
        var ground = world.Ground!;
        ground.Builds.Remove(work);
        ground.BuildsAbandoned++;
        SiteWork.Touch(world);
        if (world.FindBuilding(new BuildingId(work.Building)) is { UnderWork: not null } canteiro) world.RemoveBuilding(canteiro);
        if (work.Escrow > 0) Refund(world, ctx, work);
        foreach (long id in work.Crew)
            ctx.LogEvent(WorldEventKind.SiteWorkLeft, $"{id}|{work.Id}|desfeita", sourceSystem: SiteWork.SourceSystem);
        long donePm = work.HoursTotal <= 0 ? 0 : (work.HoursTotal - Math.Max(0, work.HoursLeft)) * 1000 / work.HoursTotal;
        WorkEvents.Log(ctx, WorldEventKind.WorkAbandoned, work.Id, work.City, TypeOf(work), work.X, work.Y, $"{reason}|{donePm}",
            cause: work.StartEventId);
        world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Ground));
    }

    private static void Refund(WorldState world, TickContext ctx, BuildWork work)
    {
        long amount = work.Escrow;
        work.Escrow = 0;
        if (world.FindNpc(new NpcId(work.Owner)) is { IsAlive: true } owner) { owner.CreditWallet(new Money(amount)); return; }
        if (world.FindHousehold(new HouseholdId(work.Household)) is { } home)
            foreach (var id in home.Members.OrderBy(m => m.Value))
                if (world.FindNpc(id) is { IsAlive: true } heir) { heir.CreditWallet(new Money(amount)); return; }
        if (world.FindActiveCity(new CityId(work.City)) is { } city)
        {
            city.CreditTreasury(new Money(amount));
            world.RecordTreasury(city.Id, ctx.CurrentTick, revenue: amount, spending: 0);
        }
    }

    /// <summary>O fundador tem uma oficina em obra (não abre outra enquanto isso).</summary>
    internal static bool HasWorkshop(WorldState world, Npc npc)
    {
        if (world.Ground is not { } ground) return false;
        foreach (var w in ground.Builds)
            if (w.Kind == BuildKind.Workshop && w.Owner == npc.Id.Value) return true;
        return false;
    }
}

/// <summary>AD-184: os lugares de todo dia de uma casa — a água (natural ou à venda), o mercado de comida e o trabalho de cada um —
/// e quanto custa, por ano, morar numa célula: as idas de cada dia (`WaterTripsPerMemberDay`, `MarketTripsPerMemberDay` por
/// morador, ida e volta ao trabalho de cada empregado) pela rota, pelo valor da hora (o jornal ÷ a jornada). O peso sai da casa:
/// quantos são, quem trabalha e onde.</summary>
internal sealed class HomePlaces
{
    private readonly List<CellCoord> _water = [], _food = [], _work = [];
    private int _members;

    public static HomePlaces Of(WorldState world, CityId city, IReadOnlyList<Npc> members)
    {
        var places = new HomePlaces { _members = Math.Max(1, members.Count) };
        var economy = world.EconomyRules;
        var water = new ResourceType(economy.WaterResourceId);
        var food = new ResourceType(economy.FoodResourceId);
        foreach (var w in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            if (w.City != city || w.Closed || !world.EconomyCatalog.MarketLocationTypeIds.Contains(w.LocationType.Id)) continue;
            if (w.Prices.ContainsKey(water)) places._water.Add(w.Location);
            if (w.Prices.ContainsKey(food)) places._food.Add(w.Location);
        }
        foreach (var npc in members)
            if (npc.Employer is { } e && world.FindWorkplace(e) is { } job) places._work.Add(job.Location);
        return places;
    }

    /// <summary>Os lugares de onde a casa olha os lotes: o trabalho de cada um e a água (a fonte que vende ou a da natureza).</summary>
    public IReadOnlyList<CellCoord> Anchors(WorldState world)
    {
        var anchors = new List<CellCoord>(_work);
        if (_water.Count > 0 && !anchors.Contains(_water[0])) anchors.Add(_water[0]);
        if (_food.Count > 0 && !anchors.Contains(_food[0])) anchors.Add(_food[0]);
        return anchors;
    }

    public double YearlyCost(WorldState world, CellCoord cell)
    {
        var rules = world.StreetRules;
        double Nearest(List<CellCoord> targets)
        {
            double best = double.MaxValue;
            foreach (var t in targets) best = Math.Min(best, Streets.StreetRoutes.CostOf(world, cell, t).Cost);
            return best == double.MaxValue ? 0 : best;
        }
        // a água: a fonte de verdade da AD-185 (a ida a pé pela rota, como a casa vai buscar); sem ela, o poço que vende
        double water;
        if (LivingWorld.Simulation.Economy.Supply.WaterFetch.On(world))
            water = LivingWorld.Simulation.Economy.Supply.WaterFetch.SourceFor(world, cell) is { } source ? source.Ticks : Nearest(_water) is > 0 and var sold ? sold : 24;
        else
        {
            water = Nearest(_water);
            long natural = LivingWorld.Simulation.Economy.Supply.NaturalWaterSystem.WalkToWater(world, cell, 30);
            if (natural != long.MaxValue && (natural < water || _water.Count == 0)) water = natural;
        }
        double hours = 2 * (_members * rules.WaterTripsPerMemberDay * water + _members * rules.MarketTripsPerMemberDay * Nearest(_food));
        foreach (var job in _work) hours += 2 * Streets.StreetRoutes.CostOf(world, cell, job).Cost;
        double hourValue = PavingDecision.DayWage(world) / (double)Math.Max(1, rules.CrewHoursPerDay);
        return hours * hourValue * world.Calendar.DaysPerMonth * world.Calendar.MonthsPerYear;
    }
}
