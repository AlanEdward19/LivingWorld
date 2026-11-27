using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Emergence;
using LivingWorld.Simulation.Invention;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Invention;

/// <summary>AD-157: a carga é de quem a leva (backlog 05 item 12c): o recipiente sai da casa com a pessoa e volta com ela; quem
/// ganha a refeição no trabalho leva só para o caminho (08 item 67); quem fica em casa come primeiro.</summary>
public class PersonalCarryOwnershipTests
{
    private static readonly InventionRules Rules = InventionRules.Default with { PantryNeedsPresence = true };

    private static WorldState NewWorld() => ScenarioRunner.Create(1, costWeights: ScenarioRunner.RegionalCostWeights, inventionRules: Rules).World;

    private static Household HomeOf(WorldState world, Npc npc) => world.FindHousehold(npc.Household!.Value)!;

    private static List<Npc> Adults(WorldState world) => world.Npcs
        .Where(n => n.IsAlive && n.Household is not null && n.AgeYears(world.CurrentDate) >= 20).OrderBy(n => n.Id.Value).ToList();

    private static List<Npc> Others(WorldState world, Npc npc) => HomeOf(world, npc).Members
        .Where(id => id != npc.Id).Select(id => world.FindNpc(id)!).Where(n => n.IsAlive).ToList();

    private static void GiveContainer(WorldState world, Npc owner)
    {
        var ledger = world.Invention!;
        var m = Rules.Materials.First(x => x.Formable && !x.CoatingOnly);
        var design = Design.Create([CarryInvention.NeedSized(m, 8000)!.Value], owner.Id.Value, 1, 0, 0, DesignOrigin.Invented);
        ledger.AddDesign(design);
        ledger.MarkCarryDesign(design.Id);
        ledger.Items.Add(new CraftedItem(ledger.NextItemId++, design.Id, owner.Household!.Value.Value, owner.City, 1, long.MaxValue, 1000));
        ledger.Touch();
    }

    private static void Stock(Household home, WorldState world, long water, long food)
    {
        if (water > 0) home.Deposit(new ResourceType(world.EconomyRules.WaterResourceId), water);
        if (food > 0) home.Deposit(new ResourceType(world.EconomyRules.FoodResourceId), food);
    }

    [Fact]
    public void The_one_who_leaves_with_the_container_takes_it_and_the_next_one_out_goes_bare_handed_until_it_comes_back()
    {
        var world = NewWorld();
        var first = Adults(world).First(a => Others(world, a).Any(o => o.AgeYears(world.CurrentDate) >= 16));
        var home = HomeOf(world, first);
        var second = Others(world, first).First(o => o.AgeYears(world.CurrentDate) >= 16);
        GiveContainer(world, first);
        Stock(home, world, 50, 50);
        long now = world.CurrentDate.TotalHours;
        foreach (var n in new[] { first, second }) { n.SetThirst(40, now); n.SetHunger(40, now); }
        var bare = new PersonalCarry.Room(Rules.FootCarryKg * 1000L, Rules.HandsVolumeMl, 0);

        Assert.True(PersonalCarry.RoomOf(world, second).ItemId != 0); // em casa, os dois veem o recipiente
        PersonalCarry.PackForTrip(world, first, home, 30, now);
        var secondRoom = PersonalCarry.RoomOf(world, second);
        long held = world.Invention!.HeldBy(first.Id.Value);

        Assert.True(held != 0 && secondRoom == bare && PersonalCarry.RoomOf(world, first).ItemId == held, $"levou {held}, o outro {secondRoom}");
        PersonalCarry.Unpack(world, first, home);
        Assert.True(world.Invention.HeldBy(first.Id.Value) == 0 && PersonalCarry.RoomOf(world, second).ItemId == held);
    }

