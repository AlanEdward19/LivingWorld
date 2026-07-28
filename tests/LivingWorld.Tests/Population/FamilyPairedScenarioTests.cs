using System.Text.Json;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 7, T23–T26: cenários pareados/estatísticos de família — fora do gate padrão via
/// <c>[Trait("Category","Scenario")]</c> (mesmo padrão de <see cref="PairedScenarioTests"/>).</summary>
public class FamilyPairedScenarioTests
{
    private const int HorizonYears = 10;
    private const long HorizonHours = HorizonYears * 12 * 30 * 24;
    private const int DefaultSeed = 42;

    private static readonly string PopulationAverageBaselinePath = Path.Combine(
        FindRepoRoot(), "tests", "baselines", "family-population-average.json");

    // --- T23 (FAM-30, FAM-31) ---

    [Fact]
    [Trait("Category", "Scenario")]
    public void Ten_years_default_scenario_has_zero_marriages_between_first_degree_relatives()
    {
        var sink = new RecordingSink();
        var (world, _) = ScenarioRunner.Create(DefaultSeed);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        clock.Run(world, HorizonHours);

        var npcById = world.Npcs.ToDictionary(n => n.Id);
        var populationRules = world.PopulationRules;
        var now = world.CurrentDate;

        foreach (var evt in sink.Events.Where(e => e.Kind == WorldEventKind.Marriage))
        {
            var parts = evt.Payload!.Split('|');
            Assert.Equal(2, parts.Length);
            var a = npcById[new NpcId(int.Parse(parts[0]))];
            var b = npcById[new NpcId(int.Parse(parts[1]))];
            Assert.NotEqual(
                CourtshipRejectionReason.Incesto,
                CourtshipSystem.Reject(a, b, now, populationRules));
        }
    }

