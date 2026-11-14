using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Migration;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Population.Family;

/// <summary>Freios preventivos da vila pré-industrial (AD-145): casar e o primeiro filho dependem de meios, a família de origem
/// manda comida antes da esmola, a esmola é racionada até a virada do mês, e quem não tem lugar vai embora.</summary>
public class PreventiveCheckTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly CellCoord Home = new(1, 1);
    private static readonly ResourceType Food = new(1);

    private static readonly Personality Prudent = Personality.Create(50, 50, 100, 50, 50, 50, 50, 50, 0, 100).Value!;
    private static readonly Personality Reckless = Personality.Create(50, 50, 0, 50, 50, 50, 50, 50, 100, 0).Value!;
    private static readonly Personality Restless = Personality.Create(50, 50, 50, 50, 100, 100, 50, 100, 50, 0).Value!;

    private static EconomyRules Economy() => EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(), wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long> { [1] = 1, [2] = 1 }, priceCeiling: new Dictionary<int, long> { [1] = 5, [2] = 5 },
        priceSensitivity: 0, demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private sealed class Sink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    /// <summary>A configuração medida no AD-145 (o padrão do cenário deixa tudo desligado).</summary>
    private static readonly FamilyRules Measured = ScenarioRunner.DefaultFamilyRules with { Pressure = PopulationPressureRules.Measured };

    private static WorldState World(FamilyRules? family = null)
    {
        var world = new WorldState(
            Calendar, 3, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: Economy(), economyCatalog: ScenarioRunner.DefaultEconomyCatalog,
            familyRules: family ?? Measured);
        world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(40);
        return world;
    }

    private static Npc Person(WorldState world, Sex sex, int age, Personality personality, long wallet = 0, NpcId? mother = null)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", sex, world.CurrentDate.AddYears(-age), new CultureId(1), Home,
            motherId: mother, fatherId: null, household: null, health: 100, personality: personality,
            profession: new ProfessionType(1), currentLocation: Home, wallet: new Money(wallet));
        world.AddNpc(npc);
        return npc;
    }

    private static Household House(WorldState world, params Npc[] members)
    {
        var house = new Household(world.NextHouseholdIdAndAdvance(), Home, members[0].Id, members.Select(m => m.Id).ToList());
        world.AddHousehold(house);
        foreach (var m in members) m.JoinHousehold(house.Id);
        return house;
    }

    private static TickContext Ctx(WorldState world, Sink sink) => new(world, world.Rng, world.Scheduler, sink);

    [Fact]
    public void A_prudent_couple_with_no_work_and_no_savings_puts_off_marrying()
    {
        var world = World();
        var him = Person(world, Sex.Male, 28, Prudent);
        var her = Person(world, Sex.Female, 24, Prudent);

        Assert.True(HouseholdMeans.MarriageReadiness(world, him, her) < 0.3);
        Assert.True(HouseholdMeans.MarriageReadiness(world, her, him) < 0.3);
    }

    [Fact]
    public void Work_and_savings_lift_the_brake_and_the_reckless_ignore_it()
    {
        var world = World();
        var him = Person(world, Sex.Male, 28, Prudent, wallet: 5000);
        var her = Person(world, Sex.Female, 24, Prudent);
        him.Hire(new WorkplaceId(1));
        var poorReckless = Person(world, Sex.Male, 28, Reckless);
        var otherPoor = Person(world, Sex.Female, 24, Prudent);

        Assert.Equal(1.0, HouseholdMeans.MarriageReadiness(world, her, him), 6);
        Assert.Equal(1.0, HouseholdMeans.MarriageReadiness(world, poorReckless, otherPoor), 6);
    }

    [Fact]
    public void Without_the_rule_means_do_not_matter()
    {
        var world = World(ScenarioRunner.DefaultFamilyRules with { Pressure = null }); // desligado (o cenário de código liga desde o AD-148)
        var him = Person(world, Sex.Male, 28, Prudent);
        var her = Person(world, Sex.Female, 24, Prudent);

        Assert.Equal(1.0, HouseholdMeans.MarriageReadiness(world, him, her));
        Assert.Equal(1.0, HouseholdMeans.FirstBirthReadiness(world, her, him, House(world, her, him)));
    }

    [Fact]
    public void A_jobless_couple_with_an_empty_pantry_puts_off_only_the_first_child()
    {
        var world = World();
        var him = Person(world, Sex.Male, 28, Prudent);
        var her = Person(world, Sex.Female, 24, Prudent);
        var home = House(world, her, him);

        Assert.True(HouseholdMeans.FirstBirthReadiness(world, her, him, home) < 0.6);

        her.SetReproduction(new LivingWorld.Domain.Population.Body.ReproductiveState(Parity: 1));
        Assert.Equal(1.0, HouseholdMeans.FirstBirthReadiness(world, her, him, home));
    }

    [Fact]
    public void A_working_couple_does_not_put_off_the_first_child()
    {
        var world = World();
        var him = Person(world, Sex.Male, 28, Prudent);
        var her = Person(world, Sex.Female, 24, Prudent);
        him.Hire(new WorkplaceId(1));

        Assert.Equal(1.0, HouseholdMeans.FirstBirthReadiness(world, her, him, House(world, her, him)));
    }

    [Fact]
    public void An_old_grandmother_with_a_full_pantry_sends_food_to_her_hungry_grandchildren_before_any_alms()
    {
        var world = World();
        var granny = Person(world, Sex.Female, 70, Restless); // idosa: sem a chave do AD-145 não ajuda
        var mother = Person(world, Sex.Female, 30, Prudent, mother: granny.Id);
        var child = Person(world, Sex.Male, 5, Prudent, mother: mother.Id);
        House(world, mother, child);
        var grannyHome = House(world, granny);
        grannyHome.Deposit(Food, 300);
        var sink = new Sink();

        for (int day = 0; day < 10; day++)
        {
            world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(40).AddDays(day);
            new MutualAidSystem().Tick(world, Ctx(world, sink));
        }

        var needy = world.FindHousehold(mother.Household!.Value)!;
        Assert.True(needy.Stock.GetValueOrDefault(Food) > 0);
        Assert.Equal(300, needy.Stock.GetValueOrDefault(Food) + grannyHome.Stock.GetValueOrDefault(Food)); // só mudou de casa
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.AidGiven && e.Payload.EndsWith("|food", StringComparison.Ordinal));
    }

    [Fact]
    public void Alms_are_rationed_over_the_days_left_until_the_treasury_refills()
    {
        var world = World();
        var city = new City(world.NextCityId(), Home, 0, foundedFromCityId: null, AggregatePopulationPool.Empty);
        city.CreditTreasury(new Money(300));
        world.AddCity(city);
        for (int i = 0; i < 10; i++)
        {
            var adult = Person(world, Sex.Female, 30, Prudent);
            adult.JoinCity(city.Id);
            var house = new Household(world.NextHouseholdIdAndAdvance(), Home, adult.Id, [adult.Id], city: city.Id);
            world.AddHousehold(house);
            adult.JoinHousehold(house.Id);
        }

        world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(40); // dia 0: faltam 30 dias para o tesouro encher de novo
        new MutualAidSystem().Tick(world, Ctx(world, new Sink()));

        Assert.True(300 - city.Treasury.Amount <= 10, $"deu {300 - city.Treasury.Amount} no primeiro dia");
        Assert.True(city.Treasury.Amount < 300);
    }

    private static (WorldState World, Npc Youth, Npc Mother, Workplace Farm) EmigrationScene(int openSlots, FamilyRules? family = null)
    {
        var world = World(family);
        var mother = Person(world, Sex.Female, 45, Prudent, wallet: 100);
        var youth = Person(world, Sex.Male, 20, Restless, wallet: 37, mother: mother.Id);
        House(world, mother, youth);
        var farm = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), Home, 1 + openSlots, employees: [mother.Id],
            stock: new Dictionary<ResourceType, long>(), treasury: new Money(1000), prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(farm);
        mother.Hire(farm.Id);
        return (world, youth, mother, farm);
    }

    private static List<WorldEvent> RunMonths(WorldState world, int months)
    {
        var sink = new Sink();
        var system = new EmigrationSystem();
        for (int m = 0; m < months; m++)
        {
            world.CurrentDate = WorldDate.Epoch(Calendar).AddYears(40).AddHours(m * Calendar.HoursPerMonth);
            system.Tick(world, Ctx(world, sink));
        }
        return sink.Events;
    }

    [Fact]
    public void A_young_single_adult_without_work_or_any_vacancy_leaves_the_world_and_leaves_the_savings_at_home()
    {
        var (world, youth, mother, _) = EmigrationScene(openSlots: 0);
        long money = youth.Wallet.Amount + mother.Wallet.Amount;

        var events = RunMonths(world, 120);

        var gone = Assert.Single(events, e => e.Kind == WorldEventKind.Emigrated);
        Assert.Equal($"{youth.Id.Value}|fora", gone.Payload);
        Assert.True(youth.Emigrated);
        Assert.False(youth.IsAlive);
        Assert.DoesNotContain(youth.Id, world.FindHousehold(mother.Household!.Value)!.Members);
        Assert.Equal(money, mother.Wallet.Amount);
        Assert.DoesNotContain(youth, world.AliveNpcIndex.Alive);
    }

    [Fact]
    public void With_a_paid_vacancy_in_town_nobody_leaves()
    {
        var (world, youth, _, _) = EmigrationScene(openSlots: 1);

        Assert.DoesNotContain(RunMonths(world, 120), e => e.Kind == WorldEventKind.Emigrated);
        Assert.True(youth.IsAlive);
    }

    /// <summary>AD-149: a vaga que segura é a que o contrataria. Antes qualquer vaga aberta da cidade segurava — a forja de 40
    /// vagas (outro ofício, contrata só pela encomenda) segurava todo lavrador sem trabalho, e a emigração nunca disparava.</summary>
    [Fact]
    public void An_open_slot_in_another_trade_does_not_hold_a_young_farmer_without_work()
    {
        var (world, youth, _, _) = EmigrationScene(openSlots: 0);
        world.AddWorkplace(new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(2), Home, 40, employees: [],
            stock: new Dictionary<ResourceType, long>(), treasury: new Money(1000), prices: new Dictionary<ResourceType, long>()));

        Assert.Contains(RunMonths(world, 120), e => e.Kind == WorldEventKind.Emigrated && e.Payload.StartsWith($"{youth.Id.Value}|", StringComparison.Ordinal));
    }

    [Fact]
    public void A_married_adult_does_not_leave_alone()
    {
        var (world, youth, _, _) = EmigrationScene(openSlots: 0);
        var wife = Person(world, Sex.Female, 20, Prudent);
        youth.Marry(wife.Id);
        wife.Marry(youth.Id);

        Assert.DoesNotContain(RunMonths(world, 120), e => e.Kind == WorldEventKind.Emigrated);
    }

    [Fact]
    public void Turning_emigration_off_changes_the_world()
    {
        var (on, _, _, _) = EmigrationScene(openSlots: 0);
        RunMonths(on, 120);
        var (off, _, _, _) = EmigrationScene(
            openSlots: 0, Measured with { Pressure = PopulationPressureRules.Measured with { Emigration = EmigrationRules.Disabled } });
        RunMonths(off, 120);

        Assert.NotEqual(WorldSnapshot.CanonicalHash(on), WorldSnapshot.CanonicalHash(off));
    }
}
