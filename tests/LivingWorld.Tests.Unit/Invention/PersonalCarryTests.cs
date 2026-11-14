using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Invention;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Invention;

/// <summary>AD-147: o que a pessoa leva consigo. O recipiente é um design que a sonda de conter mede (volume, parede estanque, peso,
/// tira que prende ao corpo); de mãos nuas cabe pouco e nada que escorra; a despensa fica em casa; ao sair, leva o que a viagem pede;
/// longe, come do que leva; em casa, devolve. As peças saem da natureza do cenário (material e o volume que faltou), nunca de literais.</summary>
public class PersonalCarryTests
{
    /// <summary>Com a despensa em casa ligada (desligada por padrão no AD-147 até a sede longe de casa fechar).</summary>
    private static readonly InventionRules Rules = InventionRules.Default with { PantryNeedsPresence = true };

    private static MaterialSpec Formable => Rules.Materials.First(m => m.Formable && !m.CoatingOnly);

    private static MaterialSpec Leaky => Rules.Materials.First(m => !m.CoatingOnly && m.ImpermeabilityPm < Rules.WatertightPm);

    private static MaterialSpec Densest => Rules.Materials.Where(m => !m.CoatingOnly).OrderByDescending(m => m.DensityKgM3).First();

    private static (WorldState World, TickContext Ctx) NewWorld(InventionRules? rules = null)
    {
        var (world, _) = ScenarioRunner.Create(1, inventionRules: rules ?? Rules);
        return (world, new TickContext(world, world.Rng, world.Scheduler));
    }

    private static Household HomeOf(WorldState world, Npc npc) => world.FindHousehold(npc.Household!.Value)!;

    private static List<Npc> Adults(WorldState world) => world.Npcs
        .Where(n => n.IsAlive && n.Household is not null && n.AgeYears(world.CurrentDate) >= 20).OrderBy(n => n.Id.Value).ToList();

    /// <summary>Um oco do volume pedido no material, com uma tira presa por junção flexível (feita da mesma lâmina) quando pedido.</summary>
    private static List<DesignPart> Container(MaterialSpec m, int needMl, bool strap)
    {
        var body = CarryInvention.NeedSized(m, needMl)!.Value;
        var parts = new List<DesignPart> { body };
        if (strap)
            parts.Add(body with
            {
                WallMm = 0,
                Parent = 0,
                Joint = JointKind.Flexible,
                LengthMm = (short)Math.Min(m.MaxPieceMm, 8 * body.LengthMm),
                WidthMm = (short)Math.Max(m.MinWallMm * 8, body.WidthMm / 4),
                HeightMm = (short)m.MinWallMm,
                OffsetZMm = (short)body.Top,
            });
        return parts;
    }

    private static Design GiveContainer(WorldState world, Npc npc, IReadOnlyList<DesignPart> parts)
    {
        var ledger = world.Invention!;
        var design = Design.Create(parts, npc.Id.Value, 1, 0, 0, DesignOrigin.Invented);
        ledger.AddDesign(design);
        ledger.MarkCarryDesign(design.Id);
        ledger.Learn(new KnownDesign(npc.Id.Value, design.Id, 1000, DesignOrigin.Invented, 1));
        ledger.Items.Add(new CraftedItem(ledger.NextItemId++, design.Id, npc.Household!.Value.Value, npc.City, 1, long.MaxValue, 1000));
        ledger.Touch();
        return design;
    }

    /// <summary>Uma célula a mais de uma hora de caminho da casa.</summary>
    private static CellCoord FarFrom(WorldState world, Npc npc, Household home) =>
        world.Map.Cells.Where(c => !c.HasWater).Select(c => c.Coord).OrderBy(c => c.Y).ThenBy(c => c.X)
            .First(c => LivingWorld.Simulation.Emergence.Mobility.TravelTicks(world, npc, c, home.Location, 1.0) > 3);

