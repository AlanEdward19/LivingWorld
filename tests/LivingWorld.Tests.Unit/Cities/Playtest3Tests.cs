using System.Text.Json.Nodes;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Careers;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>#267 (épico #246): o 3º teste de jogo do sandbox. Humor que não satura e acompanha a emoção, briga que custa, desejos novos por dia, comer sem fome sem momento, o aviso do turno
/// para quem assume depois, a volta do trabalho para dentro de casa, "ir até" pela rota do motor e a aparência de cada pessoa autorada. Tudo vem do dado (catálogos e período).</summary>
public class Playtest3Tests
{
    private static (WorldState World, WorldClock Clock) Load(Action<JsonObject>? edit = null, ulong seed = 267, bool couple = false)
    {
        var root = couple ? SandboxFixtures.CoupleOnly(SandboxFixtures.Parse()) : SandboxFixtures.Parse();
        root["Seed"] = seed;
        edit?.Invoke(root);
        var result = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(result.IsSuccess, result.Error);
        return result.Value;
    }

    private static Npc Marina(WorldState world) => world.Npcs.Single(n => n.Name == "Marina");

    private static Npc Rafael(WorldState world) => world.Npcs.Single(n => n.Name == "Rafael");

    private static long Now(WorldState world) => world.CurrentDate.TotalMilliseconds;

    private static int Score(WorldState world, Npc npc) => SocialInteractions.Mood(world, world.ObjectCatalog!.Social!, npc, Now(world)).Score;

    private static void FillNeeds(WorldState world, Npc npc)
    {
        foreach (var need in new[] { "hygiene", "fun", "bladder", "social" }) HomeLife.Add(world, npc, need, 100, Now(world));
        npc.SetHunger(100, world.CurrentDate.TotalHours);
        npc.SetSleep(100, world.CurrentDate.TotalHours);
    }

    private static void Social(WorldState world, string id, Npc actor, Npc target) =>
        MoodMechanic.OnSocial(world, world.ObjectCatalog!.Social!.Find(id)!, actor, target, accepted: true, Now(world));

    // --- humor ---

    [Fact]
    public void A_pile_of_good_moments_does_not_saturate_the_mood_and_repeating_the_same_one_is_worth_less()
    {
        var (world, _) = Load(couple: true);
        var marina = Marina(world);
        var rafael = Rafael(world);
        FillNeeds(world, marina);
        int rested = Score(world, marina);

        foreach (var use in new[] { "shower", "wash", "toilet", "meal", "snack", "cook", "watch", "relax" }) MoodMechanic.OnUse(world, marina, use, Now(world));
        foreach (var id in new[] { "chat", "compliment", "hug", "kiss", "joke" }) Social(world, id, rafael, marina);
        int pile = Score(world, marina);

        Assert.InRange(pile, rested, 96); // sobe, mas a nota nunca chega a "100 aos 20 minutos"
        var hugged = world.ObjectCatalog!.Moods!.Moment("hugged")!;
        Assert.True(hugged.Falloff < 1);
        double once = MoodMechanic.WeightOf(hugged, 1), again = MoodMechanic.WeightOf(hugged, 3);
        Assert.True(again < once); // o mesmo momento repetido vale cada vez menos
        for (int i = 0; i < 4; i++) Social(world, "hug", rafael, marina);
        var view = MoodMechanic.Moments(world, marina, Now(world)).Single(m => m.Id == "hugged");
        Assert.True(view.Count > 1 && view.Weight < hugged.Weight * view.Count);
    }

    [Fact]
    public void The_score_follows_the_dominant_emotion_an_angry_person_is_never_radiant()
    {
        var (world, _) = Load(couple: true);
        var marina = Marina(world);
        var rafael = Rafael(world);
        FillNeeds(world, marina);
        foreach (var id in new[] { "hug", "kiss" }) Social(world, id, rafael, marina);
        Social(world, "argue", rafael, marina);
        Social(world, "argue", rafael, marina);
        Social(world, "argue", rafael, marina);

        var emotion = MoodMechanic.EmotionOf(world, marina, Now(world));
        var (score, label) = SocialInteractions.Mood(world, world.ObjectCatalog!.Social!, marina, Now(world));

        Assert.Equal("angry", emotion!.Id);
        Assert.True(score <= emotion.ScoreMax, $"nota {score} com emoção {emotion.Id}");
        Assert.NotEqual("Radiante", label);
        Assert.Equal(score, MoodMechanic.ViewOf(world, marina, Now(world))!.Score); // o cartão diz o mesmo que a aceitação lê
    }

