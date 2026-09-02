using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;

namespace LivingWorld.Simulation.Snapshot;

/// <summary>Snapshot canônico binário posicional (PERF-11).</summary>
public sealed class BinarySnapshotWriter
{
    private static readonly byte[] Magic = "LWSNAP1\0"u8.ToArray();

    public void WriteFull(WorldState world, Stream output)
    {
        using var ms = new MemoryStream();
        ms.Write(Magic);
        ms.WriteByte(0);
        WritePayload(world, baseline: null, ms, dirtyNpcIds: null);
        ms.Position = 0;
        ms.CopyTo(output);
    }

    public void WriteDelta(WorldState world, WorldState baseline, IReadOnlySet<long> dirtyNpcIds, Stream output)
    {
        using var ms = new MemoryStream();
        ms.Write(Magic);
        ms.WriteByte(1);
        WritePayload(world, baseline, ms, dirtyNpcIds);
        ms.Position = 0;
        ms.CopyTo(output);
    }

    public WorldState ReadAndApply(Stream input, WorldState baseline)
    {
        Span<byte> magic = stackalloc byte[Magic.Length];
        input.ReadExactly(magic);
        if (!magic.SequenceEqual(Magic))
            throw new InvalidDataException("magic inválido");

        int marker = input.ReadByte();
        if (marker < 0)
            throw new EndOfStreamException();
        IReadOnlySet<long>? dirty = marker == 1 ? ReadDirtySet(input) : null;
        _ = dirty;

        int jsonLen = ReadInt32(input);
        var jsonBytes = new byte[jsonLen];
        input.ReadExactly(jsonBytes);
        var json = Encoding.UTF8.GetString(jsonBytes);

        return marker == 1
            ? ApplyDeltaToBaseline(baseline, JsonNode.Parse(json)!.AsObject())
            : WorldSnapshot.Deserialize(json);
    }

    private static void WritePayload(
        WorldState world,
        WorldState? baseline,
        Stream ms,
        IReadOnlySet<long>? dirtyNpcIds)
    {
        if (dirtyNpcIds is not null)
            WriteDirtySet(ms, dirtyNpcIds);

        var json = dirtyNpcIds is null
            ? WorldSnapshot.Serialize(world)
            : BuildFieldDiffJson(world, baseline!, dirtyNpcIds);
        var bytes = Encoding.UTF8.GetBytes(json);
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(len, bytes.Length);
        ms.Write(len);
        ms.Write(bytes);
    }

    private static void WriteDirtySet(Stream ms, IReadOnlySet<long> dirtyNpcIds)
    {
        var ids = dirtyNpcIds.OrderBy(id => id).ToArray();
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(buf, ids.Length);
        ms.Write(buf[..4]);
        foreach (var id in ids)
        {
            BinaryPrimitives.WriteInt64LittleEndian(buf, id);
            ms.Write(buf);
        }
    }

    private static IReadOnlySet<long> ReadDirtySet(Stream input)
    {
        int count = ReadInt32(input);
        var set = new HashSet<long>();
        for (int i = 0; i < count; i++)
            set.Add(ReadInt64(input));
        return set;
    }

    private static int ReadInt32(Stream input)
    {
        Span<byte> buf = stackalloc byte[4];
        input.ReadExactly(buf);
        return BinaryPrimitives.ReadInt32LittleEndian(buf);
    }

    private static long ReadInt64(Stream input)
    {
        Span<byte> buf = stackalloc byte[8];
        input.ReadExactly(buf);
        return BinaryPrimitives.ReadInt64LittleEndian(buf);
    }

    private static string BuildFieldDiffJson(WorldState world, WorldState baseline, IReadOnlySet<long> dirtyNpcIds)
    {
        var currentRoot = ParseResolvedSnapshot(world);
        var baselineNpcById = IndexNpcsById(ParseResolvedSnapshot(baseline)["Npcs"]!.AsArray());

        var currentNpcs = currentRoot["Npcs"]!.AsArray();
        var diffNpcs = new JsonArray();
        foreach (var entry in currentNpcs)
        {
            var id = entry!["Id"]!["Value"]!.GetValue<long>();
            if (!dirtyNpcIds.Contains(id))
                continue;

            baselineNpcById.TryGetValue(id, out var baselineNpc);
            diffNpcs.Add(BuildNpcFieldDiff(entry.AsObject(), baselineNpc));
        }

        currentRoot["Npcs"] = diffNpcs;
        return currentRoot.ToJsonString();
    }

    private static JsonObject ParseResolvedSnapshot(WorldState world)
    {
        var root = JsonNode.Parse(WorldSnapshot.Serialize(world))!.AsObject();
        SnapshotStringInterning.Resolve(root);
        return root;
    }

    private static Dictionary<long, JsonObject> IndexNpcsById(JsonArray npcs)
    {
        var map = new Dictionary<long, JsonObject>();
        foreach (var entry in npcs)
        {
            var id = entry!["Id"]!["Value"]!.GetValue<long>();
            map[id] = entry.AsObject();
        }

        return map;
    }

    private static JsonObject BuildNpcFieldDiff(JsonObject current, JsonObject? baseline)
    {
        var diff = new JsonObject();
        if (current.TryGetPropertyValue("Id", out var idNode) && idNode is not null)
            diff["Id"] = idNode.DeepClone();

        foreach (var prop in current.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (prop.Key == "Id")
                continue;

            var currentVal = prop.Value;
            if (baseline is null
                || !baseline.TryGetPropertyValue(prop.Key, out var baseVal)
                || !JsonNode.DeepEquals(currentVal, baseVal))
            {
                diff[prop.Key] = currentVal?.DeepClone();
            }
        }

        return diff;
    }

    private static WorldState ApplyDeltaToBaseline(WorldState baseline, JsonObject deltaRoot)
    {
        var merged = ParseResolvedSnapshot(baseline);
        var deltaResolved = deltaRoot.DeepClone().AsObject();
        SnapshotStringInterning.Resolve(deltaResolved);

        foreach (var prop in deltaResolved.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (prop.Key == "Npcs")
                MergeNpcDiffs(merged, prop.Value!.AsArray());
            else
                merged[prop.Key] = prop.Value?.DeepClone();
        }

        return WorldSnapshot.Deserialize(merged.ToJsonString());
    }

    private static void MergeNpcDiffs(JsonObject baselineRoot, JsonArray diffNpcs)
    {
        var baselineNpcs = baselineRoot["Npcs"]!.AsArray();
        var index = new Dictionary<long, int>();
        for (int i = 0; i < baselineNpcs.Count; i++)
            index[baselineNpcs[i]!["Id"]!["Value"]!.GetValue<long>()] = i;

        foreach (var diffEntry in diffNpcs)
        {
            var diffObj = diffEntry!.AsObject();
            var id = diffObj["Id"]!["Value"]!.GetValue<long>();
            if (index.TryGetValue(id, out int i))
            {
                var target = baselineNpcs[i]!.AsObject();
                foreach (var prop in diffObj.OrderBy(p => p.Key, StringComparer.Ordinal))
                    target[prop.Key] = prop.Value?.DeepClone();
            }
            else
            {
                baselineNpcs.Add(diffObj.DeepClone());
            }
        }
    }
}
