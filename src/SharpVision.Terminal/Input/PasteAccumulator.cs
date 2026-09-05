// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;

using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;

/// <summary>
/// Owns buffered bytes and end-marker matching for one in-progress bracketed paste, extracted
/// from <see cref="InputDecoder"/> as one of its four self-contained protocol decoders.
/// </summary>
/// <remarks>
/// Rents its buffer from <see cref="ArrayPool{T}.Shared"/> and returns it only on
/// <see cref="Dispose"/>; <see cref="Reset"/> clears used bytes but keeps the rental so a decoder
/// that receives many pastes does not re-rent per paste.
/// </remarks>
internal sealed class PasteAccumulator: IDisposable
{
    private static readonly byte[] _endMarker = "\u001b[201~"u8.ToArray();

    private readonly int _maxBytes;
    private byte[]? _buffer;
    private int _length;
    private int _matchIndex;

    /// <summary>Initializes an accumulator bounded to a positive maximum byte count.</summary>
    /// <param name="maxBytes">The positive maximum bytes retained for one paste.</param>
    public PasteAccumulator(int maxBytes)
    {
        Debug.Assert(maxBytes > 0, "The paste byte limit must be positive.");
        _maxBytes = maxBytes;
    }

    /// <summary>Gets whether a paste is currently being accumulated.</summary>
    public bool Active { get; private set; }

    /// <summary>Gets whether the current paste discarded bytes after reaching the byte limit.</summary>
    public bool Overflowed { get; private set; }

    /// <summary>Gets the bytes discarded once the current paste overflowed.</summary>
    public long DiscardedBytes { get; private set; }

    /// <summary>Gets the bytes buffered for the current paste.</summary>
    public ReadOnlySpan<byte> Buffered => _buffer.AsSpan(0, _length);

    /// <summary>Starts accumulating a new paste, discarding any previous unfinished state.</summary>
    public void Begin()
    {
        Active = true;
        Overflowed = false;
        _length = 0;
        _matchIndex = 0;
        DiscardedBytes = 0;
    }

    /// <summary>Processes one byte of paste-mode input, matching the terminating marker as it
    /// arrives so a marker byte sequence that turns out incomplete is appended as data.</summary>
    /// <param name="value">The next raw input byte.</param>
    /// <returns>True once the terminating marker completes; the caller finishes the paste.</returns>
    [MustUseReturnValue]
    public bool Process(byte value)
    {
        while (true)
        {
            if (value == _endMarker[_matchIndex])
            {
                _matchIndex++;
                return _matchIndex == _endMarker.Length;
            }

            if (_matchIndex > 0)
            {
                Append(_endMarker.AsSpan(0, _matchIndex));
                _matchIndex = 0;
                continue;
            }

            Append(value);
            return false;
        }
    }

    /// <summary>Clears buffered bytes and returns to the inactive state without releasing the
    /// pooled buffer, so a subsequent paste reuses the rental.</summary>
    public void Reset()
    {
        ClearBuffered();
        Active = false;
        Overflowed = false;
        _length = 0;
        _matchIndex = 0;
        DiscardedBytes = 0;
    }

    /// <summary>Clears buffered bytes and returns the pooled buffer.</summary>
    public void Dispose()
    {
        ClearBuffered();
        Active = false;

        if (_buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_buffer, clearArray: true);
            _buffer = null;
        }
    }

    private void ClearBuffered()
    {
        if (_length > 0 && _buffer is not null)
        {
            _buffer.AsSpan(0, _length).Clear();
        }
    }

    private void Append(ReadOnlySpan<byte> value)
    {
        foreach (var item in value)
        {
            Append(item);
        }
    }

    private void Append(byte value)
    {
        if (Overflowed)
        {
            DiscardedBytes++;
            return;
        }

        if (_length == _maxBytes)
        {
            Overflowed = true;
            DiscardedBytes = _length + 1;

            if (_length > 0)
            {
                _buffer.AsSpan(0, _length).Clear();
            }

            _length = 0;
            return;
        }

        EnsureCapacity(_length + 1);
        _buffer![_length++] = value;
    }

    private void EnsureCapacity(int required)
    {
        Debug.Assert(required > 0 && required <= _maxBytes);

        if (_buffer is not null && required <= _buffer.Length)
        {
            return;
        }

        var size = _buffer is null
            ? Math.Min(_maxBytes, Math.Max(256, required))
            : Math.Min(_maxBytes, Math.Max(required, _buffer.Length * 2));
        var replacement = ArrayPool<byte>.Shared.Rent(size);

        if (_buffer is not null)
        {
            _buffer.AsSpan(0, _length).CopyTo(replacement);
            ArrayPool<byte>.Shared.Return(_buffer, clearArray: true);
        }

        _buffer = replacement;
    }
}
