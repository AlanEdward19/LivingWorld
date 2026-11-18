using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Economy.Supply;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>AD-185: buscar água é viagem de verdade. Alguém da casa com tempo livre vai à fonte pela rota, bebe, enche o cântaro e
/// volta carregado; a despensa só recebe o que chegou em casa. Sem ninguém que possa ir, a ajuda dos seus traz a água.</summary>
public sealed class WaterFetchTests
{
    private static (WorldState World, TickContext Ctx) NewWorld(NaturalWaterRules? rules = null)
    {
        var (world, _) = ScenarioRunner.Create(17, naturalWaterRules: rules);
        return (world, new TickContext(world, world.Rng, world.Scheduler));
    }

    private static ResourceType Water(WorldState world) => new(world.EconomyRules.WaterResourceId);

    private static void Drain(WorldState world, Household home) => home.Withdraw(Water(world), home.Stock.GetValueOrDefault(Water(world)));

    /// <summary>Uma casa com a fonte ao alcance (fora da célula dela) e dois crescidos: um sem emprego e outro.</summary>
    private static (Household Home, Npc Free, Npc Other, CellCoord Source) Scene(WorldState world)
    {
        foreach (var home in world.Households.Where(h => !h.IsEmpty && !h.IsHomeless).OrderBy(h => h.Id.Value))
        {
            if (WaterFetch.SourceFor(world, home.Location) is not { } source || source.Cell == home.Location) continue;
            var grown = home.Members.Select(m => world.FindNpc(m)!).Where(n => n.IsAlive && n.AgeYears(world.CurrentDate) >= 16).OrderBy(n => n.Id.Value).ToList();
            if (grown.Count < 2) continue;
            var free = grown[0];
            if (free.Employer is { } e) { world.FindWorkplace(e)?.Fire(free.Id); free.Fire(); }
            foreach (var n in grown) n.MoveTo(home.Location, world.CurrentDate.TotalHours);
            return (home, free, grown[1], source.Cell);
        }
        throw new InvalidOperationException("sem casa com a fonte ao alcance e dois crescidos");
    }

    private static long Now(WorldState world) => world.CurrentDate.TotalHours;

    [Fact]
    public void A_household_fed_by_the_source_restocks_food_below_the_target_not_at_half_of_it()
    {
        foreach (bool fetch in new[] { true, false })
        {
            var (world, _) = NewWorld(fetch ? null : NaturalWaterRules.Default with { Fetch = WaterFetchRules.Off });
            var market = MarketIndex.BuildForTick(world);
            var food = new ResourceType(world.EconomyRules.FoodResourceId);
            // uma casa com a fonte não mais longe que o mercado
            var home = world.Households.Where(h => !h.IsEmpty && !h.IsHomeless).OrderBy(h => h.Id.Value).First(h =>
                market.NearestSelling(h.Location, food, Water(world)) is { } m
                && BehaviorDecisionSystem.WaterFromSource(world, h, m) == fetch);
            var free = home.Members.Select(m => world.FindNpc(m)!).Where(n => n.IsAlive && n.AgeYears(world.CurrentDate) >= 16).OrderBy(n => n.Id.Value).First();
            free.CreditWallet(new Money(500));
            double target = BehaviorDecisionSystem.ProvisionTargetFor(world, free, market);
            // comida a três quartos da meta, água cheia: com a água da fonte a casa repõe; comprando a água, ainda não
            long foodLevel = (long)Math.Floor(target * 0.75 * home.Members.Count);
            home.Withdraw(food, home.Stock.GetValueOrDefault(food));
            home.Deposit(food, foodLevel);
            home.Deposit(Water(world), 1000);

            Assert.Equal(fetch, BehaviorDecisionSystem.NeedsRestocking(world, free, market));
        }
    }

    [Fact]
    public void With_the_trip_on_the_daily_water_no_longer_fills_any_pantry()
    {
        var (world, ctx) = NewWorld();
        foreach (var h in world.Households) Drain(world, h);

        new NaturalWaterSystem().Tick(world, ctx);

        Assert.True(WaterFetch.On(world));
        Assert.All(world.Households, h => Assert.Equal(0, h.Stock.GetValueOrDefault(Water(world))));
    }

    [Fact]
    public void A_free_member_at_home_with_the_water_low_sets_out_for_the_nearest_source()
    {
        var (world, _) = NewWorld();
        var (home, free, _, source) = Scene(world);
        Drain(world, home);

        Assert.True(WaterFetch.WantsToFetch(world, free, Now(world)));
        free.SetCurrentAction(ActionType.Travel, Now(world));

        Assert.Equal(source, WaterFetch.ErrandDestination(world, free));
        Assert.Equal(source, BehaviorDecisionSystem.TravelDestinationFor(world, free, MarketIndex.BuildForTick(world)));
    }

