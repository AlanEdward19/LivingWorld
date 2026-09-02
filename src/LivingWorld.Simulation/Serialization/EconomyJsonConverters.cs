using System.Text.Json;
using System.Text.Json.Serialization;
using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>System.Text.Json só serializa chave de dicionário nativamente pra string/enum. A
/// economia (Fase 5) usa <see cref="ResourceType"/> (T4: <c>Workplace.Stock</c>/<c>Prices</c>) e
/// o par <c>(ResourceId, LocationTypeId)</c> (T2: <c>EconomyRules.CapacityByResourceLocation</c>)
/// como chave — os dois conversores abaixo ensinam o round-trip do snapshot (T12) a ler/escrever
/// essas chaves como propriedade de objeto JSON.</summary>
public sealed class ResourceTypeKeyConverter : JsonConverter<ResourceType>
{
    public override ResourceType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, ResourceType value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Id);

    public override ResourceType ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(int.Parse(reader.GetString()!));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, ResourceType value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Id.ToString());
}

public sealed class ResourceLocationKeyConverter : JsonConverter<(int ResourceId, int LocationTypeId)>
{
    public override (int ResourceId, int LocationTypeId) Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("Só usado como chave de dicionário — ver ReadAsPropertyName.");

    public override void Write(
        Utf8JsonWriter writer, (int ResourceId, int LocationTypeId) value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Só usado como chave de dicionário — ver WriteAsPropertyName.");

    public override (int ResourceId, int LocationTypeId) ReadAsPropertyName(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parts = reader.GetString()!.Split(',');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    public override void WriteAsPropertyName(
        Utf8JsonWriter writer, (int ResourceId, int LocationTypeId) value, JsonSerializerOptions options) =>
        writer.WritePropertyName($"{value.ResourceId},{value.LocationTypeId}");
}
