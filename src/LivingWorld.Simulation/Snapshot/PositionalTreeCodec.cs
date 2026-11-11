using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
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

    /// <summary>Codifica uma árvore em memória: vai a texto compacto e é lida como documento (o mesmo caminho de
    /// <see cref="EncodeTo"/>, então os bytes são os mesmos).</summary>
    public static byte[] Encode(JsonNode? root)
    {
        using var json = new PooledBufferWriter();
        using (var writer = new Utf8JsonWriter(json, new JsonWriterOptions { SkipValidation = true }))
        {
            if (root is null) writer.WriteNullValue();
            else root.WriteTo(writer);
        }

        using var document = JsonDocument.Parse(json.WrittenMemory);
        return Encode(document.RootElement);
    }

    public static byte[] Encode(JsonElement root)
    {
        using var body = new PooledBufferWriter();
        EncodeTo(root, body);
        return body.WrittenSpan.ToArray();
    }

    /// <summary>Escreve o corpo codificado de <paramref name="root"/> em <paramref name="body"/>. O resultado depende só da
    /// árvore lógica (chaves, ordem, valores), não de como o texto JSON foi escrito.</summary>
    public static void EncodeTo(JsonElement root, PooledBufferWriter body) => EncodeTo(body, root, null, null);

    /// <summary>Como <see cref="EncodeTo(JsonElement, PooledBufferWriter)"/> para um objeto dado pelas suas propriedades
    /// (cada valor pode vir de um documento próprio): os mesmos bytes que o objeto inteiro daria. Propriedade com
    /// <paramref name="cached"/> não nulo (em paralelo) é codificada uma vez em trecho próprio e depois só emendada.</summary>
    public static void EncodeTo(
        IReadOnlyList<KeyValuePair<string, JsonElement>> rootObject, PooledBufferWriter body,
        IReadOnlyList<CachedProperty?>? cached = null) =>
        EncodeTo(body, default, rootObject, cached);

    private static void EncodeTo(
        PooledBufferWriter body, JsonElement root, IReadOnlyList<KeyValuePair<string, JsonElement>>? rootObject,
        IReadOnlyList<CachedProperty?>? cached)
    {
        var encoder = new Encoder();
        try
        {
            if (rootObject is null) encoder.WriteNode(root);
            else encoder.WriteObject(rootObject, cached);
            encoder.Finish(body);
        }
        finally
        {
            encoder.Dispose();
        }
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

    /// <summary>Uma subárvore já codificada, com as referências apontando para a tabela de strings própria dela.</summary>
    internal sealed class Segment(byte[] tree, string[] strings)
    {
        public byte[] Tree { get; } = tree;
        public string[] Strings { get; } = strings;

        public static Segment Of(JsonElement value)
        {
            var encoder = new Encoder();
            try
            {
                encoder.WriteNode(value);
                return encoder.ToSegment();
            }
            finally
            {
                encoder.Dispose();
            }
        }
    }

    /// <summary>Escreve o corpo a partir do documento sem materializar string por chave nem por valor: a tabela de strings
    /// é consultada pelo texto UTF-8 cru (sem escape) convertido na pilha; só string nova vira <see cref="string"/> (#156).</summary>
    private sealed class Encoder : IDisposable
    {
        private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> _lookup;
        private readonly List<string> _table = [];
        private readonly PooledBufferWriter _tree = new(1 << 20);

        public Encoder() => _lookup = _index.GetAlternateLookup<ReadOnlySpan<char>>();

        public void Dispose() => _tree.Dispose();

        public void Finish(PooledBufferWriter output)
        {
            WriteVar(output, (ulong)_table.Count);
            foreach (var s in _table)
            {
                int len = Encoding.UTF8.GetByteCount(s);
                WriteVar(output, (ulong)len);
                Encoding.UTF8.GetBytes(s, output.GetSpan(len));
                output.Advance(len);
            }

            output.Write(_tree.WrittenSpan);
        }

        private int Intern(string s)
        {
            if (_index.TryGetValue(s, out int i)) return i;
            i = _table.Count;
            _index[s] = i;
            _table.Add(s);
            return i;
        }

        // Cache de mapeamento direto texto cru → índice, para chaves e valores curtos que se repetem (nomes de campo,
        // enums): evita converter para char e consultar o dicionário a cada objeto. Só guarda o que já está na tabela.
        private const int RecentSlots = 1024, RecentMaxLength = 48;
        private readonly byte[] _recentBytes = new byte[RecentSlots * RecentMaxLength];
        private readonly int[] _recentLength = new int[RecentSlots];
        private readonly int[] _recentId = new int[RecentSlots];

        private int InternUtf8(ReadOnlySpan<byte> utf8)
        {
            if (utf8.Length > RecentMaxLength || utf8.Length == 0) return InternUtf8Slow(utf8);

            uint h = 2166136261u;
            foreach (byte b in utf8) h = (h ^ b) * 16777619u;
            int slot = (int)(h & (RecentSlots - 1));
            var stored = _recentBytes.AsSpan(slot * RecentMaxLength, RecentMaxLength);
            if (_recentLength[slot] == utf8.Length && utf8.SequenceEqual(stored[..utf8.Length])) return _recentId[slot];

            int id = InternUtf8Slow(utf8);
            utf8.CopyTo(stored);
            _recentLength[slot] = utf8.Length;
            _recentId[slot] = id;
            return id;
        }

        private int InternUtf8Slow(ReadOnlySpan<byte> utf8)
        {
            char[]? rented = null;
            Span<char> chars = utf8.Length <= 256 ? stackalloc char[256] : (rented = ArrayPool<char>.Shared.Rent(utf8.Length));
            try
            {
                var text = chars[..Encoding.UTF8.GetChars(utf8, chars)];
                if (_lookup.TryGetValue(text, out int i)) return i;
                i = _table.Count;
                var s = new string(text);
                _index[s] = i;
                _table.Add(s);
                return i;
            }
            finally
            {
                if (rented is not null) ArrayPool<char>.Shared.Return(rented);
            }
        }

        public void WriteObject(IReadOnlyList<KeyValuePair<string, JsonElement>> properties, IReadOnlyList<CachedProperty?>? cached)
        {
            _tree.WriteByte(TagObject);
            WriteVar(_tree, (ulong)properties.Count);
            for (int i = 0; i < properties.Count; i++)
            {
                var (name, value) = properties[i];
                WriteVar(_tree, (ulong)Intern(name));
                if (cached?[i] is { } artifacts) Splice(artifacts.Encoded ??= Segment.Of(value));
                else WriteNode(value);
            }
        }

        /// <summary>Emenda um trecho codificado à parte: as strings dele entram na tabela na ordem em que o trecho as viu
        /// primeiro (a ordem em que <see cref="WriteNode"/> as internaria aqui), e cada referência é renumerada.</summary>
        private void Splice(Segment segment)
        {
            var map = ArrayPool<int>.Shared.Rent(Math.Max(1, segment.Strings.Length));
            try
            {
                for (int k = 0; k < segment.Strings.Length; k++) map[k] = Intern(segment.Strings[k]);
                int pos = 0;
                CopyNode(segment.Tree, ref pos, map);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(map);
            }
        }

        private void CopyNode(ReadOnlySpan<byte> src, ref int pos, int[] map)
        {
            byte tag = src[pos++];
            _tree.WriteByte(tag);
            switch (tag)
            {
                case TagNull or TagFalse or TagTrue: return;
                case TagInt or TagUInt: CopyVar(src, ref pos); return;
                case TagDouble:
                    _tree.Write(src.Slice(pos, 8));
                    pos += 8;
                    return;
                case TagString: WriteVar(_tree, (ulong)map[ReadVar(src, ref pos)]); return;
                case TagArray:
                    {
                        long n = ReadVar(src, ref pos);
                        WriteVar(_tree, (ulong)n);
                        for (long i = 0; i < n; i++) CopyNode(src, ref pos, map);
                        return;
                    }
                case TagObject:
                    {
                        long m = ReadVar(src, ref pos);
                        WriteVar(_tree, (ulong)m);
                        for (long i = 0; i < m; i++)
                        {
                            WriteVar(_tree, (ulong)map[ReadVar(src, ref pos)]);
                            CopyNode(src, ref pos, map);
                        }

                        return;
                    }
                case TagTable:
                    {
                        long rows = ReadVar(src, ref pos), cols = ReadVar(src, ref pos);
                        WriteVar(_tree, (ulong)rows);
                        WriteVar(_tree, (ulong)cols);
                        for (long c = 0; c < cols; c++) WriteVar(_tree, (ulong)map[ReadVar(src, ref pos)]);
                        for (long cell = rows * cols; cell > 0; cell--) CopyNode(src, ref pos, map);
                        return;
                    }
                default:
                    throw new InvalidDataException($"tag binária desconhecida: {tag}");
            }
        }

        private void CopyVar(ReadOnlySpan<byte> src, ref int pos)
        {
            int start = pos;
            while ((src[pos++] & 0x80) != 0) { }
            _tree.Write(src[start..pos]);
        }

        /// <summary>Árvore codificada com tabela de strings própria (só o trecho), pronta para <see cref="Splice"/>.</summary>
        public Segment ToSegment() => new(_tree.WrittenSpan.ToArray(), [.. _table]);

        private int InternName(JsonProperty property)
        {
            var raw = JsonMarshal.GetRawUtf8PropertyName(property);
            return raw.IndexOf((byte)'\\') >= 0 ? Intern(property.Name) : InternUtf8(raw);
        }

        private int InternString(JsonElement value)
        {
            var raw = JsonMarshal.GetRawUtf8Value(value);
            raw = raw[1..^1]; // sem as aspas
            return raw.IndexOf((byte)'\\') >= 0 ? Intern(value.GetString()!) : InternUtf8(raw);
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
                    WriteVar(_tree, (ulong)node.GetPropertyCount());
                    foreach (var property in node.EnumerateObject())
                    {
                        WriteVar(_tree, (ulong)InternName(property));
                        WriteNode(property.Value);
                    }

                    return;
                case JsonValueKind.Array:
                    if (IsTable(node)) WriteTable(node);
                    else
                    {
                        _tree.WriteByte(TagArray);
                        WriteVar(_tree, (ulong)node.GetArrayLength());
                        foreach (var item in node.EnumerateArray()) WriteNode(item);
                    }

                    return;
                case JsonValueKind.True: _tree.WriteByte(TagTrue); return;
                case JsonValueKind.False: _tree.WriteByte(TagFalse); return;
                case JsonValueKind.String:
                    _tree.WriteByte(TagString);
                    WriteVar(_tree, (ulong)InternString(node));
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
                    if (!SameName(a.Current, b.Current)) return false;
                }
            }

            return true;
        }

        /// <summary>Mesmo nome: bytes crus iguais, ou (só se algum tem escape) o nome sem escape igual.</summary>
        private static bool SameName(JsonProperty a, JsonProperty b)
        {
            var ra = JsonMarshal.GetRawUtf8PropertyName(a);
            var rb = JsonMarshal.GetRawUtf8PropertyName(b);
            if (ra.SequenceEqual(rb)) return true;
            if (ra.IndexOf((byte)'\\') < 0 && rb.IndexOf((byte)'\\') < 0) return false;
            return b.NameEquals(a.Name);
        }

        private void WriteTable(JsonElement arr)
        {
            JsonElement first = default;
            foreach (var row in arr.EnumerateArray()) { first = row; break; }
            int columns = first.GetPropertyCount();

            _tree.WriteByte(TagTable);
            WriteVar(_tree, (ulong)arr.GetArrayLength());
            WriteVar(_tree, (ulong)columns);
            foreach (var property in first.EnumerateObject()) WriteVar(_tree, (ulong)InternName(property));
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
            BinaryPrimitives.WriteInt64LittleEndian(_tree.GetSpan(8), BitConverter.DoubleToInt64Bits(d));
            _tree.Advance(8);
        }
    }

    private static void WriteVar(PooledBufferWriter s, ulong v)
    {
        while (v >= 0x80)
        {
            s.WriteByte((byte)(v | 0x80));
            v >>= 7;
        }

        s.WriteByte((byte)v);
    }

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