    [Fact]
    public void A_full_pantry_sends_nobody_to_the_source()
    {
        var (world, _) = NewWorld();
        var (home, free, _, _) = Scene(world);
        Drain(world, home);
        home.Deposit(Water(world), home.Members.Count * 5L);

        Assert.False(WaterFetch.WantsToFetch(world, free, Now(world)));
    }

    [Fact]
    public void Only_one_member_of_a_household_goes_at_a_time()
    {
        var (world, _) = NewWorld();
        var (home, free, other, _) = Scene(world);
        Drain(world, home);
        home.Deposit(Water(world), home.Members.Count * 2L); // abaixo de 3 dias, acima de 1: um vai por todos
        Assert.True(WaterFetch.WantsToFetch(world, free, Now(world)));
        free.SetCurrentAction(ActionType.Travel, Now(world));

        Assert.False(WaterFetch.WantsToFetch(world, other, Now(world)));
        Drain(world, home); // abaixo de um dia, vai mais de um
        Assert.True(WaterFetch.WantsToFetch(world, other, Now(world)));
    }

    [Fact]
    public void The_employed_member_stays_while_a_free_member_can_go_unless_the_water_is_almost_gone()
    {
        var (world, _) = NewWorld();
        var (home, free, other, _) = Scene(world);
        if (other.Employer is null) { var job = world.Workplaces.First(w => !w.Closed); job.Hire(other.Id); other.Hire(job.Id); }
        Drain(world, home);
        home.Deposit(Water(world), home.Members.Count * 1L); // abaixo de 3 dias, não abaixo de 1

        Assert.False(WaterFetch.WantsToFetch(world, other, Now(world)));
        Drain(world, home);
        Assert.True(WaterFetch.WantsToFetch(world, other, Now(world)));
        _ = free;
    }

    [Fact]
    public void At_the_source_the_fetcher_drinks_and_fills_the_jug_but_the_pantry_waits_for_the_walk_home()
    {
        var (world, ctx) = NewWorld();
        var (home, free, _, source) = Scene(world);
        Drain(world, home);
        Assert.True(WaterFetch.WantsToFetch(world, free, Now(world)));
        free.SetCurrentAction(ActionType.Travel, Now(world));
        free.SetThirst(10, Now(world));

        free.MoveTo(source, Now(world));
        WaterFetch.OnArrived(world, ctx, free, source);

        int carried = world.Invention!.CarriedQuantity(free.Id.Value, Water(world).Id);
        Assert.InRange(carried, 1, world.InventionRules!.FootCarryKg * 1000 / world.InventionRules.UnitGramsOf(Water(world).Id));
        Assert.Equal(0, home.Stock.GetValueOrDefault(Water(world)));
        Assert.True(free.ThirstAt(Now(world)) > 10);
        Assert.True(WaterFetch.HeadingHome(world, free));
        Assert.Equal(home.Location, WaterFetch.ErrandDestination(world, free));
        Assert.True(LivingWorld.Simulation.Emergence.Mobility.LoadFactor(world, free) > 1.0); // a volta carregada é mais lenta
    }

    [Fact]
    public void The_water_reaches_the_pantry_only_when_the_fetcher_gets_home()
    {
        var (world, ctx) = NewWorld();
        var (home, free, _, source) = Scene(world);
        Drain(world, home);
        Assert.True(WaterFetch.WantsToFetch(world, free, Now(world)));
        free.SetCurrentAction(ActionType.Travel, Now(world));
        free.MoveTo(source, Now(world));
        WaterFetch.OnArrived(world, ctx, free, source);
        int carried = world.Invention!.CarriedQuantity(free.Id.Value, Water(world).Id);

        free.MoveTo(home.Location, Now(world));
        WaterFetch.OnArrived(world, ctx, free, home.Location);

        Assert.Equal(carried, home.Stock.GetValueOrDefault(Water(world)));
        Assert.Equal(0, world.Invention.CarriedQuantity(free.Id.Value, Water(world).Id));
        Assert.Null(world.Invention.WaterTrips);
    }

    [Fact]
    public void A_trip_in_progress_survives_a_snapshot_round_trip()
    {
        var (world, ctx) = NewWorld();
        var (home, free, _, source) = Scene(world);
        Drain(world, home);
        Assert.True(WaterFetch.WantsToFetch(world, free, Now(world)));
        free.SetCurrentAction(ActionType.Travel, Now(world));
        free.MoveTo(source, Now(world));
        WaterFetch.OnArrived(world, ctx, free, source);

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(world.Invention!.WaterTrips, restored.Invention!.WaterTrips);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
    }

