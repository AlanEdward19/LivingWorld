using System.Buffers;

namespace LivingWorld.Simulation.Snapshot;

/// <summary><see cref="IBufferWriter{T}"/> sobre o <see cref="ArrayPool{T}"/>: crescer aluga o dobro e devolve o antigo, e
/// <see cref="Dispose"/> devolve o último. Uma gravação de snapshot reaproveita os mesmos megabytes da anterior em vez de
/// alocá-los de novo (#156).</summary>
internal sealed class PooledBufferWriter(int initialCapacity = 1 << 16) : IBufferWriter<byte>, IDisposable
{
    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
    private int _written;

    public int WrittenCount => _written;
    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);
    public ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(0, _written);

    public void Advance(int count) => _written += count;

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        Ensure(sizeHint);
        return _buffer.AsMemory(_written);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        Ensure(sizeHint);
        return _buffer.AsSpan(_written);
    }

    public void Write(ReadOnlySpan<byte> bytes)
    {
        Ensure(bytes.Length);
        bytes.CopyTo(_buffer.AsSpan(_written));
        _written += bytes.Length;
    }

    public void WriteByte(byte b)
    {
        Ensure(1);
        _buffer[_written++] = b;
    }

    public void Clear() => _written = 0;

    private void Ensure(int sizeHint)
    {
        if (sizeHint < 1) sizeHint = 1;
        if (_buffer.Length - _written >= sizeHint) return;

        int wanted = Math.Max(_buffer.Length * 2, _written + sizeHint);
        var next = ArrayPool<byte>.Shared.Rent(wanted);
        _buffer.AsSpan(0, _written).CopyTo(next);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = next;
    }

    public void Dispose()
    {
        if (_buffer.Length == 0) return;
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = [];
        _written = 0;
    }
}
