using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Supply;

/// <summary>AD-185: uma ida para buscar água — quem foi, de que casa, a célula da fonte, o tick em que saiu (ou em que encheu, na
/// volta) e se já volta carregado para casa.</summary>
public readonly record struct WaterTrip(long NpcId, long HouseholdId, int X, int Y, long StartTick, bool Returning);

/// <summary>Buscar água andando (AD-185, pedido do dono: "nada deve ficar apenas no visual"). Antes (AD-135) a casa a até 1 h de um
/// rio enchia a despensa sem ninguém andar. Agora alguém da casa decide ir (tempo livre, idade, o costume: primeiro quem não tem
/// emprego), anda pela rota até a água mais perto (o pisoteio dessa viagem vira trilha), bebe, enche o cântaro da casa (ou o
/// recipiente inventado, ou o veículo de terra se a rota o comporta) no limite do peso a pé e volta carregado (o passo carregado da
/// AD-179): a despensa só recebe o que chegou, quando a pessoa desarruma em casa. A sede urgente sem água em casa também manda à
/// fonte. Tudo por evento (decisão de quem acorda, chegada, morte): nenhuma varredura por pessoa. Precisa da carga pessoal da
/// invenção (o que se leva nas mãos); sem ela, vale o enchimento de antes.</summary>
public static class WaterFetch
{
    /// <summary>A regra vale neste mundo: água da natureza e a ida ligadas, economia e carga pessoal (a despensa em casa).</summary>
    public static bool On(WorldState world) =>
        world.NaturalWaterRules is { Enabled: true, Fetch.Enabled: true } && world.EconomyRules.Enabled
        && world.InventionOn && world.InventionRules!.PantryNeedsPresence;

    private static ResourceType Water(WorldState world) => new(world.EconomyRules.WaterResourceId);

    /// <summary>A fome (déficit) a partir da qual quem está a caminho da água larga a ida (à beira da morte).</summary>
    internal const int StarvingDeficit = 95;

    /// <summary>A fonte mais perto da casa pela rota (célula e horas a pé), ou nulo se nenhuma no alcance da regra.</summary>
    public static (CellCoord Cell, long Ticks)? SourceFor(WorldState world, CellCoord home)
    {
        var rules = world.NaturalWaterRules.Fetch;
        var found = NaturalWaterSystem.NearestWater(world, home, rules.RadiusCells);
        return found is { } f && f.Ticks <= rules.MaxWalkHours ? f : null;
    }

    private static bool Grown(WorldState world, Npc npc) => npc.AgeYears(world.CurrentDate) >= world.NaturalWaterRules.Fetch.MinAgeYears;

    private static bool Employed(WorldState world, Npc npc) =>
        npc.Employer is not null || LivingWorld.Simulation.Cities.Streets.SiteWork.DestinationOf(world, npc) is not null;

    /// <summary>A ida ainda vale: na volta, enquanto quem foi vive (só acaba em casa); na ida, enquanto está viajando desde que decidiu.</summary>
    private static bool Active(WorldState world, in WaterTrip trip) =>
        world.FindNpc(new NpcId(trip.NpcId)) is { IsAlive: true } npc
        && (trip.Returning || (npc.CurrentAction == ActionType.Travel && npc.ActionStartedAtTick >= trip.StartTick));

    /// <summary>Alguém da casa (fora <paramref name="except"/>) está numa ida de água que ainda vale.</summary>
    private static bool FetcherOut(WorldState world, long householdId, long except)
    {
        if (world.Invention!.WaterTrips is not { Count: > 0 } list) return false;
        foreach (var t in list)
            if (t.HouseholdId == householdId && t.NpcId != except && Active(world, t)) return true;
        return false;
    }

    /// <summary>Quem pode ir buscar água: crescido o bastante e fora da cama.</summary>
    private static bool CanFetch(WorldState world, Npc npc) =>
        npc.IsAlive && Grown(world, npc) && !LivingWorld.Simulation.Health.SickBehavior.IsBedridden(world, npc.Id);

    /// <summary>A casa tem alguém livre (sem emprego), perto de casa, que pode ir: então quem trabalha não vai (o costume da casa).</summary>
    private static bool FreeMemberCanGo(WorldState world, Household home, Npc except)
    {
        var members = home.Members;
        for (int i = 0; i < members.Count; i++)
            if (members[i] != except.Id && world.FindNpc(members[i]) is { } m && CanFetch(world, m) && !Employed(world, m)
                && NearHome(world, m, home)) return true; // livre e por perto: quem está longe não vai
        return false;
    }

