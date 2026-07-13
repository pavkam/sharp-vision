// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Clipboard;

using System.Buffers;

/// <summary>Builds one bounded Kitty clipboard MIME value in pooled storage.</summary>
internal sealed class Builder: IDisposable
{
    private byte[]? _buffer;
    private int _length;

    /// <summary>Appends one borrowed data chunk.</summary>
    /// <param name="value">The data to copy.</param>
    internal void Append(ReadOnlySpan<byte> value)
    {
        EnsureCapacity(checked(_length + value.Length));
        value.CopyTo(_buffer.AsSpan(_length));
        _length += value.Length;
    }

    /// <summary>Copies the complete active data.</summary>
    /// <returns>An independent exact-size array.</returns>
    internal byte[] ToArray() => _buffer.AsSpan(0, _length).ToArray();

    /// <summary>Clears and returns pooled storage.</summary>
    public void Dispose()
    {
        var buffer = _buffer;

        if (buffer is null)
        {
            return;
        }

        _buffer = null;
        _length = 0;
        ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
    }

    private void EnsureCapacity(int required)
    {
        if (_buffer is not null && required <= _buffer.Length)
        {
            return;
        }

        var size = _buffer is null
            ? Math.Max(256, required)
            : Math.Max(required, checked(_buffer.Length * 2));
        var replacement = ArrayPool<byte>.Shared.Rent(size);

        if (_buffer is not null)
        {
            _buffer.AsSpan(0, _length).CopyTo(replacement);
            ArrayPool<byte>.Shared.Return(_buffer, clearArray: true);
        }

        _buffer = replacement;
    }
}
