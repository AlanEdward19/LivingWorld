using System.Text.Json.Nodes;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Population;

/// <summary>#46 fatia 2: genes de aparência herdados (mesmo modelo da personalidade), opt-in por período, com consumidor no cortejo.</summary>
public class AppearanceTests
{
    private static WorldRng Stream(ulong seed, long id) => new WorldRngRegistry(seed).StreamFor("appearance", id);

    private static int[] Genes(Appearance a) => [a.SkinTone, a.HairTone, a.HairCurl, a.Build];

    private static double Distance(Appearance a, Appearance b) =>
        Math.Sqrt(Genes(a).Zip(Genes(b), (x, y) => (double)(x - y) * (x - y)).Sum());

    private static double Variance(IReadOnlyCollection<double> v)
    {
        double mean = v.Average();
        return v.Sum(x => (x - mean) * (x - mean)) / (v.Count - 1);
    }

    [Fact]
    public void Create_rejects_a_gene_outside_zero_to_hundred()
    {
        Assert.False(Appearance.Create(101, 0, 0, 0).IsSuccess);
        Assert.False(Appearance.Create(0, -1, 0, 0).IsSuccess);
        Assert.True(Appearance.Create(0, 100, 50, 7).IsSuccess);
    }

    [Fact]
    public void With_weight_zero_the_child_is_exactly_the_independent_roll()
    {
        var mother = Appearance.RollFrom(Stream(1, 1));
        var father = Appearance.RollFrom(Stream(1, 2));
        for (long id = 10; id < 60; id++)
            Assert.Equal(Appearance.RollFrom(Stream(9, id)), Appearance.Inherit(mother, father, Stream(9, id), 0.0));
    }

    [Fact]
    public void Genes_stay_in_range_even_with_parents_at_the_extremes()
    {
        var low = new Appearance(0, 0, 0, 0);
        var high = new Appearance(100, 100, 100, 100);
        for (long id = 0; id < 300; id++)
        {
            Assert.All(Genes(Appearance.Inherit(low, low, Stream(3, id), 1.0)), g => Assert.InRange(g, 0, 100));
            Assert.All(Genes(Appearance.Inherit(high, high, Stream(3, id), 1.0)), g => Assert.InRange(g, 0, 100));
        }
    }

    [Fact]
    public void Children_look_more_like_their_parents_than_like_strangers_with_a_z_of_at_least_three()
    {
        const int pairs = 200;
        var parents = Enumerable.Range(0, pairs + 1)
            .Select(i => (Mother: Appearance.RollFrom(Stream(11, 10_000 + i)), Father: Appearance.RollFrom(Stream(11, 20_000 + i))))
            .ToList();
        var children = Enumerable.Range(0, pairs + 1)
            .Select(i => Appearance.Inherit(parents[i].Mother, parents[i].Father, Stream(11, i), 0.6)).ToList();

        // distância à média dos pais: dos próprios contra a dos pais do vizinho
        double Mid(Appearance m, Appearance f, Appearance c) =>
            Distance(c, new Appearance((m.SkinTone + f.SkinTone) / 2, (m.HairTone + f.HairTone) / 2, (m.HairCurl + f.HairCurl) / 2, (m.Build + f.Build) / 2));
        var own = Enumerable.Range(0, pairs).Select(i => Mid(parents[i].Mother, parents[i].Father, children[i])).ToArray();
        var stranger = Enumerable.Range(0, pairs).Select(i => Mid(parents[i + 1].Mother, parents[i + 1].Father, children[i])).ToArray();

        double z = (stranger.Average() - own.Average()) / Math.Sqrt(Variance(own) / pairs + Variance(stranger) / pairs);
        Assert.True(z >= 3, $"z = {z:F2} (dos pais {own.Average():F1}, de estranhos {stranger.Average():F1})");
    }

    [Fact]
    public void Without_inheritance_children_are_not_closer_to_their_parents_than_to_strangers()
    {
        const int pairs = 200;
        var parents = Enumerable.Range(0, pairs + 1)
            .Select(i => (Mother: Appearance.RollFrom(Stream(11, 10_000 + i)), Father: Appearance.RollFrom(Stream(11, 20_000 + i))))
            .ToList();
        var children = Enumerable.Range(0, pairs + 1)
            .Select(i => Appearance.Inherit(parents[i].Mother, parents[i].Father, Stream(11, i), 0.0)).ToList();
        double own = Enumerable.Range(0, pairs).Average(i => Distance(children[i], parents[i].Mother));
        double stranger = Enumerable.Range(0, pairs).Average(i => Distance(children[i], parents[i + 1].Mother));
        Assert.True(Math.Abs(own - stranger) < 8, $"controle: {own:F1} contra {stranger:F1}");
    }