    [Fact]
    public void A_fight_costs_affection_and_trust_and_an_apology_only_gets_part_of_it_back_while_the_memory_lasts_days()
    {
        var (world, _) = Load(couple: true);
        var marina = Marina(world);
        var rafael = Rafael(world);
        var social = world.ObjectCatalog!.Social!;
        var relation = SocialInteractions.RelationOf(world, marina, rafael)!;
        int affection = relation.Affection, trust = relation.Trust;

        SocialInteractions.ApplyEffects(world, social.Find("argue")!.Effects, marina, rafael, 1, Now(world), "argue");
        var after = SocialInteractions.RelationOf(world, marina, rafael)!;
        int lostAffection = affection - after.Affection, lostTrust = trust - after.Trust;
        Assert.True(lostAffection >= 12 && lostTrust >= 7, $"afeição -{lostAffection} confiança -{lostTrust}");

        SocialInteractions.ApplyEffects(world, social.Find("apologize")!.Effects, marina, rafael, 1, Now(world), "apologize");
        var healed = SocialInteractions.RelationOf(world, marina, rafael)!;
        Assert.True(healed.Affection - after.Affection < lostAffection / 2 && healed.Trust - after.Trust < lostTrust / 2); // só recupera parte
        Assert.True(healed.Affection < affection && healed.Trust < trust);

        Social(world, "argue", marina, rafael);
        var quarrel = MoodMechanic.Moments(world, rafael, Now(world)).Single(m => m.Id == "quarrel");
        Assert.True(quarrel.RemainingMs >= 2 * 24 * 60 * 60_000L); // a memória da briga pesa por dias
    }

    [Fact]
    public void Wishes_rotate_every_day_and_a_done_one_waits_before_coming_back()
    {
        var (world, _) = Load(couple: true);
        var marina = Marina(world);
        var moods = world.ObjectCatalog!.Moods!;
        Assert.True(moods.Rules.WishDailyPool is > 0 && moods.Rules.WishDailyPool < moods.Wishes.Count);
        long day = (long)world.Calendar.HoursPerDay * LivingWorld.Domain.Shared.WorldMillis.PerHour;
        var days = Enumerable.Range(0, 6).Select(d => MoodMechanic.DailyWishes(world, moods, marina, Now(world) + d * day)).ToList();

        Assert.All(days, d => Assert.Equal(moods.Rules.WishDailyPool, d.Count));
        Assert.Contains(days.Skip(1), d => !d.SetEquals(days[0])); // cada dia, outro conjunto
        Assert.True(days[2].SetEquals(MoodMechanic.DailyWishes(world, moods, marina, Now(world) + 2 * day + 1000))); // e o mesmo dia dá sempre o mesmo (RNG do mundo)
    }

    [Fact]
    public void Eating_at_the_table_without_hunger_gives_no_good_moment_and_a_real_meal_does()
    {
        var (world, _) = Load(couple: true);
        var marina = Marina(world);

        MoodMechanic.OnUse(world, marina, "meal", Now(world), share: 0.03); // a barriga cheia: o uso durou um minuto dos 30
        Assert.DoesNotContain(MoodMechanic.Moments(world, marina, Now(world)), m => m.Id == "home-meal");

        MoodMechanic.OnUse(world, marina, "meal", Now(world), share: 0.6);
        Assert.Contains(MoodMechanic.Moments(world, marina, Now(world)), m => m.Id == "home-meal");
    }

    // --- turno ---

