using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Population;

/// <summary>estate-inheritance: a carteira do morto passa aos herdeiros em ordem fixa sem criar nem destruir moeda, a
/// despensa que ficaria sem ninguém e a oficina do dono acompanham os herdeiros, e o morto sem herdeiro deixa tudo à cidade.</summary>
public class EstateSystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
    private static readonly CellCoord Home = new(2, 2);
    private static readonly CellCoord Shop = new(1, 1);

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static WorldState NewWorld() => new(
        Calendar, 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
        ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
        ScenarioRunner.DefaultLifeStageRules, familyRules: ScenarioRunner.DefaultFamilyRules);

    private static Npc Add(
        WorldState world, long wallet, int age = 30, NpcId? mother = null, NpcId? father = null, CellCoord? at = null)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", Sex.Female, WorldDate.Epoch(Calendar).AddYears(-age), new CultureId(1),
            at ?? Home, mother, father, household: null, health: 100,
            personality: Neutral, profession: new ProfessionType(1), currentLocation: at ?? Home, wallet: new Money(wallet));
        world.AddNpc(npc);
        return npc;
    }

    private static long TotalMoney(WorldState world) =>
        world.Npcs.Sum(n => n.Wallet.Amount) + world.Workplaces.Sum(w => w.Treasury.Amount) + world.Cities.Sum(c => c.Treasury.Amount);

    private static (RecordingSink Sink, TickContext Ctx) Context(WorldState world)
    {
        var sink = new RecordingSink();
        return (sink, new TickContext(world, world.Rng, world.Scheduler, sink));
    }

    private static void Die(WorldState world, Npc npc, RecordingSink sink, TickContext ctx) =>
        NpcDeath.Apply(world, ctx, npc, WorldEventKind.Death);

    [Fact]
    public void The_living_spouse_inherits_everything_and_the_world_balance_is_unchanged()
    {
        var world = NewWorld();
        var dead = Add(world, 900);
        var spouse = Add(world, 100);
        var child = Add(world, 0, age: 5, mother: dead.Id);
        dead.Marry(spouse.Id);
        long before = TotalMoney(world);
        var (sink, ctx) = Context(world);

        Die(world, dead, sink, ctx);

        Assert.Equal(1000, spouse.Wallet.Amount);
        Assert.Equal(0, child.Wallet.Amount);
        Assert.Equal(0, dead.Wallet.Amount);
        Assert.Equal(before, TotalMoney(world));
        var settled = Assert.Single(sink.Events, e => e.Kind == WorldEventKind.EstateSettled);
        Assert.Equal($"{dead.Id.Value}|{spouse.Id.Value}|900|0|0", settled.Payload);
    }

    [Fact]
    public void Children_split_equally_and_the_remainder_goes_to_the_lowest_id()
    {
        var world = NewWorld();
        var dead = Add(world, 1000);
        var a = Add(world, 0, age: 8, father: dead.Id);
        var b = Add(world, 0, age: 6, mother: dead.Id);
        var c = Add(world, 0, age: 4, mother: dead.Id);
        long before = TotalMoney(world);
        var (sink, ctx) = Context(world);

        Die(world, dead, sink, ctx);

        Assert.Equal(334, a.Wallet.Amount);
        Assert.Equal(333, b.Wallet.Amount);
        Assert.Equal(333, c.Wallet.Amount);
        Assert.Equal(before, TotalMoney(world));
    }

    [Fact]
    public void Without_spouse_or_children_the_parents_inherit_then_the_siblings()
    {
        var world = NewWorld();
        var mother = Add(world, 0, age: 60);
        var father = Add(world, 0, age: 62);
        var dead = Add(world, 600, mother: mother.Id, father: father.Id);
        var sibling = Add(world, 0, mother: mother.Id);
        var (sink, ctx) = Context(world);

        Die(world, dead, sink, ctx);
        Assert.Equal(300, mother.Wallet.Amount);
        Assert.Equal(300, father.Wallet.Amount);
        Assert.Equal(0, sibling.Wallet.Amount);

        var world2 = NewWorld();
        var m2 = Add(world2, 0, age: 60);
        var dead2 = Add(world2, 500, mother: m2.Id);
        var sib2 = Add(world2, 0, mother: m2.Id);
        var (sink2, ctx2) = Context(world2);
        NpcDeath.Apply(world2, ctx2, m2, WorldEventKind.Death); // pais mortos
        Die(world2, dead2, sink2, ctx2);
        Assert.Equal(500, sib2.Wallet.Amount);
    }

    [Fact]
    public void Without_any_heir_the_wallet_goes_to_the_city_treasury_or_stays_when_there_is_no_city()
    {
        var world = NewWorld();
        var city = new City(world.NextCityId(), Home, 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var alone = Add(world, 400);
        alone.JoinCity(city.Id);
        long before = TotalMoney(world);
        var (sink, ctx) = Context(world);

        Die(world, alone, sink, ctx);

        Assert.Equal(400, city.Treasury.Amount);
        Assert.Equal(0, alone.Wallet.Amount);
        Assert.Equal(before, TotalMoney(world));

        var noCity = NewWorld();
        var hermit = Add(noCity, 77);
        var (sink2, ctx2) = Context(noCity);
        Die(noCity, hermit, sink2, ctx2);
        Assert.Equal(77, hermit.Wallet.Amount);
    }

    [Fact]
    public void The_pantry_left_without_anyone_goes_to_the_heirs_household_instead_of_being_lost()
    {
        var world = NewWorld();
        var dead = Add(world, 0);
        var child = Add(world, 0, age: 10, mother: dead.Id);
        var oldHome = new Household(new HouseholdId(1), Home, dead.Id, [dead.Id]);
        var childHome = new Household(new HouseholdId(2), new CellCoord(3, 3), child.Id, [child.Id]);
        world.AddHousehold(oldHome);
        world.AddHousehold(childHome);
        dead.JoinHousehold(oldHome.Id);
        child.JoinHousehold(childHome.Id);
        oldHome.Deposit(new ResourceType(1), 40);
        var (sink, ctx) = Context(world);

        Die(world, dead, sink, ctx);

        Assert.Equal(40, childHome.Stock.GetValueOrDefault(new ResourceType(1)));
        Assert.DoesNotContain(sink.Events, e => e.Kind == WorldEventKind.ResourceLost);
        var settled = Assert.Single(sink.Events, e => e.Kind == WorldEventKind.EstateSettled);
        Assert.Equal($"{dead.Id.Value}|{child.Id.Value}|0|40|0", settled.Payload);
    }

    [Fact]
    public void The_owned_workplace_passes_to_the_lowest_id_heir_present_or_else_the_lowest_id_heir()
    {
        var world = NewWorld();
        var dead = Add(world, 0);
        var absent = Add(world, 0, age: 20, mother: dead.Id, at: new CellCoord(8, 8));
        var present = Add(world, 0, age: 18, mother: dead.Id, at: Shop);
        var shop = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), Shop, 5, employees: [],
            stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero, prices: new Dictionary<ResourceType, long>(), owner: dead.Id);
        world.AddWorkplace(shop);
        var (sink, ctx) = Context(world);

        Die(world, dead, sink, ctx);
        Assert.Equal(present.Id, shop.Owner);

        var (sink2, ctx2) = Context(world);
        NpcDeath.Apply(world, ctx2, present, WorldEventKind.Death); // o único herdeiro vivo não está na oficina: herda assim mesmo
        Assert.Equal(absent.Id, shop.Owner);

        NpcDeath.Apply(world, ctx2, absent, WorldEventKind.Death); // sem herdeiro vivo: oficina sem dono
        Assert.Null(shop.Owner);
    }

    [Fact]
    public void An_heir_gets_an_inheritance_relationship_with_the_dead()
    {
        var world = NewWorld();
        var dead = Add(world, 50);
        var child = Add(world, 0, age: 9, mother: dead.Id);
        var (sink, ctx) = Context(world);

        Die(world, dead, sink, ctx);

        var relationship = world.Relationships[new RelationshipKey(dead.Id, child.Id)];
        Assert.True(relationship.Trust > 0);
        Assert.True(relationship.Affection > 0);
    }

    [Fact]
    public void Money_stays_balanced_through_a_year_of_natural_deaths()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 3, initialPopulation: 60);
        long before = TotalMoney(world);
        int deadBefore = world.Npcs.Count(n => !n.IsAlive);

        for (int day = 0; day < 360; day++)
        {
            clock.Run(world, 24);
            Assert.Equal(before + world.MoneyMinted.Amount - world.MoneyDestroyed.Amount, TotalMoney(world));
        }

        Assert.True(world.Npcs.Count(n => !n.IsAlive) > deadBefore, "ninguém morreu: o teste não mediu nada");
    }

    [Fact]
    public void An_owner_survives_a_snapshot_round_trip_and_an_ownerless_workplace_keeps_its_bytes()
    {
        var world = NewWorld();
        var owner = Add(world, 0);
        var owned = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), Shop, 5, employees: [],
            stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero, prices: new Dictionary<ResourceType, long>(), owner: owner.Id);
        var free = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), new CellCoord(3, 3), 5, employees: [],
            stock: new Dictionary<ResourceType, long>(), treasury: Money.Zero, prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(owned);
        world.AddWorkplace(free);

        var json = WorldSnapshot.Serialize(world);
        var restored = WorldSnapshot.Deserialize(json);

        Assert.Equal(owner.Id, restored.Workplaces[0].Owner);
        Assert.Null(restored.Workplaces[1].Owner);
        Assert.DoesNotContain("\"Owner\":null", json); // sem dono não ocupa byte
    }

    [Fact]
    public void With_no_heir_and_no_city_the_event_reports_that_nothing_moved()
    {
        var world = NewWorld();
        var hermit = Add(world, 77);
        var (sink, ctx) = Context(world);

        Die(world, hermit, sink, ctx);

        var settled = Assert.Single(sink.Events, e => e.Kind == WorldEventKind.EstateSettled);
        Assert.Equal($"{hermit.Id.Value}||0|0|0", settled.Payload); // o dinheiro ficou com o morto: nada foi movido
    }
}
