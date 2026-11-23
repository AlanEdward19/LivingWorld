using System.Text.Json;
using System.Text.Json.Serialization;

namespace LivingWorld.Domain.Shared;

/// <summary>Forma serializada da <see cref="WorldDate"/> (save e hash canônico, ADR-0050): a mesma de antes da troca para segundos
/// (<c>Calendar</c>, <c>TotalHours</c>, <c>Hour</c>, <c>Day</c>, <c>Month</c>, <c>Year</c>) e, só quando o instante não cai na hora
/// cheia, <c>SecondOfHour</c> no fim. Um mundo que só andou de hora em hora tem os mesmos bytes e o mesmo hash de antes; um save
/// antigo (só horas) carrega como o começo da hora: a conversão horas → segundos é esta leitura.</summary>
public sealed class WorldDateJsonConverter : JsonConverter<WorldDate>
{
    public override WorldDate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("WorldDate: objeto esperado");
        WorldCalendar? calendar = null;
        long hours = 0, secondOfHour = 0;
        long? totalSeconds = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            string name = reader.GetString()!;
            reader.Read();
            if (Is(name, "Calendar")) calendar = JsonSerializer.Deserialize<WorldCalendar>(ref reader, options);
            else if (Is(name, "TotalHours")) hours = reader.GetInt64();
            else if (Is(name, "SecondOfHour")) secondOfHour = reader.GetInt64();
            else if (Is(name, "TotalSeconds")) totalSeconds = reader.GetInt64();
            else reader.Skip();
        }

        return WorldDate.FromSeconds(calendar!, totalSeconds ?? WorldSeconds.FromHours(hours) + secondOfHour);
    }

    public override void Write(Utf8JsonWriter writer, WorldDate value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("Calendar");
        JsonSerializer.Serialize(writer, value.Calendar, options);
        writer.WriteNumber("TotalHours", value.TotalHours);
        writer.WriteNumber("Hour", value.Hour);
        writer.WriteNumber("Day", value.Day);
        writer.WriteNumber("Month", value.Month);
        writer.WriteNumber("Year", value.Year);
        if (value.SecondOfHour != 0) writer.WriteNumber("SecondOfHour", value.SecondOfHour);
        writer.WriteEndObject();
    }

    private static bool Is(string name, string expected) => string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);
}
