using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>AD-197t (emenda do ADR-0053): o criador de personagem de verdade. Cada escolha vira estado, a incoerência é recusada com
/// motivo e sem efeito, a família aceita com laços reais, o recém-chegado chega como migrante e o replay dá o mesmo hash.</summary>
public class CharacterBuilderTests
{
    private static CharacterCreationTests.Arm Village(ulong seed) => CharacterCreationTests.Village(seed, pool: 0);

    private static CharacterRole Relative(string kind, Npc anchor) => new(kind, anchor.Id.Value);

    [Fact]
    public void Every_choice_becomes_state_and_what_was_left_blank_is_drawn_by_the_world()
    {
        var arm = Village(3);
        var w = arm.World;
        var service = new PlayerService(arm.Sink);
        long minted = w.MoneyMinted.Amount;
        int rows = w.Npcs.Count;
        int profession = w.PopulationCatalog.ProfessionIds.Order().Last();
        var spec = new CharacterSpec(arm.City.Id.Value, "Marina Teixeira", Sex.Female, 33, Seed: 5, ProfessionId: profession,
            Traits: new CharacterTraits(Openness: 77, Ambition: 11), Health: 81, Money: 120,
            Look: new CharacterLook(1.62, SkinTone: 20, HairTone: 90, HairCurl: 35, Build: 70, EyeTone: 15, FaceShape: 60, Outfit: 3));

        var made = service.Character(w, "ana", spec);

        Assert.True(made.IsSuccess, made.Error);
        var npc = made.Value!;
        Assert.Equal(rows + 1, w.Npcs.Count);
        Assert.Equal("Marina Teixeira", npc.Name);
        Assert.Equal(Sex.Female, npc.Sex);
        Assert.Equal(33, npc.AgeYears(w.CurrentDate));
        Assert.Equal(profession, npc.Profession.Id);
        Assert.Equal((77, 11), (npc.Personality.Openness, npc.Personality.Ambition));
        Assert.Equal(81, npc.Health);
        Assert.Equal(120, npc.Wallet.Amount);
        Assert.Equal(minted + 120, w.MoneyMinted.Amount); // o dinheiro entra de fora e fica contado
        Assert.Equal(new Appearance(20, 90, 35, 70, 15, 60, 3, 70), npc.Appearance);
        Assert.InRange(npc.Height, 1.615, 1.625);
        Assert.Equal("ana", npc.ControllerId);

        // o que ficou em branco o motor sorteou: dois jogadores com a mesma escolha parcial e sementes diferentes dão pessoas diferentes
        var a = CharacterBuilder.Resolve(w, "x", new CharacterSpec(arm.City.Id.Value, Seed: 1)).Value!.Npc;
        var again = CharacterBuilder.Resolve(w, "x", new CharacterSpec(arm.City.Id.Value, Seed: 1)).Value!.Npc;
        var other = CharacterBuilder.Resolve(w, "x", new CharacterSpec(arm.City.Id.Value, Seed: 2)).Value!.Npc;
        Assert.Equal((a.Name, a.Personality, a.Appearance), (again.Name, again.Personality, again.Appearance));
        Assert.NotEqual((a.Name, a.Personality, a.Appearance), (other.Name, other.Personality, other.Appearance));
    }

    [Fact]
    public void The_chosen_body_survives_the_monthly_growth()
    {
        var arm = Village(4);
        var w = arm.World;
        var service = new PlayerService(arm.Sink);
        var npc = service.Character(w, "bia", new CharacterSpec(arm.City.Id.Value, AgeYears: 30, Sex: Sex.Male,
            Look: new CharacterLook(1.90, Build: 90))).Value!;
        double weight = npc.Weight;

        arm.Clock.Run(w, 24 * 70);

        Assert.InRange(npc.Height, 1.89, 1.91);
        Assert.Equal(90, npc.Appearance!.Build);
        Assert.InRange(npc.Weight, weight * 0.9, weight * 1.1); // o arcabouço escolhido segue valendo, não um sorteio novo
    }

