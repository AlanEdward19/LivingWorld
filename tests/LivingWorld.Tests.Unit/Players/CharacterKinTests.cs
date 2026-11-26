using System.Text.Json.Nodes;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Authoring;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>AD-197t2: parentes genealógicos (neto, sobrinho, tio, primo) com laço real pelos pais, o irmão sem pais registrados recusado
/// com motivo, e o menor de idade que entra na casa de quem cuida.</summary>
public class CharacterKinTests
{
    // avós (0, 1); filha Ana (2) casada com Davi (3); filho Bruno (4); filhos de Ana e Davi: Kai (5) e Lia (6)
    private const string Clan =
        "{\"Members\":[" +
        "{\"Name\":\"Avo Reis\",\"Sex\":\"Male\",\"AgeYears\":70,\"ProfessionId\":1,\"Spouse\":1}," +
        "{\"Name\":\"Ava Reis\",\"Sex\":\"Female\",\"AgeYears\":66,\"ProfessionId\":2,\"Spouse\":0}," +
        "{\"Name\":\"Ana Reis\",\"Sex\":\"Female\",\"AgeYears\":42,\"ProfessionId\":2,\"Spouse\":3,\"Mother\":1,\"Father\":0}," +
        "{\"Name\":\"Davi Lima\",\"Sex\":\"Male\",\"AgeYears\":44,\"ProfessionId\":1,\"Spouse\":2}," +
        "{\"Name\":\"Bruno Reis\",\"Sex\":\"Male\",\"AgeYears\":38,\"ProfessionId\":1,\"Mother\":1,\"Father\":0}," +
        "{\"Name\":\"Kai Lima\",\"Sex\":\"Male\",\"AgeYears\":12,\"ProfessionId\":1,\"Mother\":2,\"Father\":3}," +
        "{\"Name\":\"Lia Lima\",\"Sex\":\"Female\",\"AgeYears\":9,\"ProfessionId\":1,\"Mother\":2,\"Father\":3}]}";

    private sealed record Arm(WorldState World, WorldClock Clock, BufferingWorldEventSink Sink, LivingWorld.Domain.Cities.City City, IReadOnlyList<Npc> Clan);

    private static Arm Village()
    {
        var (world, _) = ScenarioRunner.Create(seed: 21, initialPopulation: 30);
        var sink = new BufferingWorldEventSink();
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        var made = WorldAuthoringCommands.CreateHousehold(world, new TickContext(world, world.Rng, world.Scheduler, sink), JsonNode.Parse(Clan)!.AsObject());
        Assert.True(made.IsSuccess, made.Error);
        var members = made.Value!.NpcIds.Select(id => world.FindNpc(new LivingWorld.Domain.Shared.NpcId(id))!).ToList();
        var city = world.FindActiveCity(members[0].City)!;
        sink.DrainAll();
        return new Arm(world, clock, sink, city, members);
    }

    private static CharacterRole Role(string kind, Npc anchor, Npc? via = null) => new(kind, anchor.Id.Value, ViaId: via?.Id.Value);

    private static CharacterSpec Spec(Arm arm, CharacterRole role, int? age = null, long seed = 1) =>
        new(arm.City.Id.Value, AgeYears: age, Seed: seed, Role: role);

    private static (int Min, int Max) Window(Arm arm, CharacterRole role) => CharacterBuilder.AgeWindow(arm.World, arm.City, role).Value;

    [Fact]
    public void A_grandchild_is_the_child_of_a_child_of_the_relative_with_real_ties_and_lives_with_the_parent()
    {
        var arm = Village();
        var (grandfather, _, ana, davi) = (arm.Clan[0], arm.Clan[1], arm.Clan[2], arm.Clan[3]);
        var service = new PlayerService(arm.Sink);
        var role = Role(CharacterRole.Grandchild, grandfather, ana);

        var made = service.Character(arm.World, "neta", Spec(arm, role, Window(arm, role).Min));

        Assert.True(made.IsSuccess, made.Error);
        var grandchild = made.Value!;
        Assert.Equal((ana.Id, davi.Id), (grandchild.MotherId, grandchild.FatherId));
        Assert.Equal(ana.Household, grandchild.Household);
        Assert.Contains(grandchild.Id, arm.World.FindHousehold(ana.Household!.Value)!.Members);
        Assert.True(LivingWorld.Simulation.Population.Family.Kinship.Relatedness(arm.World, grandchild, grandfather, 3) > 0);
        Assert.Contains(arm.Sink.DrainAll(), e => e.Kind == WorldEventKind.CharacterJoinedFamily);
    }

