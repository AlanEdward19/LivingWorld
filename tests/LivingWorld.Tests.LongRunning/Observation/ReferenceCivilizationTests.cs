using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Observation;

/// <summary>Issue #112 fatia D: o cenário de referência de séculos (<c>scenarios/test-civilization.json</c>) rodado por todas as
/// seeds do baseline pelo mesmo caminho do <c>civ-report</c>, com asserts de propriedade a cada ano e o relatório gravado em
/// <c>artifacts/</c>. Seeds, anos e diversidade vêm de <c>tests/baselines/civilization-diversity.json</c> (bloco
/// <c>Reference</c>), sem número no teste. Categoria <c>Heavy</c>: só o <c>verify.sh --nightly</c> roda.</summary>
public class ReferenceCivilizationTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LivingWorld.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("LivingWorld.sln não encontrado");
    }

    // Verificações puras, cada uma com seu par de mutação nos testes abaixo.
    internal static string? ExtinctionViolation(WorldYearSample sample) =>
        sample.Cities.Any(c => c.Metrics["population"] > 0) ? null : $"ano {sample.Year}: nenhuma cidade com população";

    internal static string? LongevityViolation(WorldState world)
    {
        int max = world.PopulationRules.LifeTable.MaxLongevityYears;
        var oldest = world.Npcs.Where(n => n.IsAlive).Select(n => (Npc: n, Age: n.AgeYears(world.CurrentDate)))
            .OrderByDescending(x => x.Age).FirstOrDefault();
        return oldest.Npc is not null && oldest.Age > max ? $"npc {oldest.Npc.Id.Value} vivo com {oldest.Age} anos (máx {max})" : null;
    }

    /// <summary>Moeda total (carteiras, oficinas, cofres das cidades e riqueza dos pools) menos o saldo cunhado e destruído
    /// deve ser o total do início.</summary>
    internal static long NetCoin(WorldState world) =>
        world.Npcs.Sum(n => n.Wallet.Amount) + world.Workplaces.Sum(w => w.Treasury.Amount)
        + world.Cities.Sum(c => c.Treasury.Amount + c.AggregatePool.WealthSum)
        - (world.MoneyMinted.Amount - world.MoneyDestroyed.Amount);

    internal static string? CoinViolation(WorldState world, long initialNetCoin) =>
        NetCoin(world) == initialNetCoin ? null : $"moeda não conservada: {NetCoin(world)} contra {initialNetCoin} no início";

    [Fact]
    [Trait("Category", "Heavy")]
    public void Reference_scenario_keeps_its_properties_for_every_seed_and_writes_the_report()
    {
        var baseline = JsonNode.Parse(File.ReadAllText(Path.Combine(Root(), "tests", "baselines", "civilization-diversity.json")))!["Reference"]!;
        string scenarioPath = Path.Combine(Root(), "scenarios", baseline["Scenario"]!.GetValue<string>());
        string scenario = File.ReadAllText(scenarioPath);
        ulong firstSeed = baseline["FirstSeed"]!.GetValue<ulong>();
        int seeds = baseline["Seeds"]!.GetValue<int>(), years = baseline["Years"]!.GetValue<int>();

        var runs = new SeedRun[seeds];
        var violations = new ConcurrentBag<string>();
        Parallel.For(0, seeds, i =>
        {
            ulong seed = firstSeed + (ulong)i;
            long initialNet = 0;
            runs[i] = CivilizationRunner.Run(scenario, seed, years,
                onStart: w => initialNet = NetCoin(w),
                afterYear: (w, sample) =>
                {
                    foreach (var v in new[] { ExtinctionViolation(sample), LongevityViolation(w), CoinViolation(w, initialNet) })
                        if (v is not null) violations.Add($"seed {seed}: {v}");
                });
        });

        string json = CivilizationReport.ToJson(Path.GetFileName(scenarioPath), runs);
        string artifact = Path.Combine(Root(), "artifacts", "civ-report-reference.json");
        Directory.CreateDirectory(Path.GetDirectoryName(artifact)!);
        File.WriteAllText(artifact, json);

        Assert.True(violations.IsEmpty, string.Join("\n", violations.OrderBy(v => v).Take(20)));
        var diversity = JsonNode.Parse(json)!["summary"]!["diversity"]!;
        Assert.Equal(baseline["Diversity"]!["Index"]!.GetValue<double>(), diversity["index"]!.GetValue<double>(), 12);
        foreach (var (name, expected) in baseline["Diversity"]!["ByMetric"]!.AsObject())
            Assert.Equal(expected!.GetValue<double>(), diversity["byMetric"]![name]!.GetValue<double>(), 12);
    }

    // ---- pares de mutação (rápidos, sem Heavy): cada verificação aceita o estado são e reprova o estado quebrado ----

    private static WorldYearSample Sample(params double[] populations) => new(
        7, 0, new Dictionary<string, double>(),
        populations.Select((p, i) => new CitySample($"c{i}", new Dictionary<string, double> { ["population"] = p })).ToList());

    [Fact]
    public void Extinction_check_accepts_one_living_city_and_rejects_none()
    {
        Assert.Null(ExtinctionViolation(Sample(0, 1)));
        Assert.NotNull(ExtinctionViolation(Sample(0, 0)));
        Assert.NotNull(ExtinctionViolation(Sample()));
    }

    [Fact]
    public void Longevity_check_accepts_the_limit_and_rejects_one_year_over()
    {
        var (world, clock) = ScenarioLoaderV2.LoadWorld(File.ReadAllText(Path.Combine(Root(), "scenarios", "test-civilization.json"))).Value;
        clock.Run(world, world.Calendar.HoursPerDay);
        Assert.Null(LongevityViolation(world));

        int max = world.PopulationRules.LifeTable.MaxLongevityYears;
        var old = world.Npcs.First(n => n.IsAlive);
        var born = world.CurrentDate.AddYears(-(max + 1));
        var atLimit = world.CurrentDate.AddYears(-max);
        SetBirth(old, atLimit);
        Assert.Null(LongevityViolation(world));
        SetBirth(old, born);
        Assert.NotNull(LongevityViolation(world));
    }

    private static void SetBirth(Npc npc, WorldDate date) =>
        typeof(Npc).GetField("<BirthDate>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(npc, date);

    [Fact]
    public void Coin_check_accepts_a_wallet_move_and_rejects_a_coin_from_nowhere()
    {
        var (world, clock) = ScenarioLoaderV2.LoadWorld(File.ReadAllText(Path.Combine(Root(), "scenarios", "test-civilization.json"))).Value;
        long start = NetCoin(world);
        clock.Run(world, world.Calendar.HoursPerDay * 30);
        Assert.Null(CoinViolation(world, start));

        world.Npcs.First(n => n.IsAlive).CreditWallet(new Money(1)); // moeda criada fora do livro de cunhagem
        Assert.NotNull(CoinViolation(world, start));
    }
}
