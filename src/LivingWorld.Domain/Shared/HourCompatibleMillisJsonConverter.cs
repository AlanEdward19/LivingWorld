using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LivingWorld.Domain.Shared;

/// <summary>Instante ou duração de mundo guardado em milissegundos (ADR-0050) num campo que antes guardava horas. Na hora cheia
/// escreve as horas, o número de antes: um mundo que só andou de hora em hora tem o mesmo save e o mesmo hash canônico, e um save
/// antigo (horas) carrega convertido. Dentro da hora escreve o texto <c>"&lt;ms&gt;ms"</c>; lê também <c>"&lt;s&gt;s"</c>, a forma
/// em segundos de antes da troca para ms. Valor grande demais para caber em horas × 3.600.000 (as sentinelas de "nunca",
/// <see cref="long.MaxValue"/>) passa como está, nos dois sentidos.</summary>
public sealed class HourCompatibleMillisJsonConverter : JsonConverter<long>
{
    private const long Limit = long.MaxValue / WorldMillis.PerHour;

    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string text = reader.GetString()!;
            if (text.EndsWith("ms", StringComparison.Ordinal))
                return long.Parse(text.AsSpan(0, text.Length - 2), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            return checked(long.Parse(text.AsSpan(0, text.Length - 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)
                * WorldMillis.PerSecond);
        }

        long value = reader.GetInt64();
        return value is > Limit or < -Limit ? value : value * WorldMillis.PerHour;
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        if (value is > Limit * WorldMillis.PerHour or < -Limit * WorldMillis.PerHour) writer.WriteNumberValue(value);
        else if (value % WorldMillis.PerHour == 0) writer.WriteNumberValue(value / WorldMillis.PerHour);
        else writer.WriteStringValue(string.Create(CultureInfo.InvariantCulture, $"{value}ms"));
    }
}