    /// <summary>Unidades de água da casa por morador abaixo de <paramref name="days"/> dias.</summary>
    private static bool Below(WorldState world, Household home, int days)
    {
        var partner = PotPartner(world, home);
        long members = Math.Max(1, home.Members.Count + (partner?.Members.Count ?? 0));
        return PotWater(world, home, partner) < members * days;
    }

    /// <summary>AD-184: a casa que divide o pote com esta (de favor ↔ quem acolhe, morando juntos), ou nulo.</summary>
    internal static Household? PotPartner(WorldState world, Household home) =>
        world.StreetRules.LodgersSharePot ? RoofPartner(world, home) : null;

    /// <summary>AD-184: a casa que mora sob o mesmo teto que esta (de favor ↔ quem acolhe), ou nulo.</summary>
    internal static Household? RoofPartner(WorldState world, Household home)
    {
        if (world.Ground is not { Lodgers.Count: > 0 } ground || home.Dwelling is not { } dwelling) return null;
        long day = world.CurrentDate.TotalHours / Math.Max(1, world.Calendar.HoursPerDay);
        if (ground.PotIndex is null || ground.PotIndexDay != day) { ground.PotIndex = BuildPotIndex(world, ground); ground.PotIndexDay = day; }
        if (!ground.PotIndex.TryGetValue(home.Id.Value, out var other)) return null;
        return world.FindHousehold(new HouseholdId(other)) is { IsEmpty: false, IsHomeless: false } p && p.Dwelling == dwelling ? p : null;
    }

    private static Dictionary<long, long> BuildPotIndex(WorldState world, LivingWorld.Simulation.Cities.Streets.GroundLedger ground)
    {
        var index = new Dictionary<long, long>();
        foreach (var lodgerId in ground.Lodgers)
        {
            if (index.ContainsKey(lodgerId) || world.FindHousehold(new HouseholdId(lodgerId)) is not { IsEmpty: false, Dwelling: { } dwelling } lodger) continue;
            Household? host = null;
            foreach (var other in world.Households) // quem acolhe: a casa mais antiga (menor id) que mora ali e não é de favor
                if (other != lodger && !other.IsEmpty && other.Dwelling == dwelling && !ground.Lodgers.Contains(other.Id.Value)
                    && (host is null || other.Id.Value < host.Id.Value)) host = other;
            if (host is null || index.ContainsKey(host.Id.Value)) continue;
            index[lodgerId] = host.Id.Value;
            index[host.Id.Value] = lodgerId;
        }
        return index;
    }

    /// <summary>A água no pote: a da casa mais a de quem divide o pote com ela.</summary>
    private static long PotWater(WorldState world, Household home, Household? partner)
    {
        var water = Water(world);
        return home.Stock.GetValueOrDefault(water) + (partner?.Stock.GetValueOrDefault(water) ?? 0);
    }

    /// <summary>A água do pote para quem está em casa (a da própria casa ou a de quem divide o pote).</summary>
    internal static long PotWater(WorldState world, Household home) => PotWater(world, home, PotPartner(world, home));

    /// <summary>AD-184: sem comida na despensa da casa, um prato da de quem divide o teto (a mesma cozinha); devolve o que comeu.</summary>
    internal static ResourceType? DrawMealFromTable(WorldState world, Household? home)
    {
        if (home is null || !world.StreetRules.LodgersShareTable || RoofPartner(world, home) is not { } partner) return null;
        var meal = LivingWorld.Simulation.Behavior.Resolvers.FoodResolver.ResolveMeal(world, partner);
        return meal.Id > 0 && partner.Withdraw(meal, 1).IsSuccess ? meal : null;
    }

    /// <summary>AD-184: sem água na própria parte, um gole do pote de quem divide a casa (verdadeiro se tirou).</summary>
    internal static bool DrawFromPot(WorldState world, Household? home, ResourceType water)
    {
        if (home is null || water.Id != world.EconomyRules.WaterResourceId || PotPartner(world, home) is not { } partner) return false;
        return partner.Withdraw(water, 1).IsSuccess;
    }

