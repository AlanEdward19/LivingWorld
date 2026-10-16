using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Objects;

/// <summary>#47 fatia 3 (ADR-0038): interações e efeitos do objeto aplicados pelo motor (duração do sono e da refeição) e o conforto no contexto de decisão.</summary>
public class ObjectEffectsTests
{
    private const int HouseTypeId = -1; // tipo dos lares semeados (PopulationSeeder.InitialHouseBuildingTypeId)

    private static JsonObject Root(string? bedEffect = null, string? tableEffect = null, Action<JsonObject>? tweak = null)
    {
        var root = new JsonObject();
        DefaultObjects.AddTo(root);
        ((JsonObject)root["Furnishings"]!)[HouseTypeId.ToString()] = new JsonArray("bed", "table");
        if (bedEffect is not null) Piece(root, "bed")["effects"] = new JsonArray(bedEffect);
        if (tableEffect is not null) Piece(root, "table")["effects"] = new JsonArray(tableEffect);
        tweak?.Invoke(root);
        return root;
    }

    private static JsonObject Piece(JsonObject root, string id) => (JsonObject)((JsonArray)root["Objects"]!).Single(o => (string)o!["id"]! == id)!;

    private static ObjectCatalog Catalog(string? bedEffect = null, string? tableEffect = null, Action<JsonObject>? tweak = null) =>
        ObjectCatalogScenarioLoader.Load(Root(bedEffect, tableEffect, tweak)).Value!;

    private static (WorldState World, WorldClock Clock) World(ulong seed, ObjectCatalog? catalog, int population = 30) =>
        ScenarioRunner.Create(seed: seed, initialPopulation: population, objectCatalog: catalog);

    private static Npc Resident(WorldState world) => world.Npcs.First(n => n.IsAlive && n.Household is not null);

    private static void PutAtHome(WorldState world, Npc npc) =>
        npc.MoveTo(world.FindHousehold(npc.Household!.Value)!.Location, world.CurrentDate.TotalHours);

    // ---- formato ----

    [Fact]
    public void Effects_and_interactions_are_read_from_the_scenario()
    {
        var bed = Catalog(bedEffect: "sleep:0.5", tweak: r => Piece(r, "bed")["interactions"] = new JsonArray(
            new JsonObject { ["action"] = 1, ["profession"] = 2, ["skill"] = new JsonObject { ["id"] = 7, ["min"] = 3.5 } })).Find("bed")!;

        Assert.Equal(new ObjectEffect("sleep", 0.5), bed.Effects!.Single());
        var interaction = bed.Interactions!.Single();
        Assert.Equal(1, interaction.Action);
        Assert.Equal(2, interaction.Profession);
        Assert.Equal(7, interaction.SkillId);
        Assert.Equal(3.5, interaction.SkillMin);
    }