    [Fact]
    public void Whoever_takes_the_body_after_the_notice_hour_still_gets_the_shift_notice_once()
    {
        var (world, clock) = Load(root => root["StartHour"] = 6);
        world.UsePlayers(null);
        var service = new PlayerService(new AllEvents());
        var marina = Marina(world);
        var rafael = Rafael(world);
        Assert.True(service.Incarnate(world, marina.Id, "alan", PossessionMode.Full).IsSuccess);
        clock.Tick(world); // 07:00: o aviso da hora exata sai só para quem o jogador conduz (Marina não tem turno)
        Assert.DoesNotContain(world.Careers!.Notices, n => n.Kind == "shift-soon" && n.Npc == rafael.Id.Value);

        Assert.True(service.Switch(world, rafael.Id, "alan").IsSuccess); // assume Rafael já dentro da janela do turno

        Assert.Single(world.Careers.Notices, n => n.Kind == "shift-soon" && n.Npc == rafael.Id.Value);
        Assert.True(CareerViews.Of(world, rafael)!.Job!.CanGo);
        Assert.Contains("shift-soon", CareerViews.Of(world, rafael)!.StopKinds!); // o tempo cai para 1× nos eventos que o dado diz
        Assert.True(service.Switch(world, marina.Id, "alan").IsSuccess);
        Assert.True(service.Switch(world, rafael.Id, "alan").IsSuccess);
        Assert.Single(world.Careers.Notices, n => n.Kind == "shift-soon" && n.Npc == rafael.Id.Value); // voltar à janela não repete o aviso
    }

    [Fact]
    public void The_worker_comes_back_inside_the_house_and_the_sale_is_never_offered_on_the_doorstep()
    {
        var (world, clock) = Load();
        world.UsePlayers(null);
        var service = new PlayerService(new AllEvents());
        var rafael = Rafael(world);
        Assert.True(service.Incarnate(world, Marina(world).Id, "alan", PossessionMode.Full).IsSuccess);
        clock.Tick(world); // 08:00: Rafael sai
        Assert.True(CareerWork.IsAway(world, rafael));
        var mates = CareerViews.Of(world, Marina(world))!.Mates!;
        Assert.Single(mates, m => m.Id == rafael.Id.Value && m.Away && m.ReturnText is not null); // "Rafael volta às ..."
        Assert.Equal(CareerWork.NextReturnMs(world, Marina(world)), mates.Single().ReturnMs);

        for (int h = 0; h < 11; h++) clock.Tick(world); // volta às 17:40
        Assert.False(CareerWork.IsAway(world, rafael));

        // o corpo que o jogador conduz volta para dentro, pela porta, e na soleira não há "Vender · Sua casa"
        var (world2, clock2) = Load(root => root["StartHour"] = 6);
        world2.UsePlayers(null);
        var service2 = new PlayerService(new AllEvents());
        var body = Rafael(world2);
        Assert.True(service2.Incarnate(world2, body.Id, "alan", PossessionMode.Full).IsSuccess);
        clock2.Tick(world2); // 07:00
        Assert.True(service2.Interact(world2, "alan", "job", body.Id.Value, "go").IsSuccess);
        for (int h = 0; h < 12; h++) clock2.Tick(world2);

        Assert.False(CareerWork.IsAway(world2, body));
        var home = world2.FindBuilding(world2.Households.First().Dwelling!.Value)!;
        var door = PlayerGround.DoorOf(home)!.Value;
        var at = PlayerGround.PoseOf(world2, body);
        Assert.NotEqual(door.Doorstep, at); // não na soleira
        Assert.Equal(home.Id, PlayerGround.At(world2, at)!.Value.Building.Id); // dentro da casa
        Assert.DoesNotContain(PlayerService.Reach(world2, "alan")!, o => o.Kind == "door" && o.Verbs.Contains("enter"));
        body.MoveTo(door.Doorstep, 0);
        Assert.DoesNotContain(PlayerService.Reach(world2, "alan")!, o => o.Kind == "house"); // na soleira não há "Vender · Sua casa"
    }

