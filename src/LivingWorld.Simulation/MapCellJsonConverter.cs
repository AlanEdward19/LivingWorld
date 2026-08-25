using System.Text.Json;
using System.Text.Json.Serialization;
using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary><see cref="MapCell.Temperature"/> é função pura de bioma/altitude (PWR-74) —
/// recomputável → não persiste nem entra no payload canônico (ADR-0014). Overlay causal mora
/// em <see cref="EnvironmentTemperatureAdjustment"/>. Omite ~4 bytes×células do snapshot/hash.</summary>
public sealed class MapCellJsonConverter : JsonConverter<MapCell>
{
    public override MapCell Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var coord = root.GetProperty("Coord").Deserialize<CellCoord>(options)!;
        var terrain = root.GetProperty("Terrain").Deserialize<TerrainType>(options)!;
        var biome = root.GetProperty("Biome").Deserialize<BiomeType>(options)!;
        int altitude = root.GetProperty("Altitude").GetInt32();
        bool hasWater = root.GetProperty("HasWater").GetBoolean();
        var resources = root.TryGetProperty("Resources", out var resourcesNode)
            ? resourcesNode.Deserialize<List<ResourceType>>(options) ?? []
            : [];
        // Ignora Temperature persistido (legado/default 0) — sempre DeriveBase.
        return MapCell.WithDerivedTemperature(coord, terrain, biome, altitude, hasWater, resources);
    }

    public override void Write(Utf8JsonWriter writer, MapCell value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("Coord");
        JsonSerializer.Serialize(writer, value.Coord, options);
        writer.WritePropertyName("Terrain");
        JsonSerializer.Serialize(writer, value.Terrain, options);
        writer.WritePropertyName("Biome");
        JsonSerializer.Serialize(writer, value.Biome, options);
        writer.WriteNumber("Altitude", value.Altitude);
        writer.WriteBoolean("HasWater", value.HasWater);
        writer.WritePropertyName("Resources");
        JsonSerializer.Serialize(writer, value.Resources, options);
        writer.WriteEndObject();
    }
}