    /// <summary>AD-184: a água da casa baixou ao ponto de alguém ir buscá-la (a hora livre é da fonte, não da obra).</summary>
    internal static bool HomeNeedsWater(WorldState world, Npc npc) =>
        On(world) && npc.Household is { } h && world.FindHousehold(h) is { IsHomeless: false, IsEmpty: false } home
        && Below(world, home, world.NaturalWaterRules.Fetch.RefillBelowDays);

    /// <summary>Tempo livre, em casa ou perto dela: a água da casa baixou e ninguém da casa já foi. Marca a ida (a viagem começa nesta hora) e
    /// devolve verdadeiro para a decisão virar a viagem. Fonte na própria célula da casa: enche já, sem viagem.</summary>
    internal static bool WantsToFetch(WorldState world, Npc npc, long now)
    {
        if (!On(world) || npc.Household is not { } h || world.FindHousehold(h) is not { IsHomeless: false, IsEmpty: false } home) return false;
        var rules = world.NaturalWaterRules.Fetch;
        // o caso comum primeiro (a água da casa ainda dá): uma leitura de estoque, sem olhar corpo nem fonte
        if (!Below(world, home, rules.RefillBelowDays) || !CanFetch(world, npc) || !NearHome(world, npc, home)) return false;
        bool employed = Employed(world, npc);
        if (employed && !Below(world, home, rules.EmployedBelowDays) && FreeMemberCanGo(world, home, npc)) return false;
        // um vai por todos; com a água abaixo de um dia, vai mais de um (a sede não espera a volta do primeiro)
        if ((!Below(world, home, rules.EmployedBelowDays) && (FetcherOut(world, h.Value, npc.Id.Value)
                || (PotPartner(world, home) is { } partner && FetcherOut(world, partner.Id.Value, npc.Id.Value))))
            || SourceFor(world, home.Location) is not { } source) return false;
        if (source.Cell == home.Location) { Fill(world, npc, home, now); Unload(world, npc, home); return false; }
        if (rules.NoEmptyTrips && source.Cell == npc.CurrentLocation)
        {
            // AD-191: já na fonte, enche aqui e a ida vira a volta para casa
            Fill(world, npc, home, now);
            world.Invention!.PutWaterTrip(new WaterTrip(npc.Id.Value, h.Value, source.Cell.X, source.Cell.Y, now, Returning: true));
            world.InventionTally.WaterTrips++;
            return true;
        }
        Start(world, npc, h.Value, source.Cell, now);
        if (employed) world.InventionTally.WaterTripsByEmployed++;
        return true;
    }

    /// <summary>Em casa, sem uma gota de água na despensa: quem decide por urgência (sono, choro, conversa) larga e vai buscar.</summary>
    internal static bool HomeDry(WorldState world, Npc npc) =>
        On(world) && npc.Household is { } h && world.FindHousehold(h) is { IsHomeless: false } home
        && PotWater(world, home) <= 0 && NearHome(world, npc, home);

    /// <summary>Em casa ou no passeio perto dela (a despensa ao alcance da hora): de onde se sai para a fonte com o cântaro da casa.</summary>
    private static bool NearHome(WorldState world, Npc npc, Household home) =>
        npc.CurrentLocation == home.Location || !LivingWorld.Simulation.Invention.PersonalCarry.PantryOutOfReach(world, npc, home);

    /// <summary>AD-184: a fome à beira da morte e a sede urgente: a sede vem antes (mata primeiro), a ida à água segue.</summary>
    internal static bool ThirstFirst(WorldState world, Npc npc, long now) =>
        world.NaturalWaterRules.Fetch.ThirstFirstWhenBothStarving && 100 - npc.ThirstAt(now) > world.NeedsRules.UrgencyThreshold;