    [Fact]
    public void Skipping_until_the_return_of_the_shift_runs_the_time_to_that_instant_and_is_refused_without_anyone_away()
    {
        var (world, clock) = Load();
        world.UsePlayers(null);
        var service = new PlayerService(new AllEvents());
        Assert.True(service.Incarnate(world, Marina(world).Id, "alan", PossessionMode.Full).IsSuccess);
        Assert.Null(CareerWork.NextReturnMs(world, Marina(world))); // ninguém fora

        clock.Tick(world);
        long back = CareerWork.NextReturnMs(world, Marina(world))!.Value;
        Assert.True(service.Accelerate(world, "alan", back).IsSuccess);
        Assert.Equal(back, service.Skip!.UntilMs);
        Assert.False(service.Accelerate(world, "alan", Now(world) - 1).IsSuccess); // horário que já passou
    }

    // --- aparência ---

    [Fact]
    public void Each_authored_person_of_the_sandbox_has_a_look_of_their_own_declared_in_the_scenario()
    {
        var (world, _) = Load();
        var looks = world.Npcs.Select(n => (n.Name, n.Appearance)).ToList();

        Assert.All(looks, l => Assert.NotNull(l.Appearance));
        Assert.Equal(looks.Count, looks.Select(l => l.Appearance!.Outfit).Distinct().Count()); // um traje por pessoa
        var marina = Marina(world).Appearance!;
        var rafael = Rafael(world).Appearance!;
        Assert.NotEqual(marina.Outfit, rafael.Outfit);
        Assert.True(Math.Abs(marina.SkinTone - rafael.SkinTone) >= 20 && Math.Abs(marina.Build - rafael.Build) >= 20); // diferentes de verdade, não só de nome
    }

    // --- ir até ---

    [Fact]
    public void Going_to_a_person_walks_the_body_through_the_engine_route_and_stops_in_reach_and_walking_takes_the_body_back()
    {
        var (world, clock) = Load(couple: true);
        world.UsePlayers(null);
        var service = new PlayerService(new AllEvents());
        Assert.True(service.SetTickDelta(world, "alan", 250).IsSuccess);
        var marina = Marina(world);
        var rafael = Rafael(world);
        Assert.True(service.Incarnate(world, marina.Id, "alan", PossessionMode.Full).IsSuccess);
        var home = world.FindBuilding(world.Households.First().Dwelling!.Value)!;
        var door = PlayerGround.DoorOf(home)!.Value;
        marina.MoveTo(door.Doorstep, 0); // fora, de frente para a rua
        marina.SetFine(new FinePose(door.Doorstep.X * FinePose.PerCell + 500, door.Doorstep.Y * FinePose.PerCell + 500, 0, 0, Now(world)));

        var refused = service.Go(world, "alan", null, null, 9999, null, null);
        Assert.False(refused.IsSuccess);
        var went = service.Go(world, "alan", null, null, rafael.Id.Value, null, "Rafael");
        Assert.True(went.IsSuccess, went.Error);
        Assert.Equal("go", PlayerTasks.Of(world, marina)!.Action);
        Assert.Equal("Rafael", PlayerTasks.Of(world, marina)!.Label);

        for (int i = 0; i < 4000 && marina.BodyTask is not null; i++) clock.Tick(world, 250);

        Assert.Null(marina.BodyTask); // chegou (ou ficou ao alcance) pela rota, passando pela porta
        var a = PlayerGround.PoseOf(world, marina);
        var b = PlayerGround.PoseOf(world, rafael);
        Assert.InRange(Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)), 0, 2);
        Assert.Equal(home.Id, PlayerGround.At(world, a)!.Value.Building.Id);

        marina.MoveTo(door.Doorstep, 0);
        marina.SetFine(new FinePose(door.Doorstep.X * FinePose.PerCell + 500, door.Doorstep.Y * FinePose.PerCell + 500, 0, 0, Now(world)));
        Assert.True(service.Go(world, "alan", door.Door.X, door.Door.Y, null, null, "Casa").IsSuccess);
        Assert.True(service.Walk(world, "alan", 0, -1000).IsSuccess); // WASD manda de volta
        Assert.Null(marina.BodyTask);
    }
}
