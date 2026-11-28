using System.Text.Json.Nodes;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Careers;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>#263 (épico #246): carreira, turno, desempenho, promoção, salário e contas da casa. Nada disto está escrito no código: o emprego, os níveis, os horários, os salários e as
/// contas são o catálogo <c>careers.json</c> e o período <c>sandbox-casal.json</c> (<c>CareerStart</c>); o motor só tem o mecanismo.</summary>
public class Careers263Tests
{
    private static (WorldState World, WorldClock Clock) Load(Action<JsonObject>? edit = null, ulong seed = 263)
    {
        var root = SandboxFixtures.Parse();
        root["Seed"] = seed;
        edit?.Invoke(root);
        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        return result.Value;
    }

    private static Npc Marina(WorldState world) => world.Npcs.Single(n => n.Name == "Marina");

    private static Npc Rafael(WorldState world) => world.Npcs.Single(n => n.Name == "Rafael");

    private static long Coins(WorldState world) =>
        world.Npcs.Sum(n => n.Wallet.Amount) + world.Workplaces.Sum(w => w.Treasury.Amount) + world.ActiveCities().Sum(c => c.Treasury.Amount);

    private static void Hours(WorldState world, WorldClock clock, int hours) { for (int h = 0; h < hours; h++) clock.Tick(world); }

    private static JsonObject Override(JsonObject root, string json) { root["Careers"] = JsonNode.Parse(json); return root; }

    [Fact]
    public void The_catalog_gives_the_careers_and_the_bills_and_the_scenario_puts_rafael_in_one()
    {
        var (world, _) = Load();

        var catalog = world.ObjectCatalog!.Careers!;
        Assert.Equal(["escritorio", "ensino"], catalog.Careers.Select(c => c.Id));
        Assert.Equal(["iptu", "agua", "luz"], catalog.Bills.Select(b => b.Id));
        var job = world.Careers!.JobOf(Rafael(world).Id.Value)!;
        Assert.Equal("escritorio", job.Career);
        Assert.Equal(0, job.Level);
        Assert.Equal(world.Workplaces[1].Id.Value, job.Employer);
        Assert.Null(world.Careers.JobOf(Marina(world).Id.Value)); // ela procura
        Assert.Equal(3, world.Careers.Bills.Count(b => b.Household == Rafael(world).Household!.Value.Value)); // as contas da casa abrem com a primeira carreira
    }

    [Fact]
    public void The_worker_leaves_at_the_hour_vanishes_from_the_map_and_comes_back_paid_by_the_employer_without_creating_coins()
    {
        var (world, clock) = Load();
        var rafael = Rafael(world);
        long coins = Coins(world), wallet = rafael.Wallet.Amount, employer = world.Workplaces[1].Treasury.Amount;

        clock.Tick(world); // 08:00: a hora de sair (a ida leva 40 min, o turno começa às 09:00)

        Assert.True(CareerWork.IsAway(world, rafael));
        Assert.Null(rafael.Interior);
        Hours(world, clock, 11); // 19:00: passou do fim do turno (17:00) e da volta (17:40)

        Assert.False(CareerWork.IsAway(world, rafael));
        Assert.Equal(wallet + 9 * 8, rafael.Wallet.Amount); // salário por hora × as 8 horas do turno, do dado
        Assert.Equal(employer - 72, world.Workplaces[1].Treasury.Amount); // quem paga é o empregador
        Assert.Equal(coins, Coins(world)); // a moeda só mudou de mão
        var job = world.Careers!.JobOf(rafael.Id.Value)!;
        Assert.Equal(1, job.Shifts);
        Assert.InRange(job.Score, 0, 100);
        Assert.Contains(world.Careers.Notices, n => n.Kind == "paid" && n.Amount == 72 && n.Npc == rafael.Id.Value);
        Assert.Contains(world.Careers.Notices, n => n.Kind == "departed");
    }

    [Fact]
    public void The_shift_gives_the_body_what_the_level_says_and_a_hungry_worker_performs_worse()
    {
        var (world, clock) = Load();
        var rafael = Rafael(world);
        var def = world.ObjectCatalog!.Careers!.Find("escritorio")!;
        long now = world.CurrentDate.TotalMilliseconds;
        double rested = CareerWork.Performance(world, rafael, def, now);
        rafael.SetHunger(5, 0);
        rafael.SetSleep(5, 0);

        Assert.True(CareerWork.Performance(world, rafael, def, now) < rested - 20);
        Hours(world, clock, 12);
        Assert.True(world.Careers!.JobOf(rafael.Id.Value)!.Score >= 0);
    }