    [Fact]
    public void A_hollow_of_a_formable_watertight_sheet_holds_liquid_and_a_leaky_one_holds_only_dry_things()
    {
        var tight = PhysicsEvaluator.Contain(Container(Formable, 3000, strap: false), Rules);
        var leaky = PhysicsEvaluator.Contain(Container(Leaky, 3000, strap: false), Rules);

        Assert.True(tight.Works && tight.WatertightMl >= 2000 && leaky.Works && leaky.WatertightMl == 0 && leaky.InnerMl >= 2000,
            $"estanque {tight}, vazado {leaky}");
    }

    [Fact]
    public void A_hollow_of_the_densest_material_is_too_heavy_to_carry_and_a_tiny_one_holds_nothing()
    {
        var heavy = PhysicsEvaluator.Contain(Container(Densest, 20_000, strap: false), Rules);
        var tiny = PhysicsEvaluator.Contain(Container(Formable, 50, strap: false), Rules);

        Assert.True(heavy.Failure == FailureMode.TooHeavy && tiny.Failure == FailureMode.TooSmall, $"pesado {heavy}, pequeno {tiny}");
    }

    [Fact]
    public void A_thin_flat_part_tied_by_a_flexible_joint_makes_it_worn_on_the_body()
    {
        Assert.False(PhysicsEvaluator.Contain(Container(Formable, 3000, strap: false), Rules).Worn);
        Assert.True(PhysicsEvaluator.Contain(Container(Formable, 3000, strap: true), Rules).Worn);
    }

    [Fact]
    public void Bare_hands_carry_one_ration_and_no_water_and_a_watertight_container_carries_water()
    {
        var (world, _) = NewWorld();
        var npc = Adults(world)[0];
        var bare = PersonalCarry.RoomOf(world, npc);
        GiveContainer(world, npc, Container(Formable, 5000, strap: true));
        var held = PersonalCarry.RoomOf(world, npc);

        Assert.True(bare.WetMl == 0 && bare.DryMl / Rules.UnitMlOf(1) == 1 && held.WetMl >= Rules.UnitMlOf(2) * 2,
            $"mãos {bare}, com recipiente {held}");
    }

    [Fact]
    public void Leaving_for_a_long_trip_packs_what_thirst_and_hunger_will_ask_and_leaves_some_for_the_others()
    {
        var (world, _) = NewWorld();
        var npc = Adults(world).First(a => HomeOf(world, a).Members.Count >= 2);
        var home = HomeOf(world, npc);
        GiveContainer(world, npc, Container(Formable, 8000, strap: true));
        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        home.Deposit(water, 3);
        home.Deposit(food, 3);
        long others = home.Members.Count - 1;
        long now = world.CurrentDate.TotalHours;
        long waterBefore = home.Stock.GetValueOrDefault(water);

        PersonalCarry.PackForTrip(world, npc, home, 72, now);

        int carriedWater = world.Invention!.CarriedQuantity(npc.Id.Value, water.Id);
        Assert.True(carriedWater > 0 && world.Invention.CarriedQuantity(npc.Id.Value, food.Id) > 0
                    && home.Stock.GetValueOrDefault(water) >= Math.Min(waterBefore, others * 3) && home.Stock.GetValueOrDefault(water) == waterBefore - carriedWater,
            $"levou água {carriedWater}, comida {world.Invention.CarriedQuantity(npc.Id.Value, food.Id)}, ficou {home.Stock.GetValueOrDefault(water)}, "
            + $"sede {npc.ThirstAt(now)}, espaço {PersonalCarry.RoomOf(world, npc)}, recipiente {world.Invention.BestCarrier(home.Id.Value, Rules)}");
    }

    [Fact]
    public void A_short_trip_packs_nothing()
    {
        var (world, _) = NewWorld();
        var npc = Adults(world)[0];
        var home = HomeOf(world, npc);
        npc.SetThirst(100, world.CurrentDate.TotalHours);
        npc.SetHunger(100, world.CurrentDate.TotalHours);

        PersonalCarry.PackForTrip(world, npc, home, 2, world.CurrentDate.TotalHours);

        Assert.False(world.Invention!.CarriesAnything(npc.Id.Value));
    }

