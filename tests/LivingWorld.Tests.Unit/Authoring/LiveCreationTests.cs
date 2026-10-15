using System.Text.Json.Nodes;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Authoring;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Authoring;

/// <summary>#46 fatia 4 (ADR-0033): a família e o local de trabalho criados no mundo em andamento vivem pelas regras de sempre.</summary>
public sealed class LiveCreationTests
{
    private const string Family =
        "{\"Members\":[{\"Name\":\"Ana\",\"Sex\":\"Female\",\"AgeYears\":30,\"ProfessionId\":1,\"Spouse\":1}," +
        "{\"Name\":\"Beto\",\"Sex\":\"Male\",\"AgeYears\":32,\"ProfessionId\":2,\"Spouse\":0}," +
        "{\"Name\":\"Cai\",\"Sex\":\"Male\",\"AgeYears\":4,\"ProfessionId\":1,\"Mother\":0,\"Father\":1}]}";

    private static (WorldState World, WorldClock Clock) Create() => ScenarioRunner.Create(seed: 21, initialPopulation: 30);

    private static TickContext Context(WorldState world) => new(world, world.Rng, world.Scheduler);

    private static JsonObject Body(string json) => JsonNode.Parse(json)!.AsObject();

    [Fact]
    public void The_same_command_on_the_same_world_gives_the_same_hash_and_it_differs_from_not_creating()
    {
        var (a, _) = Create();
        var (b, _) = Create();
        var (control, _) = Create();
        Assert.True(WorldAuthoringCommands.CreateHousehold(a, Context(a), Body(Family)).IsSuccess);
        Assert.True(WorldAuthoringCommands.CreateHousehold(b, Context(b), Body(Family)).IsSuccess);

        Assert.Equal(WorldSnapshot.CanonicalHash(a), WorldSnapshot.CanonicalHash(b));
        Assert.NotEqual(WorldSnapshot.CanonicalHash(control), WorldSnapshot.CanonicalHash(a));
    }

    [Fact]
    public void Newcomers_are_fed_with_minted_money_and_the_arrival_is_logged_with_its_origin()
    {
        var (world, _) = Create();
        var sink = new RecordingSink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        long mintedBefore = world.MoneyMinted.Amount;

        var created = WorldAuthoringCommands.CreateHousehold(world, ctx, Body(Family));

        Assert.True(created.IsSuccess, created.Error);
        Assert.Equal(3, created.Value!.NpcIds.Count);
        Assert.Equal(mintedBefore + 50 * 3, world.MoneyMinted.Amount); // pecúlio cunhado, não criado do nada
        var arrival = Assert.Single(sink.Events, e => e.Kind == WorldEventKind.AuthoredImmigration);
        Assert.StartsWith($"{created.Value.HouseholdId}|{created.Value.CityId}|", arrival.Payload);
        Assert.All(created.Value.NpcIds, id => Assert.NotNull(world.FindNpc(new LivingWorld.Domain.Shared.NpcId(id))));
    }

    [Fact]
    public void The_created_family_lives_a_year_by_the_usual_rules()
    {
        var (world, clock) = Create();
        var created = WorldAuthoringCommands.CreateHousehold(world, Context(world), Body(Family)).Value!;
        var members = created.NpcIds.Select(id => world.FindNpc(new LivingWorld.Domain.Shared.NpcId(id))!).ToList();
        var agesBefore = members.Select(m => m.AgeYears(world.CurrentDate)).ToList();

        clock.Run(world, world.Calendar.HoursPerYear);

        Assert.All(members, m => Assert.True(m.IsAlive, $"{m.Name} morreu no primeiro ano"));
        Assert.Equal(agesBefore.Select(a => a + 1), members.Select(m => m.AgeYears(world.CurrentDate)));
        Assert.Contains(members, m => m.Employer is not null); // ao menos um adulto passou a trabalhar
        Assert.All(members, m => Assert.True(m.Household is not null));
    }

    [Fact]
    public void A_rejected_family_writes_nothing()
    {
        var (world, _) = Create();
        string before = WorldSnapshot.CanonicalHash(world);
        string bad = Family.Replace("\"Mother\":0", "\"Mother\":1"); // o pai não pode ser a mãe

        var result = WorldAuthoringCommands.CreateHousehold(world, Context(world), Body(bad));

        Assert.False(result.IsSuccess);
        Assert.Equal(before, WorldSnapshot.CanonicalHash(world));
    }

    [Fact]
    public void A_workplace_is_placed_like_the_scenario_places_it_and_its_treasury_is_minted()
    {
        var (world, _) = Create();
        long mintedBefore = world.MoneyMinted.Amount;
        var body = Body("{\"LocationTypeId\":1,\"X\":5,\"Y\":5,\"MaxVacancies\":4,\"Treasury\":1000,\"Stock\":{},\"Prices\":{}}");

        var created = WorldAuthoringCommands.CreateWorkplace(world, Context(world), body);

        Assert.True(created.IsSuccess, created.Error);
        var workplace = world.Workplaces.Single(w => w.Id.Value == created.Value!.WorkplaceId);
        Assert.Equal(4, workplace.MaxVacancies);
        Assert.NotNull(workplace.Building);
        Assert.Contains(world.Buildings, b => b.Id == workplace.Building);
        Assert.Equal(mintedBefore + 1000, world.MoneyMinted.Amount);
    }

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];

        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
