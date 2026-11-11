using System.Buffers;
using System.IO.Compression;
using System.Text.Json.Nodes;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Snapshot;

/// <summary>Base de um encadeamento de snapshots: a árvore completa de um instante, contra a qual os diffs seguintes
/// são calculados. Quem persiste a guarda em memória entre uma gravação e outra.</summary>
public sealed class SnapshotBase
{
    internal SnapshotBase(JsonObject tree, long tick, WorldState world)
    {
        Fingerprint = SnapshotFingerprint.Of(tree);
        Tick = tick;
        World = new WeakReference<WorldState>(world);
    }

    internal SnapshotFingerprint Fingerprint { get; }
    public long Tick { get; }
    internal WeakReference<WorldState> World { get; }

    /// <summary>Só vale para o mesmo mundo, em ponto estritamente posterior da linha do tempo (um diff no próprio tick
    /// da base sobrescreveria a base gravada).</summary>
    public bool Fits(WorldState world) =>
        World.TryGetTarget(out var w) && ReferenceEquals(w, world) && world.CurrentDate.TotalHours > Tick;
}

/// <summary>Formato binário posicional v2 do snapshot (door 1): <c>LWSNAP2\0</c>, um byte de tipo (0 base completa,
/// 1 diff sobre uma base) e o corpo do <see cref="PositionalTreeCodec"/>. O hash canônico do mundo não depende do
/// formato: ele é calculado sobre o mundo, nunca sobre os bytes.</summary>
public static class PositionalSnapshot
{
    private static readonly byte[] Magic = "LWSNAP2\0"u8.ToArray();
    private const byte KindFull = 0, KindDelta = 1;

    /// <summary>Bit do byte de tipo que marca corpo comprimido (Brotli). Bloco gravado antes da compressão não o tem e
    /// continua legível: a leitura decide pelo bit, nunca pela versão de quem gravou (AD-127).</summary>
    private const byte CompressedFlag = 0x80;
    private const int BrotliQuality = 4, BrotliWindow = 22;

    public static bool IsPositional(ReadOnlySpan<byte> data) => data.Length > Magic.Length && data[..Magic.Length].SequenceEqual(Magic);

    public static bool IsDelta(ReadOnlySpan<byte> data) => IsPositional(data) && (data[Magic.Length] & ~CompressedFlag) == KindDelta;

    public static byte[] EncodeFull(WorldState world, out SnapshotBase snapshotBase)
    {
        var tree = WorldSnapshot.BuildPersistentTree(world);
        snapshotBase = new SnapshotBase(tree, world.CurrentDate.TotalHours, world);
        return Wrap(KindFull, PositionalTreeCodec.Encode(tree));
    }

    /// <summary>Diff do mundo atual sobre <paramref name="snapshotBase"/>; <paramref name="dirtyNpcIds"/>, quando dado,
    /// restringe a comparação de NPCs aos sujos (NPCs novos entram sempre).</summary>
    public static byte[] EncodeDelta(WorldState world, SnapshotBase snapshotBase, IReadOnlySet<long>? dirtyNpcIds = null) =>
        Wrap(KindDelta, PositionalTreeCodec.Encode(
            SnapshotTreeDiff.Diff(WorldSnapshot.BuildPersistentTree(world), snapshotBase.Fingerprint, dirtyNpcIds)));

    /// <summary>Reconstrói o mundo de uma linha gravada: bloco completo, ou diff sobre a base que <paramref name="loadBase"/>
    /// entrega pelo tick. Devolve também a base em memória, para o próximo diff continuar a cadeia.</summary>
    public static WorldState Decode(
        byte[] data, long tick, long? baseTick, Func<long, byte[]?> loadBase, out SnapshotBase snapshotBase)
    {
        if (baseTick is null)
        {
            var tree = DecodeTree(data, KindFull);
            var full = WorldSnapshot.FromNode(tree);
            snapshotBase = new SnapshotBase(tree, tick, full);
            return full;
        }

        var baseData = loadBase(baseTick.Value)
            ?? throw new InvalidDataException($"base do snapshot no tick {baseTick} não encontrada");
        var baseTree = DecodeTree(baseData, KindFull);
        var world = WorldSnapshot.FromNode(SnapshotTreeDiff.Apply(baseTree, DecodeTree(data, KindDelta)));
        snapshotBase = new SnapshotBase(baseTree, baseTick.Value, world);
        return world;
    }

    public static WorldState DecodeFull(byte[] data) => WorldSnapshot.FromNode(DecodeTree(data, KindFull));

    public static WorldState DecodeDelta(byte[] data, byte[] baseData) =>
        WorldSnapshot.FromNode(SnapshotTreeDiff.Apply(DecodeTree(baseData, KindFull), DecodeTree(data, KindDelta)));

    /// <summary>Corpo do bloco (a árvore no codec posicional), já descomprimido se for o caso.</summary>
    internal static byte[] Body(byte[] data)
    {
        if (!IsPositional(data)) throw new InvalidDataException("magic inválido");
        var body = data.AsSpan(Magic.Length + 1);
        return (data[Magic.Length] & CompressedFlag) != 0 ? Decompress(body) : body.ToArray();
    }

    private static JsonObject DecodeTree(byte[] data, byte expectedKind)
    {
        if (!IsPositional(data)) throw new InvalidDataException("magic inválido");
        byte kind = data[Magic.Length];
        if ((kind & ~CompressedFlag) != expectedKind) throw new InvalidDataException("tipo de snapshot inesperado");
        var body = data.AsSpan(Magic.Length + 1);
        return ((kind & CompressedFlag) != 0 ? PositionalTreeCodec.Decode(Decompress(body)) : PositionalTreeCodec.Decode(body))!.AsObject();
    }

    private static byte[] Wrap(byte kind, byte[] body)
    {
        int header = Magic.Length + 1;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(header + BrotliEncoder.GetMaxCompressedLength(body.Length));
        try
        {
            if (!BrotliEncoder.TryCompress(body, buffer.AsSpan(header), out int written, BrotliQuality, BrotliWindow))
                throw new InvalidOperationException("compressão do snapshot falhou");
            Magic.CopyTo(buffer, 0);
            buffer[Magic.Length] = (byte)(kind | CompressedFlag);
            return buffer.AsSpan(0, header + written).ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static byte[] Decompress(ReadOnlySpan<byte> compressed)
    {
        using var input = new MemoryStream(compressed.ToArray(), writable: false);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream(compressed.Length * 4);
        brotli.CopyTo(output);
        return output.ToArray();
    }
}
