// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;

/// <summary>
/// Incrementally decodes borrowed UTF-8 text bytes into Unicode scalar values, extracted from
/// <see cref="InputDecoder"/> as one of its four self-contained protocol decoders.
/// </summary>
/// <remarks>
/// A multi-byte UTF-8 sequence that spans two <see cref="Process"/> calls is buffered until the
/// next call supplies its remaining bytes; an invalid sequence yields one replacement character.
/// </remarks>
internal sealed class Utf8TextAccumulator
{
    private readonly byte[] _buffer = new byte[4];
    private readonly Action<Rune> _emit;
    private int _length;

    /// <summary>Initializes an accumulator that reports each decoded rune to a host callback.</summary>
    /// <param name="emit">The non-null callback invoked for each decoded or replacement rune.</param>
    public Utf8TextAccumulator(Action<Rune> emit) => _emit = emit;

    /// <summary>Gets whether an incomplete multi-byte sequence is buffered.</summary>
    public bool HasPending => _length > 0;

    /// <summary>Clears buffered bytes without emitting a replacement rune.</summary>
    public void Clear()
    {
        _buffer.AsSpan().Clear();
        _length = 0;
    }

    /// <summary>Decodes borrowed text bytes, emitting a rune per code point or invalid unit.</summary>
    /// <param name="value">The borrowed text bytes.</param>
    public void Process(ReadOnlySpan<byte> value)
    {
        var position = 0;

        while (position < value.Length)
        {
            if (_length > 0)
            {
                Debug.Assert(_length < _buffer.Length, "Pending UTF-8 must remain bounded.");
                _buffer[_length++] = value[position++];
                ProcessPending();
                continue;
            }

            var status = Rune.DecodeFromUtf8(value[position..], out var rune, out var consumed);

            if (status == OperationStatus.Done)
            {
                _emit(rune);
                position += consumed;
            }
            else if (status == OperationStatus.NeedMoreData)
            {
                var remaining = value[position..];
                Debug.Assert(remaining.Length <= 3, "A valid UTF-8 prefix retains at most three bytes.");
                remaining.CopyTo(_buffer);
                _length = remaining.Length;
                return;
            }
            else
            {
                _emit(Rune.ReplacementChar);
                position += Math.Max(1, consumed);
            }
        }
    }

    /// <summary>Emits a replacement rune for any buffered incomplete sequence and clears it.</summary>
    public void Flush()
    {
        if (_length == 0)
        {
            return;
        }

        _buffer.AsSpan(0, _length).Clear();
        _length = 0;
        _emit(Rune.ReplacementChar);
    }

    private void ProcessPending()
    {
        while (_length > 0)
        {
            var status = Rune.DecodeFromUtf8(_buffer.AsSpan(0, _length), out var rune, out var consumed);

            if (status == OperationStatus.NeedMoreData)
            {
                return;
            }

            if (status == OperationStatus.Done)
            {
                _emit(rune);
            }
            else
            {
                _emit(Rune.ReplacementChar);
                consumed = Math.Max(1, consumed);
            }

            Shift(consumed);
        }
    }

    private void Shift(int count)
    {
        Debug.Assert(count > 0 && count <= _length, "UTF-8 consumption must be bounded.");
        _buffer.AsSpan(count, _length - count).CopyTo(_buffer);
        _buffer.AsSpan(_length - count, count).Clear();
        _length -= count;
    }
}
