using System.Text.Json;
using System.Text.Json.Nodes;
using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Carrega o mapa de um cenário (task 5): autoral, se o JSON traz "Cells", ou
/// procedural a partir de "Seed", se não traz. Validação na borda — todo erro vira
/// <see cref="Result{T}"/> apontando o campo, nunca exceção nem null.</summary>
public static class MapScenarioLoader
{
    public static Result<WorldMap> Load(string json)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json)?.AsObject() ?? throw new JsonException("corpo vazio");
        }
        catch (JsonException ex)
        {
            return Result<WorldMap>.Fail($"json: {ex.Message}");
        }

        if (root["Width"] is not JsonValue widthNode || !widthNode.TryGetValue<int>(out var width))
            return Result<WorldMap>.Fail("Width: campo obrigatório ausente ou inválido");
        if (root["Height"] is not JsonValue heightNode || !heightNode.TryGetValue<int>(out var height))
            return Result<WorldMap>.Fail("Height: campo obrigatório ausente ou inválido");
        if (root["Seed"] is not JsonValue seedNode || !seedNode.TryGetValue<ulong>(out var seed))
            return Result<WorldMap>.Fail("Seed: campo obrigatório ausente ou inválido");
        if (root["RegionSize"] is not JsonValue regionSizeNode || !regionSizeNode.TryGetValue<int>(out var regionSize))
            return Result<WorldMap>.Fail("RegionSize: campo obrigatório ausente ou inválido");

        var catalog = new GeographyCatalog(
            ReadIntSet(root, "TerrainIds"), ReadIntSet(root, "BiomeIds"), ReadIntSet(root, "ResourceIds"));

        if (root["CostWeights"] is not JsonObject costNode)
            return Result<WorldMap>.Fail("CostWeights: campo obrigatório ausente");
        if (costNode["Base"] is not JsonValue baseNode || !baseNode.TryGetValue<double>(out var costBase))
            return Result<WorldMap>.Fail("CostWeights.Base: campo obrigatório ausente ou inválido");
        if (costNode["AltitudeWeight"] is not JsonValue altNode || !altNode.TryGetValue<double>(out var altitudeWeight))
            return Result<WorldMap>.Fail("CostWeights.AltitudeWeight: campo obrigatório ausente ou inválido");

        var terrainWeight = new Dictionary<int, double>();
        if (costNode["TerrainWeight"] is JsonObject terrainWeightNode)
            foreach (var (key, value) in terrainWeightNode)
                if (int.TryParse(key, out var id) && value is JsonValue v && v.TryGetValue<double>(out var w))
                    terrainWeight[id] = w;

        var cost = new CostWeights(costBase, altitudeWeight, terrainWeight);

        var settlements = new List<SettlementAnchor>();
        if (root["Settlements"] is JsonArray settlementsNode)
        {
            foreach (var node in settlementsNode)
            {
                if (node is not JsonObject s || s["Name"]?.GetValue<string>() is not { } name
                    || s["X"]?.GetValue<int>() is not { } x || s["Y"]?.GetValue<int>() is not { } y)
                    return Result<WorldMap>.Fail("Settlements: item precisa de Name, X e Y");

                string id = s["Id"]?.GetValue<string>() ?? "";
                int orientation = s["Orientation"]?.GetValue<int>() ?? 0;

                var streets = new List<CellCoord>();
                if (s["Streets"] is JsonArray streetsNode)
                {
                    foreach (var streetNode in streetsNode)
                    {
                        if (streetNode is not JsonObject street
                            || street["X"]?.GetValue<int>() is not { } sx || street["Y"]?.GetValue<int>() is not { } sy)
                            return Result<WorldMap>.Fail($"Settlements[{name}].Streets: item precisa de X e Y");
                        streets.Add(new CellCoord(sx, sy));
                    }
                }

                settlements.Add(new SettlementAnchor(name, new CellCoord(x, y), id, orientation, streets));
            }
        }

        if (root["Cells"] is JsonArray cellsNode)
            return LoadAuthored(cellsNode, width, height, regionSize, seed, catalog, cost, settlements);

        return MapGenerator.Generate(seed, width, height, regionSize, catalog, cost, settlements);
    }

    private static Result<WorldMap> LoadAuthored(
        JsonArray cellsNode, int width, int height, int regionSize, ulong seed,
        GeographyCatalog catalog, CostWeights cost, IReadOnlyList<SettlementAnchor> settlements)
    {
        var cells = new List<MapCell>();
        foreach (var node in cellsNode)
        {
            if (node is not JsonObject c
                || c["X"]?.GetValue<int>() is not { } x || c["Y"]?.GetValue<int>() is not { } y
                || c["Terrain"]?.GetValue<int>() is not { } terrain
                || c["Altitude"]?.GetValue<int>() is not { } altitude)
                return Result<WorldMap>.Fail("Cells: item precisa de X, Y, Terrain e Altitude");

            int biome = c["Biome"]?.GetValue<int>() ?? 0;
            bool water = c["Water"]?.GetValue<bool>() ?? false;
            var resources = c["Resources"] is JsonArray resNode
                ? resNode.Select(r => new ResourceType(r!.GetValue<int>())).ToArray()
                : Array.Empty<ResourceType>();

            cells.Add(new MapCell(new CellCoord(x, y), new TerrainType(terrain), new BiomeType(biome), altitude, water, resources));
        }

        var regions = RegionGrid.Partition(width, height, regionSize);
        return WorldMap.Create(width, height, seed, catalog, cost, cells, regions, settlements);
    }

    private static HashSet<int> ReadIntSet(JsonObject root, string field) =>
        root[field] is JsonArray arr
            ? arr.Select(n => n!.GetValue<int>()).ToHashSet()
            : new HashSet<int>();
}