    [Fact]
    public void Far_from_home_eating_uses_what_is_carried_and_not_the_pantry()
    {
        var (world, _) = NewWorld();
        var npc = Adults(world).First(a => a.Employer is null);
        var home = HomeOf(world, npc);
        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        home.Deposit(water, 5);
        long pantry = home.Stock.GetValueOrDefault(water);
        world.Invention!.Carry(npc.Id.Value, water.Id, 1);
        npc.MoveTo(FarFrom(world, npc, home), world.CurrentDate.TotalHours);
        long now = world.CurrentDate.TotalHours;
        npc.SetThirst(5, now);

        BehaviorDecisionSystem.ResolveFoodPlan(world, npc, MarketIndex.BuildForTick(world), now, ActionType.Eat);

        Assert.True(npc.ThirstAt(now) > 50 && home.Stock.GetValueOrDefault(water) == pantry && !world.Invention.CarriesAnything(npc.Id.Value),
            $"sede {npc.ThirstAt(now)}, despensa {home.Stock.GetValueOrDefault(water)} de {pantry}");
    }

    [Fact]
    public void Far_from_home_with_nothing_carried_the_pantry_does_not_reach()
    {
        var (world, _) = NewWorld();
        var npc = Adults(world).First(a => a.Employer is null);
        var home = HomeOf(world, npc);
        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        home.Deposit(water, 5);
        long pantry = home.Stock.GetValueOrDefault(water);
        npc.MoveTo(FarFrom(world, npc, home), world.CurrentDate.TotalHours);
        long now = world.CurrentDate.TotalHours;
        npc.SetThirst(5, now);

        BehaviorDecisionSystem.ResolveFoodPlan(world, npc, MarketIndex.BuildForTick(world), now, ActionType.Eat);

        Assert.True(npc.ThirstAt(now) == 5 && home.Stock.GetValueOrDefault(water) == pantry);
    }

    [Fact]
    public void The_pantry_reaches_anywhere_by_default()
    {
        Assert.False(InventionRules.Default.PantryNeedsPresence);
    }

    [Fact]
    public void Without_the_presence_rule_the_pantry_reaches_anywhere_as_before()
    {
        var (world, _) = NewWorld(Rules with { PantryNeedsPresence = false });
        var npc = Adults(world).First(a => a.Employer is null);
        var home = HomeOf(world, npc);
        npc.MoveTo(FarFrom(world, npc, home), world.CurrentDate.TotalHours);

        Assert.False(PersonalCarry.PantryOutOfReach(world, npc, home));
    }

    [Fact]
    public void Back_home_what_is_left_returns_to_the_pantry()
    {
        var (world, _) = NewWorld();
        var npc = Adults(world)[0];
        var home = HomeOf(world, npc);
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        long before = home.Stock.GetValueOrDefault(food);
        world.Invention!.Carry(npc.Id.Value, food.Id, 2);

        PersonalCarry.Unpack(world, npc, home);

        Assert.True(home.Stock.GetValueOrDefault(food) == before + 2 && !world.Invention.CarriesAnything(npc.Id.Value));
    }

    [Fact]
    public void Critical_thirst_away_drinks_from_what_is_carried_but_mild_thirst_waits()
    {
        var (world, _) = NewWorld();
        var npc = Adults(world)[0];
        var water = world.EconomyRules.WaterResourceId;
        world.Invention!.Carry(npc.Id.Value, water, 1);
        long now = world.CurrentDate.TotalHours;
        npc.SetThirst(60, now);
        bool mild = PersonalCarry.ConsumeFromPack(world, npc, now, 80);
        npc.SetThirst(10, now);
        bool critical = PersonalCarry.ConsumeFromPack(world, npc, now, 80);

        Assert.True(!mild && critical && npc.ThirstAt(now) > 50);
    }

