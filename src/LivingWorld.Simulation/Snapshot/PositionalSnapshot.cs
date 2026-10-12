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

    public static bool IsPositional(ReadOnlySpan<byte> data) => data.Length > Magic.Length && data[..Magic.Length].SequenceEqual(Magic);

    public static bool IsDelta(ReadOnlySpan<byte> data) => IsPositional(data) && data[Magic.Length] == KindDelta;

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

    private static JsonObject DecodeTree(byte[] data, byte expectedKind)
    {
        if (!IsPositional(data)) throw new InvalidDataException("magic inválido");
        if (data[Magic.Length] != expectedKind) throw new InvalidDataException("tipo de snapshot inesperado");
        return PositionalTreeCodec.Decode(data.AsSpan(Magic.Length + 1))!.AsObject();
    }

    private static byte[] Wrap(byte kind, byte[] body)
    {
        var result = new byte[Magic.Length + 1 + body.Length];
        Magic.CopyTo(result, 0);
        result[Magic.Length] = kind;
        body.CopyTo(result, Magic.Length + 1);
        return result;
    }
}