    /// <summary>A sede urgente sem água no que leva nem em casa (a despensa ao alcance), sem ida de água em curso: o caso em que a
    /// fonte pode valer mais que o mercado. Só leitura.</summary>
    internal static bool ThirstyWithoutWater(WorldState world, Npc npc, long now)
    {
        if (!On(world) || npc.Household is not { } h || world.FindHousehold(h) is not { IsHomeless: false } home) return false;
        if (100 - npc.ThirstAt(now) <= world.NeedsRules.UrgencyThreshold) return false;
        if (world.Map.TryGetCell(npc.CurrentLocation, out var here) && here.HasWater) { DrinkHere(world, npc, now); return false; }
        // a criança pequena não sai sozinha à fonte (a ida virava ida e volta de escolha a cada hora): o vizinho acode
        if (!Grown(world, npc)) return false;
        var water = Water(world);
        var ledger = world.Invention!;
        if (ledger.CarriedQuantity(npc.Id.Value, water.Id) > 0) return false;
        int i = ledger.WaterTripIndex(npc.Id.Value);
        if (i >= 0 && Active(world, ledger.WaterTrips![i])) return false;
        if (PotWater(world, home) <= 0) return true;
        // há água em casa: vai à fonte só quem está fora e tem a fonte mais perto que a casa (o resto bebe em casa)
        if (npc.CurrentLocation == home.Location) return false;
        var source = NaturalWaterSystem.NearestWater(world, npc.CurrentLocation, world.NaturalWaterRules.Fetch.RadiusCells * 2);
        return source is { } src && src.Ticks < LivingWorld.Simulation.Cities.Streets.StreetRoutes.Ticks(world, npc.CurrentLocation, home.Location);
    }

    /// <summary>A criança pequena com sede urgente, sem água em casa nem no que leva: a casa mais perto da mesma cidade com água
    /// sobrando acode na hora com um dia da casa (como a ajuda mútua de água, mas quando o choro aperta, sem esperar o dia).
    /// Devolve verdadeiro se a água chegou.</summary>
    internal static bool NeighbourWatersChild(WorldState world, Npc npc, long now)
    {
        if (!On(world) || Grown(world, npc) || npc.Household is not { } h || world.FindHousehold(h) is not { IsHomeless: false } home) return false;
        if (100 - npc.ThirstAt(now) <= world.NeedsRules.UrgencyThreshold) return false;
        var water = Water(world);
        if (PotWater(world, home) > 0 || world.Invention!.CarriedQuantity(npc.Id.Value, water.Id) > 0) return false;
        Household? neighbour = null;
        long bestDistance = long.MaxValue;
        foreach (var other in world.Households)
        {
            if (other.Id == home.Id || other.IsEmpty || other.City != home.City
                || other.Stock.GetValueOrDefault(water) <= Math.Max(1, other.Members.Count)) continue;
            long d = Math.Abs(other.Location.X - home.Location.X) + Math.Abs(other.Location.Y - home.Location.Y);
            if (d < bestDistance || (d == bestDistance && neighbour is not null && other.Id.Value < neighbour.Id.Value)) { neighbour = other; bestDistance = d; }
        }
        if (neighbour is null) return false;
        long give = Math.Min(Math.Max(1, home.Members.Count), neighbour.Stock.GetValueOrDefault(water) - Math.Max(1, neighbour.Members.Count));
        if (give <= 0 || !neighbour.Withdraw(water, give).IsSuccess) return false;
        home.Deposit(water, give);
        world.InventionTally.WaterAidGiven++;
        world.InventionTally.WaterAidUnits += give;
        return true;
    }

    /// <summary>De cama longe de casa, com sede urgente e sem água no que leva, a até uma hora de uma fonte: quem está por perto traz
    /// um gole da fonte (o doente não anda; antes morria de sede a uma célula do rio). Devolve verdadeiro se bebeu.</summary>
    internal static bool SickGetsWaterBrought(WorldState world, Npc npc, long now)
    {
        if (!On(world) || 100 - npc.ThirstAt(now) <= world.NeedsRules.UrgencyThreshold) return false;
        if (!LivingWorld.Simulation.Health.SickBehavior.IsBedridden(world, npc.Id)) return false;
        if (world.Invention!.CarriedQuantity(npc.Id.Value, Water(world).Id) > 0) return false;
        if (npc.Household is { } h && world.FindHousehold(h) is { } home && npc.CurrentLocation == home.Location
            && home.Stock.GetValueOrDefault(Water(world)) > 0) return false;
        if (NaturalWaterSystem.NearestWater(world, npc.CurrentLocation, world.NaturalWaterRules.Fetch.RadiusCells) is not { Ticks: <= 1 }) return false;
        DrinkHere(world, npc, now);
        world.InventionTally.WaterAidGiven++;
        return true;
    }