    [Fact]
    public void Leaving_with_little_in_the_pantry_keeps_first_what_those_who_stay_will_eat()
    {
        var world = NewWorld();
        var npc = Adults(world).First(a => Others(world, a).Count >= 2);
        var home = HomeOf(world, npc);
        GiveContainer(world, npc);
        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        if (home.Stock.GetValueOrDefault(water) > 0) home.Withdraw(water, home.Stock.GetValueOrDefault(water));
        long now = world.CurrentDate.TotalHours;
        var others = Others(world, npc);
        foreach (var o in others) o.SetThirst(5, now); // com sede em casa: em 47 h cada um bebe três vezes
        npc.SetThirst(40, now);
        // antes ficava um por morador por dia (47 h = 2 dias) e sobrava para levar; agora fica o que eles vão beber na ausência
        Stock(home, world, 3L * others.Count, 0);

        PersonalCarry.PackForTrip(world, npc, home, 47, now);

        Assert.True(home.Stock.GetValueOrDefault(water) >= 3L * others.Count,
            $"ficou {home.Stock.GetValueOrDefault(water)} para {others.Count}, levou {world.Invention!.CarriedQuantity(npc.Id.Value, water.Id)}");
    }

    [Fact]
    public void Going_to_a_workplace_that_feeds_its_workers_packs_food_only_for_the_way()
    {
        var (world, clock) = ScenarioRunner.Create(1, costWeights: ScenarioRunner.RegionalCostWeights, inventionRules: Rules);
        clock.Run(world, 2 * world.Calendar.HoursPerDay); // no tick 0 ninguém está contratado
        var npc = Adults(world).First(a => a.Employer is { } e && world.FindWorkplace(e) is not null
            && LivingWorld.Simulation.Economy.Labor.Payroll.BoardCharge(world, world.FindWorkplace(e)!, a, new ResourceType(world.EconomyRules.FoodResourceId)) >= 0);
        world.Invention!.Carried.Clear();
        world.Invention.Held = null;
        var home = HomeOf(world, npc);
        var work = world.FindWorkplace(npc.Employer!.Value)!;
        // a casa a mais de uma hora do trabalho (na seed 1 todos moram perto)
        home.JoinCity(home.City, world.Map.Cells.Where(c => !c.HasWater).Select(c => c.Coord).OrderBy(c => c.Y).ThenBy(c => c.X)
            // AD-180: "longe" é a distância a pé sem o chão do dia (a lama alonga a ida, não muda o que é perto)
            .Where(c => Mobility.TravelTicks(world, npc, c, work.Location, 1.0, weather: false) > 1)
            .MinBy(c => Mobility.TravelTicks(world, npc, c, work.Location, 1.0, weather: false)));
        Assert.True(PersonalCarry.LeavesPantryBehind(world, npc, home, work.Location));
        Stock(home, world, 50, 50);
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        long now = world.CurrentDate.TotalHours;
        npc.MoveTo(home.Location, now);
        npc.SetCurrentAction(ActionType.Travel, now);
        npc.SetTravelPurpose(ActionType.Work);
        var index = MarketIndex.BuildForTick(world);
        long leg = Mobility.TravelTicks(world, npc, home.Location, work.Location, 1.0);
        long shift = LivingWorld.Simulation.Objects.ObjectUse.DurationHours(world, npc, ActionType.Work, world.ActionCatalog);
        // a fome chega à linha no meio do turno: só o caminho e a folga não pedem comida; o turno inteiro pede
        double decay = world.NeedsRules.HungerDecayPerHour;
        npc.SetHunger((int)Math.Min(100, 100 - world.NeedsRules.UrgencyThreshold + decay * (2 * leg + Rules.PackMarginHours + shift / 2.0)), now);
        npc.SetThirst(100, now);

        if (work.Stock.GetValueOrDefault(food) > 0) work.Withdraw(food, work.Stock.GetValueOrDefault(food)); // sem comida: leva para o turno
        BehaviorDecisionSystem.PackOnDeparture(world, npc, index, now);
        int hungryShift = world.Invention!.CarriedQuantity(npc.Id.Value, food.Id);
        PersonalCarry.Unpack(world, npc, home);
        work.Deposit(food, 50, world.EconomyRules); // a oficina dá a refeição
        BehaviorDecisionSystem.PackOnDeparture(world, npc, index, now);
        int fedShift = world.Invention.CarriedQuantity(npc.Id.Value, food.Id);
        int forTheWay = PersonalCarry.UnitsFor(npc.HungerAt(now), world.NeedsRules.HungerDecayPerHour, 2 * leg + Rules.PackMarginHours,
            100 - world.NeedsRules.UrgencyThreshold, 100);

        Assert.True(4 * leg + shift <= 46, $"ida {leg} h e turno {shift} h longos demais para o teste");
        Assert.True(hungryShift > 0 && fedShift < hungryShift && fedShift <= forTheWay,
            $"sem refeição {hungryShift}, com refeição {fedShift}, caminho {forTheWay} (ida {leg} h)");
    }

