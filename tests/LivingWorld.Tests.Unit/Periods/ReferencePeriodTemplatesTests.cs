using LivingWorld.Simulation;

namespace LivingWorld.Tests.Periods;

/// <summary>Fase 13, T8 (PERIOD-17..18): os pacotes de referência (pré-histórico, medieval,
/// moderno, futurista, criaturas) em <c>scenarios/periods/*.json</c> são conteúdo, não
/// contrato do motor — carregam pelo mesmo <see cref="ScenarioLoaderV2"/> de qualquer outro
/// período e servem de baseline de compatibilidade (ficam verdes; período fora do pacote usa
/// o mesmo pipeline, ver <see cref="ScenarioLoaderV2Tests"/>).</summary>
public class ReferencePeriodTemplatesTests
{
    public static IEnumerable<object[]> ReferencePeriodFiles() =>
    [
        ["prehistoric"], ["medieval"], ["modern"], ["futuristic"], ["creatures"],
    ];

    [Theory]
    [MemberData(nameof(ReferencePeriodFiles))]
    public void Reference_period_loads_and_runs_a_short_horizon_without_error(string periodName)
    {
        string path = Path.Combine(FindRepoRoot(), "scenarios", "periods", $"{periodName}.json");
        string json = File.ReadAllText(path);

        var result = ScenarioLoaderV2.LoadWorld(json);

        Assert.True(result.IsSuccess, $"{periodName}: {result.Error}");
        var (world, clock) = result.Value;
        Assert.Equal(100, world.Npcs.Count);

        for (int tick = 0; tick < 48; tick++)
            clock.Tick(world);

        Assert.True(world.Npcs.Count(n => n.IsAlive) > 0, $"{periodName}: ninguém sobreviveu 48 ticks");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