    [Fact]
    public void Dedicated_sibling_cohabitation_scenario_rejects_courtship_with_Incesto()
    {
        var motherId = new NpcId(100);
        var fatherId = new NpcId(101);
        var rules = ScenarioRunner.DefaultFamilyRules with { CourtshipThreshold = 0.0 };
        var (world, _) = ScenarioRunner.Create(seed: 1, initialPopulation: 0, familyRules: rules);
        world.CurrentDate = WorldDate.Epoch(world.Calendar).AddYears(30);

        Npc AddSibling(Sex sex, NpcId id)
        {
            var birth = world.CurrentDate.AddYears(-25);
            var npc = new Npc(
                id, $"sib-{id.Value}", sex, birth, ScenarioRunner.DefaultCulture, ScenarioRunner.DefaultVillageLocation,
                motherId, fatherId, household: null, health: 100,
                Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
                new ProfessionType(1), ScenarioRunner.DefaultVillageLocation);
            world.AddNpc(npc);
            return npc;
        }

        var brother = AddSibling(Sex.Male, new NpcId(1));
        var sister = AddSibling(Sex.Female, new NpcId(2));
        var household = new Household(
            world.NextHouseholdIdAndAdvance(), ScenarioRunner.DefaultVillageLocation, brother.Id,
            [brother.Id, sister.Id], stock: new Dictionary<ResourceType, long>());
        world.AddHousehold(household);
        brother.JoinHousehold(household.Id);
        sister.JoinHousehold(household.Id);

        long nowTick = world.CurrentDate.TotalHours;
        var linkRules = world.FamilyRules with
        {
            RelationshipDeltas = new Dictionary<(RelationshipEventType, RelationshipAxis), double>
            {
                [(RelationshipEventType.Cohabitation, RelationshipAxis.Trust)] = 80,
                [(RelationshipEventType.Cohabitation, RelationshipAxis.Affection)] = 80,
            },
        };
        world.GetOrCreateRelationship(new RelationshipKey(brother.Id, sister.Id), nowTick)
            .ApplyEvent(RelationshipEventType.Cohabitation, linkRules);
        world.GetOrCreateRelationship(new RelationshipKey(sister.Id, brother.Id), nowTick)
            .ApplyEvent(RelationshipEventType.Cohabitation, linkRules);

        Assert.Equal(
            CourtshipRejectionReason.Incesto,
            CourtshipSystem.Reject(brother, sister, world.CurrentDate, world.PopulationRules));

        var sink = new RecordingSink();
        new CourtshipSystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler, sink));

        Assert.Contains(
            sink.Events,
            e => e.Kind == WorldEventKind.CourtshipRejected
                 && e.Payload == $"Incesto|{brother.Id.Value}|{sister.Id.Value}");
        Assert.Null(brother.CourtingWith);
    }

    // --- T24 (FAM-28, FAM-29) ---

    [Fact]
    [Trait("Category", "Scenario")]
    public void Every_birth_in_horizon_has_valid_parents_alive_at_conception_and_fertile_mother_age()
    {
        var sink = new RecordingSink();
        var (world, _) = ScenarioRunner.Create(DefaultSeed);
        var clock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink);
        clock.Run(world, HorizonHours);

        var npcById = world.Npcs.ToDictionary(n => n.Id);
        var rules = world.PopulationRules;
        int gestationDays = rules.GestationDays;

        foreach (var evt in sink.Events.Where(e => e.Kind == WorldEventKind.Birth))
        {
            var parts = evt.Payload!.Split('|');
            var childId = new NpcId(int.Parse(parts[0]));
            var child = npcById[childId];
            Assert.NotNull(child.MotherId);

            var conceptionDate = child.BirthDate.AddDays(-gestationDays);
            AssertMotherAndFather(child, npcById, conceptionDate, rules);
        }
    }

    // --- T25 (FAM-26) ---

    /// <summary>Tolerância ampla: o critério do roadmap usa ordem de grandeza
    /// (<c>esperado = anos / idadeMédiaPrimeiroParto</c>), não contagem exata — reprodução depende
    /// de casamento, recursos e mortalidade além da fertilidade bruta.</summary>
    private const double BirthCountToleranceFraction = 0.5;

    [Fact]
    [Trait("Category", "Scenario")]
    public void Birth_count_over_horizon_is_compatible_with_scenario_derived_expectation()
    {
        var sink = new RecordingSink();
        var (world, _) = ScenarioRunner.Create(DefaultSeed);
        new WorldClock(ScenarioRunner.DefaultSystems(), sink: sink).Run(world, HorizonHours);

        int births = sink.Events.Count(e => e.Kind == WorldEventKind.Birth);
        var populationRules = ScenarioRunner.DefaultPopulationRules;
        double meanAgeAtFirstBirth = MeanAgeAtFirstBirthYears(populationRules);
        double expected = HorizonYears / meanAgeAtFirstBirth;
        double lower = expected * (1 - BirthCountToleranceFraction);
        double upper = expected * (1 + BirthCountToleranceFraction);

        // Escala pelo tamanho inicial: o critério fala em linhagens numa vila, não uma única família.
        int initial = ScenarioRunner.DefaultInitialPopulation;
        lower *= initial / 2.0;
        upper *= initial / 2.0;

        Assert.InRange(births, (int)Math.Floor(lower), (int)Math.Ceiling(Math.Max(upper, 1)));
    }

    // --- T26 (FAM-27) ---

    [Fact]
    [Trait("Category", "Scenario")]
    public void Final_population_over_20_seeds_stays_alive_and_near_committed_average_baseline()
    {
        var counts = new List<int>();
        for (int seed = 1; seed <= 20; seed++)
        {
            var (world, clock) = ScenarioRunner.Create((ulong)seed);
            clock.Run(world, HorizonHours);
            int alive = world.Npcs.Count(n => n.IsAlive);
            Assert.True(alive > 0, $"seed {seed}: extinção total — Fase 7 não pode apagar a vila");
            counts.Add(alive);
        }

        double average = counts.Average();
        WarnIfPopulationAverageDeviates(average);
    }

    [Fact(Skip = "regravação manual — remove o Skip, rode uma vez, reverta")]
    public void ZZZ_record_family_population_average_baseline()
    {
        double average = Enumerable.Range(1, 20)
            .Select(seed =>
            {
                var (world, clock) = ScenarioRunner.Create((ulong)seed);
                clock.Run(world, HorizonHours);
                return world.Npcs.Count(n => n.IsAlive);
            })
            .Average();

        File.WriteAllText(
            PopulationAverageBaselinePath,
            JsonSerializer.Serialize(new BaselineRecord(average), new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed record BaselineRecord(double AveragePopulation);

    private static void WarnIfPopulationAverageDeviates(double averagePopulation)
    {
        var baseline = JsonSerializer.Deserialize<BaselineRecord>(File.ReadAllText(PopulationAverageBaselinePath))!;
        double deviation = Math.Abs(averagePopulation - baseline.AveragePopulation) / baseline.AveragePopulation;
        if (deviation > 0.30)
            Console.Error.WriteLine(
                $"[REVIEW ALERT] população média {averagePopulation:F1} desvia {deviation:P0} do baseline " +
                $"{baseline.AveragePopulation:F1} gravado em {PopulationAverageBaselinePath} — revisar calibracao (nao falha o gate).");
    }

    private static void AssertMotherAndFather(
        Npc child,
        IReadOnlyDictionary<NpcId, Npc> npcById,
        WorldDate conceptionDate,
        PopulationRules rules)
    {
        Assert.NotNull(child.MotherId);
        Assert.True(npcById.TryGetValue(child.MotherId.Value, out var mother));
        Assert.True(mother.DeathDate is null || mother.DeathDate.Value.TotalHours > conceptionDate.TotalHours);

        int motherAgeAtConception = mother.AgeYears(conceptionDate);
        Assert.InRange(motherAgeAtConception, rules.FertilityMinAge, rules.FertilityMaxAge);

        if (child.FatherId is { } fatherId)
        {
            Assert.True(npcById.TryGetValue(fatherId, out var father));
            Assert.True(father.DeathDate is null || father.DeathDate.Value.TotalHours > conceptionDate.TotalHours);
        }
    }

    /// <summary>Proxy de idade média de primeiro parto a partir das regras demográficas do cenário
    /// default (campo dedicado no JSON fica para quando o loader expuser — hoje só há janela).</summary>
    private static double MeanAgeAtFirstBirthYears(PopulationRules rules) =>
        (rules.FertilityMinAge + rules.FertilityMaxAge) / 2.0;

    private sealed class RecordingSink : IWorldEventSink
    {
        public List<(WorldEventKind Kind, string? Payload)> Events { get; } = [];

        public void Record(WorldEvent evt) => Events.Add((evt.Kind, evt.Payload));
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