    /// <summary>Bebe um gole da despensa da casa (em casa, antes de sair).</summary>
    internal static void DrinkFromPantry(WorldState world, Npc npc, long now)
    {
        if (npc.Household is not { } h || world.FindHousehold(h) is not { } home) return;
        var water = Water(world);
        int level = LivingWorld.Simulation.Objects.ObjectUse.ConsumeLevel(world, water, LivingWorld.Domain.Objects.ObjectEffectTargets.Thirst);
        if (npc.ThirstAt(now) >= level || (!home.Withdraw(water, 1).IsSuccess && !DrawFromPot(world, home, water))) return;
        npc.SetThirst(level, now);
        world.RecordResourceConsumed(water, 1);
    }

    /// <summary>AD-184: a hora de beber. Em casa e acordado (ou indo deitar), com água no pote: bebe um copo quando a sede passou da
    /// linha da refeição, ou quando a noite a levaria até lá. A criança pequena só bebe com um crescido da casa ali, acordado (quem cuida
    /// dá a água). Devolve verdadeiro se bebeu.</summary>
    internal static bool SipAtHome(WorldState world, Npc npc, ActionType chosen, long nightHours, long now)
    {
        if (!world.NaturalWaterRules.Fetch.DrinkAtMealLine || !On(world)) return false;
        if (npc.CurrentAction == ActionType.Sleep && chosen == ActionType.Sleep) return false; // dormindo não bebe
        int line = 100 - LivingWorld.Simulation.Invention.PersonalCarry.MealTimeDeficit;
        int thirst = npc.ThirstAt(now);
        bool due = thirst <= line || (chosen == ActionType.Sleep && thirst - world.NeedsRules.ThirstDecayPerHour * nightHours <= line);
        if (!due || npc.Household is not { } h || world.FindHousehold(h) is not { IsHomeless: false } home || npc.CurrentLocation != home.Location
            || PotWater(world, home) <= 0) return false;
        if (npc.AgeYears(world.CurrentDate) < world.NaturalWaterRules.Fetch.SelfDrinkAgeYears && !CarerHome(world, home, npc)) return false;
        DrinkFromPantry(world, npc, now);
        if (npc.ThirstAt(now) <= thirst) return false;
        // com o copo, a fome já na hora da refeição come da despensa (com apetite só, comia antes da hora e a cidade comia o celeiro) (antes era a sede que levava à refeição: sem ela, a fome ficava para a crítica)
        if (100 - npc.HungerAt(now) >= LivingWorld.Simulation.Invention.PersonalCarry.MealTimeDeficit)
        {
            var meal = LivingWorld.Simulation.Behavior.Resolvers.FoodResolver.ResolveMeal(world, home);
            if (meal.Id > 0 && home.Withdraw(meal, 1).IsSuccess)
            {
                npc.SetHunger(Math.Max(npc.HungerAt(now), LivingWorld.Simulation.Objects.ObjectUse.ConsumeLevel(world, meal, LivingWorld.Domain.Objects.ObjectEffectTargets.Hunger)), now);
                world.RecordResourceConsumed(meal, 1);
            }
        }
        world.InventionTally.DrankBeforeSleep += chosen == ActionType.Sleep ? 1 : 0;
        return true;
    }

    /// <summary>Um crescido da casa (ou de quem divide o pote) em casa e acordado.</summary>
    private static bool CarerHome(WorldState world, Household home, Npc child)
    {
        var partner = PotPartner(world, home);
        foreach (var hh in partner is null ? new[] { home } : new[] { home, partner })
            foreach (var id in hh.Members)
                if (id != child.Id && world.FindNpc(id) is { IsAlive: true } m && Grown(world, m) && m.CurrentLocation == home.Location
                    && m.CurrentAction != ActionType.Sleep) return true;
        return false;
    }

    /// <summary>Em casa, com sede urgente e água na despensa.</summary>
    internal static bool ThirstyAtHomeWithWater(WorldState world, Npc npc, long now) =>
        On(world) && 100 - npc.ThirstAt(now) > world.NeedsRules.UrgencyThreshold && npc.Household is { } h
        && world.FindHousehold(h) is { IsHomeless: false } home && npc.CurrentLocation == home.Location
        && PotWater(world, home) > 0;