    [Fact]
    public void An_object_without_effects_or_interactions_has_none_and_the_json_omits_them()
    {
        var table = Catalog().Find("table")!;

        Assert.Null(table.Effects);
        Assert.Null(table.Interactions);
        var json = System.Text.Json.JsonSerializer.Serialize(table, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.DoesNotContain("effects", json);
        Assert.DoesNotContain("interactions", json);
    }

    [Theory]
    [InlineData("sleep", "effects[0]")]
    [InlineData(":0.5", "effects[0]")]
    [InlineData("fly:0.5", "alvo 'fly' desconhecido")]
    [InlineData("sleep:abc", "magnitude")]
    [InlineData("sleep:-1", "magnitude")]
    [InlineData("sleep:-3", "magnitude")]
    [InlineData("sleep:NaN", "magnitude")]
    public void An_invalid_effect_token_is_refused_with_its_path(string token, string expected)
    {
        var result = ObjectCatalogScenarioLoader.Load(Root(bedEffect: token));

        Assert.False(result.IsSuccess);
        Assert.Contains("Objects[", result.Error);
        Assert.Contains(expected, result.Error);
    }

    [Fact]
    public void A_negative_magnitude_above_minus_one_is_accepted()
    {
        Assert.Equal(-0.5, Catalog(bedEffect: "sleep:-0.5").Find("bed")!.Effects!.Single().Magnitude);
    }

    [Fact]
    public void Invalid_interactions_are_refused_with_their_path()
    {
        string? Error(JsonNode interactions) => ObjectCatalogScenarioLoader.Load(Root(tweak: r => Piece(r, "bed")["interactions"] = interactions)).Error;

        Assert.Contains("interactions[0].action", Error(new JsonArray(new JsonObject { ["action"] = 99 })));
        Assert.Contains("interactions[1].action", Error(new JsonArray(new JsonObject { ["action"] = 1 }, new JsonObject { ["action"] = 1 })));
        Assert.Contains("interactions[0].profession", Error(new JsonArray(new JsonObject { ["action"] = 1, ["profession"] = "x" })));
        Assert.Contains("interactions[0].skill.id", Error(new JsonArray(new JsonObject { ["action"] = 1, ["skill"] = new JsonObject { ["min"] = 1 } })));
        Assert.Contains("interactions[0].skill.min", Error(new JsonArray(new JsonObject { ["action"] = 1, ["skill"] = new JsonObject { ["id"] = 1, ["min"] = -1 } })));
        Assert.Contains("interactions", Error(new JsonObject()));
    }

    [Fact]
    public void Effects_and_interactions_survive_the_snapshot_round_trip()
    {
        var world = World(7, Catalog(bedEffect: "sleep:0.5", tweak: r => Piece(r, "bed")["interactions"] = new JsonArray(new JsonObject { ["action"] = 1 }))).World;

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        var bed = restored.ObjectCatalog!.Find("bed")!;
        Assert.Equal(0.5, bed.Effects!.Single().Magnitude);
        Assert.Equal(1, bed.Interactions!.Single().Action);
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
    }

    // ---- interações ----

    [Fact]
    public void Without_interactions_anyone_may_use_an_object_that_has_a_use_point()
    {
        var npc = Resident(World(3, Catalog()).World);

        Assert.True(ObjectUse.Allows(Catalog().Find("bed")!, ActionType.Sleep, npc));
    }

    [Fact]
    public void With_interactions_only_the_listed_actions_are_allowed()
    {
        var bed = Catalog(tweak: r => Piece(r, "bed")["interactions"] = new JsonArray(new JsonObject { ["action"] = (int)ActionType.Sleep })).Find("bed")!;
        var npc = Resident(World(3, Catalog()).World);

        Assert.True(ObjectUse.Allows(bed, ActionType.Sleep, npc));
        Assert.False(ObjectUse.Allows(bed, ActionType.Eat, npc));
    }

    [Fact]
    public void A_profession_requirement_blocks_other_professions()
    {
        var world = World(3, Catalog()).World;
        var npc = Resident(world);
        var forProfession = (int professionId) => Catalog(tweak: r => Piece(r, "bed")["interactions"] = new JsonArray(
            new JsonObject { ["action"] = 1, ["profession"] = professionId })).Find("bed")!;

        Assert.True(ObjectUse.Allows(forProfession(npc.Profession.Id), ActionType.Sleep, npc));
        Assert.False(ObjectUse.Allows(forProfession(npc.Profession.Id + 1), ActionType.Sleep, npc));
    }

    [Fact]
    public void A_skill_requirement_blocks_those_below_the_minimum()
    {
        var npc = Resident(World(3, Catalog()).World);
        var withMinimum = (double min) => Catalog(tweak: r => Piece(r, "bed")["interactions"] = new JsonArray(
            new JsonObject { ["action"] = 1, ["skill"] = new JsonObject { ["id"] = 5, ["min"] = min } })).Find("bed")!;
        double skill = npc.Skills.Get(new SkillType(5));

        Assert.True(ObjectUse.Allows(withMinimum(skill), ActionType.Sleep, npc));
        Assert.False(ObjectUse.Allows(withMinimum(skill + 0.5), ActionType.Sleep, npc));
    }

    [Fact]
    public void The_first_piece_the_npc_may_use_serves_the_action()
    {
        var npc = Resident(World(3, Catalog()).World);
        var catalog = Catalog(tweak: r =>
        {
            var cot = (JsonObject)Piece(r, "bed").DeepClone();
            cot["id"] = "cot";
            ((JsonArray)r["Objects"]!).Add(cot);
            ((JsonObject)r["Furnishings"]!)[HouseTypeId.ToString()] = new JsonArray("bed", "cot");
            Piece(r, "bed")["interactions"] = new JsonArray(new JsonObject { ["action"] = 1, ["profession"] = npc.Profession.Id + 1 });
        });

        Assert.Equal("cot", ObjectUse.Serving(catalog, HouseTypeId, ActionType.Sleep, npc)!.Id);
    }

    // ---- efeito na duração ----

    [Fact]
    public void Without_a_catalog_the_duration_is_the_action_catalogs()
    {
        var (world, _) = World(3, null);
        var npc = Resident(world);
        PutAtHome(world, npc);

        Assert.Equal(8, ObjectUse.DurationHours(world, npc, ActionType.Sleep, world.ActionCatalog));
        Assert.Equal(2, ObjectUse.DurationHours(world, npc, ActionType.Eat, world.ActionCatalog));
    }

    [Theory]
    [InlineData("sleep:1.0", 4)] // 8 / 2
    [InlineData("sleep:0.5", 6)] // 8 / 1.5 = 5.33 -> 6
    [InlineData("sleep:-0.5", 16)] // 8 / 0.5
    [InlineData("sleep:0", 8)]
    [InlineData("sleep:100", 1)] // nunca menos que 1 h
    public void The_bed_effect_divides_the_sleep_duration_by_one_plus_the_magnitude(string effect, int expectedHours)
    {
        var (world, _) = World(3, Catalog(bedEffect: effect));
        var npc = Resident(world);
        PutAtHome(world, npc);

        Assert.Equal(expectedHours, ObjectUse.DurationHours(world, npc, ActionType.Sleep, world.ActionCatalog));
    }

    [Fact]
    public void The_table_effect_shortens_eating_and_does_not_touch_sleep()
    {
        var (world, _) = World(3, Catalog(tableEffect: "eat:1.0"));
        var npc = Resident(world);
        PutAtHome(world, npc);

        Assert.Equal(1, ObjectUse.DurationHours(world, npc, ActionType.Eat, world.ActionCatalog));
        Assert.Equal(8, ObjectUse.DurationHours(world, npc, ActionType.Sleep, world.ActionCatalog));
    }

    [Fact]
    public void A_sleep_effect_does_not_apply_to_eating_and_the_reverse()
    {
        var (world, _) = World(3, Catalog(bedEffect: "eat:1.0", tableEffect: "sleep:1.0"));
        var npc = Resident(world);
        PutAtHome(world, npc);

        Assert.Equal(8, ObjectUse.DurationHours(world, npc, ActionType.Sleep, world.ActionCatalog));
        Assert.Equal(2, ObjectUse.DurationHours(world, npc, ActionType.Eat, world.ActionCatalog));
    }

    [Fact]
    public void Away_from_home_the_object_has_no_effect_on_the_duration()
    {
        var (world, _) = World(3, Catalog(bedEffect: "sleep:1.0"));
        var npc = Resident(world);
        var home = world.FindHousehold(npc.Household!.Value)!.Location;
        npc.MoveTo(new LivingWorld.Domain.Geography.CellCoord(home.X + 1, home.Y), world.CurrentDate.TotalHours);

        Assert.Equal(8, ObjectUse.DurationHours(world, npc, ActionType.Sleep, world.ActionCatalog));
    }

    [Fact]
    public void A_dwelling_whose_type_has_no_such_furniture_gets_no_effect()
    {
        var (world, _) = World(3, Catalog(bedEffect: "sleep:1.0", tweak: r => ((JsonObject)r["Furnishings"]!)[HouseTypeId.ToString()] = new JsonArray("table")));
        var npc = Resident(world);
        PutAtHome(world, npc);

        Assert.Equal(8, ObjectUse.DurationHours(world, npc, ActionType.Sleep, world.ActionCatalog));
    }

    [Fact]
    public void An_npc_who_fails_the_requirement_gets_no_effect()
    {
        var (world, _) = World(3, null);
        var npc = Resident(world);
        var catalog = Catalog(bedEffect: "sleep:1.0", tweak: r => Piece(r, "bed")["interactions"] = new JsonArray(
            new JsonObject { ["action"] = 1, ["profession"] = npc.Profession.Id + 1 }));
        var (restricted, _) = World(3, catalog);
        var resident = restricted.FindNpc(npc.Id)!;
        PutAtHome(restricted, resident);

        Assert.Equal(8, ObjectUse.DurationHours(restricted, resident, ActionType.Sleep, restricted.ActionCatalog));
    }

    private static void RestEverything(Npc npc, long tick)
    {
        npc.SetSleep(100, tick);
        npc.SetHunger(100, tick);
        npc.SetThirst(100, tick);
        npc.SetSocial(100, tick);
    }

    private static long WakeOfSleeper(ulong seed, ObjectCatalog catalog, long start)
    {
        var (world, _) = World(seed, catalog);
        var npc = Resident(world);
        PutAtHome(world, npc);
        RestEverything(npc, start);
        npc.SetCurrentAction(ActionType.Sleep, start);
        return LivingWorld.Simulation.Behavior.Needs.NpcWakeScheduler.ComputeNextWakeTick(npc, world.NeedsRules, world.ActionCatalog, now: start, world);
    }

    [Fact]
    public void The_wake_scheduler_wakes_the_sleeper_at_the_shortened_end_when_nothing_else_comes_first()
    {
        var starts = Enumerable.Range(0, 48).Select(i => 200L + i).ToList();
        var plain = starts.Select(start => WakeOfSleeper(3, Catalog(bedEffect: "sleep:0"), start)).ToList();
        var bed = starts.Select(start => WakeOfSleeper(3, Catalog(bedEffect: "sleep:1.0"), start)).ToList();

        Assert.All(starts.Zip(plain, bed), t => Assert.True(t.Third <= t.Second, "o efeito nunca atrasa o despertar"));
        Assert.Contains(starts.Zip(plain, bed), t => t.Third == t.First + 4 && t.Second > t.Third);
    }

    [Fact]
    public void No_sleep_or_meal_outlasts_its_shortened_duration_when_the_npc_is_at_home()
    {
        foreach (ulong seed in new ulong[] { 2, 3, 4 })
        {
            var (world, clock) = World(seed, Catalog(bedEffect: "sleep:1.0", tableEffect: "eat:1.0"), population: 30);
            int checkedAtHome = 0;
            for (int t = 0; t < 24 * 12; t++)
            {
                clock.Run(world, 1);
                long now = world.CurrentDate.TotalHours;
                foreach (var npc in world.Npcs)
                {
                    if (!npc.IsAlive || npc.CurrentAction is not ({ } action and (ActionType.Sleep or ActionType.Eat))) continue;
                    if (npc.Household is not { } hid || world.FindHousehold(hid) is not { } household || npc.CurrentLocation != household.Location) continue;
                    int allowed = ObjectUse.DurationHours(world, npc, action, world.ActionCatalog);
                    checkedAtHome++;
                    Assert.True(now - npc.ActionStartedAtTick <= allowed,
                        $"seed {seed}: {action} do NPC {npc.Id.Value} dura {now - npc.ActionStartedAtTick} h e o permitido é {allowed} h");
                }
            }

            Assert.True(checkedAtHome > 0, "ninguém dormiu nem comeu em casa na janela");
        }
    }

    [Fact]
    public void The_rest_and_meal_presentations_report_the_shortened_remaining_time()
    {
        var (world, _) = World(3, Catalog(bedEffect: "sleep:1.0", tableEffect: "eat:1.0"));
        var npc = Resident(world);
        PutAtHome(world, npc);
        long now = world.CurrentDate.TotalHours;

        npc.SetCurrentAction(ActionType.Sleep, now);
        var rest = LivingWorld.Simulation.Behavior.Resolvers.RestPresentation.Of(world, npc)!;
        npc.SetCurrentAction(ActionType.Eat, now);
        var food = LivingWorld.Simulation.Behavior.Resolvers.FoodPresentation.Of(world, npc)!;

        Assert.Equal(4, rest.RemainingHours);
        Assert.Equal(1, food.RemainingHours);
    }

    [Fact]
    public void The_process_progress_uses_the_shortened_duration()
    {
        var (world, clock) = World(3, Catalog(bedEffect: "sleep:1.0"));
        var npc = Resident(world);
        PutAtHome(world, npc);
        npc.SetCurrentAction(ActionType.Sleep, world.CurrentDate.TotalHours - 2);

        var process = LivingWorld.Simulation.Behavior.Resolvers.RestPresentation.ToProcess(world, npc);

        Assert.Equal(0.5, process.Progress, 6);
        _ = clock;
    }

    [Fact]
    public void The_comfort_is_the_expectation_of_the_home_even_when_the_npc_is_away()
    {
        var (world, _) = World(3, Catalog(bedEffect: "sleep:0.5", tableEffect: "eat:0.25"));
        var npc = Resident(world);
        var home = world.FindHousehold(npc.Household!.Value)!.Location;
        npc.MoveTo(new LivingWorld.Domain.Geography.CellCoord(home.X + 1, home.Y), world.CurrentDate.TotalHours);

        Assert.Equal(new ObjectComfort(0.5f, 0.25f), ObjectUse.ComfortOf(world, npc));
        Assert.Equal(8, ObjectUse.DurationHours(world, npc, ActionType.Sleep, world.ActionCatalog));
    }

    [Fact]
    public void Two_effects_on_the_same_target_add_up()
    {
        var (world, _) = World(3, Catalog(tweak: r => Piece(r, "bed")["effects"] = new JsonArray("sleep:0.25", "sleep:0.75", "eat:5")));
        var npc = Resident(world);
        PutAtHome(world, npc);

        Assert.Equal(4, ObjectUse.DurationHours(world, npc, ActionType.Sleep, world.ActionCatalog)); // 8 / (1 + 1.0)
    }

    [Fact]
    public void A_listed_interaction_is_open_when_there_is_no_npc_to_check()
    {
        var bed = Catalog(tweak: r => Piece(r, "bed")["interactions"] = new JsonArray(new JsonObject { ["action"] = 1, ["profession"] = 2 })).Find("bed")!;

        Assert.True(ObjectUse.Allows(bed, ActionType.Sleep, npc: null));
        Assert.False(ObjectUse.Allows(bed, ActionType.Eat, npc: null));
    }

    [Fact]
    public void Negative_requirement_ids_and_malformed_blocks_are_refused()
    {
        string? Error(Action<JsonObject> tweak) => ObjectCatalogScenarioLoader.Load(Root(tweak: tweak)).Error;

        Assert.Contains("interactions[0].profession", Error(r => Piece(r, "bed")["interactions"] = new JsonArray(new JsonObject { ["action"] = 1, ["profession"] = -1 })));
        Assert.Contains("interactions[0].skill.id", Error(r => Piece(r, "bed")["interactions"] = new JsonArray(new JsonObject { ["action"] = 1, ["skill"] = new JsonObject { ["id"] = -1, ["min"] = 1.0 } })));
        Assert.Contains("interactions[0].skill.min", Error(r => Piece(r, "bed")["interactions"] = new JsonArray(new JsonObject { ["action"] = 1, ["skill"] = new JsonObject { ["id"] = 1, ["min"] = -0.5 } })));
        Assert.Contains("effects", Error(r => Piece(r, "bed")["effects"] = "sleep:1"));
        Assert.Contains("Furnishings.03", Error(r => ((JsonObject)r["Furnishings"]!)["03"] = new JsonArray("bed")));
    }

    // ---- decisão (oportunidade) ----

    [Fact]
    public void The_comfort_is_the_dwellings_effect_and_zero_without_a_catalog()
    {
        var (with, _) = World(3, Catalog(bedEffect: "sleep:0.5", tableEffect: "eat:0.25"));
        var (without, _) = World(3, null);

        Assert.Equal(new ObjectComfort(0.5f, 0.25f), ObjectUse.ComfortOf(with, Resident(with)));
        Assert.Equal(default, ObjectUse.ComfortOf(without, Resident(without)));
    }

    [Fact]
    public void The_decision_context_carries_the_comfort_and_scoring_prefers_a_better_bed()
    {
        var (world, _) = World(3, Catalog(bedEffect: "sleep:1.0"));
        var npc = Resident(world);

        var context = DecisionContextBuilder.Build(world, npc, tick: 5);
        var plain = context with { Comfort = default, Needs = new NeedsSnapshot(100, 100, 40, 100) };
        var cozy = plain with { Comfort = new ObjectComfort(1.0f, 0f) };

        Assert.Equal(1.0, context.Comfort.Sleep);
        double plainScore = BehaviorDecisionSystem.UtilityBaseOf(plain, ActionType.Sleep, world.EconomyRules, default);
        double cozyScore = BehaviorDecisionSystem.UtilityBaseOf(cozy, ActionType.Sleep, world.EconomyRules, default);
        Assert.Equal(plainScore * 2.0, cozyScore, 9);
        Assert.True(plainScore > 0);
    }

    [Fact]
    public void The_eat_comfort_scales_the_eat_utility_and_leaves_other_actions_alone()
    {
        var (world, _) = World(3, Catalog(tableEffect: "eat:0.5"));
        var npc = Resident(world);
        var plain = DecisionContextBuilder.Build(world, npc, tick: 5) with { Comfort = default, Needs = new NeedsSnapshot(30, 30, 100, 100) };
        var cozy = plain with { Comfort = new ObjectComfort(0f, 0.5f) };

        Assert.Equal(BehaviorDecisionSystem.UtilityBaseOf(plain, ActionType.Eat, world.EconomyRules, default) * 1.5, BehaviorDecisionSystem.UtilityBaseOf(cozy, ActionType.Eat, world.EconomyRules, default), 9);
        Assert.Equal(BehaviorDecisionSystem.UtilityBaseOf(plain, ActionType.Sleep, world.EconomyRules, default), BehaviorDecisionSystem.UtilityBaseOf(cozy, ActionType.Sleep, world.EconomyRules, default));
        Assert.Equal(BehaviorDecisionSystem.UtilityBaseOf(plain, ActionType.Socialize, world.EconomyRules, default), BehaviorDecisionSystem.UtilityBaseOf(cozy, ActionType.Socialize, world.EconomyRules, default));
    }

    // ---- o mundo ----

    private static string NpcDigest(WorldState world)
    {
        var sb = new StringBuilder();
        foreach (var npc in world.Npcs.OrderBy(n => n.Id.Value))
            sb.Append(npc.Id.Value).Append('|').Append(npc.IsAlive).Append('|').Append(npc.CurrentAction).Append('|').Append(npc.ActionStartedAtTick)
                .Append('|').Append(npc.SleepAt(world.CurrentDate.TotalHours)).Append('|').Append(npc.HungerAt(world.CurrentDate.TotalHours))
                .Append('|').Append(npc.CurrentLocation.X).Append(',').Append(npc.CurrentLocation.Y).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static string Run(ulong seed, ObjectCatalog? catalog, int ticks)
    {
        var (world, clock) = World(seed, catalog);
        clock.Run(world, ticks);
        return NpcDigest(world);
    }

    private static double MeanSleepLevel(ulong seed, ObjectCatalog? catalog, int ticks)
    {
        var (world, clock) = World(seed, catalog);
        double sum = 0;
        long samples = 0;
        for (int t = 0; t < ticks; t++)
        {
            clock.Run(world, 1);
            long now = world.CurrentDate.TotalHours;
            foreach (var npc in world.Npcs)
            {
                if (!npc.IsAlive) continue;
                sum += npc.SleepAt(now);
                samples++;
            }
        }

        return samples == 0 ? 0 : sum / samples;
    }

    [Fact]
    public void Effects_of_zero_leave_the_npc_world_exactly_as_without_objects()
    {
        Assert.Equal(Run(11, null, 24 * 6), Run(11, Catalog(bedEffect: "sleep:0", tableEffect: "eat:0"), 24 * 6));
    }

    [Fact]
    public void Turning_the_effects_on_changes_the_world_the_npcs_live_in()
    {
        Assert.NotEqual(Run(11, Catalog(), 24 * 6), Run(11, Catalog(bedEffect: "sleep:1.0", tableEffect: "eat:1.0"), 24 * 6));
    }

    [Fact]
    public void The_same_seed_and_catalog_give_the_same_world_twice()
    {
        Assert.Equal(Run(5, Catalog(bedEffect: "sleep:0.5"), 24 * 4), Run(5, Catalog(bedEffect: "sleep:0.5"), 24 * 4));
        Assert.NotEqual(Run(5, Catalog(bedEffect: "sleep:0.5"), 24 * 4), Run(6, Catalog(bedEffect: "sleep:0.5"), 24 * 4));
    }

    [Fact]
    public void A_better_bed_keeps_the_npcs_better_rested_in_at_least_18_of_20_seeds()
    {
        int better = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            double basic = MeanSleepLevel(seed, Catalog(bedEffect: "sleep:0"), ticks: 24 * 8);
            double bed = MeanSleepLevel(seed, Catalog(bedEffect: "sleep:1.0"), ticks: 24 * 8);
            if (bed > basic) better++;
        }

        Assert.True(better >= 18, $"cama melhor deixou o sono médio maior em {better}/20 seeds");
    }

    [Fact]
    public void Faster_meals_and_sleep_keep_the_money_and_stock_accounting_closed_every_day()
    {
        var (world, clock) = World(9, Catalog(bedEffect: "sleep:1.0", tableEffect: "eat:1.0"), population: 40);
        long Total() => world.Npcs.Sum(n => n.Wallet.Amount) + world.Workplaces.Sum(w => w.Treasury.Amount) + world.Cities.Sum(c => c.Treasury.Amount);
        long initialTotal = Total() - (world.MoneyMinted.Amount - world.MoneyDestroyed.Amount);

        for (int day = 0; day < 90; day++)
        {
            clock.Run(world, 24);
            Assert.Equal(world.MoneyMinted.Amount - world.MoneyDestroyed.Amount, Total() - initialTotal);
            Assert.All(world.Households, h => Assert.All(h.Stock.Values, q => Assert.True(q >= 0, "estoque nunca fica negativo")));
        }

        Assert.Contains(world.Npcs, n => n.IsAlive);
    }
}