    [Fact]
    public void A_worn_container_raises_what_the_purchase_across_water_brings_home()
    {
        var (world, _) = NewWorld();
        var npc = Adults(world)[0];
        var home = HomeOf(world, npc);
        var market = world.Map.Cells.Where(c => !c.HasWater).Select(c => c.Coord).OrderBy(c => c.Y).ThenBy(c => c.X)
            .First(c => WaterCrossing.CellsBetween(world.Map, home.Location, c) >= 3);
        long foot = InventionMarket.CargoGrams(world, home, market);
        GiveContainer(world, npc, Container(Formable, 8000, strap: true));
        long worn = InventionMarket.CargoGrams(world, home, market);

        Assert.True(worn > foot && foot == Rules.FootCarryKg * 1000L, $"a pé {foot}, com recipiente {worn}");
    }

    [Fact]
    public void A_purchase_cut_by_what_fits_becomes_the_problem_of_carrying()
    {
        var (world, _) = NewWorld();
        var npc = Adults(world)[0];
        var home = HomeOf(world, npc);
        var market = world.Map.Cells.Where(c => !c.HasWater).Select(c => c.Coord).OrderBy(c => c.Y).ThenBy(c => c.X)
            .First(c => WaterCrossing.CellsBetween(world.Map, home.Location, c) >= 3);

        PersonalCarry.FeltCargoCut(world, npc, home, market, cutG: 10_000, carriedG: 30_000);

        int i = world.Invention!.CarryMindIndex(npc.Id.Value);
        Assert.True(i >= 0 && world.Invention.CarryMinds[i].FrustrationMinutes > 0 && world.Invention.CarryMinds[i].NeedMl >= 10_000);
    }

    [Fact]
    public void Someone_who_needs_to_cross_water_does_not_buy_a_container_and_someone_who_needs_to_carry_does()
    {
        var (world, ctx) = NewWorld();
        var adults = Adults(world);
        var seller = adults[0];
        var buyer = adults.First(a => a.Household != seller.Household);
        var market = InventionMarket.MarketFor(world, buyer)!;
        var design = GiveContainer(world, seller, Container(Formable, 5000, strap: true));
        var ledger = world.Invention!;
        ledger.Items.Clear();
        ledger.Items.Add(new CraftedItem(ledger.NextItemId++, design.Id, seller.Household!.Value.Value, seller.City, 0,
            120L * world.Calendar.HoursPerMonth, 1000, seller.Id.Value, market.Id.Value, 5));
        ledger.Touch();
        buyer.CreditWallet(new Money(5000));
        ledger.PutMind(new InventionMind(buyer.Id.Value, 6000, 0, 0, 0, FailureMode.None, null, 0));

        InventionMarket.AtMarket(world, ctx, buyer, HomeOf(world, buyer), market);
        bool boatBuyerTook = !ledger.Items[0].ForSale;
        ledger.PutCarryMind(new InventionMind(buyer.Id.Value, 6000, 0, 0, 0, FailureMode.None, null, 0, NeedMl: 3000));
        InventionMarket.AtMarket(world, ctx, buyer, HomeOf(world, buyer), market);

        Assert.True(!boatBuyerTook && !ledger.Items[0].ForSale && ledger.Items[0].HouseholdId == buyer.Household!.Value.Value);
    }

    /// <summary>AD-149 (backlog 05 item 12b): a casa com um oco na mão que ainda tem a compra cortada melhora o recipiente até
    /// prendê-lo ao corpo. Antes parava no primeiro que servia e nenhuma das 20 seeds chegou a uma peça com tira.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_household_with_a_hand_held_container_still_short_of_room_comes_to_tie_it_to_the_body(bool improve)
    {
        var eager = Rules with { BaseAttemptChancePm = 1000, FrustrationThresholdMinutes = 30, ManufacturingNoisePm = 0, ImproveCarrier = improve };
        var (world, clock) = ScenarioRunner.Create(1, inventionRules: eager);
        var npc = Adults(world)[0];
        var home = npc.Household!.Value.Value;
        GiveContainer(world, npc, Container(Formable, 4000, strap: false));
        Assert.False(world.Invention!.BestCarrier(home, eager).Worn);

        for (int month = 0; month < 36 && !world.Invention.BestCarrier(home, eager).Worn; month++)
        {
            var ledger = world.Invention;
            int i = ledger.CarryMindIndex(npc.Id.Value);
            var mind = i >= 0 ? ledger.CarryMinds[i] : new InventionMind(npc.Id.Value, 0, 0, 0, 0, FailureMode.None, null, 0);
            ledger.PutCarryMind(mind with { FrustrationMinutes = 100_000, NeedMl = 4000 }); // a compra segue cortada todo mês
            clock.Run(world, world.Calendar.HoursPerMonth);
        }

        Assert.Equal(improve, world.Invention.BestCarrier(home, eager).Worn);
    }

