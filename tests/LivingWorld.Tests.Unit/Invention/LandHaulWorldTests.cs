using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Invention;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Invention;

/// <summary>AD-153: o veículo de terra nasce do problema que já existe (a compra cortada pelo que os braços levam), pelo mesmo motor da
/// embarcação e do recipiente (tentativa, falha, ensino, cópia), e muda de fato o que a casa traz do mercado por terra.</summary>
public class LandHaulWorldTests
{
    private static readonly InventionRules Eager = InventionRules.Default with
    {
        BaseAttemptChancePm = 1000,
        FrustrationThresholdMinutes = 30,
        ManufacturingNoisePm = 0,
    };

    private static List<Npc> Adults(WorldState world) => world.Npcs
        .Where(n => n.IsAlive && n.Household is not null && n.AgeYears(world.CurrentDate) >= 20).OrderBy(n => n.Id.Value).ToList();

    private static Household HomeOf(WorldState world, Npc npc) => world.FindHousehold(npc.Household!.Value)!;

    /// <summary>Uma célula seca com caminho por terra (sem água na reta) que não é a porta de casa.</summary>
    private static CellCoord LandMarket(WorldState world, Household home) =>
        world.Map.Cells.Where(c => !c.HasWater && c.Coord != home.Location).Select(c => c.Coord).OrderBy(c => c.Y).ThenBy(c => c.X)
            .First(c => WaterCrossing.CellsBetween(world.Map, home.Location, c) == 0);

    /// <summary>Um carrinho: tabuleiro de madeira com duas peças redondas presas por eixo nas pontas (só para testar o consumidor).</summary>
    private static Design GiveCart(WorldState world, Npc npc)
    {
        var wood = world.InventionRules!.Materials.First(m => !m.CoatingOnly && m.NaturalShape == PartShape.Cylinder && m.TensileMPa >= 30);
        var deck = new DesignPart(PartShape.Box, 1000, 600, 40, 0, (short)wood.Id, 0, JointKind.Fixed, -1, 0, 0, 0);
        var wheel = new DesignPart(PartShape.Cylinder, 60, 300, 300, 0, (short)wood.Id, 0, JointKind.Axle, 0, 470, 0, -300);
        var ledger = world.Invention!;
        var design = Design.Create([deck, wheel, wheel with { OffsetXMm = -470 }], npc.Id.Value, 1, 0, 0, DesignOrigin.Invented);
        ledger.AddDesign(design);
        ledger.MarkHaulDesign(design.Id);
        ledger.Learn(new KnownDesign(npc.Id.Value, design.Id, 1000, DesignOrigin.Invented, 1));
        ledger.Items.Add(new CraftedItem(ledger.NextItemId++, design.Id, npc.Household!.Value.Value, npc.City, 1, long.MaxValue, 1000));
        ledger.Touch();
        return design;
    }

    private static void Frustrate(WorldState world, Npc npc, int needMl)
    {
        var ledger = world.Invention!;
        int i = ledger.CarryMindIndex(npc.Id.Value);
        var mind = i >= 0 ? ledger.CarryMinds[i] : new InventionMind(npc.Id.Value, 0, 0, 0, 0, FailureMode.None, null, 0);
        ledger.PutCarryMind(mind with { FrustrationMinutes = 100_000, NeedMl = needMl });
    }

    [Fact]
    public void Over_land_a_person_brings_back_only_what_the_arms_carry_and_a_cart_brings_much_more()
    {
        var (world, _) = ScenarioRunner.Create(1, inventionRules: Eager);
        var npc = Adults(world)[0];
        var home = HomeOf(world, npc);
        var market = LandMarket(world, home);
        long foot = Eager.FootCarryKg * 1000L;

        Assert.Equal(foot, InventionMarket.CargoGrams(world, home, market));
        Assert.Equal(long.MaxValue, InventionMarket.CargoGrams(world, home, home.Location)); // mercado na porta: nada a levar

        var cart = GiveCart(world, npc);
        // AD-176: o veículo de terra pede caminho que o comporte: pelo mato até o mercado, a carga volta aos braços
        Assert.Equal(foot, InventionMarket.CargoGrams(world, home, market));
        Assert.Equal(1, world.InventionTally.HaulerRouteBlocked);
        var ground = world.Ground!;
        foreach (var cell in LivingWorld.Simulation.Cities.Streets.StreetRoutes.PathOf(world, home.Location, market))
            ground.SetSurface(ground.IndexOf(cell), LivingWorld.Simulation.Cities.Streets.GroundSurface.Dirt);
        ground.SurfaceVersion++;
        long withCart = InventionMarket.CargoGrams(world, home, market);
        Assert.Equal(PhysicsEvaluator.Haul(cart.Parts, Eager).CapacityG, withCart);
        Assert.True(withCart > 3 * foot, $"{withCart}");

        long cargo = withCart, cut = 0;
        long water = InventionMarket.Fit(world, world.EconomyRules.WaterResourceId, 40, ref cargo, ref cut);
        Assert.True(water * Eager.UnitGramsOf(world.EconomyRules.WaterResourceId) > foot, "o carrinho traz mais água que os braços");
        InventionMarket.NoteTrip(world, home, market, withCart, cargo, cut);
        Assert.Equal(1, world.InventionTally.HaulTrips);
    }

