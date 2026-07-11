using System.Buffers;
using System.Diagnostics;
using System.Text;

using SharpVision.Terminal.Protocols;

using InputAction = SharpVision.Terminal.Input.Action;

namespace SharpVision.Terminal.Input;

/// <summary>
/// Incrementally decodes UTF-8 and legacy VT keyboard input into stable values.
/// </summary>
/// <remarks>
/// The decoder is single-threaded. Input bytes and parser callback spans are
/// borrowed only for each synchronous call; emitted values retain none of them.
/// </remarks>
public sealed partial class Decoder: IDisposable
{
    private static readonly byte[] _pasteEnd = "\u001b[201~"u8.ToArray();

    private readonly IInputSink _sink;
    private readonly Options _options;
    private readonly Parser _parser;
    private readonly TimeProvider _timeProvider;
    private readonly byte[] _utf8 = new byte[4];
    private readonly byte[] _x10 = new byte[12];
    private byte[]? _paste;
    private DateTimeOffset _escapeDeadline;
    private Geometry.Metrics? _cellMetrics;
    private Modifiers _nextTextModifiers;
    private int _utf8Length;
    private int _pasteLength;
    private int _pasteMatch;
    private int _x10Length;
    private long _pasteDiscarded;
    private long _skippedBytes;
    private bool _completed;
    private bool _disposed;
    private bool _escapePending;
    private bool _pasteMode;
    private readonly bool _pixelMouse;
    private bool _pasteOverflow;
    private bool _ss3Pending;
    private bool _x10Pending;

    /// <summary>Initializes a decoder with a stable synchronous event sink.</summary>
    /// <param name="sink">The non-null event sink.</param>
    /// <param name="options">Finite policy, or null for conservative defaults.</param>
    /// <param name="timeProvider">The Escape-deadline clock, or null for system time.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is null.</exception>
    public Decoder(
        IInputSink sink,
        Options? options = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
        _options = options ?? Options.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _parser = new Parser(_options.Limits);
        _cellMetrics = _options.CellMetrics;
        _pixelMouse = _options.PixelMouse;
    }

    /// <summary>Consumes one borrowed transport fragment synchronously.</summary>
    /// <param name="input">The borrowed bytes.</param>
    /// <exception cref="InvalidOperationException">The input stream was completed.</exception>
    /// <exception cref="ObjectDisposedException">The decoder is disposed.</exception>
    public void Decode(ReadOnlySpan<byte> input)
    {
        ThrowIfDisposed();

        if (_completed)
        {
            throw new InvalidOperationException("The input stream is already complete.");
        }

        var adapter = new Adapter(this);
        var position = 0;

        while (position < input.Length)
        {
            var value = input[position];

            if (_pasteMode)
            {
                _skippedBytes = checked(_skippedBytes + 1);
                ProcessPaste(value);
                position++;
                continue;
            }

            if (_escapePending)
            {
                if (value == 0x1b)
                {
                    _skippedBytes = checked(_skippedBytes + 1);
                    EmitStroke(Code.Escape);
                    BeginEscape();
                    position++;
                    continue;
                }

                _escapePending = false;

                // Legacy terminals encode Alt plus non-ASCII text as ESC then UTF-8.
                if (value >= 0x80)
                {
                    _skippedBytes = checked(_skippedBytes + 1);
                    _nextTextModifiers = Modifiers.Alt;
                }
                else
                {
                    _parser.Parse("\u001b"u8, ref adapter);
                }
            }

            if (_parser.IsGround && value == 0x1b)
            {
                BeginEscape();
                position++;
                continue;
            }

            if (_parser.IsGround && value == 0x7f)
            {
                _skippedBytes = checked(_skippedBytes + 1);
                FlushUtf8();
                HandleControl(value);
                position++;
                continue;
            }

            _parser.Parse(input.Slice(position, 1), ref adapter);
            position++;
        }
    }

    /// <summary>Emits a pending lone Escape after its ambiguity deadline.</summary>
    /// <returns>Whether an Escape key was emitted.</returns>
    /// <exception cref="ObjectDisposedException">The decoder is disposed.</exception>
    public bool ExpireEscape()
    {
        ThrowIfDisposed();

        if (!_escapePending || _timeProvider.GetUtcNow() < _escapeDeadline)
        {
            return false;
        }

        _escapePending = false;
        _skippedBytes = checked(_skippedBytes + 1);
        FlushUtf8();
        EmitStroke(Code.Escape);
        return true;
    }

