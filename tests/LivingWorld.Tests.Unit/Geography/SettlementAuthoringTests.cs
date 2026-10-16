using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Simulation.Geography;

namespace LivingWorld.Tests.Unit.Geography;

/// <summary>Fase 15.1, T44 (backend-gaps.md G3): id estável, orientação e ruas autoradas por
/// assentamento — extensão opcional e retrocompatível de <see cref="SettlementAnchor"/>.</summary>
public class SettlementAuthoringTests
{
    private const string ScenarioWithFullAuthoring = """
        {
          "Seed": 42, "Width": 4, "Height": 4, "RegionSize": 2,
          "TerrainIds": [1, 2], "BiomeIds": [1], "ResourceIds": [],
          "CostWeights": { "Base": 1.0, "AltitudeWeight": 0.5, "TerrainWeight": { "1": 1.0, "2": 2.0 } },
          "Settlements": [
            {
              "Name": "Valen", "Id": "settlement-1", "X": 1, "Y": 1, "Orientation": 90,
              "Streets": [ { "X": 1, "Y": 0 }, { "X": 2, "Y": 1 } ]
            }
          ]
        }
        """;

    [Fact]
    public void Settlement_id_orientation_and_streets_are_parsed()
    {
        var result = MapScenarioLoader.Load(ScenarioWithFullAuthoring);

        Assert.True(result.IsSuccess, result.Error);
        var settlement = result.Value!.Settlements[0];
        Assert.Equal("settlement-1", settlement.Id);
        Assert.Equal(90, settlement.Orientation);
        Assert.Equal([new CellCoord(1, 0), new CellCoord(2, 1)], settlement.Streets);
    }

    [Fact]
    public void Settlement_without_the_new_fields_still_loads_with_backward_compatible_defaults()
    {
        const string legacyScenario = """
            {
              "Seed": 42, "Width": 4, "Height": 4, "RegionSize": 2,
              "TerrainIds": [1, 2], "BiomeIds": [1], "ResourceIds": [],
              "CostWeights": { "Base": 1.0, "AltitudeWeight": 0.5, "TerrainWeight": { "1": 1.0, "2": 2.0 } },
              "Settlements": [ { "Name": "Valen", "X": 1, "Y": 1 } ]
            }
            """;

        var result = MapScenarioLoader.Load(legacyScenario);

        Assert.True(result.IsSuccess, result.Error);
        var settlement = result.Value!.Settlements[0];
        Assert.Equal("", settlement.Id);
        Assert.Equal(0, settlement.Orientation);
        Assert.Empty(settlement.Streets);
    }

    [Fact]
    public void A_street_cell_outside_the_grid_is_rejected_naming_the_field()
    {
        const string scenario = """
            {
              "Seed": 1, "Width": 2, "Height": 2, "RegionSize": 2,
              "TerrainIds": [1], "BiomeIds": [], "ResourceIds": [],
              "CostWeights": { "Base": 1.0, "AltitudeWeight": 0.5, "TerrainWeight": { "1": 1.0 } },
              "Settlements": [
                { "Name": "Valen", "X": 0, "Y": 0, "Streets": [ { "X": 99, "Y": 99 } ] }
              ]
            }
            """;

        var result = MapScenarioLoader.Load(scenario);

        Assert.False(result.IsSuccess);
        Assert.Contains("Streets", result.Error);
        Assert.Null(result.Value);
    }
}
