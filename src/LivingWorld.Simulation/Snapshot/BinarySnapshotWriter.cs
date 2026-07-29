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
        WritePayload(world, ms, dirtyNpcIds: null);
        ms.Position = 0;
        ms.CopyTo(output);
    }

    public void WriteDelta(WorldState world, IReadOnlySet<long> dirtyNpcIds, Stream output)
    {
        using var ms = new MemoryStream();
        ms.Write(Magic);
        ms.WriteByte(1);
        WritePayload(world, ms, dirtyNpcIds);
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

        int jsonLen = ReadInt32(input);
        var jsonBytes = new byte[jsonLen];
        input.ReadExactly(jsonBytes);
        var json = Encoding.UTF8.GetString(jsonBytes);
        _ = baseline;
        return WorldSnapshot.Deserialize(json);
    }

    private static void WritePayload(WorldState world, Stream ms, IReadOnlySet<long>? dirtyNpcIds)
    {
        var json = dirtyNpcIds is null ? WorldSnapshot.Serialize(world) : BuildPartialJson(world, dirtyNpcIds);
        var bytes = Encoding.UTF8.GetBytes(json);
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(len, bytes.Length);
        ms.Write(len);
        ms.Write(bytes);
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

    private static string BuildPartialJson(WorldState world, IReadOnlySet<long> dirtyNpcIds)
    {
        var full = WorldSnapshot.Serialize(world);
        var node = JsonNode.Parse(full)!.AsObject();
        var npcs = node["Npcs"]!.AsArray();
        var filtered = new JsonArray();
        foreach (var entry in npcs)
        {
            var id = entry!["Id"]!["Value"]!.GetValue<long>();
            if (dirtyNpcIds.Contains(id))
                filtered.Add(entry.DeepClone());
        }
        node["Npcs"] = filtered;
        return node.ToJsonString();
    }
}
