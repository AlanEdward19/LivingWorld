using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Invention;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;
using static LivingWorld.Tests.Unit.Players.PlayerControlTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>AD-197n: os recipientes e veículos inventados pela casa (designs da invenção) entram na oferta de interações; levar um é o que
/// o NPC faz ao sair com ele, e o motor aplica o efeito real (a carga cabe no recipiente; o veículo enche o barril na fonte).</summary>
public class PlayerItemsTests
{
    /// <summary>Um oco do volume pedido (o de PersonalCarryTests), feito pela casa do corpo.</summary>
    private static long GiveContainer(WorldState world, Npc npc)
    {
        var rules = world.InventionRules!;
        var material = rules.Materials.Where(m => !m.CoatingOnly && m.ImpermeabilityPm >= rules.WatertightPm).OrderBy(m => m.Id).First();
        var body = CarryInvention.NeedSized(material, 6000)!.Value;
        return Give(world, npc, [body], carry: true);
    }

    /// <summary>Um carrinho (o de LandHaulWorldTests): tabuleiro de madeira e duas rodas por eixo.</summary>
    private static long GiveCart(WorldState world, Npc npc)
    {
        var wood = world.InventionRules!.Materials.First(m => !m.CoatingOnly && m.NaturalShape == PartShape.Cylinder && m.TensileMPa >= 30);
        var deck = new DesignPart(PartShape.Box, 1000, 600, 40, 0, (short)wood.Id, 0, JointKind.Fixed, -1, 0, 0, 0);
        var wheel = new DesignPart(PartShape.Cylinder, 60, 300, 300, 0, (short)wood.Id, 0, JointKind.Axle, 0, 470, 0, -300);
        return Give(world, npc, [deck, wheel, wheel with { OffsetXMm = -470 }], carry: false);
    }

    private static long Give(WorldState world, Npc npc, IReadOnlyList<DesignPart> parts, bool carry)
    {
        var ledger = world.Invention!;
        var design = Design.Create(parts, npc.Id.Value, 1, 0, 0, DesignOrigin.Invented);
        ledger.AddDesign(design);
        if (carry) ledger.MarkCarryDesign(design.Id);
        else ledger.MarkHaulDesign(design.Id);
        ledger.Learn(new KnownDesign(npc.Id.Value, design.Id, 1000, DesignOrigin.Invented, 1));
        long id = ledger.NextItemId++;
        ledger.Items.Add(new CraftedItem(id, design.Id, npc.Household!.Value.Value, npc.City, 1, long.MaxValue, 1000));
        ledger.Touch();
        return id;
    }

    [Fact]
    public void The_invented_container_is_offered_at_home_and_taking_it_makes_the_load_fit_in_it()
    {
        var (arm, npc, service) = Body(1);
        var w = arm.World;
        var home = w.FindHousehold(npc.Household!.Value)!;
        Place(w, npc, home.Location);
        var bare = PersonalCarry.RoomOf(w, npc);
        long item = GiveContainer(w, npc);
        var offer = PlayerService.Reach(w, "alan")!.Single(o => o.Kind == "item" && o.Id == item);
        Assert.Equal(["take"], offer.Verbs);

        Assert.True(service.Interact(w, "alan", "item", item, "take").IsSuccess);
        Assert.Equal(item, w.Invention!.HeldBy(npc.Id.Value));
        var room = PersonalCarry.RoomOf(w, npc);
        Assert.Equal(item, room.ItemId);
        Assert.True(room.WetMl > bare.WetMl || room.DryMl > bare.DryMl, "a carga cabe no recipiente");
        var other = home.Members.Select(m => w.FindNpc(m)!).First(m => m.Id != npc.Id && m.IsAlive);
        w.Invention.CarrierFor(other.Id.Value, home.Id.Value, w.InventionRules!, out long otherItem);
        Assert.NotEqual(item, otherItem); // quem fica em casa não sai com ele
        Assert.Equal(["return"], PlayerService.Reach(w, "alan")!.Single(o => o.Kind == "item" && o.Id == item).Verbs);
        Assert.True(service.Interact(w, "alan", "item", item, "return").IsSuccess);
        Assert.Equal(0, w.Invention.HeldBy(npc.Id.Value));
    }

