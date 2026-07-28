using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Economy;

/// <summary>Fase 7, T10 (FAM-21): <see cref="WagePaymentSystem"/> aplica
/// <see cref="FamilyRules.ApplyUpbringingWeight"/> ao salário quando o canal ambiental está
/// ligado — mesmo valor debitado do <see cref="Workplace.Treasury"/> e creditado no
/// <see cref="Npc.Wallet"/>.</summary>
public class WagePaymentUpbringingTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static readonly EconomyRules Economy = EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long> { [1] = 100 },
        priceFloor: new Dictionary<int, long>(),
        priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0,
        demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static IReadOnlyDictionary<(RelationshipEventType, RelationshipAxis), double> FullDeltas()
    {
        var deltas = new Dictionary<(RelationshipEventType, RelationshipAxis), double>();
        foreach (var type in Enum.GetValues<RelationshipEventType>())
            foreach (var axis in Enum.GetValues<RelationshipAxis>())
                deltas[(type, axis)] = 0.0;
        return deltas;
    }

    private static FamilyRules MakeFamilyRules(bool environmentalWealthChannelEnabled) =>
        FamilyRules.Create(
            relationshipDeltas: FullDeltas(),
            decayPerDay: 0.5,
            contactLossThresholdDays: 30,
            neutralAxisValue: 50,
            attractionWeights: Enum.GetValues<AttractionFactor>().ToDictionary(f => f, _ => 1.0),
            courtshipThreshold: 0.6,
            courtshipDurationDays: 90,
            marriageInitialStock: new Dictionary<int, long> { [1] = 100 },
            conceptionHealthFloor: 40,
            conceptionRelationshipFloor: 40,
            conceptionResourceFloor: new Dictionary<int, long> { [1] = 10 },
            maternalDeathRisk: 0.02,
            infantDeathRisk: 0.05,
            vitalityMotherWeight: 0.5,
            vitalityFatherWeight: 0.5,
            vitalityMutationStdDev: 5,
            vitalityMortalityWeight: 0.3,
            upbringingWealthWeight: 0.3,
            environmentalWealthChannelEnabled: environmentalWealthChannelEnabled,
            neutralDriftEnabled: false).Value!;

    private static WorldState BuildWorld(FamilyRules familyRules) =>
        new(
            Calendar, 1, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: Economy,
            economyCatalog: new EconomyCatalog(new Dictionary<int, ProductionRecipe>(), [], new Dictionary<int, int>()),
            familyRules: familyRules);

    private static Npc MakeEmployee(WorldState world, double upbringing)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "worker", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            new CellCoord(1, 1), motherId: null, fatherId: null, household: null, health: 100,
            personality: SomePersonality, profession: new ProfessionType(1), currentLocation: new CellCoord(1, 1),
            upbringing: upbringing);
        world.AddNpc(npc);
        return npc;
    }

    private static Workplace MakeWorkplace(WorldState world, Money treasury, params NpcId[] employees)
    {
        var workplace = new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), new CellCoord(1, 1), maxVacancies: employees.Length,
            employees: employees, stock: new Dictionary<ResourceType, long>(), treasury: treasury,
            prices: new Dictionary<ResourceType, long>());
        world.AddWorkplace(workplace);
        return workplace;
    }

    private static long TotalMoney(WorldState world) =>
        world.Npcs.Sum(n => n.Wallet.Amount) + world.Workplaces.Sum(w => w.Treasury.Amount);

    [Fact]
    public void When_environmental_wealth_channel_disabled_pays_base_wage_regardless_of_upbringing()
    {
        var rules = MakeFamilyRules(environmentalWealthChannelEnabled: false);
        var world = BuildWorld(rules);
        var employee = MakeEmployee(world, upbringing: 100);
        var workplace = MakeWorkplace(world, new Money(200), employee.Id);
        employee.Hire(workplace.Id);

        new WagePaymentSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        Assert.Equal(new Money(100), employee.Wallet);
        Assert.Equal(new Money(100), workplace.Treasury);
    }

    [Fact]
    public void Higher_upbringing_receives_larger_payment_than_lower_with_same_base_wage()
    {
        var rules = MakeFamilyRules(environmentalWealthChannelEnabled: true);
        var worldHigh = BuildWorld(rules);
        var worldLow = BuildWorld(rules);
        var high = MakeEmployee(worldHigh, upbringing: 100);
        var low = MakeEmployee(worldLow, upbringing: 0);
        var wpHigh = MakeWorkplace(worldHigh, new Money(500), high.Id);
        var wpLow = MakeWorkplace(worldLow, new Money(500), low.Id);
        high.Hire(wpHigh.Id);
        low.Hire(wpLow.Id);

        new WagePaymentSystem().Tick(worldHigh, new TickContext(worldHigh, worldHigh.Rng, worldHigh.Scheduler));
        new WagePaymentSystem().Tick(worldLow, new TickContext(worldLow, worldLow.Rng, worldLow.Scheduler));

        Assert.True(high.Wallet.Amount > low.Wallet.Amount);
        Assert.Equal(130, high.Wallet.Amount);
        Assert.Equal(70, low.Wallet.Amount);
    }

    [Fact]
    public void Payment_preserves_total_money_by_transferring_exactly_from_treasury_to_wallet()
    {
        var rules = MakeFamilyRules(environmentalWealthChannelEnabled: true);
        var world = BuildWorld(rules);
        var employee = MakeEmployee(world, upbringing: 80);
        var workplace = MakeWorkplace(world, new Money(500), employee.Id);
        employee.Hire(workplace.Id);
        long before = TotalMoney(world);

        new WagePaymentSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

        long expectedWage = (long)Math.Round(rules.ApplyUpbringingWeight(100, employee.Upbringing));
        Assert.Equal(new Money(expectedWage), employee.Wallet);
        Assert.Equal(new Money(500 - expectedWage), workplace.Treasury);
        Assert.Equal(before, TotalMoney(world));
    }

    [Fact]
    public void Insufficient_treasury_for_upbringing_adjusted_wage_emits_WageUnpaid_without_creating_money()
    {
        var rules = MakeFamilyRules(environmentalWealthChannelEnabled: true);
        var world = BuildWorld(rules);
        var employee = MakeEmployee(world, upbringing: 100);
        long adjusted = (long)Math.Round(rules.ApplyUpbringingWeight(100, 100));
        var workplace = MakeWorkplace(world, new Money(adjusted - 1), employee.Id);
        employee.Hire(workplace.Id);
        long before = TotalMoney(world);
        var sink = new RecordingSink();

        new WagePaymentSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler, sink));

        Assert.Equal(Money.Zero, employee.Wallet);
        Assert.Equal(new Money(adjusted - 1), workplace.Treasury);
        Assert.Equal(before, TotalMoney(world));
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.WageUnpaid);
    }

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
