// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty;

/// <summary>Provides one pooled synchronous byte writer with a hard active budget.</summary>
internal sealed class BoundedWriter: IBufferWriter<byte>, IDisposable
{
    private byte[]? _buffer;
    private readonly int _maximum;
    private int _limit;
    private int _written;

    /// <summary>Rents storage for one positive finite maximum.</summary>
    /// <param name="limit">The positive hard byte limit.</param>
    public BoundedWriter(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        _buffer = ArrayPool<byte>.Shared.Rent(Math.Min(256, limit));
        _maximum = limit;
        _limit = limit;
    }

    /// <summary>Gets bytes written within the current budget.</summary>
    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

    /// <summary>Resets the writer with a non-negative limit no greater than the rented capacity.</summary>
    /// <param name="limit">The next non-negative hard limit.</param>
    public void Reset(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);

        if (_buffer is null || limit > _maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "The limit exceeds the configured maximum.");
        }

        _buffer.AsSpan(0, _written).Clear();
        _written = 0;
        _limit = limit;
    }

    /// <inheritdoc />
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count > _limit - _written)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "The write exceeds its finite byte budget.");
        }

        _written += count;
    }

    /// <inheritdoc />
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        var length = GetLength(sizeHint);
        return _buffer.AsMemory(_written, length);
    }

    /// <inheritdoc />
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        var length = GetLength(sizeHint);
        return _buffer.AsSpan(_written, length);
    }

    /// <summary>Clears and returns pooled storage.</summary>
    public void Dispose()
    {
        if (_buffer is not { } buffer)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        _buffer = null;
        _written = 0;
        _limit = 0;
    }

    private int GetLength(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        var requested = Math.Max(1, sizeHint);

        ObjectDisposedException.ThrowIf(_buffer is null, this);

        if (requested > _limit - _written)
        {
            throw new InvalidOperationException(
                "The prepared Kitty transaction exceeds its finite byte limit.");
        }

        EnsureCapacity(checked(_written + requested));
        return Math.Min(_buffer.Length, _limit) - _written;
    }

    private void EnsureCapacity(int required)
    {
        Debug.Assert(_buffer is not null, "Disposed writers are rejected before growth.");

        if (required <= _buffer.Length)
        {
            return;
        }

        var capacity = Math.Min(_limit, Math.Max(required, checked(_buffer.Length * 2)));
        var replacement = ArrayPool<byte>.Shared.Rent(capacity);
        _buffer.AsSpan(0, _written).CopyTo(replacement);
        ArrayPool<byte>.Shared.Return(_buffer, clearArray: true);
        _buffer = replacement;
    }
}
