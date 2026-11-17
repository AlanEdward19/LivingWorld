using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Emergence;
using LivingWorld.Simulation.Invention;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Invention;

/// <summary>AD-157b: com a despensa em casa, a refeição é rotina e não só reação à urgência. Desjejum antes de sair, ceia antes de
/// dormir, hora da refeição em casa, pausa do turno no local, passeio ao alcance da despensa, fim do turno em casa, e a volta pelo que
/// a carga deixou na banca (08b item 77). Cada teste falha no comportamento de antes.</summary>
public class MealRoutineTests
{
    private static readonly InventionRules Rules = InventionRules.Default with { PantryNeedsPresence = true };

    private static WorldState NewWorld(InventionRules? rules = null) => ScenarioRunner.Create(1, inventionRules: rules ?? Rules).World;

    private static Household HomeOf(WorldState world, Npc npc) => world.FindHousehold(npc.Household!.Value)!;

    private static Npc Adult(WorldState world) => world.Npcs
        .Where(n => n.IsAlive && n.Household is not null && n.AgeYears(world.CurrentDate) >= 20).OrderBy(n => n.Id.Value).First();

    private static ResourceType Food(WorldState w) => new(w.EconomyRules.FoodResourceId);

    private static ResourceType Water(WorldState w) => new(w.EconomyRules.WaterResourceId);

    [Fact]
    public void Leaving_for_longer_than_the_hunger_waits_eats_at_home_what_the_hands_cannot_carry()
    {
        var world = NewWorld();
        var npc = Adult(world);
        var home = HomeOf(world, npc);
        home.Deposit(Food(world), 20);
        home.Deposit(Water(world), 20);
        long now = world.CurrentDate.TotalHours;
        npc.SetHunger(40, now);
        npc.SetThirst(40, now);

        PersonalCarry.PackForTrip(world, npc, home, 48, now); // dois dias fora: a fome chega à crítica, a sede à urgência

        // de mãos nuas não leva água: bebe antes de sair; a comida que passa da ração das mãos come em casa
        Assert.True(npc.ThirstAt(now) >= 90 && world.InventionTally.DrankBeforeLeaving == 1, $"sede {npc.ThirstAt(now)}");
        Assert.True(npc.HungerAt(now) >= 90 && world.InventionTally.AteBeforeLeaving == 1, $"fome {npc.HungerAt(now)}");
    }

    [Fact]
    public void A_short_absence_that_only_reaches_urgent_hunger_does_not_eat_ahead()
    {
        var world = NewWorld();
        var npc = Adult(world);
        var home = HomeOf(world, npc);
        home.Deposit(Food(world), 20);
        long now = world.CurrentDate.TotalHours;
        npc.SetHunger(45, now);
        npc.SetThirst(100, now);

        PersonalCarry.PackForTrip(world, npc, home, 10, now); // 45 − 2×10 = 25: urgente, não crítica

        Assert.Equal(0, world.InventionTally.AteBeforeLeaving);
        Assert.True(world.Invention!.CarriedQuantity(npc.Id.Value, Food(world).Id) == 1, "leva a ração nas mãos");
    }

    [Fact]
    public void Going_to_bed_with_a_night_longer_than_the_thirst_waits_has_supper_first()
    {
        var world = NewWorld();
        var npc = Adult(world);
        var home = HomeOf(world, npc);
        npc.MoveTo(home.Location, world.CurrentDate.TotalHours);
        home.Deposit(Food(world), 5);
        home.Deposit(Water(world), 5);
        long now = world.CurrentDate.TotalHours;
        npc.SetHunger(100, now);
        npc.SetThirst(50, now); // 8 h de noite a 3/h: passa da urgência

        Assert.True(BehaviorDecisionSystem.SupperBeforeSleep(world, npc, world.ActionCatalog, now));
        npc.SetThirst(100, now);
        Assert.False(BehaviorDecisionSystem.SupperBeforeSleep(world, npc, world.ActionCatalog, now));
    }

    [Fact]
    public void At_home_without_urgency_hunger_near_the_line_is_meal_time()
    {
        var world = NewWorld();
        var npc = Adult(world);
        var home = HomeOf(world, npc);
        npc.MoveTo(home.Location, world.CurrentDate.TotalHours);
        home.Deposit(Food(world), 5);
        long now = world.CurrentDate.TotalHours;
        npc.SetHunger(100 - PersonalCarry.MealTimeDeficit, now);

        Assert.True(BehaviorDecisionSystem.MealTimeAtHome(world, npc, ActionType.Idle, now));
        Assert.False(BehaviorDecisionSystem.MealTimeAtHome(world, npc, ActionType.Sleep, now)); // deitado, só a fome urgente levanta
        Assert.False(BehaviorDecisionSystem.MealTimeAtHome(world, npc, ActionType.Work, now));
        npc.SetHunger(100 - world.NeedsRules.UrgencyThreshold - 1, now);
        Assert.True(BehaviorDecisionSystem.MealTimeAtHome(world, npc, ActionType.Sleep, now));
        npc.SetHunger(100, now);
        Assert.False(BehaviorDecisionSystem.MealTimeAtHome(world, npc, ActionType.Idle, now));
        Assert.False(BehaviorDecisionSystem.MealTimeAtHome(NewWorld(Rules with { PantryNeedsPresence = false }), npc, ActionType.Idle, now));
    }