    [Fact]
    public void A_nephew_and_a_cousin_are_children_of_a_sibling_and_of_an_uncle_with_the_family_name_of_the_father()
    {
        var arm = Village();
        var (ana, bruno, kai) = (arm.Clan[2], arm.Clan[4], arm.Clan[5]);
        var service = new PlayerService(arm.Sink);
        var nephew = Role(CharacterRole.Nephew, bruno, ana); // sobrinho de Bruno: filho da irmã dele, Ana
        var cousin = Role(CharacterRole.Cousin, kai, bruno); // primo de Kai: filho de Bruno, irmão da mãe dele

        var asNephew = service.Character(arm.World, "sobrinho", Spec(arm, nephew, Window(arm, nephew).Max));
        service.Logout(arm.World, "sobrinho");
        var asCousin = service.Character(arm.World, "primo", Spec(arm, cousin, Window(arm, cousin).Max, seed: 2));

        Assert.True(asNephew.IsSuccess, asNephew.Error);
        Assert.Equal(ana.Id, asNephew.Value!.MotherId);
        Assert.True(asCousin.IsSuccess, asCousin.Error);
        Assert.Equal(bruno.Id, asCousin.Value!.FatherId);
        Assert.EndsWith("Reis", asCousin.Value.Name); // o nome de família do pai, como em qualquer criança
        Assert.Equal(bruno.Household, asCousin.Value.Household);
    }

    [Fact]
    public void An_uncle_is_a_sibling_of_the_parent_and_shares_the_grandparents()
    {
        var arm = Village();
        var (grandfather, grandmother, ana, kai) = (arm.Clan[0], arm.Clan[1], arm.Clan[2], arm.Clan[5]);
        var role = Role(CharacterRole.Uncle, kai, ana); // tio de Kai: irmão de Ana, a mãe dele
        var window = Window(arm, role);

        var made = new PlayerService(arm.Sink).Character(arm.World, "tio", Spec(arm, role, window.Min));

        Assert.True(made.IsSuccess, made.Error);
        Assert.Equal((grandmother.Id, grandfather.Id), (made.Value!.MotherId, made.Value.FatherId));
        Assert.True(LivingWorld.Simulation.Population.Family.CourtshipSystem.IsFirstDegreeRelative(made.Value, ana)); // irmão de Ana
    }

    [Fact]
    public void A_sibling_of_a_founder_without_recorded_parents_is_refused_with_the_reason_and_the_world_is_untouched()
    {
        var arm = Village();
        var service = new PlayerService(arm.Sink);
        string before = WorldSnapshot.CanonicalHash(arm.World);

        var refused = service.Character(arm.World, "irma", Spec(arm, Role(CharacterRole.Sibling, arm.Clan[0])));

        Assert.StartsWith(PlayerCommands.Conflict, refused.Error);
        Assert.Contains("antepassados", refused.Error);
        Assert.Empty(service.Log.Records);
        Assert.Equal(before, WorldSnapshot.CanonicalHash(arm.World));
    }

    [Fact]
    public void An_unrelated_or_missing_via_is_refused_with_a_reason_and_nothing_changes()
    {
        var arm = Village();
        var (grandfather, davi, bruno) = (arm.Clan[0], arm.Clan[3], arm.Clan[4]);
        var service = new PlayerService(arm.Sink);
        string before = WorldSnapshot.CanonicalHash(arm.World);

        var refusals = new[]
        {
            Spec(arm, Role(CharacterRole.Grandchild, grandfather)), // sem ViaId
            Spec(arm, Role(CharacterRole.Grandchild, grandfather, davi)), // Davi não é filho do avô
            Spec(arm, Role(CharacterRole.Nephew, bruno, davi)), // Davi não é irmão de Bruno
            Spec(arm, Role(CharacterRole.Cousin, bruno, davi)),
            Spec(arm, Role(CharacterRole.Uncle, grandfather, arm.Clan[2])), // Ana não é pai nem mãe do avô
        };
        foreach (var spec in refusals)
        {
            var result = service.Character(arm.World, "x", spec);
            Assert.StartsWith(PlayerCommands.Conflict, result.Error);
            Assert.Contains("parent", result.Error); // parente / parentesco
        }
        Assert.Empty(service.Log.Records);
        Assert.Equal(before, WorldSnapshot.CanonicalHash(arm.World));
    }

