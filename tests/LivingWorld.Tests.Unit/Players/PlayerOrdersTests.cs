using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>AD-197d: ordens contra o que a pessoa valoriza (vender a casa, abandonar o filho dependente) pela mesma disputa de vontade
/// do golpe: o peso sai dos vínculos e dos valores dela; a posse plena nunca recusa.</summary>
public class PlayerOrdersTests
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, string> Born = new();

    /// <summary>A vila da semente depois de nascerem filhos (a população semeada não tem parentesco): roda de meio em meio ano até
    /// haver quem more com um filho dependente; o mundo fica guardado (snapshot) e cada braço parte dele.</summary>
    internal static Arm Family(ulong seed)
    {
        string snapshot = Born.GetOrAdd(seed, s =>
        {
            var arm = Village(s);
            for (int i = 0; i < 12 && !arm.World.Npcs.Any(n => n.IsAlive && arm.World.Npcs.Any(c => PlayerOrders.DependentChild(arm.World, n, c))); i++)
                arm.Clock.Run(arm.World, arm.World.Calendar.HoursPerYear / 2);
            return WorldSnapshot.Serialize(arm.World);
        });
        var world = WorldSnapshot.Deserialize(snapshot);
        var sink = new AllEvents();
        return new Arm(world, new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink), sink,
            new LivingWorld.Simulation.Scheduling.TickContext(world, world.Rng, world.Scheduler, sink));
    }

    /// <summary>Um adulto que mora com um filho dependente.</summary>
    internal static Npc Parent(WorldState w) =>
        w.Npcs.Where(n => n.IsAlive && n.Household is not null
                && w.LifeStageRules.LifeStageOf(n.AgeYears(w.CurrentDate)) == LifeStage.Adult
                && w.Npcs.Any(c => PlayerOrders.DependentChild(w, n, c)))
            .OrderBy(n => n.Id.Value).First();

    internal static Npc ChildOf(WorldState w, Npc parent) =>
        w.Npcs.Where(c => PlayerOrders.DependentChild(w, parent, c)).OrderBy(c => c.Id.Value).First();

    /// <summary>Vontade média; muito apegada (lealdade, laço com a casa, aversão a risco) ou pouco.</summary>
    internal static void Temper(WorldState w, Npc npc, Npc child, bool attached)
    {
        npc.ImprintPersonality(npc.Personality with
        {
            EmotionalStability = 70,
            Conscientiousness = 70,
            Impulsivity = 30,
            Loyalty = attached ? 95 : 5,
            Altruism = attached ? 60 : 5,
            RiskAversion = attached ? 90 : 5,
        });
        npc.SetHealth(100);
        long now = w.CurrentDate.TotalHours;
        double tie = attached ? 100 : 0;
        foreach (var member in w.FindHousehold(npc.Household!.Value)!.Members.Where(m => m != npc.Id).Append(child.Id).Distinct())
            w.GetOrCreateRelationship(new RelationshipKey(npc.Id, member), now).CopyAxesFrom(Relationship.FromAxes(tie, tie, 50, 0, now));
    }

    private static (Arm Arm, Npc Npc, Npc Child, PlayerService Service) Scene(ulong seed, bool attached, PossessionMode mode)
    {
        var arm = Family(seed);
        var w = arm.World;
        var npc = Parent(w);
        var child = ChildOf(w, npc);
        Temper(w, npc, child, attached);
        var home = w.FindHousehold(npc.Household!.Value)!;
        PlayerControlTests.Place(w, npc, home.Location);
        PlayerControlTests.Place(w, child, home.Location); // o filho em casa, ao alcance
        // quem compra: uma casa da cidade sem teto, com dinheiro
        var buyer = w.Households.Where(h => h.Id != home.Id && h.City == home.City && w.FindNpc(h.Head) is { IsAlive: true })
            .OrderBy(h => h.Id.Value).First();
        buyer.Evict(w.CurrentDate.TotalHours);
        w.FindNpc(buyer.Head)!.CreditWallet(new Money(2_000));
        var service = new PlayerService(arm.Sink);
        Assert.True(service.Incarnate(w, npc.Id, "alan", mode).IsSuccess);
        return (arm, npc, child, service);
    }

    private static bool Abandons(ulong seed, bool attached, PossessionMode mode = PossessionMode.Common)
    {
        var (arm, npc, child, service) = Scene(seed, attached, mode);
        string hash = WorldSnapshot.CanonicalHash(arm.World);
        var result = service.Interact(arm.World, "alan", "person", child.Id.Value, "abandon");
        if (!result.IsSuccess)
        {
            Assert.StartsWith(PlayerCommands.Refused, result.Error);
            Assert.Equal(hash, WorldSnapshot.CanonicalHash(arm.World)); // a recusa não muda o mundo
            return false;
        }
        Assert.NotEqual(child.Household, npc.Household);
        // AD-197j: quem abandona vai para outro povoado ou fica sem teto, nunca para uma "casa nova" na mesma célula
        var gone = arm.World.FindHousehold(npc.Household!.Value)!;
        Assert.True(gone.IsHomeless || gone.PendingRelocationCity is not null);
        Assert.Contains(arm.Sink.Events, e => e.Kind == WorldEventKind.ChildAbandoned);
        return true;
    }

    private static bool Sells(ulong seed, bool attached, PossessionMode mode = PossessionMode.Common)
    {
        var (arm, npc, _, service) = Scene(seed, attached, mode);
        var w = arm.World;
        var home = w.FindHousehold(npc.Household!.Value)!;
        var house = home.Dwelling!.Value;
        long wallet = npc.Wallet.Amount;
        string hash = WorldSnapshot.CanonicalHash(w);
        var result = service.Interact(w, "alan", "house", house.Value, "sell");
        if (!result.IsSuccess)
        {
            Assert.StartsWith(PlayerCommands.Refused, result.Error);
            Assert.Equal(hash, WorldSnapshot.CanonicalHash(w));
            return false;
        }
        Assert.Null(home.Dwelling);
        Assert.True(home.IsHomeless);
        Assert.Contains(w.Households, h => h.Id != home.Id && h.Dwelling == house);
        Assert.True(npc.Wallet.Amount > wallet);
        Assert.Contains(arm.Sink.Events, e => e.Kind == WorldEventKind.HouseSold);
        return true;
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void A_parent_very_attached_to_the_child_refuses_to_abandon_it_and_a_detached_one_obeys_in_at_least_18_of_20_seeds()
    {
        int wins = 0;
        for (ulong seed = 1; seed <= 20; seed++)
            if (!Abandons(seed, attached: true) && Abandons(seed, attached: false)) wins++;
        Assert.True(wins >= 18, $"{wins}/20");
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void A_person_attached_to_home_and_family_refuses_to_sell_the_house_and_a_detached_one_sells_in_at_least_18_of_20_seeds()
    {
        int wins = 0;
        for (ulong seed = 1; seed <= 20; seed++)
            if (!Sells(seed, attached: true) && Sells(seed, attached: false)) wins++;
        Assert.True(wins >= 18, $"{wins}/20");
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void Under_full_possession_she_never_refuses_and_keeps_the_objection()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            Assert.True(Abandons(seed, attached: true, PossessionMode.Full), $"abandono, seed {seed}");
            Assert.True(Sells(seed, attached: true, PossessionMode.Full), $"venda, seed {seed}");
        }
        var (arm, npc, child, service) = Scene(1, attached: true, PossessionMode.Full);
        Assert.True(service.Interact(arm.World, "alan", "person", child.Id.Value, "abandon").IsSuccess);
        Assert.Equal(child.Id.Value, npc.Captive!.LastObjectionTarget);
    }

    [Fact]
    public void The_weight_comes_from_what_the_order_does_to_bonds_and_values_not_from_the_name_of_the_action()
    {
        var (arm, npc, child, _) = Scene(2, attached: true, PossessionMode.Common);
        var w = arm.World;
        var home = w.FindHousehold(npc.Household!.Value)!;
        double abandon = PossessionContest.OrderWeight(w, npc, PlayerOrders.AbandonHarm(child));
        double strike = PossessionContest.HarmWeight(w, npc, child);
        double sale = PossessionContest.OrderWeight(w, npc, PlayerOrders.SaleHarm(w, npc, home));

        Assert.Equal(strike, abandon); // ferir o filho com as mãos ou deixá-lo: o mesmo dano inteiro nele
        Assert.Equal(PossessionWill.CareFor(w, npc, child) * w.Players.Possession.HomeLoss, sale, 6);
        Assert.Equal(npc.Personality.RiskAversion * w.Players.Possession.HomeValue,
            PossessionContest.OrderWeight(w, npc, new OrderHarm([], LosesHome: true)), 6); // sozinha, pesa a casa
    }
}
