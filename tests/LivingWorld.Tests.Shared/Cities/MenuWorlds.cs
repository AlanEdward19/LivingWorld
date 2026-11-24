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
        // AD-184: LW_AD184=off mede a obra como na AD-179 (antes × depois pareado)
        var off = Environment.GetEnvironmentVariable("LW_AD184") == "off"
            ? LivingWorld.Domain.Cities.CityLayoutRules.Default with { Streets = LivingWorld.Domain.Cities.StreetRules.Default.WithoutAd184() }
            : Environment.GetEnvironmentVariable("LW_AD194") == "off" // AD-194: material em horas, sem eventos de dia perdido
                ? LivingWorld.Domain.Cities.CityLayoutRules.Default with { Streets = LivingWorld.Domain.Cities.StreetRules.Default.WithoutAd194() }
                : null;
        // AD-192: LW_AD192=off|nohygiene|noregard|noleft mede a fatia desligada (pareado no mesmo binário)
        if (Environment.GetEnvironmentVariable("LW_AD192") is { } ad192)
        {
            var baseRules = off ?? LivingWorld.Domain.Cities.CityLayoutRules.Default;
            off = baseRules with { Streets = baseRules.Streets.WithAd192Mode(ad192) };
        }
        if (Environment.GetEnvironmentVariable("LW_AD195") is { } ad195) // AD-195: off|nowalk|nohome
        {
            var baseRules = off ?? LivingWorld.Domain.Cities.CityLayoutRules.Default;
            off = baseRules with { Streets = baseRules.Streets.WithAd195Mode(ad195) };
        }
        if (period == "padrao") return ScenarioRunner.Create(seed, cityLayout: off);
        var root = System.Text.Json.Nodes.JsonNode.Parse(DefaultPeriodSeeder.PayloadFor(period))!;
        root["Seed"] = seed;
        if (off is not null) root["CityLayout"] = System.Text.Json.JsonSerializer.SerializeToNode(off);
        var loaded = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        if (!loaded.IsSuccess) throw new InvalidOperationException($"{period}: {loaded.Error}");
        return loaded.Value;
    }
}