    /// <summary>A sede urgente fora de casa, com água em casa mais perto que qualquer fonte: a escolha tem de ser voltar para beber
    /// (a utilidade às vezes ficava à toa e a pessoa morria de sede a uma hora da despensa).</summary>
    internal static bool ThirstyAwayWithWaterHome(WorldState world, Npc npc, long now)
    {
        if (!On(world) || npc.Household is not { } h || world.FindHousehold(h) is not { IsHomeless: false } home || npc.CurrentLocation == home.Location) return false;
        if (100 - npc.ThirstAt(now) <= world.NeedsRules.UrgencyThreshold) return false;
        var water = Water(world);
        return PotWater(world, home) > 0 && world.Invention!.CarriedQuantity(npc.Id.Value, water.Id) <= 0;
    }

    /// <summary>Depois de <see cref="ThirstyWithoutWater"/>: a ida à fonte vence o mercado quando a fonte fica mais perto (ou não há
    /// com que pagar). Devolve verdadeiro com a ida marcada.</summary>
    internal static bool ThirstSendsToSource(WorldState world, Npc npc, long now, long marketTicks, bool canPay)
    {
        var rules = world.NaturalWaterRules.Fetch;
        // a sede urgente anda o dobro do que se anda para encher o cântaro. AD-187: perto de casa, vai à fonte que a casa conhece (a de
        // sempre, a rota já sabida); longe, à alcançável mais perto de onde a pessoa está
        if (ThirstSource(world, npc) is not { } source || source.Ticks > rules.MaxWalkHours * 2) return false;
        if (source.Cell == npc.CurrentLocation) { DrinkHere(world, npc, now); return false; }
        if (canPay && marketTicks < source.Ticks) return false;
        Start(world, npc, npc.Household!.Value.Value, source.Cell, now);
        world.InventionTally.WaterTripsByThirst++;
        return true;
    }

    private static (CellCoord Cell, long Ticks)? ThirstSource(WorldState world, Npc npc)
    {
        var rules = world.NaturalWaterRules.Fetch;
        if (npc.Household is { } h && world.FindHousehold(h) is { IsHomeless: false } home && NearHome(world, npc, home)
            && SourceFor(world, home.Location) is { } known)
        {
            if (npc.CurrentLocation == home.Location || known.Cell == npc.CurrentLocation) return known.Cell == npc.CurrentLocation ? (known.Cell, 0) : known;
            return (known.Cell, world.StreetsOn ? LivingWorld.Simulation.Cities.Streets.StreetRoutes.Ticks(world, npc.CurrentLocation, known.Cell)
                : LivingWorld.Simulation.Economy.Market.TravelTicksCache.Between(world.Map, npc.CurrentLocation, known.Cell));
        }
        return NaturalWaterSystem.NearestWater(world, npc.CurrentLocation, rules.RadiusCells * 2);
    }

    /// <summary>Na beira da água: bebe dali (a água entra e sai do mundo no mesmo gole).</summary>
    internal static void DrinkHere(WorldState world, Npc npc, long now)
    {
        var water = Water(world);
        int level = LivingWorld.Simulation.Objects.ObjectUse.ConsumeLevel(world, water, LivingWorld.Domain.Objects.ObjectEffectTargets.Thirst);
        if (npc.ThirstAt(now) >= level) return;
        npc.SetThirst(level, now);
        world.RecordResourceProduced(water, 1);
        world.RecordResourceConsumed(water, 1);
        world.InventionTally.DrankAtSource++;
    }

    private static void Start(WorldState world, Npc npc, long household, CellCoord source, long now)
    {
        world.Invention!.PutWaterTrip(new WaterTrip(npc.Id.Value, household, source.X, source.Y, now, Returning: false));
        world.InventionTally.WaterTrips++;
    }

    /// <summary>A volta carregado: quem já encheu na fonte vai para casa antes de qualquer outra coisa que não seja urgente.</summary>
    internal static bool HeadingHome(WorldState world, Npc npc)
    {
        if (world.Invention?.WaterTrips is not { Count: > 0 } || npc.Household is not { } h) return false;
        int i = world.Invention.WaterTripIndex(npc.Id.Value);
        if (i < 0) return false;
        var trip = world.Invention.WaterTrips![i];
        if (!trip.Returning || trip.HouseholdId != h.Value || world.FindHousehold(h) is not { } home || home.Location == npc.CurrentLocation)
            return false;
        return true;
    }