    /// <summary>Completes pending UTF-8, Escape, SS3, and protocol state once.</summary>
    /// <exception cref="ObjectDisposedException">The decoder is disposed.</exception>
    public void Complete()
    {
        ThrowIfDisposed();

        if (_completed)
        {
            return;
        }

        _completed = true;
        FlushUtf8();

        if (_escapePending)
        {
            _escapePending = false;
            _skippedBytes = checked(_skippedBytes + 1);
            EmitStroke(Code.Escape);
        }

        if (_ss3Pending)
        {
            _ss3Pending = false;
            Report(DiagnosticCode.Truncated, SequenceKind.Escape);
        }

        if (_x10Pending)
        {
            EndX10IfPending();
        }

        if (_pasteMode)
        {
            ResetPaste();
            Report(DiagnosticCode.Truncated, SequenceKind.Csi);
        }

        var adapter = new Adapter(this);
        _parser.Complete(ref adapter);
    }

    /// <summary>Updates pixel-to-cell inference after an ordered resize event.</summary>
    /// <param name="value">Positive cell metrics, or null when unavailable.</param>
    internal void SetCellMetrics(Geometry.Metrics? value)
    {
        ThrowIfDisposed();
        _cellMetrics = value;
    }

    /// <summary>Clears pending bytes and returns parser-owned pooled storage.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _utf8.AsSpan().Clear();
        _x10.AsSpan().Clear();
        _utf8Length = 0;
        ResetPaste();

        if (_paste is not null)
        {
            ArrayPool<byte>.Shared.Return(_paste, clearArray: true);
            _paste = null;
        }