    [Fact]
    public void A_good_shift_streak_promotes_to_the_next_level_of_the_data_and_the_skill_grows_with_the_work()
    {
        var (world, clock) = Load(root => Override(root, """
        {"careers":[{"id":"escritorio","name":"Escritório","employerType":2,"commuteMin":40,"skillId":1,"performance":{"needs":{"sleep":1},"skill":0,"practice":0.8},
            "levels":[{"id":"a","title":"Auxiliar","wagePerHour":9,"startHour":9,"hours":8,"days":[0,1,2,3,4],"promoteAt":0,"promoteAfter":1},
                      {"id":"b","title":"Analista","wagePerHour":14,"startHour":9,"hours":8,"days":[0,1,2,3,4],"minSkill":15}]}]}
        """));
        var rafael = Rafael(world);
        double skill = rafael.Skills.Get(new LivingWorld.Domain.Population.Skills.SkillType(1));

        Hours(world, clock, 12);

        var job = world.Careers!.JobOf(rafael.Id.Value)!;
        Assert.Equal(1, job.Level);
        Assert.Contains(world.Careers.Notices, n => n.Kind == "promoted" && n.Text.Contains("Analista"));
        Assert.True(rafael.Skills.Get(new LivingWorld.Domain.Population.Skills.SkillType(1)) > skill);
        long before = rafael.Wallet.Amount;
        Hours(world, clock, 24); // o próximo dia útil paga o novo salário
        Assert.True(rafael.Wallet.Amount - before >= 14 * 8 - 1);
    }

    [Fact]
    public void A_bad_streak_demotes_and_at_the_bottom_fires()
    {
        var (world, clock) = Load(root => Override(root, """
        {"careers":[{"id":"escritorio","name":"Escritório","employerType":2,"commuteMin":40,"performance":{"needs":{"sleep":1}},
            "levels":[{"id":"a","title":"Auxiliar","wagePerHour":9,"startHour":9,"hours":8,"days":[0,1,2,3,4],"demoteBelow":101,"demoteAfter":1}]}]}
        """));

        Hours(world, clock, 12);

        Assert.Null(world.Careers!.JobOf(Rafael(world).Id.Value)); // sem nível abaixo, a demissão
        Assert.Contains(world.Careers.Notices, n => n.Kind == "fired");
    }

    [Fact]
    public void Looking_for_a_job_lists_the_real_vacancies_and_the_application_hires_by_the_chance_of_the_data()
    {
        var (world, _) = Load(root => Override(root, """
        {"careers":[{"id":"ensino","name":"Escola","employerType":3,"commuteMin":15,"skillId":2,"search":{"baseChance":1},"performance":{"needs":{"sleep":1}},
            "levels":[{"id":"m","title":"Monitor escolar","wagePerHour":8,"startHour":8,"hours":6,"days":[0,1,2,3,4]}]}]}
        """));
        var marina = Marina(world);
        long now = world.CurrentDate.TotalMilliseconds;

        Assert.False(CareerWork.Apply(world, marina, "ensino", world.Workplaces[2].Id.Value, now).IsSuccess); // sem procurar antes, nada
        CareerWork.Search(world, marina, now);
        var vacancy = Assert.Single(CareerWork.Vacancies(world, marina), v => v.Career.Id == "ensino");
        Assert.Equal(world.Workplaces[2].Id.Value, vacancy.Employer.Id.Value);

        var applied = CareerWork.Apply(world, marina, "ensino", vacancy.Employer.Id.Value, now);

        Assert.True(applied.IsSuccess && applied.Value.Hired);
        Assert.Equal("ensino", world.Careers!.JobOf(marina.Id.Value)!.Career);
        Assert.Contains(world.Careers.Notices, n => n.Kind == "hired" && n.Npc == marina.Id.Value);
        Assert.Null(world.Careers.SearchOf(marina.Id.Value)); // a busca é gasta
    }