    [Fact]
    public void A_person_who_could_not_carry_comes_to_make_something_that_holds_it_from_the_materials_they_know()
    {
        var eager = Rules with { BaseAttemptChancePm = 1000, FrustrationThresholdMinutes = 30, ManufacturingNoisePm = 0 };
        var (world, clock) = ScenarioRunner.Create(1, inventionRules: eager);
        var npc = Adults(world)[0];
        world.Invention!.PutCarryMind(new InventionMind(npc.Id.Value, 100_000, 0, 0, 0, FailureMode.None, null, 0, NeedMl: 4000, NeedsLiquid: true));

        for (int month = 0; month < 24 && world.Invention.CarryDesigns.Count == 0; month++)
            clock.Run(world, world.Calendar.HoursPerMonth);

        var ledger = world.Invention;
        Assert.True(ledger.CarryDesigns.Count > 0, $"tentativas {world.InventionTally.CarryAttempts}, falhas {world.InventionTally.CarryPrototypeFailures}, descartes {world.InventionTally.CarryIdeasDiscarded}");
        var design = ledger.FindDesign(ledger.CarryDesigns[0])!;
        Assert.True(PhysicsEvaluator.Contain(design.Parts, eager).Works, $"{PhysicsEvaluator.Contain(design.Parts, eager)}");
        // com o mundo ansioso, outra pessoa que teve a compra cortada pode inventar antes: vale quem inventou, desde que exista
        Assert.True(world.FindNpc(new NpcId(design.InventorId)) is not null && design.Origin == DesignOrigin.Invented, $"{design.InventorId} {design.Origin}");
        var item = ledger.Items.First(i => i.DesignId == design.Id);
        Assert.True(ledger.BestCarrier(item.HouseholdId, eager).Exists);
    }

    [Fact]
    public void Carried_loads_and_the_problem_of_carrying_survive_the_snapshot_and_an_old_snapshot_opens_without_them()
    {
        var (world, _) = NewWorld();
        var npc = Adults(world)[0];
        world.Invention!.Carry(npc.Id.Value, 2, 3);
        world.Invention.PutCarryMind(new InventionMind(npc.Id.Value, 500, 0, 1, 0, FailureMode.TooHeavy, null, 0, NeedMl: 2000, NeedsLiquid: true));
        var design = GiveContainer(world, npc, Container(Formable, 3000, strap: true));

        var back = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(3, back.Invention!.CarriedQuantity(npc.Id.Value, 2));
        Assert.Equal(world.Invention.CarryMinds, back.Invention.CarryMinds);
        Assert.Contains(design.Id, back.Invention.CarryDesigns);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(back));

        var old = System.Text.Json.Nodes.JsonNode.Parse(WorldSnapshot.Serialize(world))!.AsObject();
        var ledger = old["Invention"]!.AsObject();
        ledger.Remove("Carried");
        ledger.Remove("CarryMinds");
        ledger.Remove("CarryDesigns");
        var reopened = WorldSnapshot.Deserialize(old.ToJsonString());
        Assert.True(reopened.Invention!.Carried.Count == 0 && reopened.Invention.CarryMinds.Count == 0 && reopened.Invention.CarryDesigns.Count == 0);
    }
}
