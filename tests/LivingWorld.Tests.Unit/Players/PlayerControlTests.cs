using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>AD-197d: o passo por intenção no chão do motor (parede, porta, gente), a interação com o que está perto, oferecida e
/// resolvida no motor, e o registro que reproduz o mundo.</summary>
public class PlayerControlTests
{
    private static readonly CellCoord[] Around = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(-1, -1), new(1, -1), new(-1, 1)];

    /// <summary>Encarna um adulto e o deixa parado, à toa, fora de prédio, em <paramref name="at"/>.</summary>
    internal static (Arm Arm, Npc Npc, PlayerService Service) Body(ulong seed, Func<WorldState, Npc>? pick = null)
    {
        var arm = Village(seed);
        var npc = (pick ?? Pick)(arm.World);
        var service = new PlayerService(arm.Sink);
        Assert.True(service.Incarnate(arm.World, npc.Id, "alan").IsSuccess);
        return (arm, npc, service);
    }

    internal static void Place(WorldState w, Npc npc, CellCoord at)
    {
        npc.SetCurrentAction(ActionType.Idle, w.CurrentDate.TotalHours);
        npc.ExitBuilding();
        npc.MoveTo(at, w.CurrentDate.TotalHours);
    }

    /// <summary>Uma casa posicionada e concluída cuja soleira é chão aberto, sem ninguém.</summary>
    private static (Building House, CellCoord Door, CellCoord Doorstep) Door(WorldState w, Npc self) =>
        w.Buildings.Where(b => b.Position is not null && b.CompletedAtTick <= w.CurrentDate.TotalHours).OrderBy(b => b.Id.Value)
            .Select(b => (b, PlayerGround.DoorOf(b)!.Value))
            .Where(x => PlayerGround.At(w, x.Item2.Doorstep) is null && w.Map.TryGetCell(x.Item2.Doorstep, out _)
                && PlayerGround.PersonAt(w, self, x.Item2.Doorstep) is null && PlayerGround.PersonAt(w, self, x.Item2.Door) is null)
            .Select(x => (x.b, x.Item2.Door, x.Item2.Doorstep)).First();

    [Fact]
    public void A_move_intent_reaches_the_requested_cell_and_goes_in_and_out_only_through_the_door()
    {
        var (arm, npc, service) = Body(1);
        var w = arm.World;
        var (_, door, doorstep) = Door(w, npc);
        Place(w, npc, doorstep);

        Assert.True(service.Move(w, "alan", door.X - doorstep.X, door.Y - doorstep.Y).IsSuccess);
        Assert.Equal(door, npc.CurrentLocation);
        Assert.True(service.Move(w, "alan", doorstep.X - door.X, doorstep.Y - door.Y).IsSuccess);
        Assert.Equal(doorstep, npc.CurrentLocation);

        var open = Around.Select(d => new CellCoord(doorstep.X + d.X, doorstep.Y + d.Y))
            .First(c => PlayerGround.At(w, c) is null && w.Map.TryGetCell(c, out _) && PlayerGround.PersonAt(w, npc, c) is null
                && PlayerGround.Step(w, npc, doorstep, c).IsSuccess);
        Assert.True(service.Move(w, "alan", open.X - doorstep.X, open.Y - doorstep.Y).IsSuccess);
        Assert.Equal(open, npc.CurrentLocation);
        Assert.Equal(ActionType.Idle, npc.ControlledAction); // andar é tomar o rumo: o motor não o leva de volta para a rota
    }

    [Fact]
    public void A_move_never_goes_through_a_wall_nor_a_person_and_the_refusal_leaves_the_world_as_it_was()
    {
        int walls = 0, people = 0;
        for (ulong seed = 1; seed <= 5; seed++)
        {
            var (arm, npc, service) = Body(seed);
            var w = arm.World;
            var (house, _, _) = Door(w, npc);
            var origin = house.Position!.Value;
            var wall = BuildingFootprintGenerator.Generate(house).Where(c => !InteriorWalkability.IsWalkable(c.Material))
                .Select(c => new CellCoord(origin.X + c.Cell.X, origin.Y + c.Cell.Y))
                .SelectMany(c => Around.Take(4).Select(d => (Wall: c, Out: new CellCoord(c.X + d.X, c.Y + d.Y))))
                .First(p => PlayerGround.At(w, p.Out) is null && w.Map.TryGetCell(p.Out, out _) && PlayerGround.PersonAt(w, npc, p.Out) is null);
            Place(w, npc, wall.Out);
            string hash = WorldSnapshot.CanonicalHash(w);
            if (!service.Move(w, "alan", wall.Wall.X - wall.Out.X, wall.Wall.Y - wall.Out.Y).IsSuccess
                && npc.CurrentLocation == wall.Out && hash == WorldSnapshot.CanonicalHash(w))
                walls++;

            var free = Around.Select(d => new CellCoord(wall.Out.X + d.X, wall.Out.Y + d.Y))
                .First(c => PlayerGround.Step(w, npc, wall.Out, c).IsSuccess);
            var stranger = w.Npcs.First(n => n.IsAlive && n.Id != npc.Id && n.City == npc.City && n.CurrentAction != ActionType.Travel);
            stranger.ExitBuilding();
            stranger.MoveTo(free, w.CurrentDate.TotalHours);
            hash = WorldSnapshot.CanonicalHash(w);
            var blocked = service.Move(w, "alan", free.X - wall.Out.X, free.Y - wall.Out.Y);
            if (!blocked.IsSuccess && blocked.Error!.Contains(stranger.Name, StringComparison.Ordinal) && hash == WorldSnapshot.CanonicalHash(w))
                people++;
        }
        Assert.Equal((5, 5), (walls, people));
    }

    [Fact]
    public void The_offer_comes_from_the_engine_and_what_is_not_offered_is_refused_without_changing_the_world()
    {
        var (arm, npc, service) = Body(2);
        var w = arm.World;
        var home = w.FindHousehold(npc.Household!.Value)!;
        Place(w, npc, new CellCoord(-50, -50)); // longe de tudo
        Assert.Empty(PlayerService.Reach(w, "alan")!);
        string hash = WorldSnapshot.CanonicalHash(w);
        Assert.False(service.Interact(w, "alan", "stock", home.Id.Value, "take").IsSuccess);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(w));

        Place(w, npc, home.Location);
        home.Deposit(new ResourceType(w.EconomyRules.FoodResourceId), 5);
        var kinds = PlayerService.Reach(w, "alan")!.Select(o => o.Kind).ToHashSet();
        Assert.Contains("stock", kinds);
        Assert.Contains("bed", kinds);
        Assert.Contains("house", kinds);
        Assert.Contains("person", kinds); // a família em casa
    }

    [Fact]
    public void Taking_from_the_pantry_and_depositing_moves_the_real_stock_through_the_hands()
    {
        var (arm, npc, service) = Body(3);
        var w = arm.World;
        var home = w.FindHousehold(npc.Household!.Value)!;
        var food = new ResourceType(w.EconomyRules.FoodResourceId);
        home.Deposit(food, 6);
        Place(w, npc, home.Location);
        long before = home.Stock.GetValueOrDefault(food);

        Assert.True(service.Interact(w, "alan", "stock", home.Id.Value, "take", food.Id, 4).IsSuccess);
        Assert.Equal((food.Id, 4L), (npc.CarriedResourceId, npc.CarriedQuantity));
        Assert.Equal(before - 4, home.Stock.GetValueOrDefault(food));
        Assert.True(service.Interact(w, "alan", "stock", home.Id.Value, "deposit").IsSuccess);
        Assert.False(npc.IsCarrying);
        Assert.Equal(before, home.Stock.GetValueOrDefault(food));
    }

    [Fact]
    public void Buying_and_selling_at_a_stall_moves_money_and_stock_through_the_real_market()
    {
        var (arm, npc, service) = Body(4);
        var w = arm.World;
        var stall = w.Workplaces.Where(p => p.Prices.Any(x => x.Value > 0 && p.Stock.GetValueOrDefault(x.Key) > 0)).OrderBy(p => p.Id.Value).First();
        var (resource, unit) = stall.Prices.Where(x => x.Value > 0 && stall.Stock.GetValueOrDefault(x.Key) > 0).OrderBy(x => x.Key.Id).First();
        var home = w.FindHousehold(npc.Household!.Value)!;
        npc.CreditWallet(new Money(unit * 10));
        Place(w, npc, stall.Location);
        long wallet = npc.Wallet.Amount, treasury = stall.Treasury.Amount, stock = stall.Stock.GetValueOrDefault(resource);
        long pantry = home.Stock.GetValueOrDefault(resource);

        var bought = service.Interact(w, "alan", "stall", stall.Id.Value, "buy", resource.Id, 1);
        Assert.True(bought.IsSuccess, bought.Error);
        Assert.Equal((wallet - unit, treasury + unit, stock - 1, pantry + 1),
            (npc.Wallet.Amount, stall.Treasury.Amount, stall.Stock.GetValueOrDefault(resource), home.Stock.GetValueOrDefault(resource)));

        Assert.True(npc.PickUp(resource, 2).IsSuccess);
        stall.CreditTreasury(new Money(unit * 5));
        wallet = npc.Wallet.Amount;
        treasury = stall.Treasury.Amount;
        var sold = service.Interact(w, "alan", "stall", stall.Id.Value, "sell");
        Assert.True(sold.IsSuccess, sold.Error);
        Assert.Equal((wallet + 2 * unit, treasury - 2 * unit, false), (npc.Wallet.Amount, stall.Treasury.Amount, npc.IsCarrying));
    }

    [Fact]
    public void The_door_bed_and_talk_interactions_change_the_engine()
    {
        var (arm, npc, service) = Body(5);
        var w = arm.World;
        var (house, door, doorstep) = Door(w, npc);
        Place(w, npc, doorstep);
        Assert.True(service.Interact(w, "alan", "door", house.Id.Value, "enter").IsSuccess);
        Assert.Equal(door, npc.CurrentLocation);
        Assert.True(service.Interact(w, "alan", "door", house.Id.Value, "exit").IsSuccess);
        Assert.Equal(doorstep, npc.CurrentLocation);

        var home = w.FindHousehold(npc.Household!.Value)!;
        Place(w, npc, home.Location);
        Assert.True(service.Interact(w, "alan", "bed", home.Id.Value, "sleep").IsSuccess);
        Assert.Equal(ActionType.Sleep, npc.ControlledAction);

        var other = w.Npcs.First(n => n.IsAlive && n.Id != npc.Id && n.City == npc.City && n.Household != npc.Household
            && !w.Relationships.ContainsKey(new RelationshipKey(n.Id, npc.Id)));
        other.SetCurrentAction(ActionType.Idle, w.CurrentDate.TotalHours);
        other.ExitBuilding();
        other.MoveTo(npc.CurrentLocation, w.CurrentDate.TotalHours);
        var talked = service.Interact(w, "alan", "person", other.Id.Value, "talk");
        Assert.True(talked.IsSuccess, talked.Error);
        Assert.True(w.Relationships.ContainsKey(new RelationshipKey(other.Id, npc.Id)));
        Assert.True(w.Relationships.ContainsKey(new RelationshipKey(npc.Id, other.Id)));
    }

    [Fact]
    public void A_log_with_steps_interactions_and_a_refused_order_replays_to_the_same_hash()
    {
        var arm = PlayerOrdersTests.Family(6);
        var w = arm.World;
        var npc = PlayerOrdersTests.Parent(w);
        var child = PlayerOrdersTests.ChildOf(w, npc);
        PlayerOrdersTests.Temper(w, npc, child, attached: true);
        var home = w.FindHousehold(npc.Household!.Value)!;
        Place(w, npc, home.Location);
        home.Deposit(new ResourceType(w.EconomyRules.FoodResourceId), 3);
        string snapshot = WorldSnapshot.Serialize(w);
        var service = new PlayerService(arm.Sink);
        Assert.True(service.Incarnate(w, npc.Id, "alan").IsSuccess);
        Assert.False(service.Interact(w, "alan", "person", child.Id.Value, "abandon").IsSuccess); // recusa: vai ao registro
        Assert.True(service.Interact(w, "alan", "stock", home.Id.Value, "take").IsSuccess);
        arm.Clock.Run(w, 1);
        foreach (var d in Around)
            if (service.Move(w, "alan", d.X, d.Y).IsSuccess) break;
        arm.Clock.Run(w, 3);
        long end = w.CurrentDate.TotalMilliseconds;

        Assert.Contains(service.Log.Records, r => r.Refused && r.Kind == PlayerCommandKind.Interact);
        Assert.Contains(service.Log.Records, r => !r.Refused && r.Kind == PlayerCommandKind.Interact);
        var replayed = PlayerReplay.Run(snapshot, service.Log.Records,
            new WorldClock(ScenarioRunner.DefaultSystems(), sink: new BufferingWorldEventSink()), new BufferingWorldEventSink(), end);
        Assert.Equal(WorldSnapshot.CanonicalHash(w), WorldSnapshot.CanonicalHash(replayed));
    }
}
