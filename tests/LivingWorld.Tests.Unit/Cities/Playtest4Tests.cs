using System.Text.Json.Nodes;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Careers;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>#268 (épico #246): o 4º teste de jogo do sandbox. A candidatura guiada pelo jornal, a necessidade crítica que desce o tempo e o livre-arbítrio do corpo, a entrega com hora, o social com custo
/// (repetição, briga que dura, chamar quem está num móvel) e a beleza esperada por tipo de cômodo. Tudo vem do dado (catálogos).</summary>
public class Playtest4Tests
{
    private const int Fine = 250;

    private static (WorldState World, WorldClock Clock, PlayerService Service, Npc Marina, Npc Rafael) Playing(int startHour = 9, ulong seed = 268, bool fine = true, Action<JsonObject>? edit = null)
    {
        var root = SandboxFixtures.Parse();
        root["Seed"] = seed;
        root["StartHour"] = startHour;
        edit?.Invoke(root);
        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        var (world, clock) = result.Value;
        world.UsePlayers(null);
        var service = new PlayerService(new AllEvents());
        if (fine) Assert.True(service.SetTickDelta(world, "alan", Fine).IsSuccess);
        var marina = world.Npcs.Single(n => n.Name == "Marina");
        var rafael = world.Npcs.Single(n => n.Name == "Rafael");
        Assert.True(service.Incarnate(world, marina.Id, "alan", PossessionMode.Full).IsSuccess);
        return (world, clock, service, marina, rafael);
    }

    private static long Now(WorldState world) => world.CurrentDate.TotalMilliseconds;

    private static string ApplyVerb(WorldState world, Npc npc)
    {
        var vacancy = CareerWork.Vacancies(world, npc).First();
        return $"apply:{vacancy.Career.Id}@{vacancy.Employer.Id.Value}";
    }

    // --- emprego guiado ---

    [Fact]
    public void Reading_the_paper_lists_the_vacancies_with_employer_and_wage_and_says_to_apply_from_the_wheel()
    {
        var (world, _, _, marina, _) = Playing(fine: false);
        CareerWork.Search(world, marina, Now(world));

        string text = world.Careers!.Notices.Last(n => n.Kind == "search").Text;
        Assert.Contains("/h", text); // o salário e o empregador (a carreira) vêm no aviso
        Assert.Contains("roda do E", text);
        var listings = CareerViews.Of(world, marina)!.Listings;
        Assert.NotEmpty(listings);
        Assert.All(listings, l => Assert.True(l.CommuteMin > 0));
        Assert.Contains(PlayerService.Reach(world, "alan")!, o => o.Kind == "job" && o.Verbs.Any(v => v.StartsWith("apply:", StringComparison.Ordinal)));
    }

    [Fact]
    public void Applying_far_from_the_employer_sends_the_body_there_and_the_result_comes_on_arrival_in_the_service_hours()
    {
        var (world, clock, service, marina, _) = Playing(startHour: 9);
        CareerWork.Search(world, marina, Now(world));
        string verb = ApplyVerb(world, marina);

        var started = service.Interact(world, "alan", "job", marina.Id.Value, verb);
        Assert.True(started.IsSuccess, started.Error);
        Assert.Equal("going", started.Value!.Outcome);
        Assert.Equal("go", PlayerTasks.Of(world, marina)!.Action); // o "ir até" do jogo
        Assert.NotNull(world.Careers!.SearchOf(marina.Id.Value)!.PendingCareer);

        for (int i = 0; i < 6000 && marina.BodyTask is not null; i++) clock.Tick(world, Fine);

        Assert.Null(marina.BodyTask);
        var outcome = world.Careers.Notices.LastOrDefault(n => n.Npc == marina.Id.Value && n.Kind is "hired" or "rejected");
        Assert.NotNull(outcome); // contratado, ou recusado com a chance dita: o resultado é claro
        if (outcome.Kind == "rejected") Assert.Contains("chance", outcome.Text);
        Assert.Null(world.Careers.SearchOf(marina.Id.Value)?.PendingCareer);
    }