    [Fact]
    public void Pulling_the_invented_cart_to_the_source_fills_more_water_than_bare_hands()
    {
        long Filled(bool cart)
        {
            var (arm, npc, service) = Body(3);
            var w = arm.World;
            var home = w.FindHousehold(npc.Household!.Value)!;
            var water = new LivingWorld.Domain.Geography.ResourceType(w.EconomyRules.WaterResourceId);
            home.Withdraw(water, home.Stock.GetValueOrDefault(water));
            long item = GiveCart(w, npc);
            Place(w, npc, home.Location);
            if (cart)
            {
                Assert.True(service.Interact(w, "alan", "item", item, "take").IsSuccess);
                Assert.True(w.Invention!.HaulerHeldBy(npc.Id.Value, w.InventionRules!).Exists, "o carrinho funciona");
            }
            else w.Invention!.Items.RemoveAll(i => i.Id == item); // sem o carrinho na casa
            w.Invention!.Touch();
            var shore = w.Map.Cells.Where(c => c.HasWater).Select(c => c.Coord)
                .OrderBy(c => Math.Abs(c.X - home.Location.X) + Math.Abs(c.Y - home.Location.Y))
                .SelectMany(c => new[] { new CellCoord(c.X + 1, c.Y), new CellCoord(c.X - 1, c.Y), new CellCoord(c.X, c.Y + 1), new CellCoord(c.X, c.Y - 1) })
                .First(c => w.Map.TryGetCell(c, out var m) && !m.HasWater && PlayerGround.At(w, c) is null
                    && LivingWorld.Simulation.Cities.Streets.StreetRoutes.CartFits(w, home.Location, c)); // o carrinho passa pelo caminho
            Place(w, npc, shore);
            var source = PlayerService.Reach(w, "alan")!.Single(o => o.Kind == "water");
            Assert.True(service.Interact(w, "alan", "water", source.Id, "fill").IsSuccess);
            return w.Invention.CarriedQuantity(npc.Id.Value, water.Id);
        }

        long with = Filled(cart: true), without = Filled(cart: false);
        Assert.True(with > without, $"o veículo da casa enche o barril na fonte (com {with}, sem {without})");
    }

    [Fact]
    public void A_log_with_taking_and_returning_an_invented_item_replays_to_the_same_hash()
    {
        var arm = Village(5, NoPace);
        var w = arm.World;
        var npc = Pick(w);
        var home = w.FindHousehold(npc.Household!.Value)!;
        Place(w, npc, home.Location);
        long item = GiveContainer(w, npc);
        string snapshot = WorldSnapshot.Serialize(w);
        var service = new PlayerService(arm.Sink);
        Assert.True(service.Incarnate(w, npc.Id, "alan").IsSuccess);
        Assert.True(service.Interact(w, "alan", "item", item, "take").IsSuccess);
        Assert.True(service.Act(w, "alan", LivingWorld.Domain.Behavior.ActionType.Idle).IsSuccess); // fica em casa
        arm.Clock.Run(w, 2);
        Assert.True(service.Interact(w, "alan", "item", item, "return").IsSuccess);
        arm.Clock.Run(w, 2);
        long end = w.CurrentDate.TotalMilliseconds;

        Assert.Contains(service.Log.Records, r => r.Kind == PlayerCommandKind.Interact && r.Interaction == "item");
        var replayed = PlayerReplay.Run(snapshot, service.Log.Records,
            new WorldClock(ScenarioRunner.DefaultSystems(), sink: new BufferingWorldEventSink()), new BufferingWorldEventSink(), end);
        Assert.Equal(WorldSnapshot.CanonicalHash(w), WorldSnapshot.CanonicalHash(replayed));
    }
}