    [Fact]
    public void Similarity_is_one_for_identical_genes_and_zero_for_opposite_extremes()
    {
        Assert.Equal(1.0, Appearance.Similarity(new Appearance(10, 20, 30, 40), new Appearance(10, 20, 30, 40)));
        Assert.Equal(0.0, Appearance.Similarity(new Appearance(0, 0, 0, 0), new Appearance(100, 100, 100, 100)));
    }

    [Fact]
    public void Attraction_rewards_similar_looks_and_a_pair_without_looks_scores_as_before()
    {
        var withLook = ScenarioRunner.DefaultFamilyRules.WithAppearanceAttraction(1.0);
        var population = ScenarioRunner.DefaultPopulationRules;
        var (world, _) = ScenarioRunner.Create(seed: 3, initialPopulation: 0);
        var now = world.CurrentDate;
        Npc Make(long id, Appearance? look) => new(
            new NpcId(id), $"n{id}", Sex.Female, now.AddYears(-25), new CultureId(1), default, null, null, null, 100,
            Personality.RollFrom(new WorldRngRegistry(1).StreamFor("p", id)), new ProfessionType(1), default, appearance: look);

        var me = Make(1, new Appearance(20, 20, 20, 20));
        var alike = Make(2, new Appearance(22, 18, 20, 21));
        var unlike = Make(3, new Appearance(95, 90, 99, 80));
        Assert.True(
            CourtshipSystem.AttractionScore(me, alike, null, null, withLook, population, now)
            > CourtshipSystem.AttractionScore(me, unlike, null, null, withLook, population, now));

        // sem aparência em um dos dois, o fator some da média: o score é o do cenário sem o fator
        var bare = Make(4, null);
        Assert.Equal(
            CourtshipSystem.AttractionScore(me, bare, null, null, ScenarioRunner.DefaultFamilyRules, population, now),
            CourtshipSystem.AttractionScore(me, bare, null, null, withLook, population, now));
    }

    // --- mundo ---

    private static (string Canonical, WorldState World) Run(AppearanceRules? rules, long ticks)
    {
        var (world, clock) = ScenarioRunner.Create(seed: 5, initialPopulation: 30, appearanceRules: rules);
        clock.Run(world, ticks);
        return (WorldSnapshot.CanonicalHash(world), world);
    }

    [Fact]
    public void Without_the_rules_nobody_has_looks_and_the_snapshot_has_no_trace_of_them()
    {
        var (world, _) = ScenarioRunner.Create(seed: 5, initialPopulation: 30);
        Assert.All(world.Npcs, n => Assert.Null(n.Appearance));
        string json = WorldSnapshot.Serialize(world);
        Assert.DoesNotContain("Appearance", json);
    }

    [Fact]
    public void With_the_rules_every_founder_has_looks_and_the_same_seed_gives_the_same_world()
    {
        var rules = new AppearanceRules(0.6);
        var (world, _) = ScenarioRunner.Create(seed: 5, initialPopulation: 30, appearanceRules: rules);
        Assert.All(world.Npcs, n => Assert.NotNull(n.Appearance));
        Assert.Equal(
            WorldSnapshot.CanonicalHash(world),
            WorldSnapshot.CanonicalHash(ScenarioRunner.Create(seed: 5, initialPopulation: 30, appearanceRules: rules).World));
    }