    [Fact]
    public void A_minor_enters_a_family_as_a_dependent_child_living_with_the_caretaker_under_the_rules_of_any_child()
    {
        var arm = Village();
        var (ana, davi) = (arm.Clan[2], arm.Clan[3]);
        var service = new PlayerService(arm.Sink);
        var role = Role(CharacterRole.Child, ana);

        var made = service.Character(arm.World, "menino", new CharacterSpec(arm.City.Id.Value, "Teo Lima", AgeYears: 10, Role: role,
            Look: new CharacterLook(HeightM: 1.40)));

        Assert.True(made.IsSuccess, made.Error);
        var child = made.Value!;
        Assert.Equal(10, child.AgeYears(arm.World.CurrentDate));
        Assert.Equal(LivingWorld.Domain.Population.Body.LifeStage.Child, arm.World.LifeStageRules.LifeStageOf(10));
        Assert.Equal(ProfessionType.None, child.Profession); // ninguém tem ofício antes da juventude
        Assert.Equal(0, child.Wallet.Amount);
        Assert.Equal((ana.Id, davi.Id), (child.MotherId, child.FatherId));
        Assert.Equal(ana.Household, child.Household);
        Assert.InRange(child.Height, 1.39, 1.41); // a altura da idade (a faixa é a de quem ainda cresce)
        Assert.True(child.AdultHeight > child.Height); // ainda cresce até a estatura adulta

        arm.Clock.Run(arm.World, 24 * 5);
        Assert.True(child.IsAlive);
        Assert.Equal(ana.Household, child.Household); // segue dependente de quem cuida
    }

    [Fact]
    public void Minor_rules_refuse_what_a_child_cannot_have_or_do_and_a_minor_alone()
    {
        var arm = Village();
        var ana = arm.Clan[2];
        var service = new PlayerService(arm.Sink);
        string before = WorldSnapshot.CanonicalHash(arm.World);
        var child = Role(CharacterRole.Child, ana);

        var refusals = new[]
        {
            new CharacterSpec(arm.City.Id.Value, AgeYears: 10, Money: 50, Role: child), // sem dinheiro próprio
            new CharacterSpec(arm.City.Id.Value, AgeYears: 10, ProfessionId: 1, Role: child), // sem ofício
            new CharacterSpec(arm.City.Id.Value, AgeYears: 10, Look: new CharacterLook(HeightM: 1.80), Role: child), // altura de adulto aos 10
            new CharacterSpec(arm.City.Id.Value, AgeYears: 10), // recém-chegado sozinho: só adulto
            new CharacterSpec(arm.City.Id.Value, AgeYears: 10, Role: Role(CharacterRole.Spouse, arm.Clan[4])),
            new CharacterSpec(arm.City.Id.Value, AgeYears: 3, Role: child), // abaixo da menor idade do criador
        };
        foreach (var spec in refusals)
        {
            var result = service.Character(arm.World, "m", spec);
            Assert.StartsWith(PlayerCommands.Conflict, result.Error);
        }
        Assert.Empty(service.Log.Records);
        Assert.Equal(before, WorldSnapshot.CanonicalHash(arm.World));
    }

    [Fact]
    public void Kin_choices_replay_to_the_same_hash()
    {
        var arm = Village();
        string snapshot = WorldSnapshot.Serialize(arm.World);
        var service = new PlayerService(arm.Sink);
        var (grandfather, ana, bruno, kai) = (arm.Clan[0], arm.Clan[2], arm.Clan[4], arm.Clan[5]);
        var grandchild = Role(CharacterRole.Grandchild, grandfather, ana);
        arm.Clock.Run(arm.World, 3);
        Assert.True(service.Character(arm.World, "a", Spec(arm, grandchild, 8)).IsSuccess);
        arm.Clock.Run(arm.World, 30);
        Assert.True(service.Logout(arm.World, "a").IsSuccess);
        var cousin = Role(CharacterRole.Cousin, kai, bruno);
        Assert.True(service.Character(arm.World, "a", Spec(arm, cousin, Window(arm, cousin).Min, seed: 7)).IsSuccess);
        arm.Clock.Run(arm.World, 48);

        var replayed = PlayerReplay.Run(snapshot, service.Log.Records, new WorldClock(ScenarioRunner.DefaultSystems(), sink: new BufferingWorldEventSink()),
            new BufferingWorldEventSink(), arm.World.CurrentDate.TotalMilliseconds);

        Assert.Equal(WorldSnapshot.CanonicalHash(arm.World), WorldSnapshot.CanonicalHash(replayed));
    }
}
