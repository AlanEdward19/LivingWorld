using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Cities.Works;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>AD-184: toda construção é obra de verdade. A casa de quem casa é obra num lote (o casal mora de favor até ela ficar
/// pronta), feita pela família nas horas livres, por parentes e vizinhos e por diaristas que a casa pode pagar; o jornal disputa a mão
/// com o ofício e sobe quando falta braço; obra sem gente para, retoma e é abandonada; a oficina de quem empreende abre quando o
/// prédio fica pronto; os eventos de obra levam o id e a célula.</summary>
public class BuildWorksTests
{
    private sealed class Sink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static (WorldState World, WorldClock Clock, Sink Sink) World(ulong seed = 3, StreetRules? streets = null)
    {
        var (world, clock) = ScenarioRunner.Create(seed, cityLayout: streets is null ? null : CityLayoutRules.Default with { Streets = streets });
        var sink = new Sink();
        return (world, new WorldClock(clock.Systems, clock.MaxIterationsPerTick, sink), sink);
    }

    private static TickContext Ctx(WorldState world, Sink? sink = null) => new(world, world.Rng, world.Scheduler, sink);

    private static bool Adult(WorldState world, Npc npc) =>
        world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) == LifeStage.Adult;

    /// <summary>Dois adultos solteiros vivos de casas diferentes, cada um morando numa casa com mais gente (a casa dos pais).</summary>
    private static (Npc A, Npc B) Singles(WorldState world)
    {
        var singles = world.Npcs.Where(n => n.IsAlive && n.Spouse is null && Adult(world, n) && n.Household is { } h
            && world.FindHousehold(h) is { Dwelling: not null } home && home.Members.Count > 1).OrderBy(n => n.Id.Value).ToList();
        var a = singles.First();
        var b = singles.First(n => n.Household != a.Household && n.Id != a.Id);
        return (a, b);
    }

    [Fact]
    public void A_married_couple_lives_where_they_can_and_their_house_exists_only_when_the_work_ends()
    {
        var (world, clock, sink) = World(streets: StreetRules.Default with { HouseHours = 60, HouseGatherHours = 0, HouseMaterialUnits = 0 }); // AD-194: sem material, o teste é a obra
        var (a, b) = Singles(world);
        var head = a.Id.Value <= b.Id.Value ? a : b;
        var parents = world.FindHousehold(head.Household!.Value)!;
        var parentsHouse = parents.Dwelling!.Value;
        var otherParents = world.FindHousehold((head == a ? b : a).Household!.Value)!.Dwelling!.Value;
        int buildingsBefore = world.Buildings.Count;
        foreach (var spouse in new[] { a, b }) spouse.TryDebitWallet(spouse.Wallet); // sem moeda: só os próprios braços e os da família
        MarriageSystem.Marry(world, Ctx(world, sink), a, b);
        var couple = world.FindHousehold(a.Household!.Value)!;
        // de favor na casa dos pais de um dos dois (AD-192: a rota pela melhor entrada da rede mudou qual é a mais conveniente)
        Assert.Contains(couple.Dwelling!.Value, new[] { parentsHouse, otherParents });
        parentsHouse = couple.Dwelling!.Value;
        Assert.Equal(buildingsBefore, world.Buildings.Count); // casar não ergue casa nenhuma

        BuildWork? work = null;
        for (int h = 0; h < 24 * 10 && work is null; h++)
        {
            clock.Run(world, 1);
            work = world.Ground!.Builds.FirstOrDefault(w => w.Household == couple.Id.Value);
        }
        Assert.NotNull(work);
        var canteiro = world.FindBuilding(new BuildingId(work.Building))!;
        Assert.Equal(work.Id, canteiro.UnderWork); // o lote é dela, mas o prédio ainda não existe
        Assert.Equal(parentsHouse, couple.Dwelling); // seguem de favor enquanto a obra anda
        var started = sink.Events.Single(e => e.Kind == WorldEventKind.BuildWorkStarted && e.Payload.StartsWith($"{work.Id}|", StringComparison.Ordinal));
        var f = started.Payload.Split('|');
        Assert.Equal(("casa", work.X, work.Y), (f[2], int.Parse(f[3]), int.Parse(f[4]))); // evento com id, tipo e célula

        for (int h = 0; h < 24 * 120 && world.Ground!.Builds.Contains(work); h++) clock.Run(world, 1);
        Assert.DoesNotContain(work, world.Ground!.Builds);
        Assert.Null(canteiro.UnderWork); // pronta: a casa passa a existir
        Assert.Equal(canteiro.Id, couple.Dwelling); // e o casal muda para ela
        Assert.True(work.HoursByHelpers > 0, "a família e os parentes trabalharam nas horas livres");
        Assert.True(work.HoursByHelpers > work.HoursByCrew, $"ajuda {work.HoursByHelpers} h, turma paga {work.HoursByCrew} h"); // sem moeda no começo, a maior parte é braço da família
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.BuildWorkDone && e.Payload.StartsWith($"{work.Id}|", StringComparison.Ordinal)
            && e.CauseEventId == started.EventId);
    }

    /// <summary>A família escolhe o lote pelas idas de cada dia (água, mercado, o trabalho de cada um): o lote da obra não custa mais
    /// a ela que o lote de sempre (o mais perto do largo), nem que nenhum dos outros que ela podia ter.</summary>
    [Fact]
    public void The_family_picks_the_lot_that_costs_it_least_in_daily_trips()
    {
        var (world, clock, _) = World(streets: StreetRules.Default with { QuarterInLotChoice = false, ObstacleOnCompletion = false }); // AD-192: só as idas pesam, e o canteiro não muda as rotas que a conta relê depois
        clock.Run(world, 24); // o primeiro dia contrata
        var city = world.ActiveCities().First();
        var home = world.Households.First(h => !h.IsEmpty && h.Members.Any(m => world.FindNpc(m)?.Employer is not null));
        var members = home.Members.Select(world.FindNpc).Where(n => n is { IsAlive: true }).Cast<Npc>().ToList();
        var places = HomePlaces.Of(world, city.Id, members);
        var bounds = LivingWorld.Simulation.Cities.CityOccupancy.ResolveGrownBounds(world, city,
            LivingWorld.Simulation.Cities.Queries.CityPopulationQuery.Population(world, city.Id)).Bounds;
        var profile = LivingWorld.Simulation.Cities.Layout.LotProfiles.ForHousehold(world, city.Id, members);
        var options = LivingWorld.Simulation.Cities.CityOccupancy.CandidateLots(world, city, bounds, new BuildingId(world.NextBuildingId), -1, 0, profile, 1, 8);
        Assert.True(options.Count > 1, "há mais de um lote para escolher");
        foreach (var npc in members) npc.CreditWallet(new Money(100_000)); // o foro não decide aqui
        home.Evict(world.CurrentDate.TotalHours); // sem casa: muda de qualquer jeito, o teste é qual lote

        Assert.True(BuildWorks.StartHouse(world, Ctx(world), city, home, members));
        var work = world.Ground!.Builds.Single(w => w.Household == home.Id.Value);
        double chosen = places.YearlyCost(world, new LivingWorld.Domain.Geography.CellCoord(work.X, work.Y));
        Assert.True(chosen <= places.YearlyCost(world, options[0].Origin) + 1e-6, "não pior que o lote de sempre");
        Assert.True(chosen <= options.Min(o => places.YearlyCost(world, o.Origin)) + 1e-6, "o melhor dos que ela podia ter");
    }

    /// <summary>Quem mora (de favor, apertado) só muda se a casa nova não custa em idas mais que o que vale ter casa própria; quem não
    /// tem casa nenhuma muda sempre.</summary>
    [Fact]
    public void A_family_with_a_roof_moves_only_if_the_new_place_is_worth_its_daily_trips()
    {
        var (world, clock, _) = World(streets: StreetRules.Default with { OwnHomeDaysPerYear = -100_000 }); // casa própria não vale nada
        clock.Run(world, 24);
        var city = world.ActiveCities().First();
        var home = world.Households.First(h => !h.IsEmpty && h.Dwelling is not null);
        var members = home.Members.Select(world.FindNpc).Where(n => n is { IsAlive: true }).Cast<Npc>().ToList();
        Assert.False(BuildWorks.StartHouse(world, Ctx(world), city, home, members));
        home.Evict(world.CurrentDate.TotalHours);
        Assert.True(BuildWorks.StartHouse(world, Ctx(world), city, home, members));
    }

    [Fact]
    public void Without_the_rule_the_couple_stays_in_the_cell_and_no_house_work_opens()
    {
        var (world, clock, _) = World(streets: StreetRules.Default.WithoutAd184());
        var (a, b) = Singles(world);
        MarriageSystem.Marry(world, Ctx(world), a, b);
        Assert.Null(world.FindHousehold(a.Household!.Value)!.Dwelling);
        clock.Run(world, 24 * 10);
        Assert.Empty(world.Ground!.Builds);
    }

    [Fact]
    public void The_house_work_hours_add_the_croft_and_the_material_gathered_from_the_land()
    {
        var (world, clock, sink) = World(streets: StreetRules.Default with { OwnHomeDaysPerYear = 100_000 }); // o custo das idas não impede a obra aqui
        var (a, b) = Singles(world);
        MarriageSystem.Marry(world, Ctx(world, sink), a, b);
        clock.Run(world, 24 * 8);
        var started = sink.Events.First(e => e.Kind == WorldEventKind.BuildWorkStarted && e.Payload.Split('|')[2] == "casa");
        var f = started.Payload.Split('|');
        long hours = long.Parse(f[6]);
        int croft = int.Parse(f[7]);
        var rules = world.StreetRules;
        // AD-194: o material chega carga por carga pelas horas da obra (não entra nas horas da parede); sem a regra, a vila de código
        // que não vende madeira somava as horas de buscá-la na natureza
        Assert.Equal(rules.HouseHours + croft * rules.CroftHoursPerCell + (rules.MaterialOnSite ? 0 : rules.HouseGatherHours), hours);
    }

    /// <summary>A obra da casa rica, numa vila de pleno emprego: ninguém sem emprego aceita no jornal do dia; a oferta sobe toda semana
    /// até passar o salário pela margem e então um empregado larga o ofício pela obra (evento de saída com a obra).</summary>
    [Fact]
    public void The_offer_rises_weekly_without_hands_and_an_employed_worker_quits_for_it_when_it_beats_his_wage()
    {
        var (world, _, sink) = World(streets: StreetRules.Default with { HelpersBuild = false });
        var ctx = Ctx(world, sink);
        foreach (var npc in world.Npcs.Where(n => n.IsAlive && n.Employer is null && Adult(world, n)))
        {
            var farm = world.Workplaces.OrderBy(w => w.Id.Value).First(w => w.Employees.Count < w.MaxVacancies);
            farm.Hire(npc.Id);
            npc.Hire(farm.Id);
        }
        var city = world.ActiveCities().First();
        var owner = world.Households.First(h => !h.IsEmpty);
        var rich = world.FindNpc(owner.Head)!;
        rich.CreditWallet(new Money(1_000_000));
        var work = BuildWorks.Open(world, ctx, city, BuildKind.House, -1, city.Location, 0, 0, owner.Id.Value, rich.Id.Value, 4000, 0, "teste");
        var site = new WorkSite(null, null, null, work);
        Assert.Equal(1000, work.OfferPm);
        int quitAt = -1;
        for (int day = 0; day < 120 && quitAt < 0; day++)
        {
            world.CurrentDate = world.CurrentDate.AddHours(24);
            SiteWork.Staff(world, ctx, site);
            if (sink.Events.Any(e => e.Kind == WorldEventKind.Fired && e.Payload.Contains($"|obra-{work.Id}", StringComparison.Ordinal))) quitAt = day;
        }
        Assert.True(quitAt > 0, $"oferta {work.OfferPm}");
        double wage = world.EconomyRules.WageByProfession.Values.Min() / (double)world.Calendar.DaysPerMonth;
        double offer = PavingDecision.DayWage(world) * work.OfferPm / 1000.0;
        Assert.True(offer >= wage * world.StreetRules.QuitMarginSlack, $"oferta {offer} salario {wage}");
        Assert.True(work.OfferPm <= world.StreetRules.OfferMaxPm);
        var quitter = world.FindNpc(new NpcId(work.Crew[0]))!;
        Assert.Null(quitter.Employer); // largou o ofício: é diarista agora
    }

    /// <summary>Quem largou o ofício pela obra volta ao lugar de antes quando sai da turma (a obra acabou), se ainda há vaga lá.</summary>
    [Fact]
    public void Who_left_the_trade_for_the_work_gets_the_old_place_back_when_it_ends()
    {
        var (world, clock, _) = World(streets: StreetRules.Default with { HelpersBuild = false });
        clock.Run(world, 24);
        var ctx = Ctx(world);
        var city = world.ActiveCities().First();
        var owner = world.Households.First(h => !h.IsEmpty);
        world.FindNpc(owner.Head)!.CreditWallet(new Money(1_000_000));
        var work = BuildWorks.Open(world, ctx, city, BuildKind.House, -1, city.Location, 0, 0, owner.Id.Value, owner.Head.Value, 4000, 0, "teste");
        work.OfferPm = world.StreetRules.OfferMaxPm; // paga o bastante para tirar alguém do ofício
        var site = new WorkSite(null, null, null, work);
        long? quitter = null;
        WorkplaceId? from = null;
        for (int day = 0; day < 30 && quitter is null; day++)
        {
            var before = world.Npcs.Where(n => n.IsAlive && n.Employer is not null).ToDictionary(n => n.Id.Value, n => n.Employer!.Value);
            world.CurrentDate = world.CurrentDate.AddHours(24);
            SiteWork.Staff(world, ctx, site);
            foreach (long id in work.Crew)
                if (before.TryGetValue(id, out var shop)) { quitter = id; from = shop; break; }
        }
        Assert.NotNull(quitter);
        var npc = world.FindNpc(new NpcId(quitter!.Value))!;
        Assert.Null(npc.Employer);
        work.MaterialOnSite = work.MaterialNeeded - work.MaterialPlaced; // AD-195: o material já trazido
        BuildWorks.Progress(world, ctx, work, npc, work.HoursLeft + 1_000, paid: 0, world.CurrentDate.TotalHours); // a obra acaba (AD-194: com as cargas)
        BuildWorks.Tick(world, ctx);
        Assert.Equal(from, npc.Employer);
    }

    [Fact]
    public void Employed_take_the_work_only_above_their_wage_by_the_margin_and_the_unemployed_first()
    {
        var (world, clock, _) = World();
        clock.Run(world, 24); // o primeiro dia contrata
        var ctx = Ctx(world);
        var city = world.ActiveCities().First();
        var employed = world.Npcs.First(n => n.IsAlive && n.Employer is not null && Adult(world, n)
            && world.FindWorkplace(n.Employer!.Value)!.Owner != n.Id);
        var owner = world.Households.First(h => !h.IsEmpty && !h.Members.Contains(employed.Id));
        var work = BuildWorks.Open(world, ctx, city, BuildKind.House, -1, city.Location, 0, 0, owner.Id.Value, owner.Head.Value, 400, 0, "teste");
        var site = new WorkSite(null, null, null, work);
        employed.MarkJobChange(0); // sem carência de troca recente
        var low = SiteWork.Evaluate(world, employed, site);
        Assert.Equal(0, low.Value);
        Assert.Equal("salario", low.Reason);
        work.OfferPm = 2500;
        var high = SiteWork.Evaluate(world, employed, site);
        Assert.True(high.Value > 0, high.Reason);
        Assert.True(high.OfferDay >= high.WageDay * SiteWork.MarginFor(world, employed));

        world.FindWorkplace(employed.Employer!.Value)!.Fire(employed.Id);
        employed.Fire();
        var idle = SiteWork.Evaluate(world, employed, site);
        Assert.True(idle.Value > high.Value, $"sem emprego {idle.Value} empregado {high.Value}");
    }

    [Fact]
    public void A_farmhand_leaves_for_the_work_more_easily_in_the_slack_season_than_at_harvest()
    {
        var (world, clock, _) = World();
        clock.Run(world, 24); // o primeiro dia contrata
        var farmhand = world.Npcs.First(n => n.IsAlive && n.Employer is { } e && world.FindCropAt(world.FindWorkplace(e)!.Location) is not null);
        var agri = world.AgricultureRules;
        while (agri.IsSowingMonth(world.CurrentDate.Month) || world.CurrentDate.Month >= agri.EarliestRipeningMonth && world.CurrentDate.Month <= agri.ExpectedHarvestMonth + 1)
            world.CurrentDate = world.CurrentDate.AddMonths(1);
        Assert.Equal(world.StreetRules.QuitMarginSlack, SiteWork.MarginFor(world, farmhand));
        while (world.CurrentDate.Month != agri.ExpectedHarvestMonth) world.CurrentDate = world.CurrentDate.AddMonths(1);
        Assert.Equal(world.StreetRules.QuitMarginPeak, SiteWork.MarginFor(world, farmhand));
        Assert.True(world.StreetRules.QuitMarginPeak > world.StreetRules.QuitMargin && world.StreetRules.QuitMarginSlack < world.StreetRules.QuitMargin);
    }

    [Fact]
    public void A_work_without_hands_stalls_resumes_and_is_abandoned_freeing_the_lot()
    {
        var (world, _, sink) = World(streets: StreetRules.Default with { HelpersBuild = false });
        var ctx = Ctx(world, sink);
        var city = world.ActiveCities().First();
        var owner = world.Households.First(h => !h.IsEmpty);
        foreach (var id in owner.Members) world.FindNpc(id)?.TryDebitWallet(world.FindNpc(id)!.Wallet); // não paga ninguém
        var work = BuildWorks.Open(world, ctx, city, BuildKind.House, -1, city.Location, 0, 0, owner.Id.Value, owner.Head.Value, 400, 0, "teste");
        for (int d = 0; d < 9; d++) { world.CurrentDate = world.CurrentDate.AddHours(24); BuildWorks.Tick(world, ctx); }
        Assert.True(work.Stalled);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.WorkStalled && e.Payload.StartsWith($"{work.Id}|", StringComparison.Ordinal));
        var someone = world.FindNpc(owner.Head)!;
        BuildWorks.Progress(world, ctx, work, someone, 4, paid: 0, world.CurrentDate.TotalHours);
        Assert.False(work.Stalled);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.WorkResumed && e.Payload.StartsWith($"{work.Id}|", StringComparison.Ordinal));
        for (int d = 0; d < 30 * 7; d++) { world.CurrentDate = world.CurrentDate.AddHours(24); BuildWorks.Tick(world, ctx); }
        Assert.DoesNotContain(work, world.Ground!.Builds);
        Assert.Null(world.FindBuilding(new BuildingId(work.Building))); // o canteiro saiu: o lote está livre
        var abandoned = sink.Events.Single(e => e.Kind == WorldEventKind.WorkAbandoned && e.Payload.StartsWith($"{work.Id}|", StringComparison.Ordinal));
        Assert.Equal("sem-mao", abandoned.Payload.Split('|')[5]);
    }

    [Fact]
    public void A_workshop_opens_only_when_its_building_is_ready_with_the_owner_working_in_it()
    {
        var (world, _, sink) = World();
        var ctx = Ctx(world, sink);
        var city = world.ActiveCities().First();
        var founder = world.Npcs.First(n => n.IsAlive && Adult(world, n) && n.Household is not null);
        int shopsBefore = world.Workplaces.Count;
        var forge = world.Workplaces.First(w => w.LocationType.Id == 2);
        var work = BuildWorks.Open(world, ctx, city, BuildKind.Workshop, world.FindBuilding(forge.Building!.Value)!.BuildingTypeId,
            city.Location, 0, 0, founder.Household!.Value.Value, founder.Id.Value, 100, 0, "teste");
        work.Escrow = 500;
        work.Capital = 500;
        work.LocationTypeId = 2;
        work.TradeProfession = 2;
        work.Prices[4] = 10;
        work.Origin = "poupanca|500|500|0";
        Assert.Equal(shopsBefore, world.Workplaces.Count); // em obra: o negócio ainda não existe
        work.MaterialOnSite = work.MaterialNeeded - work.MaterialPlaced; // AD-195: o material já trazido
        BuildWorks.Progress(world, ctx, work, founder, 1_200, paid: 0, world.CurrentDate.TotalHours); // AD-194: as cargas e a parede
        Assert.Equal(shopsBefore + 1, world.Workplaces.Count);
        var shop = world.Workplaces[^1];
        Assert.Equal(founder.Id, shop.Owner);
        Assert.Equal(shop.Id, founder.Employer);
        Assert.Equal(new BuildingId(work.Building), shop.Building);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.VentureFounded && e.Payload.StartsWith($"{shop.Id.Value}|", StringComparison.Ordinal));
    }

    [Fact]
    public void Reopened_world_with_house_works_goes_on_the_same()
    {
        var (world, clock, _) = World(seed: 6, streets: StreetRules.Default with { OwnHomeDaysPerYear = 100_000 });
        var (a, b) = Singles(world);
        MarriageSystem.Marry(world, Ctx(world), a, b);
        clock.Run(world, 24 * 12 + 7);
        Assert.NotEmpty(world.Ground!.Builds);
        var copy = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        var copyClock = new WorldClock(ScenarioRunner.DefaultSystems());
        clock.Run(world, 24 * 20);
        copyClock.Run(copy, 24 * 20);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(copy));
    }
    /// <summary>O casal de favor e a casa que o acolhe dividem o pote de água: sem água na parte do casal, há a da casa (não está
    /// seco); quem do casal vai à fonte enche para as duas casas e, em casa, a água chega ao pote de todos. Sem a regra, cada casa
    /// enche o seu e o casal fica seco com a água dos pais ao lado.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_lodging_couple_and_the_hosts_share_the_water_pot(bool share)
    {
        var (world, _, _) = World(streets: StreetRules.Default with { LodgersSharePot = share });
        Assert.True(LivingWorld.Simulation.Economy.Supply.WaterFetch.On(world));
        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        var (a, b) = Singles(world);
        MarriageSystem.Marry(world, Ctx(world), a, b);
        var couple = world.FindHousehold(a.Household!.Value)!;
        var host = world.Households.Single(h => h != couple && !h.IsEmpty && h.Dwelling == couple.Dwelling);
        long now = world.CurrentDate.TotalHours;
        foreach (var h in new[] { couple, host }) h.Withdraw(water, h.Stock.GetValueOrDefault(water));
        host.Deposit(water, 4);
        foreach (var n in new[] { a, b }) n.MoveTo(couple.Location, now);
        Assert.Equal(!share, LivingWorld.Simulation.Economy.Supply.WaterFetch.HomeDry(world, a)); // com o pote, a água dos pais é da casa

        host.Withdraw(water, 4);
        var source = LivingWorld.Simulation.Economy.Supply.WaterFetch.SourceFor(world, couple.Location);
        Assert.NotNull(source);
        if (a.Employer is { } e) { world.FindWorkplace(e)?.Fire(a.Id); a.Fire(); }
        Assert.True(LivingWorld.Simulation.Economy.Supply.WaterFetch.WantsToFetch(world, a, now));
        if (source!.Value.Cell != couple.Location)
        {
            a.SetCurrentAction(LivingWorld.Domain.Behavior.ActionType.Travel, now);
            a.MoveTo(source.Value.Cell, now);
            LivingWorld.Simulation.Economy.Supply.WaterFetch.OnArrived(world, Ctx(world), a, source.Value.Cell);
            a.MoveTo(couple.Location, now);
            LivingWorld.Simulation.Economy.Supply.WaterFetch.OnArrived(world, Ctx(world), a, couple.Location);
        }
        Assert.True(couple.Stock.GetValueOrDefault(water) > 0);
        if (share) Assert.True(host.Stock.GetValueOrDefault(water) > 0, "a água que chegou ficou no pote das duas casas");
        else Assert.Equal(0, host.Stock.GetValueOrDefault(water));
    }
    /// <summary>E dividem a mesa: sem comida na despensa do casal, quem come em casa come da de quem o acolhe (a mesma cozinha; a
    /// criança do casal de favor da metrópole morria de fome com a despensa dos avós ao lado). Sem a regra, só a sua.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_lodging_couple_eats_from_the_hosts_table_when_its_own_pantry_is_empty(bool share)
    {
        var (world, _, _) = World(streets: StreetRules.Default with { LodgersShareTable = share });
        var (a, b) = Singles(world);
        MarriageSystem.Marry(world, Ctx(world), a, b);
        var couple = world.FindHousehold(a.Household!.Value)!;
        var host = world.Households.Single(h => h != couple && !h.IsEmpty && h.Dwelling == couple.Dwelling);
        long now = world.CurrentDate.TotalHours;
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        foreach (var r in couple.Stock.Keys.ToList()) if (r.Id != world.EconomyRules.WaterResourceId) couple.Withdraw(r, couple.Stock[r]);
        host.Deposit(food, 5);
        long before = host.Stock.Values.Sum();
        foreach (var n in new[] { a, b }) { n.MoveTo(couple.Location, now); n.TryDebitWallet(n.Wallet); }
        a.SetHunger(10, now);
        LivingWorld.Simulation.Behavior.Decision.BehaviorDecisionSystem.ResolveFoodPlan(world, a,
            LivingWorld.Simulation.Economy.Market.MarketIndex.BuildForTick(world), now, LivingWorld.Domain.Behavior.ActionType.Eat);
        Assert.Equal(share, a.HungerAt(now) > 10);
        Assert.Equal(share, host.Stock.Values.Sum() < before);
    }
}