    [Fact]
    public void The_looks_survive_a_snapshot_round_trip()
    {
        var (world, _) = ScenarioRunner.Create(seed: 5, initialPopulation: 30, appearanceRules: new AppearanceRules(0.6, 0.5));
        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.Equal(world.AppearanceRules, restored.AppearanceRules);
        Assert.Equal(world.Npcs.Select(n => n.Appearance).ToList(), restored.Npcs.Select(n => n.Appearance).ToList());
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored));
    }

    [Fact]
    public void Turning_looks_on_changes_the_canonical_hash_as_the_cohesion_rule_requires()
    {
        string off = Run(null, 24 * 30).Canonical;
        string on = Run(new AppearanceRules(0.6), 24 * 30).Canonical;
        Assert.NotEqual(off, on);
    }

    [Fact]
    public void Newborns_inherit_looks_from_their_parents_when_the_rules_are_on()
    {
        var (_, world) = Run(new AppearanceRules(0.6), 24 * 365 * 2);
        var born = world.Npcs.Where(n => n.MotherId is not null).ToList();
        Assert.NotEmpty(born);
        Assert.All(born, n => Assert.NotNull(n.Appearance));
    }

    // --- bloco do período e autoria ---

    private static JsonObject Scenario(JsonNode? appearance, JsonNode? authored)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LivingWorld.sln"))) dir = dir.Parent;
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "scenarios", "default.json")))!.AsObject();
        json["InitialPopulation"] = 0;
        if (appearance is not null) json["Appearance"] = appearance;
        if (authored is not null) json["AuthoredHouseholds"] = authored;
        return json;
    }

    private static JsonArray Family(params string[] people) =>
        JsonNode.Parse("[{\"Members\":[" + string.Join(",", people) + "]}]")!.AsArray();

    private static string Person(string name, string sex, int age, int profession, string extra = "") =>
        $"{{\"Name\":\"{name}\",\"Sex\":\"{sex}\",\"AgeYears\":{age},\"ProfessionId\":{profession}{extra}}}";

    private static LivingWorld.Domain.Shared.Result<(WorldState World, WorldClock Clock)> Load(string appearanceBlock) =>
        ScenarioLoaderV2.LoadWorld(Scenario(JsonNode.Parse(appearanceBlock), null).ToJsonString());

    [Fact]
    public void The_block_is_validated_in_the_border()
    {
        Assert.False(Load("{\"Heritability\":1.5}").IsSuccess);
        Assert.False(Load("{\"Heritability\":0.5,\"AttractionWeight\":-1}").IsSuccess);
        Assert.False(Load("{}").IsSuccess);
        Assert.Null(Load("{\"Enabled\":false,\"Heritability\":0.5}").Value.World.AppearanceRules);
        Assert.Equal(0.5, Load("{\"Heritability\":0.5}").Value.World.AppearanceRules!.Heritability);
    }

    [Fact]
    public void Authored_looks_and_skills_are_applied_exactly_and_the_rest_is_rolled()
    {
        var block = JsonNode.Parse("{\"Heritability\":0.6}");
        var family = Family(
            Person("Ana", "Female", 30, 1,
                ",\"Appearance\":{\"SkinTone\":12,\"HairTone\":34,\"HairCurl\":56,\"Build\":78},\"Skills\":{\"0\":40,\"7\":15}"),
            Person("Beto", "Male", 32, 2));
        var world = ScenarioLoaderV2.LoadWorld(Scenario(block, family).ToJsonString()).Value.World;

        var ana = world.Npcs.Single(n => n.Name == "Ana");
        Assert.Equal(new Appearance(12, 34, 56, 78), ana.Appearance);
        Assert.Equal(40, ana.Skills.Get(new LivingWorld.Domain.Population.Skills.SkillType(0)), 3);
        Assert.Equal(15, ana.Skills.Get(new LivingWorld.Domain.Population.Skills.SkillType(7)), 3);
        Assert.NotNull(world.Npcs.Single(n => n.Name == "Beto").Appearance); // não autorada: sorteada
    }

    [Fact]
    public void Authored_looks_without_the_period_block_are_refused_naming_the_field()
    {
        var family = Family(Person("Ana", "Female", 30, 1,
            ",\"Appearance\":{\"SkinTone\":1,\"HairTone\":1,\"HairCurl\":1,\"Build\":1}"));
        var result = ScenarioLoaderV2.LoadWorld(Scenario(null, family).ToJsonString());
        Assert.False(result.IsSuccess);
        Assert.Contains("Appearance", result.Error);
    }

    [Theory]
    [InlineData(",\"Appearance\":{\"SkinTone\":101,\"HairTone\":1,\"HairCurl\":1,\"Build\":1}", "Appearance.SkinTone")]
    [InlineData(",\"Appearance\":{\"SkinTone\":1,\"HairTone\":1,\"HairCurl\":1}", "Appearance.Build")]
    [InlineData(",\"Skills\":{\"7\":101}", "Skills.7")]
    [InlineData(",\"Skills\":{\"x\":10}", "Skills")]
    public void Invalid_looks_or_skills_are_refused_naming_the_field(string extra, string field)
    {
        var block = JsonNode.Parse("{\"Heritability\":0.6}");
        var result = ScenarioLoaderV2.LoadWorld(Scenario(block, Family(Person("Ana", "Female", 30, 1, extra))).ToJsonString());
        Assert.False(result.IsSuccess);
        Assert.Contains(field, result.Error);
    }
}
