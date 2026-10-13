using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Crime;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Crime;

/// <summary>crime-and-theft: o desesperado com o temperamento certo furta moeda de quem está na mesma célula; a moeda só muda de
/// carteira; descoberto, a vítima perde confiança no ladrão; cada guarda reduz o sucesso; desligado no padrão.</summary>
public class TheftTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly CellCoord Here = new(1, 1);
    private const int GuardId = 7;

    private static readonly Personality Criminal = Personality.Create(50, 50, 0, 50, 50, 50, 50, 0, 100, 0).Value!;
    private static readonly Personality Honest = Personality.Create(50, 50, 100, 50, 50, 50, 50, 100, 0, 100).Value!;

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static TheftRules Rules(double success = 1.0, double discovery = 1.0, double guardEffect = 0.25, bool enabled = true) =>
        new(enabled, 25, 1.1, success, discovery, 0.5, GuardId, guardEffect);

    private static WorldState NewWorld(TheftRules? rules = null, ulong seed = 1) => new(
        Calendar, seed, ScenarioRunner.DefaultMap(1), ScenarioRunner.DefaultPopulationCatalog,
        ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
        ScenarioRunner.DefaultLifeStageRules, familyRules: ScenarioRunner.DefaultFamilyRules, theftRules: rules);

    private static Npc Add(WorldState world, Personality personality, long wallet = 0, int profession = 1, int hunger = 100)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-25), new CultureId(1),
            Here, null, null, household: null, health: 100, personality: personality,
            profession: new ProfessionType(profession), currentLocation: Here);
        if (wallet > 0) npc.CreditWallet(new Money(wallet));
        world.AddNpc(npc);
        return npc;
    }

    private static TickContext Ctx(WorldState world, IWorldEventSink? sink = null) =>
        new(world, world.Rng, world.Scheduler, sink);

    private static long TotalCoin(WorldState world) => world.Npcs.Sum(n => n.Wallet.Amount);

    [Fact]
    public void Steal_is_an_extra_action_with_a_one_hour_default_and_a_label()
    {
        var catalog = ActionCatalog.Create(
            ScenarioRunner.DefaultActionCatalog.MaxDurationHours.Where(p => p.Key != ActionType.Steal).ToDictionary(p => p.Key, p => p.Value),
            ScenarioRunner.DefaultActionCatalog.RoutineSlots, ScenarioRunner.DefaultActionCatalog.DefaultAction);

        Assert.True(catalog.IsSuccess);
        Assert.Equal(1, catalog.Value!.DurationOf(ActionType.Steal));
        Assert.Equal("Furtando", LivingWorld.Api.Visual.Catalogs.ActionLabelCatalog.LabelOf(ActionType.Steal));
    }

    [Fact]
    public void Only_a_desperate_penniless_and_ill_tempered_npc_next_to_someone_with_coin_wants_to_steal()
    {
        var world = NewWorld(Rules());
        var thief = Add(world, Criminal);
        Add(world, Honest, wallet: 50);
        long hungry = 0;
        thief.SetHunger(10, hungry);
        thief.SetThirst(10, hungry);

        Assert.True(TheftResolver.Wants(world, thief, hungry));

        var saint = Add(world, Honest);
        saint.SetHunger(10, hungry);
        saint.SetThirst(10, hungry);
        Assert.False(TheftResolver.Wants(world, saint, hungry)); // temperamento

        var fed = Add(world, Criminal);
        Assert.False(TheftResolver.Wants(world, fed, hungry)); // sem desespero (necessidades cheias)

        thief.CreditWallet(new Money(5));
        Assert.False(TheftResolver.Wants(world, thief, hungry)); // tem dinheiro: compra
    }

    [Fact]
    public void A_pantry_stock_or_a_housemate_with_coin_does_not_make_a_victim_or_a_motive()
    {
        var world = NewWorld(Rules());
        var home = new HouseholdId(1);
        Npc InHome()
        {
            var npc = new Npc(
                world.NextNpcIdAndAdvance(), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-25), new CultureId(1),
                Here, null, null, household: home, health: 100, personality: Criminal,
                profession: new ProfessionType(1), currentLocation: Here);
            world.AddNpc(npc);
            return npc;
        }
        var thief = InHome();
        var housemate = InHome();
        housemate.CreditWallet(new Money(50));
        var household = new Household(home, Here, thief.Id, [thief.Id, housemate.Id]);
        world.AddHousehold(household);
        thief.SetHunger(10, 0);
        thief.SetThirst(10, 0);

        Assert.False(TheftResolver.Wants(world, thief, 0)); // só há a coin da própria casa

        Add(world, Honest, wallet: 50);
        Assert.True(TheftResolver.Wants(world, thief, 0)); // um estranho com moeda, despensa vazia

        household.Deposit(new ResourceType(world.EconomyRules.FoodResourceId), 1);
        Assert.False(TheftResolver.Wants(world, thief, 0)); // despensa com comida: não precisa roubar
    }

    [Fact]
    public void Nobody_steals_when_the_scenario_has_no_theft_and_a_victim_is_needed()
    {
        var off = NewWorld();
        var thief = Add(off, Criminal);
        Add(off, Honest, wallet: 50);
        thief.SetHunger(10, 0);
        Assert.False(TheftResolver.Wants(off, thief, 0));

        var alone = NewWorld(Rules());
        var lonely = Add(alone, Criminal);
        lonely.SetHunger(10, 0);
        Assert.False(TheftResolver.Wants(alone, lonely, 0)); // ninguém com moeda ao lado
    }

    [Fact]
    public void A_successful_theft_moves_existing_coin_only_and_a_discovered_one_betrays_the_victim_s_trust()
    {
        var world = NewWorld(Rules(success: 1.0, discovery: 1.0));
        var thief = Add(world, Criminal);
        var victim = Add(world, Honest, wallet: 100);
        var bond = world.GetOrCreateRelationship(new RelationshipKey(victim.Id, thief.Id), 0);
        for (int i = 0; i < 25; i++) bond.ApplyEvent(RelationshipEventType.Help, world.FamilyRules); // confiança 50
        long before = TotalCoin(world);
        var sink = new RecordingSink();

        TheftResolver.Resolve(world, thief, 5, Ctx(world, sink));

        Assert.Equal(before, TotalCoin(world));
        Assert.Equal(50, thief.Wallet.Amount);
        Assert.Equal(50, victim.Wallet.Amount);
        Assert.Single(sink.Events, e => e.Kind == WorldEventKind.TheftDiscovered && e.Payload == $"{thief.Id.Value}|{victim.Id.Value}|50|1");
        Assert.Equal(42, world.Relationships[new RelationshipKey(victim.Id, thief.Id)].Trust); // 50 - 8
    }

    [Fact]
    public void An_undiscovered_theft_leaves_no_event_and_no_betrayal_and_a_failed_one_is_caught_in_the_act()
    {
        var quiet = NewWorld(Rules(success: 1.0, discovery: 0.0));
        var thief = Add(quiet, Criminal);
        Add(quiet, Honest, wallet: 100);
        var sink = new RecordingSink();
        TheftResolver.Resolve(quiet, thief, 5, Ctx(quiet, sink));
        Assert.Equal(50, thief.Wallet.Amount);
        Assert.Empty(sink.Events);
        Assert.Empty(quiet.Relationships);

        var failed = NewWorld(Rules(success: 0.0, discovery: 0.0));
        var clumsy = Add(failed, Criminal);
        var target = Add(failed, Honest, wallet: 100);
        var failSink = new RecordingSink();
        TheftResolver.Resolve(failed, clumsy, 5, Ctx(failed, failSink));
        Assert.Equal(0, clumsy.Wallet.Amount);
        Assert.Equal(100, target.Wallet.Amount);
        Assert.Single(failSink.Events, e => e.Kind == WorldEventKind.TheftDiscovered && e.Payload.EndsWith("|0|0"));
    }

    [Fact]
    public void The_same_world_gives_the_same_thefts()
    {
        static List<long> Run()
        {
            var world = NewWorld(Rules(success: 0.5, discovery: 0.5), seed: 9);
            var thief = Add(world, Criminal);
            Add(world, Honest, wallet: 1000);
            for (long tick = 1; tick <= 40; tick++) TheftResolver.Resolve(world, thief, tick, Ctx(world));
            return world.Npcs.Select(n => n.Wallet.Amount).ToList();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Guards_in_the_city_cut_the_successful_thefts_with_a_z_of_at_least_three_over_twenty_seeds()
    {
        long open = 0, guarded = 0;
        const int attempts = 100;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var plain = NewWorld(Rules(success: 0.8, discovery: 0.0, guardEffect: 0.25), seed);
            var watched = NewWorld(Rules(success: 0.8, discovery: 0.0, guardEffect: 0.25), seed);
            var a = Add(plain, Criminal);
            var b = Add(watched, Criminal);
            var richA = Add(plain, Honest, wallet: 1_000_000);
            var richB = Add(watched, Honest, wallet: 1_000_000);
            for (int i = 0; i < 2; i++) Add(watched, Honest, profession: GuardId); // 2 guardas: sucesso x (1 - 0,5)
            for (long tick = 1; tick <= attempts; tick++)
            {
                richA.CreditWallet(new Money(1_000_000 - richA.Wallet.Amount)); // a vítima não se esgota entre as tentativas
                richB.CreditWallet(new Money(1_000_000 - richB.Wallet.Amount));
                long beforeA = a.Wallet.Amount, beforeB = b.Wallet.Amount;
                TheftResolver.Resolve(plain, a, tick, Ctx(plain));
                TheftResolver.Resolve(watched, b, tick, Ctx(watched));
                if (a.Wallet.Amount > beforeA) open++;
                if (b.Wallet.Amount > beforeB) guarded++;
            }
        }

        long n = 20L * attempts;
        double p1 = (double)open / n, p2 = (double)guarded / n, pooled = (double)(open + guarded) / (2 * n);
        double z = (p1 - p2) / Math.Sqrt(pooled * (1 - pooled) * 2.0 / n);
        Assert.True(z >= 3, $"sem guarda {open}, com guarda {guarded}, z = {z:F2}");
    }

    [Fact]
    public void The_theft_rules_survive_a_snapshot_round_trip_with_an_equal_hash()
    {
        var world = NewWorld(Rules(success: 0.7));
        var thief = Add(world, Criminal);
        Add(world, Honest, wallet: 100);
        TheftResolver.Resolve(world, thief, 3, Ctx(world));

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(world.TheftRules, restored.TheftRules);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
    }

    private sealed class CountingSink : IWorldEventSink
    {
        public int Thefts { get; private set; }
        public void Record(WorldEvent evt) { if (evt.Kind == WorldEventKind.TheftDiscovered) Thefts++; }
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void A_harsh_village_conserves_money_every_day_for_a_year_and_the_thefts_are_discovered()
    {
        var harsh = new TheftRules(true, 90, 0.9, 0.6, 0.5, 0.5, -1, 0.25);
        var (world, _) = ScenarioRunner.Create(2, theftRules: harsh);
        var sink = new CountingSink();
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        long Total() => world.Npcs.Sum(n => n.Wallet.Amount) + world.Workplaces.Sum(w => w.Treasury.Amount) + world.Cities.Sum(c => c.Treasury.Amount);
        long initialTotal = Total() - (world.MoneyMinted.Amount - world.MoneyDestroyed.Amount);

        for (int day = 0; day < 12 * 30; day++)
        {
            clock.Run(world, 24);
            Assert.Equal(world.MoneyMinted.Amount - world.MoneyDestroyed.Amount, Total() - initialTotal);
        }

        Assert.True(world.Npcs.Any(n => n.IsAlive), "a vila se extinguiu");
        Assert.True(sink.Thefts > 0, "nenhum furto descoberto em um ano");
    }
}
