using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LivingWorld.Simulation.Snapshot;

/// <summary>Codec binário posicional da árvore canônica do snapshot (formato v2): tabela de strings única (chaves e
/// valores aparecem uma vez), inteiros em varint com sinal zigzag, doubles em 8 bytes, e array de objetos de mesmas chaves
/// vira tabela (as chaves uma vez, depois só os valores na ordem das chaves). Determinístico: mesma árvore, mesmos bytes;
/// decodificar e recodificar devolve os mesmos bytes.</summary>
internal static class PositionalTreeCodec
{
    private const byte TagNull = 0, TagFalse = 1, TagTrue = 2, TagInt = 3, TagUInt = 4, TagDouble = 5, TagString = 6,
        TagArray = 7, TagObject = 8, TagTable = 9;

    public static byte[] Encode(JsonNode? root)
    {
        // A árvore vai a texto compacto (cópia direta onde o nó ainda guarda o texto do serializador) e é lida como
        // documento: percorrer JsonNode materializava um dicionário por objeto, centenas de MB por gravação (#156).
        var buffer = new System.Buffers.ArrayBufferWriter<byte>(1 << 16);
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { SkipValidation = true }))
        {
            if (root is null) writer.WriteNullValue();
            else root.WriteTo(writer);
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        var encoder = new Encoder();
        encoder.WriteNode(document.RootElement);
        return encoder.Finish();
    }

    public static JsonNode? Decode(ReadOnlySpan<byte> data)
    {
        int pos = 0;
        int count = (int)ReadVar(data, ref pos);
        var strings = new string[count];
        for (int i = 0; i < count; i++)
        {
            int len = (int)ReadVar(data, ref pos);
            strings[i] = Encoding.UTF8.GetString(data.Slice(pos, len));
            pos += len;
        }

        return ReadNode(data, ref pos, strings);
    }

    private sealed class Encoder
    {
        private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);
        private readonly List<string> _table = [];
        private readonly MemoryStream _tree = new();

        public byte[] Finish()
        {
            using var output = new MemoryStream((int)_tree.Length + 1024);
            WriteVar(output, _table.Count);
            foreach (var s in _table)
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                WriteVar(output, bytes.Length);
                output.Write(bytes);
            }

            _tree.WriteTo(output);
            return output.ToArray();
        }

        private int Intern(string s)
        {
            if (_index.TryGetValue(s, out int i)) return i;
            i = _table.Count;
            _index[s] = i;
            _table.Add(s);
            return i;
        }

        public void WriteNode(JsonElement node)
        {
            switch (node.ValueKind)
            {
                case JsonValueKind.Null or JsonValueKind.Undefined:
                    _tree.WriteByte(TagNull);
                    return;
                case JsonValueKind.Object:
                    _tree.WriteByte(TagObject);
                    int properties = 0;
                    foreach (var _ in node.EnumerateObject()) properties++;
                    WriteVar(_tree, properties);
                    foreach (var property in node.EnumerateObject())
                    {
                        WriteVar(_tree, Intern(property.Name));
                        WriteNode(property.Value);
                    }

                    return;
                case JsonValueKind.Array:
                    if (IsTable(node)) WriteTable(node);
                    else
                    {
                        _tree.WriteByte(TagArray);
                        WriteVar(_tree, node.GetArrayLength());
                        foreach (var item in node.EnumerateArray()) WriteNode(item);
                    }

                    return;
                case JsonValueKind.True: _tree.WriteByte(TagTrue); return;
                case JsonValueKind.False: _tree.WriteByte(TagFalse); return;
                case JsonValueKind.String:
                    _tree.WriteByte(TagString);
                    WriteVar(_tree, Intern(node.GetString()!));
                    return;
                case JsonValueKind.Number:
                    WriteNumber(node);
                    return;
                default:
                    throw new InvalidDataException($"valor JSON inesperado: {node.ValueKind}");
            }
        }

        /// <summary>Tabela: dois ou mais objetos com as mesmas chaves na mesma ordem.</summary>
        private static bool IsTable(JsonElement arr)
        {
            if (arr.GetArrayLength() < 2) return false;
            JsonElement first = default;
            bool hasFirst = false;
            foreach (var row in arr.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) return false;
                if (!hasFirst) { first = row; hasFirst = true; continue; }

                using var a = first.EnumerateObject().GetEnumerator();
                using var b = row.EnumerateObject().GetEnumerator();
                while (true)
                {
                    bool moreA = a.MoveNext(), moreB = b.MoveNext();
                    if (moreA != moreB) return false;
                    if (!moreA) break;
                    if (!b.Current.NameEquals(a.Current.Name)) return false;
                }
            }

            return true;
        }

        private void WriteTable(JsonElement arr)
        {
            JsonElement first = default;
            foreach (var row in arr.EnumerateArray()) { first = row; break; }
            int columns = 0;
            foreach (var _ in first.EnumerateObject()) columns++;

            _tree.WriteByte(TagTable);
            WriteVar(_tree, arr.GetArrayLength());
            WriteVar(_tree, columns);
            foreach (var property in first.EnumerateObject()) WriteVar(_tree, Intern(property.Name));
            foreach (var item in arr.EnumerateArray())
                foreach (var property in item.EnumerateObject())
                    WriteNode(property.Value);
        }

        private void WriteNumber(JsonElement value)
        {
            if (value.TryGetInt64(out long l)) { WriteInt(l); return; }
            if (value.TryGetUInt64(out ulong u)) { _tree.WriteByte(TagUInt); WriteVar(_tree, u); return; }
            WriteDouble(value.GetDouble());
        }

        private void WriteInt(long l)
        {
            _tree.WriteByte(TagInt);
            WriteVar(_tree, (ulong)((l << 1) ^ (l >> 63)));
        }

        private void WriteDouble(double d)
        {
            // inteiro exato cabe em varint e é como o JSON o escreve ("5", não "5.0")
            if (d == Math.Floor(d) && Math.Abs(d) < 9e15) { WriteInt((long)d); return; }
            _tree.WriteByte(TagDouble);
            Span<byte> buf = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(buf, BitConverter.DoubleToInt64Bits(d));
            _tree.Write(buf);
        }
    }

    private static void WriteVar(Stream s, ulong v)
    {
        while (v >= 0x80)
        {
            s.WriteByte((byte)(v | 0x80));
            v >>= 7;
        }

        s.WriteByte((byte)v);
    }

    private static void WriteVar(Stream s, int v) => WriteVar(s, (ulong)v);

    private static long ReadVar(ReadOnlySpan<byte> data, ref int pos)
    {
        ulong result = 0;
        int shift = 0;
        while (true)
        {
            byte b = data[pos++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return (long)result;
            shift += 7;
        }
    }

    private static JsonNode? ReadNode(ReadOnlySpan<byte> data, ref int pos, string[] strings)
    {
        byte tag = data[pos++];
        switch (tag)
        {
            case TagNull: return null;
            case TagFalse: return JsonValue.Create(false);
            case TagTrue: return JsonValue.Create(true);
            case TagInt:
                ulong z = (ulong)ReadVar(data, ref pos);
                return JsonValue.Create((long)(z >> 1) ^ -(long)(z & 1));
            case TagUInt: return JsonValue.Create((ulong)ReadVar(data, ref pos));
            case TagDouble:
                double d = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(data.Slice(pos, 8)));
                pos += 8;
                return JsonValue.Create(d);
            case TagString: return JsonValue.Create(strings[(int)ReadVar(data, ref pos)]);
            case TagArray:
                int n = (int)ReadVar(data, ref pos);
                var arr = new JsonArray();
                for (int i = 0; i < n; i++) arr.Add(ReadNode(data, ref pos, strings));
                return arr;
            case TagObject:
                int m = (int)ReadVar(data, ref pos);
                var obj = new JsonObject();
                for (int i = 0; i < m; i++)
                {
                    string key = strings[(int)ReadVar(data, ref pos)];
                    obj[key] = ReadNode(data, ref pos, strings);
                }

                return obj;
            case TagTable:
                int rows = (int)ReadVar(data, ref pos);
                int cols = (int)ReadVar(data, ref pos);
                var keys = new string[cols];
                for (int c = 0; c < cols; c++) keys[c] = strings[(int)ReadVar(data, ref pos)];
                var table = new JsonArray();
                for (int r = 0; r < rows; r++)
                {
                    var row = new JsonObject();
                    for (int c = 0; c < cols; c++) row[keys[c]] = ReadNode(data, ref pos, strings);
                    table.Add(row);
                }

                return table;
            default:
                throw new InvalidDataException($"tag binária desconhecida: {tag}");
        }
    }
}