    [Fact]
    public void The_container_someone_took_survives_the_snapshot_and_an_old_snapshot_opens_with_it_at_home()
    {
        var world = NewWorld();
        var npc = Adults(world)[0];
        GiveContainer(world, npc);
        long item = world.Invention!.Items[^1].Id;
        world.Invention.Hold(npc.Id.Value, item);

        var back = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.Equal(item, back.Invention!.HeldBy(npc.Id.Value));
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(back));

        var old = System.Text.Json.Nodes.JsonNode.Parse(WorldSnapshot.Serialize(world))!.AsObject();
        old["Invention"]!.AsObject().Remove("Held");
        Assert.Equal(0, WorldSnapshot.Deserialize(old.ToJsonString()).Invention!.HeldBy(npc.Id.Value));
    }

    [Fact]
    public void Nobody_holds_anything_when_nobody_packs_so_the_snapshot_gains_no_new_field()
    {
        var world = NewWorld();
        Assert.DoesNotContain("\"Held\"", WorldSnapshot.Serialize(world), StringComparison.Ordinal);
    }

    /// <summary>AD-157: a memória do alcance não vale mais só na hora; vale até a casa mudar de lugar (ou as peças, ou a tecnologia).</summary>
    [Fact]
    public void The_remembered_reach_of_the_pantry_follows_the_house_when_it_moves()
    {
        var world = NewWorld();
        var npc = Adults(world).First(a => a.Employer is null);
        var home = HomeOf(world, npc);
        var cells = world.Map.Cells.Where(c => !c.HasWater).Select(c => c.Coord).OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
        var far = cells.First(c => Mobility.TravelTicks(world, npc, c, home.Location, 1.0) > 3);
        npc.MoveTo(far, world.CurrentDate.TotalHours);
        Assert.True(PersonalCarry.PantryOutOfReach(world, npc, home));

        home.JoinCity(home.City, cells.First(c => c != far && Mobility.TravelTicks(world, npc, far, c, 1.0) <= 1));

        Assert.False(PersonalCarry.PantryOutOfReach(world, npc, home));
    }

    /// <summary>AD-157: a medida da aflição conta a fome crítica de quem está longe de casa por pessoa-ano (só lê o mundo).</summary>
    [Fact]
    public void The_distress_measure_counts_critical_hunger_away_from_home()
    {
        var world = NewWorld();
        var npc = Adults(world)[0];
        var home = HomeOf(world, npc);
        var tally = new LivingWorld.Simulation.Observation.NeedDistressTally(world);
        npc.MoveTo(world.Map.Cells.Where(c => !c.HasWater).Select(c => c.Coord).First(c => c != home.Location), world.CurrentDate.TotalHours);
        npc.SetHunger(5, world.CurrentDate.TotalHours);

        tally.SampleHour();

        Assert.True(tally.HungerCrit >= 1 && tally.HungerCritAway >= 1 && tally.PersonHours == world.AliveNpcIndex.Alive.Count);
    }
}
