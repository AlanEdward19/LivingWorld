using System.Text.Json;
using System.Text.Json.Serialization;

namespace LivingWorld.Domain.Cognition;

/// <summary>Os ganhos do cérebro no snapshot (AD-160b): quando todos são múltiplos de 10‰ e cabem num byte (−1270 a +1270), vão
/// como texto base64 de um byte com sinal por ganho, em décimos de ponto percentual (os 80 ganhos herdados com ruído eram o
/// grosso dos bytes do cérebro: ~3 bytes cada em lista). Senão (degrau 1, o AD-142), lista de números. Lê os dois. A leitura
/// humana do cérebro é o <c>BrainReadout</c>, não o snapshot.</summary>
public sealed class GainsJsonConverter : JsonConverter<short[]>
{
    private const int Unit = 10;

    public override short[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<short>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) list.Add(reader.GetInt16());
            return [.. list];
        }

        byte[] bytes = reader.GetBytesFromBase64();
        var values = new short[bytes.Length];
        for (int i = 0; i < bytes.Length; i++) values[i] = (short)((sbyte)bytes[i] * Unit);
        return values;
    }

    public override void Write(Utf8JsonWriter writer, short[] value, JsonSerializerOptions options)
    {
        bool fits = true;
        foreach (short v in value)
            if (v % Unit != 0 || v / Unit is < sbyte.MinValue or > sbyte.MaxValue) { fits = false; break; }
        if (!fits)
        {
            writer.WriteStartArray();
            foreach (short v in value) writer.WriteNumberValue(v);
            writer.WriteEndArray();
            return;
        }

        Span<byte> bytes = stackalloc byte[value.Length];
        for (int i = 0; i < value.Length; i++) bytes[i] = (byte)(sbyte)(value[i] / Unit);
        writer.WriteBase64StringValue(bytes);
    }
}
