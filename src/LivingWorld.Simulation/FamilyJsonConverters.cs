using System.Text.Json;
using System.Text.Json.Serialization;
using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>System.Text.Json só serializa chave de dicionário nativamente pra string/enum —
/// <see cref="RelationshipKey"/> (Fase 7, T8: <c>WorldState.Relationships</c>) precisa do mesmo
/// tratamento que <see cref="ResourceTypeKeyConverter"/> já dá a <c>ResourceType</c>.</summary>
public sealed class RelationshipKeyConverter : JsonConverter<RelationshipKey>
{
    public override RelationshipKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("Só usado como chave de dicionário — ver ReadAsPropertyName.");

    public override void Write(Utf8JsonWriter writer, RelationshipKey value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Só usado como chave de dicionário — ver WriteAsPropertyName.");

    public override RelationshipKey ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parts = reader.GetString()!.Split(',');
        return new RelationshipKey(new NpcId(long.Parse(parts[0])), new NpcId(long.Parse(parts[1])));
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, RelationshipKey value, JsonSerializerOptions options) =>
        writer.WritePropertyName($"{value.From.Value},{value.To.Value}");
}

/// <summary>Mesma necessidade de <see cref="RelationshipKeyConverter"/>, para a chave
/// <c>(RelationshipEventType, RelationshipAxis)</c> de <see cref="FamilyRules.RelationshipDeltas"/>.</summary>
public sealed class RelationshipDeltaKeyConverter : JsonConverter<(RelationshipEventType Type, RelationshipAxis Axis)>
{
    public override (RelationshipEventType Type, RelationshipAxis Axis) Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("Só usado como chave de dicionário — ver ReadAsPropertyName.");

    public override void Write(
        Utf8JsonWriter writer, (RelationshipEventType Type, RelationshipAxis Axis) value, JsonSerializerOptions options) =>
        throw new NotSupportedException("Só usado como chave de dicionário — ver WriteAsPropertyName.");

    public override (RelationshipEventType Type, RelationshipAxis Axis) ReadAsPropertyName(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parts = reader.GetString()!.Split(',');
        return (Enum.Parse<RelationshipEventType>(parts[0]), Enum.Parse<RelationshipAxis>(parts[1]));
    }

    public override void WriteAsPropertyName(
        Utf8JsonWriter writer, (RelationshipEventType Type, RelationshipAxis Axis) value, JsonSerializerOptions options) =>
        writer.WritePropertyName($"{value.Type},{value.Axis}");
}