    /// <summary>O destino da ida de água em curso: a fonte na ida, a casa na volta; nulo fora de uma ida.</summary>
    internal static CellCoord? ErrandDestination(WorldState world, Npc npc)
    {
        if (world.Invention?.WaterTrips is not { Count: > 0 }) return null;
        int i = world.Invention.WaterTripIndex(npc.Id.Value);
        if (i < 0) return null;
        var trip = world.Invention.WaterTrips![i];
        if (trip.Returning)
            return npc.Household is { } h && h.Value == trip.HouseholdId && world.FindHousehold(h) is { } home ? home.Location : null;
        return Active(world, trip) ? new CellCoord(trip.X, trip.Y) : null;
    }

    /// <summary>Quem está na ida à fonte ou na volta com a água segue andando: a nova escolha da hora não recomeça a viagem (a
    /// troca de ideia a cada hora fazia a pessoa nunca chegar e morrer de sede a uma hora da fonte).</summary>
    internal static bool OnTheWay(WorldState world, Npc npc)
    {
        if (npc.CurrentAction != ActionType.Travel || world.Invention?.WaterTrips is not { Count: > 0 }) return false;
        int i = world.Invention.WaterTripIndex(npc.Id.Value);
        return i >= 0 && Active(world, world.Invention.WaterTrips![i]) && ErrandDestination(world, npc) is { } to && to != npc.CurrentLocation;
    }

    /// <summary>Na ida para a fonte não se arruma carga de casa (o cântaro vai vazio).</summary>
    internal static bool Outbound(WorldState world, Npc npc)
    {
        if (world.Invention?.WaterTrips is not { Count: > 0 }) return false;
        int i = world.Invention.WaterTripIndex(npc.Id.Value);
        return i >= 0 && !world.Invention.WaterTrips![i].Returning;
    }

    /// <summary>A chegada: na fonte, bebe e enche (a volta começa); em casa, na volta, desarruma o que trouxe e a ida acaba.</summary>
    internal static void OnArrived(WorldState world, TickContext ctx, Npc npc, CellCoord cell)
    {
        if (world.Invention?.WaterTrips is not { Count: > 0 }) return;
        int i = world.Invention.WaterTripIndex(npc.Id.Value);
        if (i < 0) return;
        var trip = world.Invention.WaterTrips![i];
        var home = world.FindHousehold(new HouseholdId(trip.HouseholdId));
        if (!trip.Returning && cell.X == trip.X && cell.Y == trip.Y)
        {
            if (home is null || home.IsEmpty) { world.Invention.RemoveWaterTrip(npc.Id.Value); return; }
            Fill(world, npc, home, ctx.CurrentTick);
            world.Invention.PutWaterTrip(trip with { Returning = true, StartTick = ctx.CurrentTick });
            return;
        }
        if (trip.Returning && home is not null && cell == home.Location)
        {
            Unload(world, npc, home);
            world.Invention.RemoveWaterTrip(npc.Id.Value);
        }
    }

