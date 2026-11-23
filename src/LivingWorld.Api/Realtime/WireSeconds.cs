using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Api.Realtime;

/// <summary>Contrato v2 (ADR-0050, AD-197b): todo campo de tempo que o v1 mandava em ticks sai em segundos de mundo, com o nome
/// trocado (<c>tick</c> → <c>second</c>, <c>…Tick</c> → <c>…Second</c>, <c>…Ticks</c> → <c>…Seconds</c>), sem campo irmão. Vale para
/// HTTP, WS e o canal do jogador. O que o motor ainda guarda em horas sai como hora × 3.600 (o instante do começo da hora); o que
/// já é segundo (o relógio do mundo) sai com nome próprio em segundos. Entrada em segundos arredonda para a hora de cima enquanto
/// o motor guarda horas. Sentinela grande demais (o "nunca") passa como está.</summary>
public static class WireSeconds
{
    public static JsonSerializerOptions Apply(JsonSerializerOptions options)
    {
        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()).WithAddedModifier(Rename);
        return options;
    }

    /// <summary>O nome v2 de um campo em ticks do v1, ou nulo se o campo não é de tick.</summary>
    public static string? Renamed(string name)
    {
        if (name == "tick") return "second";
        if (name == "ticks") return "seconds";
        if (name.EndsWith("Ticks", StringComparison.Ordinal)) return name[..^"Ticks".Length] + "Seconds";
        if (name.EndsWith("Tick", StringComparison.Ordinal)) return name[..^"Tick".Length] + "Second";
        return null;
    }

    private static void Rename(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object) return;
        foreach (var property in info.Properties)
        {
            if (Renamed(property.Name) is not { } name) continue;
            JsonConverter? converter = property.PropertyType switch
            {
                var t when t == typeof(long) => new HoursAsSeconds(),
                var t when t == typeof(long?) => new NullableHoursAsSeconds(),
                var t when t == typeof(int) => new IntHoursAsSeconds(),
                var t when t == typeof(int?) => new NullableIntHoursAsSeconds(),
                _ => null,
            };
            if (converter is null) continue;
            property.Name = name;
            property.CustomConverter = converter;
        }
    }

    private const long Limit = long.MaxValue / WorldSeconds.PerHour;

    private static long ToSeconds(long hours) => hours is > Limit or < -Limit ? hours : hours * WorldSeconds.PerHour;

    /// <summary>Instante ou duração em segundos vindo do cliente, na hora em que o motor ainda conta (hora de cima).</summary>
    public static long HoursFrom(long seconds) => ToHours(seconds);

    private static long ToHours(long seconds) =>
        seconds is > Limit * WorldSeconds.PerHour or < -Limit * WorldSeconds.PerHour ? seconds : WorldSeconds.ToHourCeiling(seconds);

    private sealed class HoursAsSeconds : JsonConverter<long>
    {
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ToHours(reader.GetInt64());
        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) => writer.WriteNumberValue(ToSeconds(value));
    }

    private sealed class NullableHoursAsSeconds : JsonConverter<long?>
    {
        public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null ? null : ToHours(reader.GetInt64());

        public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
        {
            if (value is { } v) writer.WriteNumberValue(ToSeconds(v));
            else writer.WriteNullValue();
        }
    }

    private sealed class IntHoursAsSeconds : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            (int)ToHours(reader.GetInt64());

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) => writer.WriteNumberValue(ToSeconds(value));
    }

    private sealed class NullableIntHoursAsSeconds : JsonConverter<int?>
    {
        public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null ? null : (int)ToHours(reader.GetInt64());

        public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
        {
            if (value is { } v) writer.WriteNumberValue(ToSeconds(v));
            else writer.WriteNullValue();
        }
    }
}
