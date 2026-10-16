using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Snapshot;

/// <summary>Snapshot canônico binário posicional (formato v2, ver <see cref="PositionalSnapshot"/>). Fachada de fluxo
/// sobre o formato: gravar base completa ou diff e reconstruir o mundo.</summary>
public sealed class BinarySnapshotWriter
{
    public void WriteFull(WorldState world, Stream output) =>
        output.Write(PositionalSnapshot.EncodeFull(world, out _));

    /// <summary>Diff de <paramref name="world"/> sobre <paramref name="baseline"/>. <paramref name="dirtyNpcIds"/>
    /// limita a comparação de NPCs aos sujos desde a base.</summary>
    public void WriteDelta(WorldState world, WorldState baseline, IReadOnlySet<long> dirtyNpcIds, Stream output)
    {
        PositionalSnapshot.EncodeFull(baseline, out var baseSnapshot);
        output.Write(PositionalSnapshot.EncodeDelta(world, baseSnapshot, dirtyNpcIds));
    }

    /// <summary>Lê um bloco completo, ou um diff aplicado a <paramref name="baseline"/>.</summary>
    public WorldState ReadAndApply(Stream input, WorldState baseline)
    {
        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        var data = buffer.ToArray();
        if (!PositionalSnapshot.IsDelta(data)) return PositionalSnapshot.DecodeFull(data);

        return PositionalSnapshot.DecodeDelta(data, PositionalSnapshot.EncodeFull(baseline, out _));
    }
}
