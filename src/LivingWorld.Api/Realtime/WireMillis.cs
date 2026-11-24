using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Api.Realtime;

/// <summary>Contrato v2 (ADR-0050, AD-197b): todo campo de tempo que o v1 mandava em ticks sai em milissegundos de mundo, com o
/// nome trocado, sem campo irmão: <c>tick</c> → <c>atMs</c>; instante <c>…AtTick</c>/<c>…Tick</c> → <c>…AtMs</c>
/// (<c>actionStartedAtTick</c> → <c>actionStartedAtMs</c>, <c>routeArrivalTick</c> → <c>routeArrivalAtMs</c>); duração
/// <c>…Ticks</c> → <c>…Ms</c> (<c>maxTicks</c> → <c>maxMs</c>). Vale para HTTP, WS e o canal do jogador. O que o motor ainda guarda
/// em horas sai como hora × 3.600.000 (o instante do começo da hora); o que já é ms (o relógio do mundo) sai com nome próprio.
/// Entrada em ms arredonda para a hora de cima enquanto o motor guarda horas. Sentinela grande demais (o "nunca") passa como está.</summary>
public static class WireMillis
{
    public static JsonSerializerOptions Apply(JsonSerializerOptions options)
    {
        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()).WithAddedModifier(Rename);
        return options;
    }

    /// <summary>O nome v2 de um campo em ticks do v1, ou nulo se o campo não é de tick.</summary>
    public static string? Renamed(string name)
    {
        if (name == "tick") return "atMs";
        if (name == "ticks") return "ms";
        if (name.EndsWith("Ticks", StringComparison.Ordinal)) return name[..^"Ticks".Length] + "Ms";
        if (name.EndsWith("AtTick", StringComparison.Ordinal)) return name[..^"AtTick".Length] + "AtMs";
        if (name.EndsWith("Tick", StringComparison.Ordinal)) return name[..^"Tick".Length] + "AtMs";
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
                var t when t == typeof(long) => new HoursAsMs(),
                var t when t == typeof(long?) => new NullableHoursAsMs(),
                var t when t == typeof(int) => new IntHoursAsMs(),
                var t when t == typeof(int?) => new NullableIntHoursAsMs(),
                _ => null,
            };
            if (converter is null) continue;
            property.Name = name;
            property.CustomConverter = converter;
        }
    }

    private const long Limit = long.MaxValue / WorldMillis.PerHour;

    private static long ToMs(long hours) => hours is > Limit or < -Limit ? hours : hours * WorldMillis.PerHour;

    /// <summary>Instante ou duração em ms vindo do cliente, na hora em que o motor ainda conta (hora de cima).</summary>
    public static long HoursFrom(long ms) => ToHours(ms);

    private static long ToHours(long ms) =>
        ms is > Limit * WorldMillis.PerHour or < -Limit * WorldMillis.PerHour ? ms : WorldMillis.ToHourCeiling(ms);

    private sealed class HoursAsMs : JsonConverter<long>
    {
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => ToHours(reader.GetInt64());
        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) => writer.WriteNumberValue(ToMs(value));
    }

    private sealed class NullableHoursAsMs : JsonConverter<long?>
    {
        public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null ? null : ToHours(reader.GetInt64());

        public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
        {
            if (value is { } v) writer.WriteNumberValue(ToMs(v));
            else writer.WriteNullValue();
        }
    }

    private sealed class IntHoursAsMs : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            (int)ToHours(reader.GetInt64());

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) => writer.WriteNumberValue(ToMs(value));
    }

    private sealed class NullableIntHoursAsMs : JsonConverter<int?>
    {
        public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null ? null : (int)ToHours(reader.GetInt64());

        public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
        {
            if (value is { } v) writer.WriteNumberValue(ToMs(v));
            else writer.WriteNullValue();
        }
    }
}
