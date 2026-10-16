using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Population.Family;

/// <summary>Ajuda mútua: a casa sem comida e sem dinheiro é socorrida por parente ou gente de afeto, na medida do
/// altruísmo, da proximidade e do que sobra da reserva de quem ajuda. O dinheiro só muda de carteira.</summary>
public class MutualAidSystemTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly CellCoord Home = new(1, 1);

    private static readonly EconomyRules Rules = EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long> { [1] = 1, [2] = 1 },
        priceCeiling: new Dictionary<int, long> { [1] = 5, [2] = 5 },
        priceSensitivity: 0,
        demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    private static Personality WithAltruism(int altruism) =>
        Personality.Create(50, 50, 50, 50, 50, 50, 50, altruism, 50, 50).Value!;

    private sealed class Scene
    {
        public required WorldState World { get; init; }
        public required Npc Helper { get; init; }
        public required Npc Recipient { get; init; }
        public required Household NeedyHouse { get; init; }
    }

    /// <summary>Casa necessitada: mãe adulta (recipient) e filho, sem comida e sem dinheiro. Ajudante: a avó,
    /// adulta, com casa própria e <paramref name="helperWallet"/> na carteira.</summary>
    private static Scene Build(int helperAltruism, long helperWallet, bool helperIsKin = true, double? affection = null)
    {
        var world = new WorldState(
            Calendar, 7, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: Rules,
            familyRules: ScenarioRunner.DefaultFamilyRules);

        Npc Make(string name, int ageYears, Personality personality, NpcId? mother = null, long wallet = 0)
        {
            var npc = new Npc(
                world.NextNpcIdAndAdvance(), name, Sex.Female, WorldDate.Epoch(Calendar).AddYears(-ageYears), new CultureId(1),
                Home, motherId: mother, fatherId: null, household: null, health: 100,
                personality: personality, profession: new ProfessionType(1), currentLocation: Home,
                wallet: new Money(wallet));
            world.AddNpc(npc);
            return npc;
        }

        var helper = Make("avó", 60, WithAltruism(helperAltruism), wallet: helperWallet);
        var mother = Make("mãe", 30, WithAltruism(50), mother: helperIsKin ? helper.Id : null);
        var child = Make("filho", 5, WithAltruism(50), mother: mother.Id);

        var needy = new Household(new HouseholdId(1), Home, mother.Id, [mother.Id, child.Id]);
        mother.JoinHousehold(needy.Id);
        child.JoinHousehold(needy.Id);
        world.AddHousehold(needy);
        var own = new Household(new HouseholdId(2), Home, helper.Id, [helper.Id]);
        helper.JoinHousehold(own.Id);
        world.AddHousehold(own);

        if (affection is { } value)
        {
            world.GetOrCreateRelationship(new RelationshipKey(helper.Id, mother.Id), 0)
                .CopyAxesFrom(Relationship.FromAxes(0, value, 0, 0, 0));
            world.GetOrCreateRelationship(new RelationshipKey(mother.Id, helper.Id), 0)
                .CopyAxesFrom(Relationship.FromAxes(0, value, 0, 0, 0));
        }

        return new Scene { World = world, Helper = helper, Recipient = mother, NeedyHouse = needy };
    }

    private static List<WorldEvent> RunDays(Scene scene, int days)
    {
        var sink = new RecordingSink();
        var system = new MutualAidSystem();
        for (int day = 0; day < days; day++)
        {
            scene.World.CurrentDate = WorldDate.Epoch(Calendar).AddDays(day);
            system.Tick(scene.World, new TickContext(scene.World, scene.World.Rng, scene.World.Scheduler, sink));
        }
        return sink.Events;
    }

    [Fact]
    public void A_generous_relative_with_surplus_gives_money_to_the_needy_household_and_nothing_is_created()
    {
        var scene = Build(helperAltruism: 100, helperWallet: 1000);
        long before = scene.Helper.Wallet.Amount + scene.Recipient.Wallet.Amount;

        var events = RunDays(scene, days: 30);

        Assert.Contains(events, e => e.Kind == WorldEventKind.AidGiven);
        Assert.True(scene.Recipient.Wallet.Amount > 0);
        Assert.True(scene.Helper.Wallet.Amount < 1000);
        Assert.Equal(before, scene.Helper.Wallet.Amount + scene.Recipient.Wallet.Amount);   // só mudou de carteira
        Assert.All(events.Where(e => e.Kind == WorldEventKind.AidGiven),
            e => Assert.Equal(MutualAidSystem.SystemName, e.SourceSystem));
    }

    [Fact]
    public void Being_helped_raises_the_recipients_trust_respect_and_debt_toward_the_helper_only()
    {
        var scene = Build(helperAltruism: 100, helperWallet: 1000);

        RunDays(scene, days: 3);

        var toHelper = scene.World.Relationships[new RelationshipKey(scene.Recipient.Id, scene.Helper.Id)];
        Assert.True(toHelper.Debt > 0);
        Assert.True(toHelper.Trust > 0);
        Assert.True(toHelper.Respect > 0);
        Assert.False(scene.World.Relationships.ContainsKey(new RelationshipKey(scene.Helper.Id, scene.Recipient.Id)));
    }

    [Fact]
    public void A_selfish_relative_never_helps_however_rich()
    {
        var scene = Build(helperAltruism: 0, helperWallet: 100_000);

        var events = RunDays(scene, days: 60);

        Assert.DoesNotContain(events, e => e.Kind == WorldEventKind.AidGiven);
        Assert.Equal(0, scene.Recipient.Wallet.Amount);
    }

    [Fact]
    public void Someone_who_cannot_spare_beyond_their_own_reserve_gives_nothing()
    {
        // Reserva = 14 dias × 1 pessoa × (comida + água) = 28; abaixo disso não há o que dar.
        var scene = Build(helperAltruism: 100, helperWallet: 20);

        var events = RunDays(scene, days: 60);

        Assert.DoesNotContain(events, e => e.Kind == WorldEventKind.AidGiven);
        Assert.Equal(20, scene.Helper.Wallet.Amount);
    }

    [Fact]
    public void A_household_that_has_money_or_food_is_not_helped()
    {
        var scene = Build(helperAltruism: 100, helperWallet: 5000);
        scene.Recipient.CreditWallet(new Money(20));   // 4 dias de compra = 16: a casa se vira, mesmo faltando para uma semana (28)

        Assert.DoesNotContain(RunDays(scene, days: 30), e => e.Kind == WorldEventKind.AidGiven);

        var fed = Build(helperAltruism: 100, helperWallet: 5000);
        fed.NeedyHouse.Deposit(new ResourceType(1), 100);
        fed.NeedyHouse.Deposit(new ResourceType(2), 100);
        Assert.DoesNotContain(RunDays(fed, days: 30), e => e.Kind == WorldEventKind.AidGiven);
    }

    [Fact]
    public void A_non_relative_helps_only_with_enough_affection()
    {
        var distant = Build(helperAltruism: 100, helperWallet: 5000, helperIsKin: false, affection: 20);
        Assert.DoesNotContain(RunDays(distant, days: 60), e => e.Kind == WorldEventKind.AidGiven);

        var friend = Build(helperAltruism: 100, helperWallet: 5000, helperIsKin: false, affection: 90);
        Assert.Contains(RunDays(friend, days: 60), e => e.Kind == WorldEventKind.AidGiven);
    }

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }
}
