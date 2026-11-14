using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Crime;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Crime;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Crime.CrimeTestWorld;

namespace LivingWorld.Tests.Unit.Crime;

/// <summary>Furto realista (AD-150; antes crime-and-theft): ligado por padrão; por fome (decidido na hora), miséria e
/// oportunidade (planejados no dia); de qualquer bem (moeda, comida da despensa e da banca, ferramenta da oficina); o que foi
/// levado sai da vítima e é comido, guardado ou vendido a um receptador; a guarda corta o sucesso; mesma seed, mesmos furtos.</summary>
public class TheftTests
{
    private static TheftRules Rules(double success = 1.0, double discovery = 1.0, double guardEffect = 0.25, int guard = -1) =>
        TheftRules.Default with { BaseSuccess = success, DiscoveryChance = discovery, GuardEffect = guardEffect, GuardProfessionId = guard };

    private static void Starve(Npc npc, int level = 10)
    {
        npc.SetHunger(level, 0); // só a fome: com sede, a pessoa vai beber antes de furtar
    }

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
    public void Theft_is_on_by_default_in_code_and_json_scenarios()
    {
        var (world, _) = ScenarioRunner.Create(1, initialPopulation: 0);
        Assert.True(world.TheftRules.Enabled);
        Assert.NotNull(world.CrimeRules);
        Assert.Equal(TheftRules.Default, LivingWorld.Simulation.Scenarios.WorldRulesScenarioLoader.LoadTheft("{}").Value);
        Assert.False(TheftRules.Disabled.Enabled);
    }

    [Fact]
    public void A_starving_person_with_coin_but_an_empty_stall_wants_to_steal_food_from_a_neighbour()
    {
        // antes (crime-and-theft): só quem não tinha uma moeda queria furtar, e só moeda
        var world = New(Rules());
        var thief = Add(world, Average, wallet: 30);
        Home(world, Here, thief);
        var neighbour = Add(world, Honest, at: new CellCoord(3, 1));
        var pantry = Home(world, new CellCoord(3, 1), neighbour);
        pantry.Deposit(new ResourceType(world.EconomyRules.FoodResourceId), 20);
        Shop(world, 1, new CellCoord(5, 5), prices: new() { [1] = 2 }); // banca sem comida
        Starve(thief, 5);

        Assert.True(TheftResolver.Wants(world, thief, 0));
        var sink = new Sink();
        TheftResolver.Resolve(world, thief, 5, Ctx(world, sink));

        var home = world.FindHousehold(thief.Household!.Value)!;
        long stolen = home.Stock.GetValueOrDefault(new ResourceType(world.EconomyRules.FoodResourceId));
        Assert.True(stolen > 0, "nada foi para a despensa do ladrão");
        Assert.Equal(20, stolen + pantry.Stock.GetValueOrDefault(new ResourceType(world.EconomyRules.FoodResourceId)));
        var evt = Assert.Single(sink.Of(WorldEventKind.TheftCommitted));
        Assert.Equal("casa|fome|comida", string.Join('|', evt.Payload.Split('|')[2..5]));
    }

    [Fact]
    public void A_fed_parent_with_a_starving_child_and_an_empty_pantry_goes_to_steal_food_for_the_family()
    {
        var world = New(Rules());
        var parent = Add(world, Average);
        var child = Add(world, Average, age: 6);
        var home = Home(world, Here, parent, child);
        var neighbour = Add(world, Honest, at: new CellCoord(3, 1));
        Home(world, new CellCoord(3, 1), neighbour).Deposit(new ResourceType(world.EconomyRules.FoodResourceId), 30);
        Assert.Equal(-1, CrimeSystem.FamilyHunger(world, parent, 0)); // ninguém com fome ainda
        child.SetHunger(5, 0);
        Assert.True(CrimeSystem.FamilyHunger(world, parent, 0) > 0.5);

        new CrimeSystem().HandleEvent(world, Ctx(world), new LivingWorld.Simulation.Scheduling.ScheduledEvent(1, 1, CrimeSystem.SystemName,
            $"T|{parent.Id.Value}|{(int)TheftMotive.Hunger}"));
        Assert.True(home.Stock.GetValueOrDefault(new ResourceType(world.EconomyRules.FoodResourceId)) > 0, "o pai não trouxe comida");
    }