    [Fact]
    public void A_full_employer_has_no_vacancy_and_a_zero_chance_application_is_refused_and_spends_the_search()
    {
        var (world, _) = Load(root =>
        {
            Override(root, """
        {"careers":[{"id":"ensino","name":"Escola","employerType":3,"commuteMin":15,"search":{"baseChance":0},"performance":{"needs":{"sleep":1}},
                "levels":[{"id":"m","title":"Monitor escolar","wagePerHour":8,"startHour":8,"hours":6,"days":[0,1,2,3,4]}]}]}
        """);
        });
        var marina = Marina(world);
        long now = world.CurrentDate.TotalMilliseconds;
        CareerWork.Search(world, marina, now);

        var applied = CareerWork.Apply(world, marina, "ensino", world.Workplaces[2].Id.Value, now);

        Assert.True(applied.IsSuccess && !applied.Value.Hired);
        Assert.Null(world.Careers!.JobOf(marina.Id.Value));
        Assert.Null(world.Careers.SearchOf(marina.Id.Value));
        Assert.Contains(world.Careers.Notices, n => n.Kind == "rejected");
    }

    [Fact]
    public void The_newspaper_use_ends_with_the_listing_of_vacancies()
    {
        var (world, clock) = Load();
        var marina = Marina(world);
        var building = world.FindBuilding(world.FindHousehold(marina.Household!.Value)!.Dwelling!.Value)!;
        int piece = HomeFurniture.Of(building, world.ObjectCatalog!).ToList().FindIndex(p => p.Id == "newspaper");
        Assert.True(piece >= 0, "o jornal está na mobília da casa");
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        var begun = ObjectUsage.Begin(world, ctx, marina, building.Id.Value, piece, "search-job");
        Assert.True(begun.IsSuccess, begun.Error);
        Hours(world, clock, 1);

        Assert.NotNull(world.Careers!.SearchOf(marina.Id.Value));
        Assert.Contains(world.Careers.Notices, n => n.Kind == "search" && n.Npc == marina.Id.Value);
    }

    [Fact]
    public void Bills_are_issued_by_the_data_with_the_measured_use_and_the_npc_pays_before_they_fall_due_to_the_city_treasury()
    {
        var (world, clock) = Load();
        var city = world.ActiveCities().Single();
        long coins = Coins(world);

        Hours(world, clock, 24 * 5);

        Assert.Equal(coins, Coins(world)); // a conta vai da carteira ao tesouro da cidade; nunca nasce moeda
        Assert.True(city.Treasury.Amount > 0, "alguma conta foi paga");
        Assert.Contains(world.Careers!.Notices, n => n.Kind == "bill-issued");
        Assert.Contains(world.Careers.Notices, n => n.Kind == "bill-paid");
        var water = world.Careers.Bills.Single(b => b.Bill == "agua");
        Assert.True(water.Paid >= 6, "a conta de água inclui o fixo do dado");
    }

    [Fact]
    public void An_unpayable_bill_gets_a_late_fee_and_then_cuts_the_service_the_data_names()
    {
        var (world, clock) = Load(root => Override(root, """
        {"bills":[{"id":"agua","name":"Água","fixed":99999,"meterUses":["shower"],"perHour":1,"everyDays":7,"dueDays":1,"lateFeeRate":0.1,"cutAfterDays":1,"blocks":["shower"],"firstIssueDay":1}]}
        """));
        var marina = Marina(world);

        Hours(world, clock, 24 * 5);

        var bill = world.Careers!.Bills.Single(b => b.Bill == "agua");
        Assert.True(bill.Cut);
        Assert.Equal(10_000, bill.Fee);
        Assert.Contains(world.Careers.Notices, n => n.Kind == "bill-late");
        Assert.Contains(world.Careers.Notices, n => n.Kind == "bill-cut");
        var shower = world.ObjectCatalog!.Find("shower")!.Uses!.Single();
        Assert.Contains("Água cortada", CareerWork.BlockedUse(world, marina, shower));
        var building = world.FindBuilding(world.FindHousehold(marina.Household!.Value)!.Dwelling!.Value)!;
        int piece = HomeFurniture.Of(building, world.ObjectCatalog).ToList().FindIndex(p => p.Id == "shower");
        var refused = ObjectUsage.Begin(world, new TickContext(world, world.Rng, world.Scheduler), marina, building.Id.Value, piece, "shower");
        Assert.False(refused.IsSuccess);
    }

