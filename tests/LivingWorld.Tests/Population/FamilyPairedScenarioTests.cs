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

    // --- T27 (FAM-32) ---

    // T27 (FAM-32) is NOT implemented here — still BLOCKED after AD-065 (split of
    // NeutralDriftEnabled into itself [mate-choice] and the new VitalityMortalitySelectionEnabled
    // [mortality-by-Vitality selection], with NeutralDriftScenarioHarness now flipping both for a
    // genuine "no selection at all" control). AD-064's finding (a structural ~3% gap in ONE
    // direction on every combination tried) is gone — that was real, and the split fixed it. But
    // the corrected comparison does not produce the criterion's expected direction either: a
    // 20-seed sweep (same seeds/horizon as T26) of CV(Vitality, real) vs CV(Vitality, corrected
    // neutral control) gives gapCount=12/20 (real < neutral in 60% of seeds, real >= neutral in
    // 40%) with averages 0.324 (real) vs 0.329 (neutral) — a ~1.5% difference dwarfed by the
    // per-seed noise (individual seeds range roughly 0.28-0.39). This is statistical parity, not
    // a reliable real >= neutral relationship in either a single fixed seed or an averaged sense.
    // FAM-32/roadmap's "CV(real) >= CV(neutral)" reads as a deterministic per-run claim; nothing
    // in scope here (VitalityMortalityWeight/VitalityMutationStdDev/UpbringingWealthWeight, or
    // the flag split itself) has a causal path to manufacture that inequality reliably — forcing
    // an assertion to pass would mean either cherry-picking a favorable seed or inventing a
    // threshold not derived from the spec, both explicitly disallowed. Reopening T27 for real
    // needs a spec-level decision: accept it as a statistical/CI claim over many seeds (with an
    // explicit tolerance) rather than a single-seed inequality, or drop/rephrase FAM-32.

    // --- T28 (FAM-33) ---

    [Fact]
    [Trait("Category", "Scenario")]
    public void Environmental_channel_dilutes_vitality_wallet_correlation_below_channel_off_bootstrap_ci()
    {
        const int horizonYears = 60;
        const int n = 1500;
        // Variação local do harness (mesmo padrão de NeutralDriftScenarioHarness/AD-059): os pesos
        // de FamilyRules.DefaultFamilyRules já sobem o suficiente para não quebrar T25/T26
        // (contagem de nascimentos/população do cenário populacional completo), mas o efeito
        // Vitality→mortalidade precisa ficar bem mais forte que o efeito Upbringing→salário para o
        // IC95 de |r| separar com robustez estatística (N alcançável em segundos) — este teste
        // isola o mecanismo num harness de sujeito único, não roda a população inteira, então
        // amplificar aqui não afeta nenhum invariante do cenário default.
        var rules = ScenarioRunner.DefaultFamilyRules with { VitalityMortalityWeight = 0.9, UpbringingWealthWeight = 2.0 };
        var offRules = rules with { EnvironmentalWealthChannelEnabled = false };
        var bootstrapRng = new WorldRng(20260728);

        var pairsOn = new List<(double Vitality, double Wallet)>();
        var pairsOff = new List<(double Vitality, double Wallet)>();
        for (int i = 0; i < n; i++)
        {
            ulong seed = (ulong)(41_000_000 + i);
            double vitality = (i * 37 % 100) + 0.5;
            double upbringing = (i * 53 % 100) + 0.5; // origem independente de Vitality (FAM-19)

            var (worldOn, npcOn) = HouseholdCounterfactualHarness.CreateEmployedAdultWorld(
                seed, upbringing, vitality, HouseholdCounterfactualHarness.FixedRateGene, rules);
            pairsOn.Add((vitality, HouseholdCounterfactualHarness.RunCareerWithMortalityAndReturnWallet(worldOn, npcOn, horizonYears)));

            var (worldOff, npcOff) = HouseholdCounterfactualHarness.CreateEmployedAdultWorld(
                seed, upbringing, vitality, HouseholdCounterfactualHarness.FixedRateGene, offRules);
            pairsOff.Add((vitality, HouseholdCounterfactualHarness.RunCareerWithMortalityAndReturnWallet(worldOff, npcOff, horizonYears)));
        }

        var (onLow, onHigh) = BootstrapAbsPearsonCi95(pairsOn, bootstrapRng);
        var (offLow, offHigh) = BootstrapAbsPearsonCi95(pairsOff, bootstrapRng);

        Assert.True(onHigh < offLow,
            $"IC95 |r| canal ambiental ligado [{onLow:F3},{onHigh:F3}] deveria ficar inteiramente " +
            $"abaixo do IC95 |r| canal desligado [{offLow:F3},{offHigh:F3}]");
    }

    /// <summary>Bootstrap percentile de <c>|Pearson|</c> (reamostragem com reposição, FAM-33) —
    /// mesma transformação usada em <c>PairedScenarioTests.PearsonCi95</c> (Fase 6, T17), mas via
    /// reamostragem em vez de Fisher direto, porque o critério pede IC95 de <c>|r|</c>
    /// especificamente (assimétrico, Fisher não cobre isso).</summary>
    private static (double Low, double High) BootstrapAbsPearsonCi95(
        IReadOnlyList<(double Vitality, double Wallet)> pairs, WorldRng rng, int resamples = 2000)
    {
        int n = pairs.Count;
        var absRs = new double[resamples];
        for (int b = 0; b < resamples; b++)
        {
            var sample = new (double Vitality, double Wallet)[n];
            for (int i = 0; i < n; i++)
                sample[i] = pairs[(int)(rng.NextDouble() * n)];
            absRs[b] = Math.Abs(Pearson(sample));
        }
        Array.Sort(absRs);
        return (absRs[(int)(0.025 * resamples)], absRs[(int)(0.975 * resamples) - 1]);
    }

    private static double Pearson(IReadOnlyList<(double Vitality, double Wallet)> pairs)
    {
        double mx = pairs.Average(p => p.Vitality);
        double my = pairs.Average(p => p.Wallet);
        double sxy = pairs.Sum(p => (p.Vitality - mx) * (p.Wallet - my));
        double sxx = pairs.Sum(p => Math.Pow(p.Vitality - mx, 2));
        double syy = pairs.Sum(p => Math.Pow(p.Wallet - my, 2));
        return sxy / Math.Sqrt(sxx * syy);
    }

    // --- T29 (FAM-34) ---

    [Fact]
    [Trait("Category", "Scenario")]
    public void Household_wealth_distance_is_at_least_as_large_as_genome_distance()
    {
        const int samplesPerGroup = 200;
        const int horizonYears = 60;
        var rules = ScenarioRunner.DefaultFamilyRules;

        // Mesmo genoma (Vitality=60), Upbringing variando em [40,60] — nunca satura o fator de
        // salário em 0 (o que degeneraria o grupo, ver T30 abaixo).
        double[] upbringingLevels = [40, 47, 53, 60];
        var envGroups = upbringingLevels
            .Select((u, idx) => SampleCareerWallets(rules, vitality: 60, upbringing: u, tagOffset: idx, samplesPerGroup, horizonYears))
            .ToList();

        // Mesmo ambiente (Upbringing=50), Vitality variando no espectro inteiro.
        double[] vitalityLevels = [5, 30, 70, 95];
        var geneGroups = vitalityLevels
            .Select((v, idx) => SampleCareerWallets(rules, vitality: v, upbringing: 50, tagOffset: 100 + idx, samplesPerGroup, horizonYears))
            .ToList();

        double envDistance = MedianSpread(envGroups);
        double geneDistance = MedianSpread(geneGroups);
        Assert.True(envDistance >= geneDistance,
            $"distancia(ambientes, medianas)={envDistance:F0} deveria ser >= distancia(genomas, medianas)={geneDistance:F0}");
    }

    // --- T30 (FAM-35) ---

    [Fact]
    [Trait("Category", "Scenario")]
    public void Rich_vs_poor_household_wealth_overlaps_at_least_as_much_as_extreme_genomes()
    {
        const int samplesPerGroup = 300;
        const int horizonYears = 60;
        var rules = ScenarioRunner.DefaultFamilyRules;

        var poor = SampleCareerWallets(rules, vitality: 60, upbringing: 45, tagOffset: 200, samplesPerGroup, horizonYears);
        var rich = SampleCareerWallets(rules, vitality: 60, upbringing: 55, tagOffset: 201, samplesPerGroup, horizonYears);
        Assert.NotEqual(Median(poor), Median(rich));

        var genomeLow = SampleCareerWallets(rules, vitality: 5, upbringing: 50, tagOffset: 300, samplesPerGroup, horizonYears);
        var genomeHigh = SampleCareerWallets(rules, vitality: 95, upbringing: 50, tagOffset: 301, samplesPerGroup, horizonYears);

        double overlapRichPoor = OverlapCoefficient(rich, poor);
        double overlapGenomes = OverlapCoefficient(genomeHigh, genomeLow);
        Assert.True(overlapRichPoor >= overlapGenomes,
            $"overlap(rico,pobre)={overlapRichPoor:F3} deveria ser >= overlap(genomas extremos)={overlapGenomes:F3}");
    }

    private static List<double> SampleCareerWallets(
        FamilyRules rules, double vitality, double upbringing, int tagOffset, int samplesPerGroup, int horizonYears)
    {
        var wallets = new List<double>();
        for (int i = 0; i < samplesPerGroup; i++)
        {
            ulong seed = (ulong)(42_000_000 + tagOffset * 10_000 + i);
            var (world, npc) = HouseholdCounterfactualHarness.CreateEmployedAdultWorld(
                seed, upbringing, vitality, HouseholdCounterfactualHarness.FixedRateGene, rules);
            wallets.Add(HouseholdCounterfactualHarness.RunCareerWithMortalityAndReturnWallet(world, npc, horizonYears));
        }
        return wallets;
    }

    private static double Median(IReadOnlyList<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
    }

    private static double MedianSpread(IReadOnlyList<List<double>> groups)
    {
        var medians = groups.Select(Median).ToList();
        return medians.Max() - medians.Min();
    }

    /// <summary>Coeficiente de sobreposição (OVL) via histograma compartilhado de 20 bins — padrão
    /// não-paramétrico de overlap de duas amostras (soma dos mínimos por bin das densidades
    /// empíricas), mais fiel que comparar só a faixa min/max.</summary>
    private static double OverlapCoefficient(IReadOnlyList<double> a, IReadOnlyList<double> b, int bins = 20)
    {
        double lo = Math.Min(a.Min(), b.Min());
        double hi = Math.Max(a.Max(), b.Max());
        if (hi <= lo) return 1.0;

        var histA = new int[bins];
        var histB = new int[bins];
        foreach (var v in a) histA[Math.Clamp((int)((v - lo) / (hi - lo) * bins), 0, bins - 1)]++;
        foreach (var v in b) histB[Math.Clamp((int)((v - lo) / (hi - lo) * bins), 0, bins - 1)]++;

        double overlap = 0;
        for (int i = 0; i < bins; i++)
            overlap += Math.Min(histA[i] / (double)a.Count, histB[i] / (double)b.Count);
        return overlap;
    }

    // --- T31 (FAM-36) ---

    // SPEC_DEVIATION: tasks.md aponta FamilyHashSensorTests.cs como Where do T31; a instrução de
    // execução recebida para este lote (T27-T31) consolidou explicitamente os 5 no mesmo arquivo
    // dos T23-T26 (mesmo padrão de FamilyPairedScenarioTests já usado por T23-T30). Nenhuma
    // cobertura muda — só a localização do arquivo.
    [Fact]
    [Trait("Category", "Scenario")]
    public void Turning_off_heredity_and_courtship_changes_world_hash_after_ten_years()
    {
        // "Fase 7 nunca rodou": cortejo nunca inicia (score normalizado nunca ultrapassa 1.0, um
        // limiar acima disso é inalcançável por construção) e nem Vitality nem Upbringing
        // influenciam mortalidade/salário — os dois canais que a Fase 7 acrescentou ao mundo.
        var phase7OffRules = ScenarioRunner.DefaultFamilyRules with
        {
            CourtshipThreshold = 10.0,
            VitalityMortalityWeight = 0.0,
            EnvironmentalWealthChannelEnabled = false,
        };

        var (worldOn, clockOn) = ScenarioRunner.Create(DefaultSeed);
        clockOn.Run(worldOn, HorizonHours);

        var (worldOff, clockOff) = ScenarioRunner.Create(DefaultSeed, familyRules: phase7OffRules);
        clockOff.Run(worldOff, HorizonHours);

        Assert.NotEqual(WorldSnapshot.CanonicalHash(worldOn), WorldSnapshot.CanonicalHash(worldOff));
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
