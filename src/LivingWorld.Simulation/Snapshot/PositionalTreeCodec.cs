using System.Buffers.Binary;
using System.Globalization;
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
        var encoder = new Encoder();
        encoder.WriteNode(root);
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

        public void WriteNode(JsonNode? node)
        {
            switch (node)
            {
                case null:
                    _tree.WriteByte(TagNull);
                    return;
                case JsonObject obj:
                    _tree.WriteByte(TagObject);
                    WriteVar(_tree, obj.Count);
                    foreach (var (key, value) in obj)
                    {
                        WriteVar(_tree, Intern(key));
                        WriteNode(value);
                    }

                    return;
                case JsonArray arr:
                    if (IsTable(arr)) WriteTable(arr);
                    else
                    {
                        _tree.WriteByte(TagArray);
                        WriteVar(_tree, arr.Count);
                        foreach (var item in arr) WriteNode(item);
                    }

                    return;
                case JsonValue value:
                    WriteValue(value);
                    return;
            }
        }

        private static bool IsTable(JsonArray arr)
        {
            if (arr.Count < 2 || arr[0] is not JsonObject first) return false;
            for (int i = 1; i < arr.Count; i++)
            {
                if (arr[i] is not JsonObject row || row.Count != first.Count) return false;
                using var a = first.GetEnumerator();
                using var b = row.GetEnumerator();
                while (a.MoveNext() && b.MoveNext())
                    if (!string.Equals(a.Current.Key, b.Current.Key, StringComparison.Ordinal)) return false;
            }

            return true;
        }

        private void WriteTable(JsonArray arr)
        {
            var first = (JsonObject)arr[0]!;
            _tree.WriteByte(TagTable);
            WriteVar(_tree, arr.Count);
            WriteVar(_tree, first.Count);
            foreach (var (key, _) in first) WriteVar(_tree, Intern(key));
            foreach (var item in arr)
                foreach (var (_, value) in (JsonObject)item!)
                    WriteNode(value);
        }

        private void WriteValue(JsonValue value)
        {
            switch (value.GetValueKind())
            {
                case JsonValueKind.True: _tree.WriteByte(TagTrue); return;
                case JsonValueKind.False: _tree.WriteByte(TagFalse); return;
                case JsonValueKind.Null: _tree.WriteByte(TagNull); return;
                case JsonValueKind.String:
                    _tree.WriteByte(TagString);
                    WriteVar(_tree, Intern(value.GetValue<string>()));
                    return;
                case JsonValueKind.Number:
                    WriteNumber(value);
                    return;
                default:
                    throw new InvalidDataException($"valor JSON inesperado: {value.GetValueKind()}");
            }
        }

        private void WriteNumber(JsonValue value)
        {
            if (value.TryGetValue(out long l)) { WriteInt(l); return; }
            if (value.TryGetValue(out ulong u)) { _tree.WriteByte(TagUInt); WriteVar(_tree, u); return; }
            if (value.TryGetValue(out int i)) { WriteInt(i); return; }
            if (value.TryGetValue(out double d)) { WriteDouble(d); return; }
            if (value.TryGetValue(out float f)) { WriteDouble(f); return; }

            // último recurso: o texto do próprio JSON
            var text = value.ToJsonString();
            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out l)) WriteInt(l);
            else if (ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out u)) { _tree.WriteByte(TagUInt); WriteVar(_tree, u); }
            else WriteDouble(double.Parse(text, CultureInfo.InvariantCulture));
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