    [Fact]
    public void Paying_the_bill_takes_from_the_wallets_and_brings_the_service_back()
    {
        var (world, clock) = Load(root => Override(root, """
        {"bills":[{"id":"agua","name":"Água","fixed":80,"everyDays":7,"dueDays":1,"lateFeeRate":0.1,"cutAfterDays":0,"blocks":["shower"],"firstIssueDay":1}]}
        """));
        var marina = Marina(world);
        var city = world.ActiveCities().Single();
        var state = world.Careers!.Bills.Single(b => b.Bill == "agua");
        state.Open = 80;
        state.DueMs = world.CurrentDate.TotalMilliseconds - 1; // vencida
        state.Cut = true;
        long coins = Coins(world);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        var pay = CareerWork.PayBill(world, ctx, marina, "agua", world.CurrentDate.TotalMilliseconds);

        Assert.True(pay.IsSuccess, pay.Error);
        Assert.False(state.Cut);
        Assert.Equal(0, state.Open);
        Assert.Equal(80, city.Treasury.Amount);
        Assert.Equal(coins, Coins(world));
        Assert.Contains(world.Careers.Notices, n => n.Kind == "bill-restored");
        Assert.False(CareerWork.PayBill(world, ctx, marina, "agua", world.CurrentDate.TotalMilliseconds).IsSuccess); // nada em aberto
        _ = clock;
    }

    [Fact]
    public void A_player_gets_the_notice_goes_to_work_by_the_offer_and_the_body_does_not_answer_until_the_shift_ends()
    {
        var (world, clock) = Load(root => root["StartHour"] = 6);
        world.UsePlayers(null);
        var service = new PlayerService(new AllEvents());
        var rafael = Rafael(world);
        Assert.True(service.Incarnate(world, rafael.Id, "alan", PossessionMode.Full).IsSuccess);
        clock.Tick(world); // 07:00: uma hora antes de sair

        Assert.Contains(world.Careers!.Notices, n => n.Kind == "shift-soon" && n.Verb == "go" && n.Text.Contains("Hora do trabalho em 1 h"));
        Assert.Contains(PlayerService.Reach(world, "alan")!, o => o.Kind == "job" && o.Verbs.Contains("go"));
        var went = service.Interact(world, "alan", "job", rafael.Id.Value, "go");

        Assert.True(went.IsSuccess, went.Error);
        Assert.True(CareerWork.IsAway(world, rafael));
        var blocked = service.Move(world, "alan", 1, 0);
        Assert.False(blocked.IsSuccess);
        Assert.Contains("no trabalho", blocked.Error);
        Assert.DoesNotContain(PlayerService.Reach(world, "alan")!, o => o.Kind == "job");
        Hours(world, clock, 12);
        Assert.False(CareerWork.IsAway(world, rafael));
        Assert.True(rafael.Wallet.Amount > 50);
    }

    [Fact]
    public void A_player_who_does_not_go_misses_the_shift_without_pay_and_repeated_absences_fire()
    {
        var (world, clock) = Load(root => root["StartHour"] = 6);
        world.UsePlayers(null);
        var service = new PlayerService(new AllEvents());
        var rafael = Rafael(world);
        Assert.True(service.Incarnate(world, rafael.Id, "alan", PossessionMode.Full).IsSuccess);

        Hours(world, clock, 6); // 12:00: a tolerância (09:00 + 1 h) passou

        var job = world.Careers!.JobOf(rafael.Id.Value)!;
        Assert.Equal(1, job.Absences);
        Assert.Equal(0, job.Score);
        Assert.Contains(world.Careers.Notices, n => n.Kind == "absent" && n.Text.Contains("Faltou"));
        Assert.Equal(50, rafael.Wallet.Amount);
        Hours(world, clock, 24 * 3);
        Assert.Null(world.Careers.JobOf(rafael.Id.Value)); // três faltas seguidas, o dado manda embora
    }

    [Fact]
    public void Careers_are_deterministic_roundtrip_the_snapshot_and_leave_the_worlds_without_the_catalog_untouched()
    {
        string Run(ulong seed)
        {
            var (world, clock) = Load(seed: seed);
            Hours(world, clock, 24 * 3);
            var again = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
            Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(again));
            Assert.Equal(world.Careers!.Jobs.Count, again.Careers!.Jobs.Count);
            return WorldSnapshot.CanonicalHash(world);
        }

        Assert.Equal(Run(263), Run(263));
        Assert.NotEqual(Run(263), Run(264));

        var (plain, plainClock) = Load(root =>
        {
            root["CatalogExtends"] = new JsonArray("home-life", "social");
            root.Remove("CareerStart");
            var furnishing = root["Furnishings"]!["-1"]!.AsArray();
            foreach (var node in furnishing.Where(n => n!.ToString() == "newspaper").ToList()) furnishing.Remove(node);
        });
        Hours(plain, plainClock, 48);
        Assert.Null(plain.Careers);
        Assert.Null(plain.ObjectCatalog!.Careers);
    }
}