    [Fact]
    public void An_incoherent_choice_is_refused_with_a_reason_and_the_world_is_untouched()
    {
        var arm = Village(5);
        var w = arm.World;
        var service = new PlayerService(arm.Sink);
        var city = arm.City.Id.Value;
        var elder = w.Npcs.First(n => n.IsAlive && n.City == arm.City.Id && n.AgeYears(w.CurrentDate) >= 40);
        string before = WorldSnapshot.CanonicalHash(w);
        int longevity = w.PopulationRules.LifeTable.MaxLongevityYears;

        var refusals = new[]
        {
            new CharacterSpec(city, AgeYears: 15),
            new CharacterSpec(city, AgeYears: longevity),
            new CharacterSpec(Guid.NewGuid()),
            new CharacterSpec(city, Name: "A"),
            new CharacterSpec(city, Money: 999_999_999),
            new CharacterSpec(city, Money: -1),
            new CharacterSpec(city, Look: new CharacterLook(HeightM: 3.4)),
            new CharacterSpec(city, Look: new CharacterLook(Outfit: 99)),
            new CharacterSpec(city, Traits: new CharacterTraits(Loyalty: 101)),
            new CharacterSpec(city, ProfessionId: 98765),
            new CharacterSpec(city, Role: new CharacterRole("pirate")),
            new CharacterSpec(city, Role: new CharacterRole(CharacterRole.Child, 987654321)),
            // filho de quem tem a idade dele: os pais não podiam tê-lo gerado
            new CharacterSpec(city, AgeYears: elder.AgeYears(w.CurrentDate), Role: Relative(CharacterRole.Child, elder)),
        };
        foreach (var spec in refusals)
        {
            var result = service.Character(w, "caio", spec);
            Assert.False(result.IsSuccess);
            Assert.StartsWith(PlayerCommands.Conflict, result.Error);
            Assert.Contains(' ', result.Error!.Trim()); // traz o motivo, não só o código
        }
        Assert.Empty(service.Log.Records);
        Assert.Equal(before, WorldSnapshot.CanonicalHash(w));
        Assert.Null(PlayerCommands.ControlledBy(w, "caio"));
    }

    [Fact]
    public void A_child_is_accepted_by_the_family_with_real_ties_to_parents_house_and_estate()
    {
        var arm = Village(6);
        var w = arm.World;
        var service = new PlayerService(arm.Sink);
        var parent = w.Npcs.First(n => n.IsAlive && n.City == arm.City.Id && n.Household is not null
            && CharacterBuilder.AgeWindow(w, arm.City, Relative(CharacterRole.Child, n)).IsSuccess);
        var window = CharacterBuilder.AgeWindow(w, arm.City, Relative(CharacterRole.Child, parent)).Value;
        var home = w.FindHousehold(parent.Household!.Value)!;

        var made = service.Character(w, "dani", new CharacterSpec(arm.City.Id.Value, "Pedro Novo", AgeYears: window.Max,
            Role: Relative(CharacterRole.Child, parent)));

        Assert.True(made.IsSuccess, made.Error);
        var child = made.Value!;
        Assert.True(child.MotherId == parent.Id || child.FatherId == parent.Id);
        Assert.Equal(home.Id, child.Household);
        Assert.Contains(child.Id, home.Members);
        Assert.True(LivingWorld.Simulation.Population.Family.CourtshipSystem.IsFirstDegreeRelative(child, parent)); // parentesco que a herança e o tabu leem
        Assert.Contains(w.Npcs, n => n.IsAlive && (n.MotherId == parent.Id || n.FatherId == parent.Id) && n.Id == child.Id);
        Assert.Contains(arm.Sink.DrainAll(), e => e.Kind == WorldEventKind.CharacterJoinedFamily);
    }

    [Fact]
    public void A_sibling_shares_the_parents_and_the_house_and_needs_parents_on_record()
    {
        var arm = Village(7);
        var w = arm.World;
        arm.Clock.Run(w, 24 * 365 * 22); // anos para nascer gente com pais registrados
        arm.Sink.DrainAll();
        var service = new PlayerService(arm.Sink);
        var city = w.ActiveCities().First();
        var sibling = w.Npcs.FirstOrDefault(n => n.IsAlive && n.City == city.Id && n.Household is not null
            && (n.MotherId is not null || n.FatherId is not null)
            && CharacterBuilder.AgeWindow(w, city, Relative(CharacterRole.Sibling, n)).IsSuccess);
        Assert.NotNull(sibling);
        var window = CharacterBuilder.AgeWindow(w, city, Relative(CharacterRole.Sibling, sibling!)).Value;

        var made = service.Character(w, "eli", new CharacterSpec(city.Id.Value, AgeYears: window.Min, Role: Relative(CharacterRole.Sibling, sibling!)));

        Assert.True(made.IsSuccess, made.Error);
        Assert.Equal((sibling!.MotherId, sibling.FatherId), (made.Value!.MotherId, made.Value.FatherId));
        Assert.Equal(sibling.Household, made.Value.Household);
        var founder = w.Npcs.First(n => n.IsAlive && n.MotherId is null && n.FatherId is null && n.City == city.Id);
        var refused = service.Character(w, "fia", new CharacterSpec(city.Id.Value, Role: Relative(CharacterRole.Sibling, founder)));
        Assert.StartsWith(PlayerCommands.Conflict, refused.Error);
        Assert.Contains("pais", refused.Error);
    }