        _parser.Dispose();
    }

    private void BeginEscape()
    {
        _escapePending = true;
        _escapeDeadline = _timeProvider.GetUtcNow().Add(_options.EscapeTimeout);
    }

    private void DecodeText(ReadOnlySpan<byte> value)
    {
        var position = 0;

        while (position < value.Length)
        {
            if (_utf8Length > 0)
            {
                Debug.Assert(_utf8Length < _utf8.Length, "Pending UTF-8 must remain bounded.");
                _utf8[_utf8Length++] = value[position++];
                ProcessPendingUtf8();
                continue;
            }

            var status = Rune.DecodeFromUtf8(value[position..], out var rune, out var consumed);

            if (status == OperationStatus.Done)
            {
                EmitText(rune);
                position += consumed;
            }
            else if (status == OperationStatus.NeedMoreData)
            {
                var remaining = value[position..];
                Debug.Assert(remaining.Length <= 3, "A valid UTF-8 prefix retains at most three bytes.");
                remaining.CopyTo(_utf8);
                _utf8Length = remaining.Length;
                return;
            }
            else
            {
                EmitText(Rune.ReplacementChar);
                position += Math.Max(1, consumed);
            }
        }
    }

    private void ProcessPendingUtf8()
    {
        while (_utf8Length > 0)
        {
            var status = Rune.DecodeFromUtf8(
                _utf8.AsSpan(0, _utf8Length),
                out var rune,
                out var consumed);

            if (status == OperationStatus.NeedMoreData)
            {
                return;
            }

            if (status == OperationStatus.Done)
            {
                EmitText(rune);
            }
            else
            {
                EmitText(Rune.ReplacementChar);
                consumed = Math.Max(1, consumed);
            }

            ShiftUtf8(consumed);
        }
    }

    private void ShiftUtf8(int count)
    {
        Debug.Assert(count > 0 && count <= _utf8Length, "UTF-8 consumption must be bounded.");
        _utf8.AsSpan(count, _utf8Length - count).CopyTo(_utf8);
        _utf8.AsSpan(_utf8Length - count, count).Clear();
        _utf8Length -= count;
    }

    private void FlushUtf8()
    {
        if (_utf8Length == 0)
        {
            return;
        }

        _utf8.AsSpan(0, _utf8Length).Clear();
        _utf8Length = 0;
        EmitText(Rune.ReplacementChar);
    }

    private void HandleControl(byte value)
    {
        EndX10IfPending();

        if (_ss3Pending)
        {
            _ss3Pending = false;
            Report(DiagnosticCode.Malformed, SequenceKind.Escape);
        }

        switch (value)
        {
            case 0x08:
            case 0x7f:
                EmitStroke(Code.Backspace);
                return;

            case 0x09:
                EmitStroke(Code.Tab);
                return;

            case 0x0a:
            case 0x0d:
                EmitStroke(Code.Enter);
                return;

            case 0x00:
                EmitStroke(Code.Character, new Rune(' '), value, Modifiers.Control);
                return;

            case >= 0x01 and <= 0x1a:
                EmitStroke(
                    Code.Character,
                    new Rune('a' + value - 1),
                    value,
                    Modifiers.Control);
                return;

            case >= 0x1c and <= 0x1f:
                EmitStroke(
                    Code.Character,
                    new Rune('\\' + value - 0x1c),
                    value,
                    Modifiers.Control);
                return;

            default:
                EmitStroke(Code.Unknown, null, value);
                return;
        }
    }

    private void HandleEscape(ReadOnlySpan<byte> intermediates, byte final)
    {
        FlushUtf8();
        EndX10IfPending();
        EndSs3IfPending();

        if (!intermediates.IsEmpty)
        {
            Report(DiagnosticCode.Unsupported, SequenceKind.Escape);
            return;
        }

        if (final == (byte) 'O')
        {
            _ss3Pending = true;
            return;
        }

        EmitText(new Rune(final), Modifiers.Alt);
    }

    private void HandleCsi(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final)
    {
        FlushUtf8();
        EndX10IfPending();
        EndSs3IfPending();

        if (intermediates.IsEmpty && final == (byte) 'u')
        {
            if (!Responses.TryCsi(parameters, intermediates, final, out _))
            {
                HandleKitty(parameters);
            }

            return;
        }

        if (intermediates.IsEmpty && parameters.IsEmpty && final is (byte) 'I' or (byte) 'O')
        {
            var focus = new Focus(final == (byte) 'I');
            _sink.Input(in focus);
            return;
        }

        if (intermediates.IsEmpty && final == (byte) '~' &&
            TryReadSingle(parameters, out var native) && native == 200)
        {
            BeginPaste();
            return;
        }

        if (intermediates.IsEmpty && final is (byte) 'M' or (byte) 'm' &&
            TryHandleMouse(parameters, final == (byte) 'm'))
        {
            return;
        }

        if (!intermediates.IsEmpty)
        {
            Report(DiagnosticCode.Unsupported, SequenceKind.Csi);
            return;
        }

        Span<int> values = stackalloc int[3];

        if (!TryReadParameters(parameters, values, out var count))
        {
            Report(DiagnosticCode.Malformed, SequenceKind.Csi);
            return;
        }

        if (final == (byte) '~')
        {
            HandleTilde(values, count);
            return;
        }

        var code = final switch
        {
            (byte) 'A' => Code.Up,
            (byte) 'B' => Code.Down,
            (byte) 'C' => Code.Right,
            (byte) 'D' => Code.Left,
            (byte) 'H' => Code.Home,
            (byte) 'F' => Code.End,
            (byte) 'P' => Code.F1,
            (byte) 'Q' => Code.F2,
            (byte) 'R' => Code.F3,
            (byte) 'S' => Code.F4,
            (byte) 'Z' => Code.Tab,
            _ => Code.Unknown,
        };

        if (!TryGetModifiers(values, count, out var modifiers))
        {
            Report(DiagnosticCode.Malformed, SequenceKind.Csi);
            return;
        }

        if (final == (byte) 'Z')
        {
            modifiers |= Modifiers.Shift;
        }

        EmitStroke(code, null, code == Code.Unknown ? final : 0, modifiers);
    }

    private void HandleTilde(ReadOnlySpan<int> values, int count)
    {
        if (count is < 1 or > 2 || values[0] < 0 ||
            !TryGetModifier(count == 2 ? values[1] : 1, out var modifiers))
        {
            Report(DiagnosticCode.Malformed, SequenceKind.Csi);
            return;
        }

        var native = values[0];
        var code = native switch
        {
            1 or 7 => Code.Home,
            2 => Code.Insert,
            3 => Code.Delete,
            4 or 8 => Code.End,
            5 => Code.PageUp,
            6 => Code.PageDown,
            11 => Code.F1,
            12 => Code.F2,
            13 => Code.F3,
            14 => Code.F4,
            15 => Code.F5,
            17 => Code.F6,
            18 => Code.F7,
            19 => Code.F8,
            20 => Code.F9,
            21 => Code.F10,
            23 => Code.F11,
            24 => Code.F12,
            _ => Code.Unknown,
        };
        EmitStroke(code, null, native, modifiers);
    }

    private void HandleSs3(byte final)
    {
        FlushUtf8();
        _ss3Pending = false;
        var code = final switch
        {
            (byte) 'A' => Code.Up,
            (byte) 'B' => Code.Down,
            (byte) 'C' => Code.Right,
            (byte) 'D' => Code.Left,
            (byte) 'H' => Code.Home,
            (byte) 'F' => Code.End,
            (byte) 'P' => Code.F1,
            (byte) 'Q' => Code.F2,
            (byte) 'R' => Code.F3,
            (byte) 'S' => Code.F4,
            _ => Code.Unknown,
        };
        EmitStroke(code, null, code == Code.Unknown ? final : 0);
    }

    private void HandleSequence(SequenceKind kind)
    {
        FlushUtf8();
        EndX10IfPending();
        EndSs3IfPending();
        Report(DiagnosticCode.Unsupported, kind);
    }

    private void HandleParserDiagnostic(in Diagnostic value)
    {
        FlushUtf8();
        EndX10IfPending();
        EndSs3IfPending();
        var adjusted = new Diagnostic(
            value.Code,
            value.Kind,
            checked(value.Offset + _skippedBytes),
            value.DiscardedBytes);
        _sink.Input(in adjusted);
    }

    private void EmitText(Rune rune, Modifiers modifiers = Modifiers.None)
    {
        var combined = modifiers | _nextTextModifiers;
        _nextTextModifiers = Modifiers.None;
        EmitStroke(Code.Character, rune, 0, combined);
        var text = new Text(rune);
        _sink.Input(in text);
    }

    private void EmitStroke(
        Code code,
        Rune? character = null,
        int nativeCode = 0,
        Modifiers modifiers = Modifiers.None)
    {
        var stroke = new Stroke(code, character, nativeCode, modifiers, InputAction.Press);
        _sink.Input(in stroke);
    }

    private void Report(DiagnosticCode code, SequenceKind kind)
    {
        var diagnostic = new Diagnostic(
            code,
            kind,
            checked(_parser.Offset + _skippedBytes),
            0);
        _sink.Input(in diagnostic);
    }

    private void EndSs3IfPending()
    {
        if (!_ss3Pending)
        {
            return;
        }

        _ss3Pending = false;
        Report(DiagnosticCode.Malformed, SequenceKind.Escape);
    }

    private static bool TryReadSingle(ReadOnlySpan<byte> input, out int value)
    {
        var parameters = new Parameters(input, 2, int.MaxValue);
        var status = parameters.Read(out value, out var separator);
        return parameters.PrivateMarker == 0 &&
            status == ParameterStatus.Value &&
            separator == ParameterSeparator.None &&
            parameters.Read(out _, out _) == ParameterStatus.End;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static bool TryGetModifiers(
        ReadOnlySpan<int> values,
        int count,
        out Modifiers modifiers)
    {
        modifiers = Modifiers.None;

        return count == 0 ||
            (count <= 2 &&
                values[0] is -1 or 1 &&
                (count == 1 || TryGetModifier(values[1], out modifiers)));
    }

    private static bool TryGetModifier(int value, out Modifiers modifiers)
    {
        value = value < 0 ? 1 : value;
        var flags = value - 1;
        modifiers = (Modifiers) flags;
        return value is >= 1 and <= 16;
    }

    private static bool TryReadParameters(
        ReadOnlySpan<byte> input,
        Span<int> destination,
        out int count)
    {
        var parameters = new Parameters(input, destination.Length + 1);
        count = 0;

        if (parameters.PrivateMarker != 0)
        {
            return false;
        }

        while (true)
        {
            var status = parameters.Read(out var value, out var separator);

            if (status == ParameterStatus.End)
            {
                return true;
            }

            if (count == destination.Length ||
                status is not (ParameterStatus.Value or ParameterStatus.Default) ||
                separator == ParameterSeparator.Colon)
            {
                return false;
            }

            destination[count++] = status == ParameterStatus.Default ? -1 : value;
        }
    }

    private readonly struct Adapter(Decoder owner): ISequenceSink
    {
        public void Text(ReadOnlySpan<byte> value)
        {
            if (owner._x10Pending)
            {
                value = value[owner.ConsumeX10(value)..];
            }

            if (owner._ss3Pending && !value.IsEmpty)
            {
                owner.HandleSs3(value[0]);
                value = value[1..];
            }

            owner.DecodeText(value);
        }

        public void Control(byte value)
        {
            owner.FlushUtf8();
            owner.HandleControl(value);
        }

        public void Escape(ReadOnlySpan<byte> intermediates, byte final) =>
            owner.HandleEscape(intermediates, final);

        public void Csi(
            ReadOnlySpan<byte> parameters,
            ReadOnlySpan<byte> intermediates,
            byte final) => owner.HandleCsi(parameters, intermediates, final);

        public void Sequence(
            SequenceKind kind,
            ReadOnlySpan<byte> value,
            StringTerminator terminator) => owner.HandleSequence(kind);

        public void Dcs(
            ReadOnlySpan<byte> parameters,
            ReadOnlySpan<byte> intermediates,
            byte final,
            ReadOnlySpan<byte> value,
            StringTerminator terminator) => owner.HandleSequence(SequenceKind.Dcs);

        public void Report(in Diagnostic value) => owner.HandleParserDiagnostic(in value);
    }
}
