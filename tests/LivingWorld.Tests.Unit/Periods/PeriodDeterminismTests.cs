using System.Text.Json.Nodes;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Periods;

/// <summary>Fase 13, T9 (PERIOD-14..16): mesmo período + mesma seed produz o mesmo hash
/// canônico; períodos distintos com a mesma seed produzem hashes diferentes. Horizonte curto
/// (48 ticks) — determinismo não precisa de simulação longa pra ser provado.</summary>
public class PeriodDeterminismTests
{
    private const long TickHorizon = 48;

    [Fact]
    public void Same_period_and_same_seed_produce_the_same_canonical_hash()
    {
        string json = ReadPeriod("medieval");

        string hashA = RunAndHash(json);
        string hashB = RunAndHash(json);

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void Different_periods_with_the_same_seed_produce_different_canonical_hashes()
    {
        // medieval (ProfessionIds [1,2,3]) e futuristic (ProfessionIds [1,2,4]) divergem no
        // catálogo de profissão em si — não só no bloco Dynamics — então a mesma seed sorteia
        // profissão inicial diferente e o mundo diverge (RollProfession, PopulationCatalog.cs).
        const ulong sharedSeed = 7;
        var medieval = WithSeed(ReadPeriod("medieval"), sharedSeed);
        var futuristic = WithSeed(ReadPeriod("futuristic"), sharedSeed);

        string hashMedieval = RunAndHash(medieval);
        string hashFuturistic = RunAndHash(futuristic);

        Assert.NotEqual(hashMedieval, hashFuturistic);
    }

    private static string RunAndHash(string json)
    {
        var result = ScenarioLoaderV2.LoadWorld(json);
        Assert.True(result.IsSuccess, result.Error);
        var (world, clock) = result.Value;

        for (long tick = 0; tick < TickHorizon; tick++)
            clock.Tick(world);

        return WorldSnapshot.CanonicalHash(world);
    }

    private static string WithSeed(string json, ulong seed)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        root["Seed"] = seed;
        return root.ToJsonString();
    }

    private static string ReadPeriod(string name) =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), "scenarios", "periods", $"{name}.json"));

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
