using LivingWorld.Api.Periods;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Shared.Cities;

/// <summary>AD-179: monta o mundo que o jogador cria pelo menu (o payload do período, com a seed trocada) ou a vila de código
/// (<c>padrao</c>), pelo mesmo caminho do jogo (<see cref="ScenarioLoaderV2"/>).</summary>
public static class MenuWorlds
{
    public static (WorldState World, WorldClock Clock) Load(string period, ulong seed)
    {
        if (period == "padrao") return ScenarioRunner.Create(seed);
        var root = System.Text.Json.Nodes.JsonNode.Parse(DefaultPeriodSeeder.PayloadFor(period))!;
        root["Seed"] = seed;
        var loaded = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        if (!loaded.IsSuccess) throw new InvalidOperationException($"{period}: {loaded.Error}");
        return loaded.Value;
    }
}
