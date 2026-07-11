using System.Buffers;
using System.Diagnostics;
using System.Text;

using SharpVision.Terminal.Protocols;

namespace SharpVision.Terminal.Input;

public sealed partial class Decoder
{
    private void BeginPaste()
    {
        _pasteMode = true;
        _pasteOverflow = false;
        _pasteLength = 0;
        _pasteMatch = 0;
        _pasteDiscarded = 0;
    }

    private void ProcessPaste(byte value)
    {
        while (true)
        {
            if (value == _pasteEnd[_pasteMatch])
            {
                _pasteMatch++;

                if (_pasteMatch == _pasteEnd.Length)
                {
                    FinishPaste();
                }

                return;
            }

            if (_pasteMatch > 0)
            {
                AppendPaste(_pasteEnd.AsSpan(0, _pasteMatch));
                _pasteMatch = 0;
                continue;
            }

            AppendPaste(value);
            return;
        }
    }

    private void AppendPaste(ReadOnlySpan<byte> value)
    {
        foreach (var item in value)
        {
            AppendPaste(item);
        }
    }

    private void AppendPaste(byte value)
    {
        if (_pasteOverflow)
        {
            _pasteDiscarded++;
            return;
        }

        if (_pasteLength == _options.MaxPasteBytes)
        {
            _pasteOverflow = true;
            _pasteDiscarded = 1;

            if (_pasteLength > 0)
            {
                _paste.AsSpan(0, _pasteLength).Clear();
            }

            _pasteLength = 0;
            return;
        }

        EnsurePasteCapacity(_pasteLength + 1);
        _paste![_pasteLength++] = value;
    }

    private void EnsurePasteCapacity(int required)
    {
        Debug.Assert(required > 0 && required <= _options.MaxPasteBytes);

        if (_paste is not null && required <= _paste.Length)
        {
            return;
        }

        var size = _paste is null
            ? Math.Min(_options.MaxPasteBytes, Math.Max(256, required))
            : Math.Min(_options.MaxPasteBytes, Math.Max(required, _paste.Length * 2));
        var replacement = ArrayPool<byte>.Shared.Rent(size);

        if (_paste is not null)
        {
            _paste.AsSpan(0, _pasteLength).CopyTo(replacement);
            ArrayPool<byte>.Shared.Return(_paste, clearArray: true);
        }

        _paste = replacement;
    }

    private void FinishPaste()
    {
        if (_pasteOverflow)
        {
            Report(
                DiagnosticCode.StringLimit,
                SequenceKind.Csi,
                _pasteDiscarded);
        }
        else
        {
            var owned = NormalizeUtf8(_paste.AsSpan(0, _pasteLength));
            var paste = Paste.Take(owned);
            _sink.Input(paste);
        }

        ResetPaste();
    }

    private void ResetPaste()
    {
        if (_pasteLength > 0 && _paste is not null)
        {
            _paste.AsSpan(0, _pasteLength).Clear();
        }

        _pasteMode = false;
        _pasteOverflow = false;
        _pasteLength = 0;
        _pasteMatch = 0;
        _pasteDiscarded = 0;
    }

    private void Report(DiagnosticCode code, SequenceKind kind, long discardedBytes)
    {
        var diagnostic = new Diagnostic(
            code,
            kind,
            checked(_parser.Offset + _skippedBytes),
            discardedBytes);
        _sink.Input(in diagnostic);
    }

    private static byte[] NormalizeUtf8(ReadOnlySpan<byte> input)
    {
        var valid = true;
        var position = 0;

        while (position < input.Length)
        {
            var status = Rune.DecodeFromUtf8(input[position..], out _, out var consumed);

            if (status != OperationStatus.Done)
            {
                valid = false;
                break;
            }

            position += consumed;
        }

        if (valid)
        {
            return input.ToArray();
        }

        var maximum = checked(Math.Max(1, input.Length * 3));
        var rented = ArrayPool<byte>.Shared.Rent(maximum);
        position = 0;
        var written = 0;

        try
        {
            while (position < input.Length)
            {
                var status = Rune.DecodeFromUtf8(
                    input[position..],
                    out var rune,
                    out var consumed);

                if (status != OperationStatus.Done)
                {
                    rune = Rune.ReplacementChar;
                    consumed = status == OperationStatus.NeedMoreData
                        ? input.Length - position
                        : Math.Max(1, consumed);
                }

                written += rune.EncodeToUtf8(rented.AsSpan(written));
                position += consumed;
            }

            return rented.AsSpan(0, written).ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }
}