    [Fact]
    public void Applying_outside_the_service_hours_books_an_interview_for_the_next_opening_with_the_hour()
    {
        var (world, clock, service, marina, _) = Playing(startHour: 20, fine: false);
        CareerWork.Search(world, marina, Now(world));

        var applied = CareerWork.BeginApply(world, null!, marina, CareerWork.Vacancies(world, marina).First().Career.Id, CareerWork.Vacancies(world, marina).First().Employer.Id.Value, Now(world));

        Assert.True(applied.IsSuccess, applied.Error);
        Assert.Equal("later", applied.Value.Outcome);
        Assert.Contains("às 08:00", applied.Value.Note); // a hora da entrevista é a abertura do dado
        Assert.Contains(world.Careers!.Notices, n => n.Kind == "interview");
        Assert.NotNull(world.Careers.SearchOf(marina.Id.Value)); // a busca segue valendo para voltar na hora
        Assert.NotNull(clock);
        Assert.NotNull(service);
    }

    [Fact]
    public void The_booked_interview_runs_at_the_opening_hour_by_itself_when_the_body_is_at_the_employer()
    {
        var (world, clock, service, marina, _) = Playing(startHour: 20);
        CareerWork.Search(world, marina, Now(world));
        Assert.True(service.Interact(world, "alan", "job", marina.Id.Value, ApplyVerb(world, marina)).IsSuccess);
        for (int i = 0; i < 9000 && marina.BodyTask is not null; i++) clock.Tick(world, Fine);
        var search = world.Careers!.SearchOf(marina.Id.Value)!;
        Assert.True(search.InterviewAtMs > Now(world), "entrevista marcada para a abertura"); // #246 (AD-237)
        Assert.NotNull(search.PendingCareer);

        for (int i = 0; i < 16 && search.InterviewAtMs > 0; i++) clock.Tick(world, WorldMillis.PerHour); // as horas passam até a abertura: o motor roda a entrevista sozinho

        Assert.Equal(0, search.InterviewAtMs);
        Assert.Null(search.PendingCareer);
        Assert.Contains(world.Careers.Notices, n => n.Npc == marina.Id.Value && n.Kind is "hired" or "rejected"); // o desfecho veio sem candidatar de novo
    }

    [Fact]
    public void A_booked_interview_is_missed_with_a_notice_when_the_body_is_not_at_the_employer()
    {
        var (world, clock, service, marina, _) = Playing(startHour: 9);
        clock.Tick(world, Fine);
        CareerWork.Search(world, marina, Now(world));
        Assert.True(service.Interact(world, "alan", "job", marina.Id.Value, ApplyVerb(world, marina)).IsSuccess); // a caminho: ainda longe
        var search = world.Careers!.SearchOf(marina.Id.Value)!;
        search.InterviewAtMs = Now(world);

        CareerSystem.RunDue(world, Now(world));

        Assert.Equal(0, search.InterviewAtMs);
        Assert.Contains(world.Careers.Notices, n => n.Kind == "rejected" && n.Text.Contains("não estava"));
        Assert.Null(world.Careers.JobOf(marina.Id.Value));
    }

    // --- necessidade crítica e livre-arbítrio ---

