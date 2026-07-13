namespace SharpVision.Terminal.Rendering;

using System.Buffers;
using System.Diagnostics;

/// <summary>
/// Provides reusable bounded pooled storage for one encoded frame batch.
/// </summary>
internal sealed class Buffer: IBufferWriter<byte>, IDisposable
{
    private readonly int _maximum;
    private byte[]? _buffer;

    /// <summary>Initializes a bounded pooled writer.</summary>
    /// <param name="maximum">The positive maximum encoded byte count.</param>
    internal Buffer(int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);
        _maximum = maximum;
        _buffer = ArrayPool<byte>.Shared.Rent(Math.Min(4 * 1024, maximum));
    }

    /// <summary>Gets the active encoded byte count.</summary>
    internal int WrittenCount { get; private set; }

    /// <summary>Gets active encoded memory borrowed until reset or disposal.</summary>
    internal ReadOnlyMemory<byte> WrittenMemory
    {
        get
        {
            ThrowIfDisposed();
            return _buffer.AsMemory(0, WrittenCount);
        }
    }

    /// <inheritdoc/>
    public void Advance(int count)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count > _buffer!.Length - WrittenCount)
        {
            throw new ArgumentException("The count exceeds the requested buffer.", nameof(count));
        }

        if (count > _maximum - WrittenCount)
        {
            throw new InvalidOperationException("The encoded frame exceeds its finite byte limit.");
        }

        WrittenCount += count;
    }

    /// <inheritdoc/>
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        Ensure(sizeHint);
        var available = Math.Min(
            _buffer!.Length - WrittenCount,
            _maximum - WrittenCount);
        return _buffer.AsMemory(WrittenCount, available);
    }

    /// <inheritdoc/>
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        Ensure(sizeHint);
        var available = Math.Min(
            _buffer!.Length - WrittenCount,
            _maximum - WrittenCount);
        return _buffer.AsSpan(WrittenCount, available);
    }

    /// <summary>Clears the active batch for reuse.</summary>
    internal void Reset()
    {
        ThrowIfDisposed();
        _buffer.AsSpan(0, WrittenCount).Clear();
        WrittenCount = 0;
    }

    /// <summary>Prepends bytes to the active batch without exposing pooled storage.</summary>
    /// <param name="value">The bytes to prepend.</param>
    internal void Prepend(ReadOnlySpan<byte> value)
    {
        ThrowIfDisposed();
        Ensure(value.Length);
        _buffer.AsSpan(0, WrittenCount).CopyTo(_buffer.AsSpan(value.Length));
        value.CopyTo(_buffer);
        WrittenCount += value.Length;
    }

    /// <summary>Clears and returns pooled encoded storage.</summary>
    public void Dispose()
    {
        var buffer = _buffer;

        if (buffer is null)
        {
            return;
        }

        _buffer = null;
        WrittenCount = 0;
        ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
    }

    private void Ensure(int sizeHint)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        sizeHint = Math.Max(1, sizeHint);
        var required = checked(WrittenCount + sizeHint);

        if (required > _maximum)
        {
            throw new InvalidOperationException("The encoded frame exceeds its finite byte limit.");
        }

        if (required <= _buffer!.Length)
        {
            return;
        }

        var doubled = _buffer.Length > _maximum / 2 ? _maximum : _buffer.Length * 2;
        var replacement = ArrayPool<byte>.Shared.Rent(Math.Max(required, doubled));
        _buffer.AsSpan(0, WrittenCount).CopyTo(replacement);
        var previous = _buffer;
        _buffer = replacement;
        ArrayPool<byte>.Shared.Return(previous, clearArray: true);
        Debug.Assert(_buffer.Length >= required, "The pooled buffer must honor its size hint.");
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_buffer is null, this);
}
