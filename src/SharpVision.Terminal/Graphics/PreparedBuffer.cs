// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics;

/// <summary>Owns one pooled atomic prepared graphics transaction under a hard byte budget.</summary>
internal sealed class PreparedBuffer: IBufferWriter<byte>, IDisposable
{
    private byte[]? _buffer;
    private readonly int _maximum;

    /// <summary>Rents storage for one positive finite maximum.</summary>
    /// <param name="maximum">The positive hard byte limit.</param>
    public PreparedBuffer(int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);
        _buffer = ArrayPool<byte>.Shared.Rent(Math.Min(256, maximum));
        _maximum = maximum;
    }

    /// <summary>Gets the number of prepared transaction bytes.</summary>
    public int WrittenCount { get; private set; }

    /// <summary>Gets the successfully prepared transaction bytes.</summary>
    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, WrittenCount);

    /// <inheritdoc />
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (count > _maximum - WrittenCount)
        {
            throw new InvalidOperationException("The prepared graphics transaction exceeds its finite byte limit.");
        }

        WrittenCount += count;
    }

    /// <inheritdoc />
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        var length = GetLength(sizeHint);
        return _buffer.AsMemory(WrittenCount, length);
    }

    /// <inheritdoc />
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        var length = GetLength(sizeHint);
        return _buffer.AsSpan(WrittenCount, length);
    }

    /// <summary>Clears and returns pooled transaction storage.</summary>
    public void Dispose()
    {
        if (_buffer is not { } buffer)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        _buffer = null;
        WrittenCount = 0;
    }

    private int GetLength(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        ObjectDisposedException.ThrowIf(_buffer is null, this);
        var requested = Math.Max(1, sizeHint);

        if (requested > _maximum - WrittenCount)
        {
            throw new InvalidOperationException("The prepared graphics transaction exceeds its finite byte limit.");
        }

        EnsureCapacity(checked(WrittenCount + requested));
        return Math.Min(_buffer.Length, _maximum) - WrittenCount;
    }

    private void EnsureCapacity(int required)
    {
        Debug.Assert(_buffer is not null, "Disposed prepared buffers are rejected before growth.");

        if (required <= _buffer.Length)
        {
            return;
        }

        var doubled = _buffer.Length > _maximum / 2 ? _maximum : _buffer.Length * 2;
        var capacity = Math.Min(_maximum, Math.Max(required, doubled));
        var replacement = ArrayPool<byte>.Shared.Rent(capacity);
        _buffer.AsSpan(0, WrittenCount).CopyTo(replacement);
        ArrayPool<byte>.Shared.Return(_buffer, clearArray: true);
        _buffer = replacement;
    }
}