    [Fact]
    public void The_trip_ends_with_the_fetcher()
    {
        var (world, _) = NewWorld();
        var (home, free, _, _) = Scene(world);
        Drain(world, home);
        Assert.True(WaterFetch.WantsToFetch(world, free, Now(world)));

        LivingWorld.Simulation.Invention.PersonalCarry.Forget(world, free.Id.Value);

        Assert.Null(world.Invention!.WaterTrips);
    }

    [Fact]
    public void Running_the_village_brings_water_home_on_foot_without_thirst_deaths()
    {
        var (world, clock) = ScenarioRunner.Create(17);
        var sink = new DeathSink();
        var watched = new WorldClock(clock.Systems, clock.MaxIterationsPerTick, sink);

        // a despensa inicial da vila dura ~45 dias: depois dela, a água só chega a pé
        watched.Run(world, 75 * world.Calendar.HoursPerDay);

        Assert.True(world.InventionTally.WaterTripsHome > 0, "ninguém trouxe água a pé");
        Assert.True(world.InventionTally.WaterUnitsHome > 0, "as idas não trouxeram água");
        Assert.Equal(0, sink.Dehydrated);
    }

    [Fact]
    public void Without_the_trip_the_old_fill_comes_back_byte_for_byte_in_the_daily_system()
    {
        var (world, ctx) = NewWorld(NaturalWaterRules.Default with { Fetch = WaterFetchRules.Off });
        foreach (var h in world.Households) Drain(world, h);
        var rules = world.NaturalWaterRules;
        var near = world.Households.Where(h => !h.IsEmpty && NaturalWaterSystem.WalkToWater(world, h.Location, rules.SearchRadiusCells) <= rules.MaxWalkTicks).ToList();
        Assert.NotEmpty(near);

        new NaturalWaterSystem().Tick(world, ctx);

        Assert.False(WaterFetch.On(world));
        Assert.All(near, h => Assert.Equal(h.Members.Count * (long)rules.DaysOfSupply, h.Stock.GetValueOrDefault(Water(world))));
    }

    [Fact]
    public void A_household_with_nobody_able_to_fetch_gets_water_from_its_own()
    {
        var (world, ctx) = NewWorld();
        // a criança pequena sozinha em casa (os crescidos foram embora) e alguém de afeto com água sobrando noutra casa
        var child = world.Npcs.Where(n => n.IsAlive && n.Household is not null && n.AgeYears(world.CurrentDate) < 8).OrderBy(n => n.Id.Value).First();
        var home = world.FindHousehold(child.Household!.Value)!;
        var otherHome = world.Households.First(h => h.Id != home.Id && !h.IsEmpty);
        foreach (var m in home.Members.ToList())
            if (m != child.Id) { home.RemoveMember(m); otherHome.AddMember(m); world.FindNpc(m)!.JoinHousehold(otherHome.Id); }
        var helper = otherHome.Members.Select(m => world.FindNpc(m)!).First(n => n.IsAlive && n.AgeYears(world.CurrentDate) >= 20);
        foreach (var key in new[] { new LivingWorld.Domain.Population.Family.RelationshipKey(helper.Id, child.Id), new LivingWorld.Domain.Population.Family.RelationshipKey(child.Id, helper.Id) })
        {
            var bond = world.GetOrCreateRelationship(key, Now(world));
            for (int i = 0; i < 200 && bond.Affection < 90; i++) bond.ApplyEvent(LivingWorld.Domain.Population.Family.RelationshipEventType.Help, world.FamilyRules);
        }
        Drain(world, home);
        otherHome.Deposit(Water(world), 500);
        Assert.True(WaterFetch.NoFetcher(world, home));

        for (int day = 0; day < 60 && home.Stock.GetValueOrDefault(Water(world)) == 0; day++)
        {
            new LivingWorld.Simulation.Population.Family.MutualAidSystem().Tick(world, ctx);
            world.CurrentDate = world.CurrentDate.AddHours(world.Calendar.HoursPerDay);
        }

        Assert.True(home.Stock.GetValueOrDefault(Water(world)) > 0);
        Assert.True(world.InventionTally.WaterAidGiven > 0);
    }

    private sealed class DeathSink : IWorldEventSink
    {
        public int Dehydrated;
        public void Record(WorldEvent evt)
        {
            if (evt.Kind == LivingWorld.Domain.History.WorldEventKind.Dehydration) Dehydrated++;
        }
    }
}