    [Fact]
    public void Hunger_lowers_the_moral_barrier_and_food_at_home_or_money_for_the_stall_removes_the_motive()
    {
        var mild = Personality.Create(50, 50, 60, 50, 50, 50, 50, 60, 40, 60).Value!; // peso de Furtar 0,6
        var world = New(Rules());
        var thief = Add(world, mild);
        Add(world, Honest, wallet: 50); // alguém com moeda ao lado
        Starve(thief, 24); // no limite do desespero: a barreira é 1,1
        Assert.False(TheftResolver.Wants(world, thief, 0));
        Starve(thief, 2); // fome extrema: a barreira cai a ~0,55
        Assert.True(TheftResolver.Wants(world, thief, 0));

        Shop(world, 1, new CellCoord(5, 5), stock: new() { [1] = 50 }, prices: new() { [1] = 2 });
        thief.CreditWallet(new Money(5));
        Assert.False(TheftResolver.Wants(world, thief, 0)); // tem como comprar

        var fed = Add(world, Criminal);
        Assert.False(TheftResolver.Wants(world, fed, 0)); // sem fome
    }

    [Fact]
    public void A_pickpocket_moves_existing_coin_only_and_the_identified_thief_loses_the_victim_s_trust()
    {
        var world = New(Rules(success: 1.0, discovery: 1.0));
        var thief = Add(world, Criminal);
        var victim = Add(world, Honest, wallet: 100);
        var bond = world.GetOrCreateRelationship(new RelationshipKey(victim.Id, thief.Id), 0);
        for (int i = 0; i < 25; i++) bond.ApplyEvent(RelationshipEventType.Help, world.FamilyRules); // confiança 50
        long before = Coin(world);
        var sink = new Sink();
        Starve(thief);

        TheftResolver.Resolve(world, thief, 5, Ctx(world, sink));

        Assert.Equal(before, Coin(world));
        Assert.Equal(50, thief.Wallet.Amount);
        Assert.Equal(50, victim.Wallet.Amount);
        Assert.Single(sink.Of(WorldEventKind.TheftDiscovered), e => e.Payload == $"{thief.Id.Value}|{victim.Id.Value}|50|1");
        Assert.Equal(42, world.Relationships[new RelationshipKey(victim.Id, thief.Id)].Trust); // 50 - 8
    }

    [Fact]
    public void Stolen_tools_are_fenced_to_the_workplace_that_uses_them_and_coin_and_iron_are_conserved()
    {
        var world = New(Rules(success: 1.0, discovery: 0.0));
        var thief = Add(world, Criminal);
        Home(world, Here, thief);
        var forge = Shop(world, 2, new CellCoord(4, 4), stock: new() { [4] = 10 }, prices: new() { [4] = 20 });
        var farm = Shop(world, 1, new CellCoord(6, 6), treasury: 500, prices: new() { [1] = 2 });
        var target = Theft.FindTarget(world, thief, TheftMotive.Opportunity)!.Value;
        long coin = Coin(world);
        var sink = new Sink();

        Theft.Commit(world, Ctx(world, sink), thief, TheftMotive.Opportunity, target, world.Rng.OneShot("t"));

        Assert.Equal(7, forge.Stock[new ResourceType(4)]);
        Assert.Equal(3, farm.Stock[new ResourceType(4)]); // a fazenda usa o ferro como ferramenta
        Assert.Equal(30, thief.Wallet.Amount); // metade de 3 × 20
        Assert.Equal(coin, Coin(world));
        Assert.Contains("|banca|oportunidade|ferramenta|3|60|levou|", Assert.Single(sink.Of(WorldEventKind.TheftCommitted)).Payload);
    }

