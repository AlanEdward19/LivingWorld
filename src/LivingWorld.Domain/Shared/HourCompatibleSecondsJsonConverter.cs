using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LivingWorld.Domain.Shared;

/// <summary>Instante ou duração de mundo guardado em segundos (ADR-0050) num campo que antes guardava horas. Na hora cheia escreve
/// as horas, o número de antes: um mundo que só andou de hora em hora tem o mesmo save e o mesmo hash canônico, e um save antigo
/// (horas) carrega convertido para segundos. Dentro da hora escreve o texto <c>"&lt;segundos&gt;s"</c>. Valor grande demais para
/// caber em horas × 3.600 (as sentinelas de "nunca", <see cref="long.MaxValue"/>) passa como está, nos dois sentidos.</summary>
public sealed class HourCompatibleSecondsJsonConverter : JsonConverter<long>
{
    private const long Limit = long.MaxValue / WorldSeconds.PerHour;

    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string text = reader.GetString()!;
            return long.Parse(text.AsSpan(0, text.Length - 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }

        long value = reader.GetInt64();
        return value is > Limit or < -Limit ? value : value * WorldSeconds.PerHour;
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        if (value is > Limit * WorldSeconds.PerHour or < -Limit * WorldSeconds.PerHour) writer.WriteNumberValue(value);
        else if (value % WorldSeconds.PerHour == 0) writer.WriteNumberValue(value / WorldSeconds.PerHour);
        else writer.WriteStringValue(string.Create(CultureInfo.InvariantCulture, $"{value}s"));
    }
}