    [Fact]
    public void A_spouse_must_be_free_and_accept_and_the_marriage_is_real()
    {
        var arm = Village(8);
        var w = arm.World;
        var service = new PlayerService(arm.Sink);
        var city = arm.City;
        var married = w.Npcs.First(n => n.IsAlive && n.City == city.Id && n.Spouse is { } s && w.FindNpc(s) is { IsAlive: true });
        string before = WorldSnapshot.CanonicalHash(w);
        var no = service.Character(w, "gil", new CharacterSpec(city.Id.Value, Role: Relative(CharacterRole.Spouse, married)));
        Assert.StartsWith(PlayerCommands.Conflict, no.Error);
        Assert.Contains("livre", no.Error);
        Assert.Equal(before, WorldSnapshot.CanonicalHash(w));

        Npc? taker = null;
        CharacterSpec? accepted = null;
        foreach (var single in w.Npcs.Where(n => n.IsAlive && n.City == city.Id && n.Spouse is null && n.AgeYears(w.CurrentDate) >= 20).OrderBy(n => n.Id.Value))
            for (long seed = 0; seed < 6 && accepted is null; seed++)
            {
                var spec = new CharacterSpec(city.Id.Value, Sex: single.Sex == Sex.Male ? Sex.Female : Sex.Male, AgeYears: single.AgeYears(w.CurrentDate),
                    Seed: seed, Money: 90, Role: Relative(CharacterRole.Spouse, single));
                if (CharacterBuilder.Resolve(w, "gil", spec).IsSuccess) (taker, accepted) = (single, spec);
            }
        Assert.NotNull(accepted);

        var made = service.Character(w, "gil", accepted!);

        Assert.True(made.IsSuccess, made.Error);
        Assert.Equal(taker!.Id, made.Value!.Spouse);
        Assert.Equal(made.Value.Id, taker.Spouse);
        Assert.NotNull(made.Value.Household);
        Assert.Equal(taker.Household, made.Value.Household);
        Assert.Contains(arm.Sink.DrainAll(), e => e.Kind == WorldEventKind.Marriage);
    }

    [Fact]
    public void A_newcomer_arrives_as_a_migrant_through_a_registered_arrival_event()
    {
        var arm = Village(9);
        var w = arm.World;
        var service = new PlayerService(arm.Sink);
        long population = w.Npcs.Count(n => n.IsAlive && n.City == arm.City.Id);
        arm.Sink.DrainAll();

        var made = service.Character(w, "hana", new CharacterSpec(arm.City.Id.Value, "Hana Vale", Money: 60, Role: new CharacterRole()));

        Assert.True(made.IsSuccess, made.Error);
        var npc = made.Value!;
        Assert.Equal(arm.City.Id, npc.City);
        Assert.NotNull(npc.ArrivedAtTick);
        Assert.Null(npc.MaterializedAtTick); // não saiu do agregado: veio de fora
        Assert.Null(npc.Household); // chega sem casa, como migrante
        Assert.Equal(population + 1, w.Npcs.Count(n => n.IsAlive && n.City == arm.City.Id));
        var arrival = Assert.Single(arm.Sink.DrainAll(), e => e.Kind == WorldEventKind.CharacterArrived);
        Assert.StartsWith($"{npc.Id.Value}|{arm.City.Id.Value}|", arrival.Payload);
    }

    [Fact]
    public void Replaying_the_log_gives_the_same_world_hash()
    {
        var arm = Village(10);
        var w = arm.World;
        string snapshot = WorldSnapshot.Serialize(w);
        var service = new PlayerService(arm.Sink);
        var parent = w.Npcs.First(n => n.IsAlive && n.City == arm.City.Id && n.Household is not null
            && CharacterBuilder.AgeWindow(w, arm.City, Relative(CharacterRole.Child, n)).IsSuccess);
        var kid = new CharacterSpec(arm.City.Id.Value, Role: Relative(CharacterRole.Child, parent), Seed: 3);
        arm.Clock.Run(w, 5);
        Assert.True(service.Character(w, "ivo", kid).IsSuccess);
        arm.Clock.Run(w, 30);
        Assert.True(service.Logout(w, "ivo").IsSuccess);
        Assert.True(service.Character(w, "ivo", new CharacterSpec(arm.City.Id.Value, "Ivo Longe", Money: 40, Seed: 9,
            Look: new CharacterLook(1.7, Outfit: 2))).IsSuccess);
        arm.Clock.Run(w, 48);

        var replayed = PlayerReplay.Run(snapshot, service.Log.Records,
            new WorldClock(ScenarioRunner.DefaultSystems(), sink: new LivingWorld.Infrastructure.EventLog.BufferingWorldEventSink()),
            new LivingWorld.Infrastructure.EventLog.BufferingWorldEventSink(), w.CurrentDate.TotalMilliseconds);

        Assert.Equal(WorldSnapshot.CanonicalHash(w), WorldSnapshot.CanonicalHash(replayed));
        Assert.Equal(2, service.Log.Records.Count(r => r.Kind == PlayerCommandKind.Character));
    }
}
