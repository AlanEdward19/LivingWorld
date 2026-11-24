using System.Buffers.Binary;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Tests.Unit.Snapshot;

/// <summary>Definição de referência da gravação de snapshot, como era antes do #156: a árvore <see cref="JsonNode"/> de
/// <see cref="WorldSnapshot.BuildPersistentTree"/>, o diff e as impressões sobre ela, o codec com tabela de strings
/// por <see cref="string"/>, e os hashes como SHA-256 do <c>ToJsonString</c>. Lenta e cheia de alocação de propósito:
/// é o oráculo contra o qual o caminho rápido tem de dar os mesmos bytes.</summary>
internal static class SnapshotWriteReference
{
    private static readonly byte[] Magic = "LWSNAP2\0"u8.ToArray();

    public static byte[] FullBody(WorldState world) => Encode(WorldSnapshot.BuildPersistentTree(world));

    public static Prints Fingerprint(WorldState world) => Prints.Of(WorldSnapshot.BuildPersistentTree(world));

    public static byte[] DeltaBody(WorldState world, Prints baseline, IReadOnlySet<long>? dirty) =>
        Encode(Diff(WorldSnapshot.BuildPersistentTree(world), baseline, dirty));

    /// <summary>O bloco inteiro: magic, tipo com o bit de compressão e o corpo em Brotli (qualidade 4, janela 22).</summary>
    public static byte[] Block(byte kind, byte[] body)
    {
        var buffer = new byte[9 + BrotliEncoder.GetMaxCompressedLength(body.Length)];
        Assert.True(BrotliEncoder.TryCompress(body, buffer.AsSpan(9), out int written, 4, 22));
        Magic.CopyTo(buffer, 0);
        buffer[8] = (byte)(kind | 0x80);
        return buffer.AsSpan(0, 9 + written).ToArray();
    }

    public static string CanonicalHash(WorldState world) =>
        Hash(world, p => p.GetCustomAttribute<CanonicalAttribute>() is not null);

    public static string VolatileHash(WorldState world) =>
        Hash(world, p => p.GetCustomAttribute<VolatileAttribute>() is not null);

