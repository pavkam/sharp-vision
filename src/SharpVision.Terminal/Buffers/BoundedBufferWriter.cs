// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Buffers;

using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;
using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Provides one pooled synchronous byte writer under a finite active byte budget.</summary>
[MustDisposeResource]
internal sealed class BoundedBufferWriter: IBufferWriter<byte>, IDisposable
{
    private readonly int _maximum;
    private byte[]? _buffer;
    private int _limit;
    private int _writtenCount;

    /// <summary>Rents storage for one positive finite maximum.</summary>
    /// <param name="maximum">The positive hard byte limit no active budget may exceed.</param>
    /// <param name="initialRentBytes">The positive initial pooled rent, clamped to <paramref name="maximum"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximum"/> or <paramref name="initialRentBytes"/> is not positive.</exception>
    public BoundedBufferWriter(int maximum, int initialRentBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialRentBytes);
        _maximum = maximum;
        _limit = maximum;
        _buffer = ArrayPool<byte>.Shared.Rent(Math.Min(initialRentBytes, maximum));
    }

    /// <summary>Gets the active written byte count.</summary>
    /// <exception cref="ObjectDisposedException">The writer is disposed.</exception>
    [NonNegativeValue]
    public int WrittenCount
    {
        get
        {
            ThrowIfDisposed();
            return _writtenCount;
        }
    }

    /// <summary>Gets the active written bytes.</summary>
    /// <exception cref="ObjectDisposedException">The writer is disposed.</exception>
    public ReadOnlySpan<byte> WrittenSpan
    {
        get
        {
            ThrowIfDisposed();
            return _buffer.AsSpan(0, _writtenCount);
        }
    }

    /// <summary>Gets active written memory borrowed until reset or disposal.</summary>
    /// <exception cref="ObjectDisposedException">The writer is disposed.</exception>
    public ReadOnlyMemory<byte> WrittenMemory
    {
        get
        {
            ThrowIfDisposed();
            return _buffer.AsMemory(0, _writtenCount);
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The writer is disposed.</exception>
    /// <exception cref="ArgumentException"><paramref name="count"/> exceeds the buffer granted by the last <see cref="GetSpan"/> or <see cref="GetMemory"/> call.</exception>
    /// <exception cref="InvalidOperationException">The write exceeds the active byte budget.</exception>
    public void Advance(int count)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count > _buffer!.Length - _writtenCount)
        {
            throw new ArgumentException("The count exceeds the requested buffer.", nameof(count));
        }

        if (count > _limit - _writtenCount)
        {
            throw new InvalidOperationException("The write exceeds its finite byte limit.");
        }

        _writtenCount += count;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The writer is disposed.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="sizeHint"/> exceeds the remaining active byte budget.</exception>
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        var length = GetLength(sizeHint);
        return _buffer.AsMemory(_writtenCount, length);
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The writer is disposed.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="sizeHint"/> exceeds the remaining active byte budget.</exception>
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        var length = GetLength(sizeHint);
        return _buffer.AsSpan(_writtenCount, length);
    }

    /// <summary>Clears the active batch for reuse under the current budget.</summary>
    /// <exception cref="ObjectDisposedException">The writer is disposed.</exception>
    public void Reset()
    {
        ThrowIfDisposed();
        _buffer.AsSpan(0, _writtenCount).Clear();
        _writtenCount = 0;
    }

    /// <summary>Clears the active batch for reuse and narrows the active budget.</summary>
    /// <param name="limit">The next non-negative active limit, no greater than the configured maximum.</param>
    /// <exception cref="ObjectDisposedException">The writer is disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is negative or exceeds the configured maximum.</exception>
    public void Reset(int limit)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(limit);

        if (limit > _maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "The limit exceeds the configured maximum.");
        }

        _buffer.AsSpan(0, _writtenCount).Clear();
        _writtenCount = 0;
        _limit = limit;
    }

    /// <summary>Prepends bytes to the active batch without exposing pooled storage.</summary>
    /// <param name="value">The bytes to prepend.</param>
    /// <exception cref="ObjectDisposedException">The writer is disposed.</exception>
    /// <exception cref="InvalidOperationException">The prepended batch exceeds the active byte budget.</exception>
    public void Prepend(ReadOnlySpan<byte> value)
    {
        ThrowIfDisposed();
        EnsureCapacity(checked(_writtenCount + value.Length));
        _buffer.AsSpan(0, _writtenCount).CopyTo(_buffer.AsSpan(value.Length));
        value.CopyTo(_buffer);
        _writtenCount += value.Length;
    }

    /// <summary>Clears and returns pooled storage.</summary>
    public void Dispose()
    {
        var buffer = _buffer;

        if (buffer is null)
        {
            return;
        }

        _buffer = null;
        _writtenCount = 0;
        _limit = 0;
        ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
    }

    private int GetLength(int sizeHint)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        var requested = Math.Max(1, sizeHint);
        EnsureCapacity(checked(_writtenCount + requested));
        return Math.Min(_buffer!.Length, _limit) - _writtenCount;
    }

    private void EnsureCapacity(int required)
    {
        if (required > _limit)
        {
            throw new InvalidOperationException("The write exceeds its finite byte limit.");
        }

        if (required <= _buffer!.Length)
        {
            return;
        }

        var doubled = _buffer.Length > _limit / 2 ? _limit : _buffer.Length * 2;
        var capacity = Math.Min(_limit, Math.Max(required, doubled));
        var replacement = ArrayPool<byte>.Shared.Rent(capacity);
        _buffer.AsSpan(0, _writtenCount).CopyTo(replacement);
        var previous = _buffer;
        _buffer = replacement;
        ArrayPool<byte>.Shared.Return(previous, clearArray: true);
        Debug.Assert(_buffer.Length >= required, "The pooled buffer must honor its required capacity.");
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_buffer is null, this);
}