    /// <summary>Na fonte: bebe até matar a sede e enche até a meta da casa, no que couber no peso a pé e no cântaro (ou no recipiente
    /// ou no veículo da casa). A água entra no mundo aqui (produzida) e fica na carga de quem a leva até chegar em casa.</summary>
    private static void Fill(WorldState world, Npc npc, Household home, long now)
    {
        var water = Water(world);
        var inv = world.InventionRules!;
        DrinkHere(world, npc, now);
        var partner = PotPartner(world, home); // AD-184: quem divide o pote enche para as duas casas
        long want = Math.Max(1, home.Members.Count + (partner?.Members.Count ?? 0)) * (long)world.NaturalWaterRules.DaysOfSupply
            - PotWater(world, home, partner) - world.Invention!.CarriedQuantity(npc.Id.Value, water.Id);
        if (want <= 0) return;
        var room = LivingWorld.Simulation.Invention.PersonalCarry.RoomOf(world, npc);
        long weight = room.WeightG;
        long volume = Math.Max(room.WetMl, world.NaturalWaterRules.Fetch.VesselMl);
        // o veículo de terra da casa leva um barril se a rota da casa até a fonte o comporta (AD-176)
        if (inv.LandVehicles && world.Invention.BestHauler(home.Id.Value, inv) is { Exists: true } hauler && hauler.CapacityG > weight
            && LivingWorld.Simulation.Cities.Streets.StreetRoutes.CartFits(world, home.Location, npc.CurrentLocation))
        {
            weight = hauler.CapacityG;
            volume = Math.Max(volume, hauler.CapacityG * 1000L / Math.Max(1, inv.WaterDensityKgM3));
        }
        int carried = 0;
        long loadedG = 0;
        int ci = world.Invention.CarriedIndex(npc.Id.Value, int.MinValue);
        for (ci = ci >= 0 ? ci : ~ci; ci < world.Invention.Carried.Count && world.Invention.Carried[ci].NpcId == npc.Id.Value; ci++)
        {
            var load = world.Invention.Carried[ci];
            loadedG += (long)load.Quantity * inv.UnitGramsOf(load.ResourceId);
            if (load.ResourceId == water.Id) carried += load.Quantity;
        }
        weight -= loadedG;
        volume -= (long)carried * inv.UnitMlOf(water.Id);
        long n = Math.Min(want, Math.Min(Math.Max(0, volume) / inv.UnitMlOf(water.Id), Math.Max(0, weight) / inv.UnitGramsOf(water.Id)));
        if (world.NaturalWaterRules.Fetch.NoEmptyTrips)
        {
            // AD-191: o recipiente da casa pesado demais (cheio passa do peso a pé) fica em casa: leva o cântaro simples, no peso do corpo
            long bareWeight = inv.FootCarryKg * 1000L - loadedG;
            long bareVolume = world.NaturalWaterRules.Fetch.VesselMl - (long)carried * inv.UnitMlOf(water.Id);
            n = Math.Max(n, Math.Min(want, Math.Min(Math.Max(0, bareVolume) / inv.UnitMlOf(water.Id), Math.Max(0, bareWeight) / inv.UnitGramsOf(water.Id))));
        }
        if (n <= 0) return;
        world.Invention.Carry(npc.Id.Value, water.Id, (int)n);
        world.RecordResourceProduced(water, n);
    }

    /// <summary>Em casa: a água que chegou vai para a despensa (o resto da carga também, como em toda volta).</summary>
    private static void Unload(WorldState world, Npc npc, Household home)
    {
        long before = home.Stock.GetValueOrDefault(Water(world));
        LivingWorld.Simulation.Invention.PersonalCarry.Unpack(world, npc, home);
        long brought = home.Stock.GetValueOrDefault(Water(world)) - before;
        world.InventionTally.WaterTripsHome++;
        world.InventionTally.WaterUnitsHome += Math.Max(0, brought);
        // AD-184: no mesmo pote, a água que chegou fica para as duas casas na proporção de quem mora em cada uma
        if (PotPartner(world, home) is not { } partner) return;
        var water = Water(world);
        long mine = home.Stock.GetValueOrDefault(water), theirs = partner.Stock.GetValueOrDefault(water);
        long share = (mine + theirs) * partner.Members.Count / Math.Max(1, home.Members.Count + partner.Members.Count);
        long give = Math.Min(mine, share - theirs);
        if (give > 0 && home.Withdraw(water, give).IsSuccess) partner.Deposit(water, give);
    }

    /// <summary>Quem morreu ou saiu da casa: a ida acaba (a água que levava segue o caminho do espólio, <see cref="LivingWorld.Simulation.Invention.Leavings"/>).</summary>
    internal static void Forget(WorldState world, long npcId) => world.Invention?.RemoveWaterTrip(npcId);

    /// <summary>A casa não tem quem busque: ninguém crescido e fora da cama. É a que a ajuda mútua socorre com água.</summary>
    internal static bool NoFetcher(WorldState world, Household home)
    {
        var members = home.Members;
        for (int i = 0; i < members.Count; i++)
            if (world.FindNpc(members[i]) is { } m && CanFetch(world, m)) return false;
        return true;
    }

    /// <summary>Idas de água que já não valem (quem foi morreu, desistiu na ida, mudou de casa) saem no mês: o razão não cresce.</summary>
    internal static void Prune(WorldState world)
    {
        if (world.Invention?.WaterTrips is not { Count: > 0 } list) return;
        foreach (var trip in list.ToList())
        {
            var npc = world.FindNpc(new NpcId(trip.NpcId));
            bool moved = npc?.Household is not { } h || h.Value != trip.HouseholdId;
            if (npc is not { IsAlive: true } || moved || !Active(world, trip)) world.Invention.RemoveWaterTrip(trip.NpcId);
        }
    }
}