    private static string Hash(WorldState world, Func<PropertyInfo, bool> filter)
    {
        var obj = new JsonObject();
        foreach (var prop in WorldSnapshot.ReflectedProperties)
        {
            if (!filter(prop)) continue;
            var value = prop.GetValue(world);
            if (value is null && WorldSnapshot.OmitsNull(prop)) continue;
            obj[prop.Name] = JsonSerializer.SerializeToNode(value, prop.PropertyType, WorldSnapshot.SnapshotJsonOptions);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(obj.ToJsonString(WorldSnapshot.SnapshotJsonOptions))));
    }

    internal sealed class Prints
    {
        public sealed class EntityPrints
        {
            public string[] Fields = [];
            public List<long> Order = [];
            public Dictionary<long, ulong[]?> ById = [];
        }

        public Dictionary<string, EntityPrints> Entities { get; } = [];
        public Dictionary<string, Dictionary<string, ulong>> Keys { get; } = [];
        public Dictionary<string, ulong> Props { get; } = [];

        public static Prints Of(JsonObject tree)
        {
            var result = new Prints();
            foreach (var (name, value) in tree)
            {
                if (value is JsonArray array && TryIndex(array, out var ids))
                {
                    var prints = new EntityPrints { Fields = array[0]!.AsObject().Select(kv => kv.Key).ToArray() };
                    foreach (var (id, at) in ids.OrderBy(kv => kv.Value))
                    {
                        prints.Order.Add(id);
                        var entry = array[at]!.AsObject();
                        if (entry.Count != prints.Fields.Length) { prints.ById[id] = null; continue; }
                        var fields = new ulong[entry.Count];
                        int f = 0;
                        bool same = true;
                        foreach (var (field, child) in entry)
                        {
                            if (field != prints.Fields[f]) { same = false; break; }
                            fields[f++] = Print(child);
                        }

                        prints.ById[id] = same ? fields : null;
                    }

                    result.Entities[name] = prints;
                }
                else if (value is JsonObject obj)
                {
                    var keys = new Dictionary<string, ulong>(obj.Count);
                    foreach (var (key, child) in obj) keys[key] = Print(child);
                    result.Keys[name] = keys;
                }
                else
                    result.Props[name] = Print(value);
            }

            return result;
        }
    }

    public static ulong Print(JsonNode? node)
    {
        if (node is null) return 1;
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { SkipValidation = true })) node.WriteTo(writer);
        var bytes = buffer.WrittenSpan;
        ulong h = 14695981039346656037UL;
        int i = 0;
        for (; i + 8 <= bytes.Length; i += 8) h = Step(h, BinaryPrimitives.ReadUInt64LittleEndian(bytes[i..]));
        ulong tail = 0;
        for (int shift = 0; i < bytes.Length; i++, shift += 8) tail |= (ulong)bytes[i] << shift;
        return Step(Step(h, tail), (ulong)bytes.Length);
    }

    private static ulong Step(ulong h, ulong x)
    {
        h = (h ^ x) * 0x9E3779B97F4A7C15UL;
        return h ^ (h >> 32);
    }

    private static bool TryIndex(JsonArray array, out Dictionary<long, int> index)
    {
        index = new Dictionary<long, int>(array.Count);
        if (array.Count == 0) return false;
        for (int i = 0; i < array.Count; i++)
            if (array[i] is not JsonObject o || o["Id"] is not JsonObject id || id["Value"] is not JsonValue v
                || !v.TryGetValue(out long value) || !index.TryAdd(value, i))
                return false;
        return true;
    }

    private static JsonObject Diff(JsonObject current, Prints baseline, IReadOnlySet<long>? dirtyNpcIds)
    {
        JsonObject entities = [], keys = [], removed = [], props = [], order = [], moved = [];
        foreach (var (name, value) in current)
        {
            if (value is JsonArray ca && baseline.Entities.TryGetValue(name, out var be) && TryIndex(ca, out var curIds))
            {
                var dirty = name == "Npcs" ? dirtyNpcIds : null;
                var changed = new JsonArray();
                foreach (var (id, at) in curIds.OrderBy(kv => kv.Value))
                {
                    var entry = ca[at]!.AsObject();
                    if (!be.ById.TryGetValue(id, out var prints)) { changed.Add(entry.DeepClone()); continue; }
                    if (dirty is not null && !dirty.Contains(id)) continue;
                    JsonObject? fieldDiff = null;
                    int f = 0;
                    bool sameShape = prints is not null && entry.Count == be.Fields.Length
                        && entry.Select(kv => kv.Key).SequenceEqual(be.Fields);
                    if (!sameShape)
                    {
                        // Forma diferente da base: a entidade vai inteira e substitui a anterior (um campo pode ter sumido).
                        var whole = new JsonObject { ["$full"] = true };
                        foreach (var (field, child) in entry) whole[field] = child?.DeepClone();
                        changed.Add(whole);
                        continue;
                    }

                    foreach (var (field, child) in entry)
                    {
                        if (prints![f] == Print(child)) { f++; continue; }
                        f++;
                        if (field == "Id") continue;
                        fieldDiff ??= new JsonObject { ["Id"] = entry["Id"]!.DeepClone() };
                        fieldDiff[field] = child?.DeepClone();
                    }

                    if (fieldDiff is not null) changed.Add(fieldDiff);
                }

                if (changed.Count > 0) entities[name] = changed;
                var gone = new JsonArray();
                foreach (var id in be.Order) if (!curIds.ContainsKey(id)) gone.Add(id);
                if (gone.Count > 0) removed[name] = gone;
                var currentOrder = curIds.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
                var expected = be.Order.Where(curIds.ContainsKey).Concat(currentOrder.Where(id => !be.ById.ContainsKey(id)));
                if (!expected.SequenceEqual(currentOrder))
                    order[name] = new JsonArray(currentOrder.Select(id => (JsonNode)JsonValue.Create(id)).ToArray());
            }
            else if (value is JsonObject co && baseline.Keys.TryGetValue(name, out var baseKeys))
            {
                // movida: chave da base fora da ordem da base, ou depois de uma chave nova
                var baseIndex = baseKeys.Keys.Select((k, i) => (k, i)).ToDictionary(t => t.k, t => t.i, StringComparer.Ordinal);
                var movedKeys = new HashSet<string>(StringComparer.Ordinal);
                bool pastStable = false;
                int last = -1;
                foreach (var (key, _) in co)
                {
                    if (!baseIndex.TryGetValue(key, out int at)) { pastStable = true; continue; }
                    if (!pastStable && at > last) { last = at; continue; }
                    pastStable = true;
                    movedKeys.Add(key);
                }

                JsonObject? changed = null;
                foreach (var (key, child) in co)
                {
                    if (baseKeys.TryGetValue(key, out var print) && print == Print(child) && !movedKeys.Contains(key)) continue;
                    changed ??= [];
                    changed[key] = child?.DeepClone();
                }

                if (changed is not null) keys[name] = changed;
                if (movedKeys.Count > 0)
                    moved[name] = new JsonArray(movedKeys.OrderBy(k => k, StringComparer.Ordinal).Select(k => (JsonNode)JsonValue.Create(k)!).ToArray());
                var goneKeys = baseKeys.Keys.Where(k => !co.ContainsKey(k)).Select(k => (JsonNode)JsonValue.Create(k)!).ToArray();
                if (goneKeys.Length > 0) removed[name] = new JsonArray(goneKeys);
            }
            else if (!baseline.Props.TryGetValue(name, out var print) || print != Print(value))
                props[name] = value?.DeepClone();
        }

        var delta = new JsonObject { ["Entities"] = entities, ["Keys"] = keys, ["Removed"] = removed, ["Props"] = props, ["Order"] = order };
        if (moved.Count > 0) delta["Moved"] = moved;
        return delta;
    }

    private const byte TagNull = 0, TagFalse = 1, TagTrue = 2, TagInt = 3, TagUInt = 4, TagDouble = 5, TagString = 6,
        TagArray = 7, TagObject = 8, TagTable = 9;

    private static byte[] Encode(JsonNode root)
    {
        using var document = JsonDocument.Parse(root.ToJsonString());
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        var table = new List<string>();
        using var tree = new MemoryStream();

        int Intern(string s)
        {
            if (index.TryGetValue(s, out int i)) return i;
            index[s] = table.Count;
            table.Add(s);
            return table.Count - 1;
        }

        void WriteInt(long l)
        {
            tree.WriteByte(TagInt);
            WriteVar(tree, (ulong)((l << 1) ^ (l >> 63)));
        }

        void Write(JsonElement node)
        {
            switch (node.ValueKind)
            {
                case JsonValueKind.Null: tree.WriteByte(TagNull); return;
                case JsonValueKind.True: tree.WriteByte(TagTrue); return;
                case JsonValueKind.False: tree.WriteByte(TagFalse); return;
                case JsonValueKind.String: tree.WriteByte(TagString); WriteVar(tree, (ulong)Intern(node.GetString()!)); return;
                case JsonValueKind.Number:
                    if (node.TryGetInt64(out long l)) { WriteInt(l); return; }
                    if (node.TryGetUInt64(out ulong u)) { tree.WriteByte(TagUInt); WriteVar(tree, u); return; }
                    double d = node.GetDouble();
                    if (d == Math.Floor(d) && Math.Abs(d) < 9e15) { WriteInt((long)d); return; }
                    tree.WriteByte(TagDouble);
                    Span<byte> buf = stackalloc byte[8];
                    BinaryPrimitives.WriteInt64LittleEndian(buf, BitConverter.DoubleToInt64Bits(d));
                    tree.Write(buf);
                    return;
                case JsonValueKind.Object:
                    var props = node.EnumerateObject().ToList();
                    tree.WriteByte(TagObject);
                    WriteVar(tree, (ulong)props.Count);
                    foreach (var p in props) { WriteVar(tree, (ulong)Intern(p.Name)); Write(p.Value); }
                    return;
                default:
                    var rows = node.EnumerateArray().ToList();
                    var names = rows.Count >= 2 && rows.All(r => r.ValueKind == JsonValueKind.Object)
                        ? rows[0].EnumerateObject().Select(p => p.Name).ToList() : null;
                    if (names is not null && rows.All(r => r.EnumerateObject().Select(p => p.Name).SequenceEqual(names)))
                    {
                        tree.WriteByte(TagTable);
                        WriteVar(tree, (ulong)rows.Count);
                        WriteVar(tree, (ulong)names.Count);
                        foreach (var n in names) WriteVar(tree, (ulong)Intern(n));
                        foreach (var r in rows) foreach (var p in r.EnumerateObject()) Write(p.Value);
                    }
                    else
                    {
                        tree.WriteByte(TagArray);
                        WriteVar(tree, (ulong)rows.Count);
                        foreach (var r in rows) Write(r);
                    }

                    return;
            }
        }

        Write(document.RootElement);
        using var output = new MemoryStream();
        WriteVar(output, (ulong)table.Count);
        foreach (var s in table)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            WriteVar(output, (ulong)bytes.Length);
            output.Write(bytes);
        }

        tree.WriteTo(output);
        return output.ToArray();
    }

    private static void WriteVar(Stream s, ulong v)
    {
        for (; v >= 0x80; v >>= 7) s.WriteByte((byte)(v | 0x80));
        s.WriteByte((byte)v);
    }
}
