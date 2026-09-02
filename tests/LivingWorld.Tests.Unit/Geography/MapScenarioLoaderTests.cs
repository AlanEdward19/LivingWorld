using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Geography;

namespace LivingWorld.Tests.Unit.Geography;

/// <summary>Fase 2, task 5/6: carregamento de cenário — autoral ou por seed, com validação
/// que falha rápido nomeando o campo, e a cidade fora do grid não cria o mundo.</summary>
public class MapScenarioLoaderTests
{
    private const string ValidGeneratedScenario = """
        {
          "Seed": 42, "Width": 4, "Height": 4, "RegionSize": 2,
          "TerrainIds": [1, 2], "BiomeIds": [1], "ResourceIds": [],
          "CostWeights": { "Base": 1.0, "AltitudeWeight": 0.5, "TerrainWeight": { "1": 1.0, "2": 2.0 } },
          "Settlements": [ { "Name": "Valen", "X": 1, "Y": 1 } ]
        }
        """;

    [Fact]
    public void Loading_a_generated_scenario_succeeds_and_settlement_resolves_to_its_cell()
    {
        var result = MapScenarioLoader.Load(ValidGeneratedScenario);

        Assert.True(result.IsSuccess, result.Error);
        var map = result.Value!;
        Assert.Equal(4, map.Width);
        Assert.Single(map.Settlements);
        Assert.Equal(new CellCoord(1, 1), map.Settlements[0].Cell);
    }

    [Fact]
    public void Same_seed_loaded_twice_produces_the_same_map()
    {
        var a = MapScenarioLoader.Load(ValidGeneratedScenario).Value!;
        var b = MapScenarioLoader.Load(ValidGeneratedScenario).Value!;

        Assert.Equal(a.Cells.Select(c => (c.Coord, c.Terrain, c.Altitude)), b.Cells.Select(c => (c.Coord, c.Terrain, c.Altitude)));
    }

    [Fact]
    public void Settlement_pointing_outside_the_grid_is_rejected_naming_the_field_and_no_map_is_built()
    {
        const string scenario = """
            {
              "Seed": 1, "Width": 2, "Height": 2, "RegionSize": 2,
              "TerrainIds": [1], "BiomeIds": [], "ResourceIds": [],
              "CostWeights": { "Base": 1.0, "AltitudeWeight": 0.5, "TerrainWeight": { "1": 1.0 } },
              "Settlements": [ { "Name": "Fora", "X": 99, "Y": 99 } ]
            }
            """;

        var result = MapScenarioLoader.Load(scenario);

        Assert.False(result.IsSuccess);
        Assert.Contains("Settlements", result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Authored_cells_out_of_grid_bounds_is_rejected_naming_the_field()
    {
        const string scenario = """
            {
              "Seed": 1, "Width": 2, "Height": 2, "RegionSize": 2,
              "TerrainIds": [1], "BiomeIds": [], "ResourceIds": [],
              "CostWeights": { "Base": 1.0, "AltitudeWeight": 0.5, "TerrainWeight": { "1": 1.0 } },
              "Cells": [
                { "X": 0, "Y": 0, "Terrain": 1, "Altitude": 0 },
                { "X": 1, "Y": 0, "Terrain": 1, "Altitude": 0 },
                { "X": 0, "Y": 1, "Terrain": 1, "Altitude": 0 },
                { "X": 5, "Y": 5, "Terrain": 1, "Altitude": 0 }
              ]
            }
            """;

        var result = MapScenarioLoader.Load(scenario);

        Assert.False(result.IsSuccess);
        Assert.Contains("Cells", result.Error);
    }

    [Fact]
    public void Missing_required_field_is_rejected_naming_the_field()
    {
        const string scenario = """{ "Width": 4, "Height": 4, "RegionSize": 2 }""";

        var result = MapScenarioLoader.Load(scenario);

        Assert.False(result.IsSuccess);
        Assert.Contains("Seed", result.Error);
    }
}