    [Fact]
    public void With_land_cargo_off_nothing_is_cut_over_land()
    {
        var rules = Eager with { LandCargo = false };
        var (world, _) = ScenarioRunner.Create(1, inventionRules: rules);
        var home = HomeOf(world, Adults(world)[0]);

        Assert.Equal(long.MaxValue, InventionMarket.CargoGrams(world, home, LandMarket(world, home)));
    }

    [Fact]
    public void Someone_whose_purchase_was_cut_far_beyond_what_arms_carry_comes_to_make_something_that_is_pulled_over_land()
    {
        // AD-165: com a tração da fonte (175 N) nenhuma peça puxada vencia os 30 kg nos braços; AD-166: a carga a pé da fonte
        // (20 kg, correções #6) é o padrão, e o que se puxa nasce da compra cortada
        var rules = Eager;
        var (world, clock) = ScenarioRunner.Create(1, inventionRules: rules);
        var npc = Adults(world)[0];

        for (int month = 0; month < 36 && world.Invention!.HaulDesigns is not { Count: > 0 }; month++)
        {
            Frustrate(world, npc, 80_000);
            clock.Run(world, world.Calendar.HoursPerMonth);
        }

        var ledger = world.Invention!;
        Assert.True(ledger.HaulDesigns is { Count: > 0 }, $"tentativas {world.InventionTally.CarryAttempts} terra {world.InventionTally.HaulAttempts} falhas {world.InventionTally.HaulPrototypeFailures}");
        var design = ledger.FindDesign(ledger.HaulDesigns![0])!;
        var haul = PhysicsEvaluator.Haul(design.Parts, rules);
        Assert.True(haul.Works && haul.CapacityG > rules.FootCarryKg * 1000L, haul.ToString());
        Assert.Equal("haul", DesignQuery.All(world).First(d => d.Id == design.Id).Function);
        var item = ledger.Items.First(i => i.DesignId == design.Id);
        Assert.True(ledger.BestHauler(item.HouseholdId, rules).Exists);
    }

    [Fact]
    public void Without_land_vehicles_the_same_problem_only_brings_containers()
    {
        var rules = Eager with { LandVehicles = false };
        var (world, clock) = ScenarioRunner.Create(1, inventionRules: rules);
        var npc = Adults(world)[0];
        for (int month = 0; month < 24; month++)
        {
            Frustrate(world, npc, 80_000);
            clock.Run(world, world.Calendar.HoursPerMonth);
        }

        Assert.True(world.Invention!.HaulDesigns is null && world.InventionTally.HaulAttempts == 0);
    }

    [Fact]
    public void Who_knows_a_cart_teaches_the_household_and_a_neighbour_who_saw_it_copies_it()
    {
        var rules = Eager with { TeachChancePm = 1000, CopyPreferencePm = 1000 };
        var (world, clock) = ScenarioRunner.Create(1, inventionRules: rules);
        var adults = Adults(world);
        var maker = adults.First(a => HomeOf(world, a).Members.Count(m => world.FindNpc(m) is { IsAlive: true } o && o.AgeYears(world.CurrentDate) >= 14) >= 2);
        var cart = GiveCart(world, maker);
        var watcher = adults.First(a => a.Household != maker.Household);
        Frustrate(world, watcher, 80_000);
        world.Invention!.PutCarryMind(world.Invention.CarryMinds[world.Invention.CarryMindIndex(watcher.Id.Value)] with { SeenDesignId = cart.Id });

        for (int month = 0; month < 12; month++) clock.Run(world, world.Calendar.HoursPerMonth);

        var ledger = world.Invention;
        Assert.True(world.InventionTally.HaulTaught > 0, "ensinou a casa");
        Assert.Contains(ledger.Known, k => k.DesignId == cart.Id && k.NpcId != maker.Id.Value && k.Origin == DesignOrigin.Taught);
        Assert.True(world.InventionTally.CarryCopies > 0, "copiou o que viu");
    }

    [Fact]
    public void The_same_seed_tells_the_same_story_of_land_vehicles_and_other_seeds_reach_other_designs()
    {
        IReadOnlyList<int> Run(ulong seed)
        {
            var (world, clock) = ScenarioRunner.Create(seed, inventionRules: Eager);
            var people = Adults(world).Take(6).ToList();
            for (int month = 0; month < 24; month++)
            {
                foreach (var npc in people.Where(p => p.IsAlive)) Frustrate(world, npc, 80_000);
                clock.Run(world, world.Calendar.HoursPerMonth);
            }
            return world.Invention!.HaulDesigns?.ToList() ?? [];
        }

        var first = Run(3);
        Assert.NotEmpty(first);
        Assert.Equal(first, Run(3));
        Assert.Empty(first.Intersect(Run(4)));
    }

    [Fact]
    public void Land_vehicle_designs_survive_the_snapshot_and_a_world_without_them_writes_no_field()
    {
        var (world, _) = ScenarioRunner.Create(1, inventionRules: Eager);
        Assert.DoesNotContain("HaulDesigns", WorldSnapshot.Serialize(world));

        var cart = GiveCart(world, Adults(world)[0]);
        var back = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Contains(cart.Id, back.Invention!.HaulDesigns!);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(back));
    }
}