    [Fact]
    public void A_stroll_does_not_step_beyond_the_reach_of_the_pantry_and_one_beyond_steps_back()
    {
        var world = NewWorld();
        var npc = Adult(world);
        var home = HomeOf(world, npc);
        var cells = world.Map.Cells.Where(c => !c.HasWater).Select(c => c.Coord).OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
        var far = cells.First(c => Mobility.TravelTicks(world, npc, c, home.Location, 1.0) > 3);
        npc.MoveTo(home.Location, world.CurrentDate.TotalHours);

        Assert.Null(PersonalCarry.StrollStep(world, npc, ActionType.Idle, [far], 0));

        npc.MoveTo(far, world.CurrentDate.TotalHours);
        var closer = new CellCoord(far.X + Math.Sign(home.Location.X - far.X), far.Y + Math.Sign(home.Location.Y - far.Y));
        var away = new CellCoord(far.X - Math.Sign(home.Location.X - far.X), far.Y - Math.Sign(home.Location.Y - far.Y));
        Assert.Equal(closer, PersonalCarry.StrollStep(world, npc, ActionType.Socialize, [away, closer], 0));
        Assert.Equal(away, PersonalCarry.StrollStep(world, npc, ActionType.Work, [away, closer], 0)); // andar no trabalho fica como era
    }

    [Fact]
    public void Idle_far_from_the_pantry_after_the_shift_goes_home()
    {
        var world = NewWorld();
        var npc = Adult(world);
        var home = HomeOf(world, npc);
        var far = world.Map.Cells.Where(c => !c.HasWater).Select(c => c.Coord).OrderBy(c => c.Y).ThenBy(c => c.X)
            .First(c => Mobility.TravelTicks(world, npc, c, home.Location, 1.0) > 3);
        npc.MoveTo(far, world.CurrentDate.TotalHours);

        Assert.True(BehaviorDecisionSystem.OffShiftFarFromPantry(world, npc));
        npc.MoveTo(home.Location, world.CurrentDate.TotalHours);
        Assert.False(BehaviorDecisionSystem.OffShiftFarFromPantry(world, npc));
    }

    /// <summary>08b item 77 (AD-163): depois da compra cortada pela carga, a casa voltava só quando a despensa caía de novo
    /// (mediana 120 h contra 79 h). Agora o resto que ficou na banca leva de volta quem pode pagar, mesmo com a despensa acima da meta.</summary>
    [Fact]
    public void After_a_purchase_cut_by_the_load_the_household_goes_back_for_what_was_left()
    {
        var world = NewWorld();
        var npc = Adult(world);
        var home = HomeOf(world, npc);
        home.Deposit(Food(world), 500);
        home.Deposit(Water(world), 500);
        npc.CreditWallet(new Money(1000));
        var index = MarketIndex.BuildForTick(world);
        Assert.False(BehaviorDecisionSystem.NeedsRestocking(world, npc, index));

        world.Invention!.LeaveAtMarket(home.Id.Value, left: true);
        Assert.True(BehaviorDecisionSystem.NeedsRestocking(world, npc, index));

        world.Invention.LeaveAtMarket(home.Id.Value, left: false); // a ida seguinte trouxe tudo
        Assert.False(BehaviorDecisionSystem.NeedsRestocking(world, npc, index));
    }

    [Fact]
    public void What_was_left_at_the_market_survives_the_snapshot_and_an_old_snapshot_opens_without_it()
    {
        var world = NewWorld();
        var home = HomeOf(world, Adult(world));
        world.Invention!.LeaveAtMarket(home.Id.Value, left: true);

        var back = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.True(back.Invention!.IsLeftAtMarket(home.Id.Value));
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(back));

        var old = System.Text.Json.Nodes.JsonNode.Parse(WorldSnapshot.Serialize(world))!.AsObject();
        old["Invention"]!.AsObject().Remove("LeftAtMarket");
        Assert.False(WorldSnapshot.Deserialize(old.ToJsonString()).Invention!.IsLeftAtMarket(home.Id.Value));
    }
}