    [Fact]
    public void A_critical_need_raises_a_notice_that_stops_the_time_and_the_body_without_orders_takes_care_of_it_itself()
    {
        var (world, clock, service, marina, _) = Playing(startHour: 22);
        Assert.Contains("need-critical", CareerViews.Of(world, marina)!.StopKinds!);
        Assert.True(CareerViews.Of(world, marina)!.FreeWill); // ligado por padrão
        HomeLife.Add(world, marina, "bladder", -100, Now(world));
        Assert.Contains(CriticalNeeds.Now(world, marina, Now(world)), c => c.Id == "bladder");

        bool used = false;
        for (int i = 0; i < 4000 && !used; i++)
        {
            clock.Tick(world, Fine);
            used = marina.BodyTask is { Action: ObjectUsage.Action, Use: "toilet" };
        }

        Assert.Contains(world.Careers!.Notices, n => n.Kind == "need-critical" && n.Npc == marina.Id.Value);
        Assert.Contains(world.Careers.Notices, n => n.Kind == "need-auto" && n.Npc == marina.Id.Value);
        Assert.True(used, "o corpo sem ordem foi ao vaso sozinho");
        Assert.NotNull(service);
    }

    [Fact]
    public void With_the_free_will_off_the_body_waits_for_the_player_and_the_notice_repeats_only_after_the_data_hours()
    {
        var (world, clock, service, marina, _) = Playing(startHour: 22);
        var off = service.Interact(world, "alan", "will", marina.Id.Value, "freewill:off");
        Assert.True(off.IsSuccess, off.Error);
        Assert.False(CareerViews.Of(world, marina)!.FreeWill);
        HomeLife.Add(world, marina, "bladder", -100, Now(world));

        for (int i = 0; i < 1500; i++) clock.Tick(world, Fine); // ~6 minutos de mundo: o aviso sai, o corpo não se mexe
        Assert.Single(world.Careers!.Notices, n => n.Kind == "need-critical" && n.Text.Contains("bexiga", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(world.Careers.Notices, n => n.Kind == "need-auto");
        Assert.Null(marina.BodyTask);

        Assert.True(service.Interact(world, "alan", "will", marina.Id.Value, "freewill:on").IsSuccess);
        Assert.True(CareerViews.Of(world, marina)!.FreeWill);
        Assert.False(service.Interact(world, "alan", "will", marina.Id.Value, "freewill:maybe").IsSuccess);
    }

    // --- entrega com hora ---

    [Fact]
    public void The_arrival_is_told_as_a_world_clock_hour_not_in_minutes()
    {
        var (world, _, _, _, _) = Playing(startHour: 18, fine: false);
        long hour = (long)LivingWorld.Domain.Shared.WorldMillis.PerHour;
        long day = world.Calendar.HoursPerDay * hour;
        long start = Now(world) / day * day;

        Assert.Equal("hoje às 21:30", CareerWork.When(world, start + 21 * hour + 30 * 60_000L));
        Assert.Equal("amanhã às 08:12", CareerWork.When(world, start + day + 8 * hour + 12 * 60_000L));
        Assert.Equal($"dia {start / day + 3} às 00:05", CareerWork.When(world, start + 3 * day + 5 * 60_000L));
    }

    [Fact]
    public void A_delivery_ordered_at_night_says_tomorrow_with_the_hour_the_courier_arrives()
    {
        var (world, _, service, marina, _) = Playing(startHour: 22, fine: false);
        marina.CreditWallet(new Money(500));
        var house = world.FindBuilding(world.FindHousehold(marina.Household!.Value)!.Dwelling!.Value)!;
        Assert.True(service.Interact(world, "alan", "home", house.Id.Value, "buy:sofa").IsSuccess);
        int seq = house.Edit!.Parcels[^1].Seq;

        var ordered = service.Interact(world, "alan", "home", house.Id.Value, $"deliver:{seq}");

        Assert.True(ordered.IsSuccess, ordered.Error);
        Assert.Contains("chega amanhã às", ordered.Value!.Note);
        Assert.DoesNotContain(" min", ordered.Value.Note);
        var parcel = HomeFreightViews.SheetOf(world, marina);
        Assert.NotNull(parcel);
        Assert.Contains("amanhã às", HomeView.Of(world, marina)!.Haul!.Parcels.Single(p => p.Seq == seq).DueText);
    }

    // --- social com custo ---

    private static SocialInteractionDefs Defs(WorldState world) => new(world, world.ObjectCatalog!.Social!);

    private sealed record SocialInteractionDefs(WorldState World, LivingWorld.Domain.Objects.SocialCatalog Catalog)
    {
        public double Chance(string id, Npc actor, Npc target) => SocialInteractions.Evaluate(World, Catalog, Catalog.Find(id)!, actor, target, Now(World)).Chance;

        public string? Reason(string id, Npc actor, Npc target) => SocialInteractions.Evaluate(World, Catalog, Catalog.Find(id)!, actor, target, Now(World)).Reason;
    }

    [Fact]
    public void Repeating_a_compliment_or_a_hug_soon_after_costs_the_chance_and_the_refusal_says_why()
    {
        var (world, _, _, marina, rafael) = Playing(fine: false);
        var defs = Defs(world);
        double first = defs.Chance("compliment", marina, rafael), hugFirst = defs.Chance("hug", marina, rafael);

        MoodMechanic.OnSocial(world, world.ObjectCatalog!.Social!.Find("compliment")!, marina, rafael, accepted: true, Now(world));
        world.GetOrCreateRelationship(new LivingWorld.Domain.Population.Family.RelationshipKey(rafael.Id, marina.Id), 0).NoteSocial("compliment", Now(world), true, actor: false);
        world.GetOrCreateRelationship(new LivingWorld.Domain.Population.Family.RelationshipKey(marina.Id, rafael.Id), 0).NoteSocial("compliment", Now(world), true, actor: true);

        Assert.True(defs.Chance("compliment", marina, rafael) < first - 40, "elogiar de novo logo depois cai muito");
        Assert.Contains("elogiou agora há pouco", defs.Reason("compliment", marina, rafael));
        Assert.True(defs.Chance("hug", marina, rafael) < hugFirst, "o carinho logo depois de outro gesto amigável também pesa");
        Assert.True(first < 100 && hugFirst < 100, "nenhum dos dois é aceito sempre"); // o abraço e o elogio não são certos
    }

    [Fact]
    public void A_fight_blocks_the_warm_gestures_for_days_until_the_apology_is_accepted()
    {
        var (world, _, _, marina, rafael) = Playing(fine: false);
        var defs = Defs(world);
        double before = defs.Chance("hug", marina, rafael), kissBefore = defs.Chance("kiss", marina, rafael);

        MoodMechanic.OnSocial(world, world.ObjectCatalog!.Social!.Find("argue")!, marina, rafael, accepted: true, Now(world));
        long later = Now(world) + 30 * 3_600_000L; // um dia e pouco depois: o momento da briga ainda vive (3 dias no dado)

        Assert.True(MoodMechanic.Lives(rafael, "quarrel", later));
        double hug = SocialInteractions.Evaluate(world, world.ObjectCatalog.Social!, world.ObjectCatalog.Social!.Find("hug")!, marina, rafael, later).Chance;
        double kiss = SocialInteractions.Evaluate(world, world.ObjectCatalog.Social!, world.ObjectCatalog.Social!.Find("kiss")!, marina, rafael, later).Chance;
        Assert.True(hug < before - 60 && kiss < kissBefore - 60, $"abraço {before}→{hug}, beijo {kissBefore}→{kiss}");
        Assert.Contains("magoado", SocialInteractions.Evaluate(world, world.ObjectCatalog.Social!, world.ObjectCatalog.Social!.Find("hug")!, marina, rafael, later).Reason);
    }

    [Fact]
    public void An_apology_with_nothing_to_apologize_for_is_unlikely_and_after_a_fight_it_is_likely() // #246 (5º teste, AD-237): desculpa sem briga rendia +4 de afeto toda vez
    {
        var (world, _, _, marina, rafael) = Playing(fine: false);
        var social = world.ObjectCatalog!.Social!;
        var apology = social.Find("apologize")!;
        double without = SocialInteractions.Evaluate(world, social, apology, marina, rafael, Now(world)).Chance;

        MoodMechanic.OnSocial(world, social.Find("argue")!, marina, rafael, accepted: true, Now(world));
        long later = Now(world) + 30 * 3_600_000L; // um dia e pouco depois: a briga ainda vive
        double after = SocialInteractions.Evaluate(world, social, apology, marina, rafael, later).Chance;

        Assert.True(without < 25, $"sem briga: {without}");
        Assert.True(after > without + 30, $"depois da briga: {without} -> {after}");
    }

    // --- beleza esperada por tipo de cômodo ---

    [Fact]
    public void A_bathroom_is_not_ugly_just_for_having_a_toilet_and_a_shower_the_ambient_is_measured_against_the_kind()
    {
        var (world, _, _, marina, _) = Playing(fine: false);
        var house = world.FindBuilding(world.FindHousehold(marina.Household!.Value)!.Dwelling!.Value)!;
        var catalog = world.ObjectCatalog!;
        var rooms = RoomBeauty.Rooms(house, catalog);
        var bath = rooms.FirstOrDefault(r => r.Kind == "banheiro");

        Assert.NotNull(bath);
        Assert.True(bath.Expected < 0);
        Assert.True(bath.Beauty - bath.Expected >= -1, $"banheiro {bath.Beauty} contra o esperado {bath.Expected}");
        Assert.DoesNotContain(rooms.Where(r => r.Kind is not null && catalog.Home!.Rules.ExpectedBeauty!.ContainsKey(r.Kind)), r => r.Beauty - r.Expected < -1);
    }

    // --- humor ---

    [Fact]
    public void A_shower_and_a_hug_in_half_an_hour_do_not_make_the_mood_radiant()
    {
        var (world, clock, _, marina, rafael) = Playing(startHour: 18, fine: false);
        var social = world.ObjectCatalog!.Social!;
        int start = SocialInteractions.Mood(world, social, marina, Now(world)).Score;
        var scores = new List<int> { start };

        MoodMechanic.OnUse(world, marina, "shower", Now(world));
        HomeLife.Add(world, marina, "hygiene", 100, Now(world));
        scores.Add(SocialInteractions.Mood(world, social, marina, Now(world)).Score);
        MoodMechanic.OnSocial(world, social.Find("hug")!, rafael, marina, accepted: true, Now(world));
        scores.Add(SocialInteractions.Mood(world, social, marina, Now(world)).Score);
        MoodMechanic.OnSocial(world, social.Find("compliment")!, rafael, marina, accepted: true, Now(world));
        for (int i = 0; i < 2; i++) clock.Tick(world);
        scores.Add(SocialInteractions.Mood(world, social, marina, Now(world)).Score);

        Assert.NotEqual("Radiante", SocialInteractions.Mood(world, social, marina, Now(world)).Label);
        Assert.True(scores.Max() < 80, $"notas {string.Join(", ", scores)}: Radiante pede mais que um banho, um abraço e um elogio");
    }

    [Fact]
    public void The_body_away_at_work_is_not_cared_for_at_home_by_the_free_will()
    {
        var (world, clock, service, marina, rafael) = Playing(startHour: 7);
        Assert.True(service.Switch(world, rafael.Id, "alan").IsSuccess);
        Assert.True(service.Interact(world, "alan", "job", rafael.Id.Value, "go").IsSuccess);
        Assert.True(CareerWork.IsAway(world, rafael));
        HomeLife.Add(world, rafael, "bladder", -100, Now(world));

        for (int i = 0; i < 800; i++) clock.Tick(world, Fine);

        Assert.True(CareerWork.IsAway(world, rafael));
        Assert.DoesNotContain(world.Careers!.Notices, n => n.Kind == "need-auto" && n.Npc == rafael.Id.Value);
        Assert.Null(rafael.BodyTask);
        Assert.NotNull(marina);
    }
}