    [Fact]
    public void Without_a_target_or_without_theft_nobody_steals_and_asking_allocates_nothing()
    {
        var off = New(TheftRules.Disabled);
        var thief = Add(off, Criminal);
        Add(off, Honest, wallet: 50);
        Starve(thief);
        Assert.False(TheftResolver.Wants(off, thief, 0));

        var alone = New(Rules());
        var lonely = Add(alone, Criminal);
        Starve(lonely);
        Assert.False(TheftResolver.Wants(alone, lonely, 0)); // nada a levar

        var world = New(Rules());
        var fed = Add(world, Criminal);
        Add(world, Honest, wallet: 50);
        TheftResolver.Wants(world, fed, 0); // aquece
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) TheftResolver.Wants(world, fed, 0);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before); // o caminho comum (saciado) não aloca
    }

    [Fact]
    public void The_same_world_gives_the_same_thefts()
    {
        static List<long> Run()
        {
            var world = New(Rules(success: 0.5, discovery: 0.5), seed: 9);
            var thief = Add(world, Criminal);
            Add(world, Honest, wallet: 1000);
            Starve(thief);
            for (long tick = 1; tick <= 40; tick++) TheftResolver.Resolve(world, thief, tick, Ctx(world));
            return world.Npcs.Select(n => n.Wallet.Amount).ToList();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Guards_in_the_city_cut_the_successful_thefts_with_a_z_of_at_least_three_over_twenty_seeds()
    {
        const int guardId = 7;
        long open = 0, guarded = 0;
        const int attempts = 100;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var plain = New(Rules(success: 0.8, discovery: 0.0, guard: guardId), seed: seed);
            var watched = New(Rules(success: 0.8, discovery: 0.0, guard: guardId), seed: seed);
            var a = Add(plain, Criminal);
            var b = Add(watched, Criminal);
            var richA = Add(plain, Honest, wallet: 1_000_000);
            var richB = Add(watched, Honest, wallet: 1_000_000);
            for (int i = 0; i < 2; i++) Add(watched, Honest, at: new CellCoord(9, 9), profession: guardId); // sucesso x 0,5
            Starve(a); Starve(b);
            for (long tick = 1; tick <= attempts; tick++)
            {
                richA.CreditWallet(new Money(1_000_000 - richA.Wallet.Amount));
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
    public void The_extended_theft_rules_survive_a_snapshot_round_trip_and_an_old_snapshot_keeps_its_rules()
    {
        var world = New(Rules(success: 0.7) with { OpportunityPerYear = 0.2 });
        var thief = Add(world, Criminal);
        Add(world, Honest, wallet: 100);
        Starve(thief);
        TheftResolver.Resolve(world, thief, 3, Ctx(world));

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.Equal(world.TheftRules, restored.TheftRules);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));

        var json = System.Text.Json.Nodes.JsonNode.Parse(WorldSnapshot.Serialize(world))!.AsObject();
        json["TheftRules"]!.AsObject().Remove("OpportunityPerYear"); // snapshot de antes do AD-150
        var old = WorldSnapshot.Deserialize(json.ToJsonString());
        Assert.Equal(TheftRules.Default.OpportunityPerYear, old.TheftRules.OpportunityPerYear);
    }

    [Fact]
    public void Poverty_multiplies_and_a_saint_never_plans_a_theft()
    {
        var world = New(Rules());
        var poor = Add(world, Criminal);
        Home(world, Here, poor);
        var rich = Add(world, Criminal, wallet: 500);
        var richHome = Home(world, new CellCoord(2, 2), rich);
        richHome.Deposit(new ResourceType(world.EconomyRules.FoodResourceId), 100);
        var saint = Add(world, Honest);

        double p = CrimeSystem.PlannedTheftPerYear(world, poor, 0, 0, out bool isPoor);
        double r = CrimeSystem.PlannedTheftPerYear(world, rich, 0, 0, out bool richPoor);
        Assert.True(isPoor && !richPoor);
        Assert.Equal(world.TheftRules.PovertyFactor, p / r, 6);
        Assert.Equal(0, CrimeSystem.PlannedTheftPerYear(world, saint, 0, 0, out _));
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void The_default_village_has_planned_thefts_of_several_goods_and_money_stays_conserved()
    {
        var (world, _) = ScenarioRunner.Create(3);
        var sink = new Sink();
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        long Total() => world.Npcs.Sum(n => n.Wallet.Amount) + world.Workplaces.Sum(w => w.Treasury.Amount) + world.Cities.Sum(c => c.Treasury.Amount);
        long initial = Total() - (world.MoneyMinted.Amount - world.MoneyDestroyed.Amount);
        for (int month = 0; month < 60; month++)
        {
            clock.Run(world, 30 * 24);
            Assert.Equal(world.MoneyMinted.Amount - world.MoneyDestroyed.Amount, Total() - initial);
        }

        var thefts = sink.Of(WorldEventKind.TheftCommitted).Select(e => e.Payload.Split('|')).ToList();
        Assert.True(thefts.Count >= 3, $"só {thefts.Count} furtos em 5 anos");
        Assert.Contains(thefts, t => t[3] is "oportunidade" or "miseria");
    }

    /// <summary>Vila dura (AD-128): a cidade sem caixa para a esmola, as despensas vazias e metade das pessoas sem moeda, sem
    /// água de graça: a fome aparece como furto de comida.</summary>
    [Fact]
    [Trait("Category", "Scenario")]
    public void In_a_harsh_village_hunger_shows_up_as_food_theft()
    {
        var (world, _) = ScenarioRunner.Create(3, naturalWaterRules: LivingWorld.Domain.Economy.NaturalWaterRules.Disabled);
        foreach (var city in world.Cities) city.TryDebitTreasury(city.Treasury);
        foreach (var household in world.Households)
            foreach (var resource in household.Stock.Keys.ToList()) household.Withdraw(resource, household.Stock[resource]);
        foreach (var npc in world.Npcs.Where(n => n.Id.Value % 2 == 1)) npc.TryDebitWallet(npc.Wallet);
        var sink = new Sink();
        new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink).Run(world, 12 * 30 * 24);

        Assert.True(world.Npcs.Any(n => n.IsAlive), "a vila se extinguiu");
        Assert.Contains(sink.Of(WorldEventKind.TheftCommitted), e => e.Payload.Split('|')[3] == "fome");
    }
}
