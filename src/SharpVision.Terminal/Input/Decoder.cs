// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;

using InputAction = Action;

/// <summary>
/// Incrementally decodes UTF-8 and legacy VT keyboard input into stable values.
/// </summary>
/// <remarks>
/// The decoder is single-threaded. Input bytes and parser callback spans are
/// borrowed only for each synchronous call; emitted values retain none of them.
/// </remarks>
[PublicAPI]
public sealed class Decoder: IDisposable
{
    private static readonly byte[] _pasteEnd = "\u001b[201~"u8.ToArray();

    private readonly IInputSink _sink;
    private readonly IProtocolSink? _protocolSink;
    private readonly Options _options;
    private readonly Parser _parser;
    private KeySequenceMatcher? _keyMatcher;
    private byte[]? _keyReplay;
    private readonly TimeProvider _timeProvider;
    private readonly byte[] _utf8 = new byte[4];
    private readonly byte[] _x10 = new byte[12];
    private byte[]? _paste;
    private DateTimeOffset _escapeDeadline;
    private Metrics? _cellMetrics;
    private Size? _localCells;
    private Size? _localPixels;
    private Size? _queriedCellPixels;
    private Size? _queriedWindowCells;
    private Size? _queriedWindowPixels;
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
        _protocolSink = sink as IProtocolSink;
        _options = options ?? Options.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _parser = new Parser(_options.Limits);
        _keyMatcher = _options.KeyMap.FallbackBindings.Count == 0
            ? null
            : new KeySequenceMatcher(_options.KeyMap.FallbackBindings);
        _keyReplay = _keyMatcher is null ? null : new byte[_keyMatcher.MaximumLength];
        _cellMetrics = _options.CellMetrics;

        if (_cellMetrics is { Cells: { } cells, Pixels: { } pixels })
        {
            _localCells = cells;
            _localPixels = pixels;
        }
        else if (_cellMetrics is { } uniform)
        {
            _queriedCellPixels = new Size(uniform.Width, uniform.Height);
        }

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
                DecodeCoreByte(value, ref adapter);
                position++;
                continue;
            }

            if (_keyMatcher is not null &&
                (_keyMatcher.IsPending || CanStartMatcher(value)))
            {
                var status = _keyMatcher.Add(
                    value,
                    out var binding,
                    out var replayOffset,
                    out var replayLength);

                if (status == KeySequenceMatchStatus.Pending)
                {
                    position++;
                    continue;
                }

                if (status == KeySequenceMatchStatus.Match)
                {
                    EmitFallbackBinding(in binding);
                    RematchMatcher(replayOffset, replayLength, ref adapter);
                }
                else
                {
                    ReplayMatcherToCore(replayOffset, replayLength, ref adapter);
                }
                position++;
                continue;
            }

            DecodeCoreByte(value, ref adapter);
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
        EmitEscape();
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
        var completionAdapter = new Adapter(this);

        while (_keyMatcher is { IsPending: true })
        {
            var status = _keyMatcher.Complete(
                out var binding,
                out var replayOffset,
                out var replayLength);

            if (status == KeySequenceMatchStatus.Match)
            {
                EmitFallbackBinding(in binding);
                RematchMatcher(replayOffset, replayLength, ref completionAdapter);
            }
            else
            {
                ReplayMatcherToCore(replayOffset, replayLength, ref completionAdapter);
            }
        }

        FlushUtf8();

        if (_escapePending)
        {
            _escapePending = false;
            _skippedBytes = checked(_skippedBytes + 1);
            EmitEscape();
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

    /// <summary>Updates highest-confidence local geometry after an ordered resize event.</summary>
    /// <param name="cells">The non-negative local text-area cell dimensions.</param>
    /// <param name="pixels">Optional non-negative local text-area pixel dimensions.</param>
    internal void SetGeometry(Size cells, Size? pixels)
    {
        ThrowIfDisposed();
        _localCells = HasPositive(cells) ? cells : null;
        _localPixels = pixels is { } pixelSize && HasPositive(pixelSize)
            ? pixelSize
            : null;
        _queriedWindowCells = null;
        _queriedWindowPixels = null;
        RecomputeCellMetrics();
    }

    /// <summary>Accounts for raw transport bytes intentionally removed before protocol decoding.</summary>
    /// <param name="count">The non-negative number of skipped raw bytes.</param>
    internal void AdvanceTransportOffset(long count)
    {
        Debug.Assert(count >= 0, "Skipped transport byte counts cannot be negative.");
        _skippedBytes = checked(_skippedBytes + count);
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

        if (_keyReplay is { } keyReplay)
        {
            keyReplay.AsSpan().Clear();
            _keyReplay = null;
        }

        _keyMatcher?.Dispose();
        _keyMatcher = null;

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

    private void DecodeCoreByte(byte value, ref Adapter adapter)
    {
        if (_pasteMode)
        {
            _skippedBytes = checked(_skippedBytes + 1);
            ProcessPaste(value);
            return;
        }

        if (_utf8Length == 0 &&
            !_escapePending &&
            !_ss3Pending &&
            !_x10Pending &&
            _parser.IsGround)
        {
            if (value == 0x8f && _options.KeyMap.RequiresEightBitSs3)
            {
                _skippedBytes = checked(_skippedBytes + 1);
                _ss3Pending = true;
                return;
            }

            if (value == 0x9b && _options.KeyMap.RequiresEightBitCsi)
            {
                _parser.BeginCsiFromEightBit();
                return;
            }
        }

        if (_escapePending)
        {
            if (value == 0x1b)
            {
                _skippedBytes = checked(_skippedBytes + 1);
                EmitEscape();
                BeginEscape();
                return;
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
            return;
        }

        if (_parser.IsGround && value == 0x7f)
        {
            _skippedBytes = checked(_skippedBytes + 1);
            FlushUtf8();
            HandleControl(value);
            return;
        }

        Span<byte> one = [value];
        _parser.Parse(one, ref adapter);
    }

    private bool CanStartMatcher(byte value) =>
        _utf8Length == 0 &&
        !_escapePending &&
        !_ss3Pending &&
        !_x10Pending &&
        _parser.IsGround &&
        value != 0x1b;

    private void RematchMatcher(int offset, int length, ref Adapter adapter)
    {
        if (length == 0)
        {
            return;
        }

        Debug.Assert(_keyMatcher is not null, "Suffix rematching belongs to an active matcher.");
        Debug.Assert(_keyReplay is not null, "An active matcher owns bounded replay workspace.");
        var matcher = _keyMatcher;
        var replay = _keyReplay;
        Debug.Assert(length <= replay.Length, "A suffix cannot exceed the longest described key.");

        matcher.Replay(offset, length).CopyTo(replay);
        var position = 0;
        var count = length;

        while (position < count)
        {
            var value = replay[position++];

            if (!matcher.IsPending && !CanStartMatcher(value))
            {
                DecodeCoreByte(value, ref adapter);
                continue;
            }

            var status = matcher.Add(
                value,
                out var binding,
                out var replayOffset,
                out var replayLength);

            if (status == KeySequenceMatchStatus.Pending)
            {
                continue;
            }

            if (status == KeySequenceMatchStatus.Replay)
            {
                ReplayMatcherToCore(replayOffset, replayLength, ref adapter);
                continue;
            }

            EmitFallbackBinding(in binding);

            if (replayLength == 0)
            {
                continue;
            }

            var remaining = count - position;
            Debug.Assert(
                remaining + replayLength <= replay.Length,
                "Rematching reorders existing bytes without growing retained input.");
            replay.AsSpan(position, remaining)
                .CopyTo(replay.AsSpan(replayLength));
            matcher.Replay(replayOffset, replayLength)
                .CopyTo(replay.AsSpan(0, replayLength));
            position = 0;
            count = replayLength + remaining;
        }
    }

    private void ReplayMatcherToCore(int offset, int length, ref Adapter adapter)
    {
        Debug.Assert(_keyMatcher is not null, "Replay belongs to an active fallback matcher.");
        var matcher = _keyMatcher;

        foreach (var value in matcher.Replay(offset, length))
        {
            DecodeCoreByte(value, ref adapter);
        }
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

        if (value == 0x8f && _options.KeyMap.RequiresEightBitSs3)
        {
            _ss3Pending = true;
            return;
        }

        if (_options.KeyMap.TryGet(
                KeySignatureKind.Control,
                [],
                [],
                value,
                out var binding))
        {
            EmitBinding(in binding);
            return;
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

        if (intermediates.IsEmpty && final == (byte) 'O')
        {
            _ss3Pending = true;
            return;
        }

        if (_options.KeyMap.TryGet(
                KeySignatureKind.Escape,
                [],
                intermediates,
                final,
                out var binding))
        {
            EmitBinding(in binding);
            return;
        }

        if (!intermediates.IsEmpty)
        {
            Report(DiagnosticCode.Unsupported, SequenceKind.Escape);
            return;
        }

        if (!_options.UseAnsiKeyGrammar)
        {
            Report(DiagnosticCode.Unsupported, SequenceKind.Escape);
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

        if (XtermResponses.TryMetricsCsi(parameters, intermediates, final, out var metrics))
        {
            if (_protocolSink is { } protocolSink)
            {
                protocolSink.Response(in metrics);
                ApplyMetrics(in metrics);
            }
            else
            {
                Report(DiagnosticCode.Unsupported, SequenceKind.Csi);
            }

            return;
        }

        if (XtermResponses.TryCsi(parameters, intermediates, final, out var response))
        {
            if (_protocolSink is { } protocolSink)
            {
                protocolSink.Response(in response);
            }
            else
            {
                Report(DiagnosticCode.Unsupported, SequenceKind.Csi);
            }

            return;
        }

        if (intermediates.IsEmpty && final == (byte) 'u')
        {
            HandleKitty(parameters);
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

        if (_options.KeyMap.TryGet(
                KeySignatureKind.Csi,
                parameters,
                intermediates,
                final,
                out var binding))
        {
            EmitBinding(in binding);
            return;
        }

        if (intermediates.IsEmpty && TryHandleCsiKey(parameters, final))
        {
            return;
        }

        if (!_options.UseAnsiKeyGrammar)
        {
            Report(DiagnosticCode.Unsupported, SequenceKind.Csi);
            return;
        }

        if (!intermediates.IsEmpty)
        {
            Report(DiagnosticCode.Unsupported, SequenceKind.Csi);
            return;
        }

        if (final == (byte) '~')
        {
            Span<int> values = stackalloc int[3];

            if (!TryReadParameters(parameters, values, out var count))
            {
                Report(DiagnosticCode.Malformed, SequenceKind.Csi);
                return;
            }

            if (count == 3 && values[0] == 27 && TryHandleModifiedOtherKey(values[1], values[2]))
            {
                return;
            }

            HandleTilde(values, count);
            return;
        }

        Report(DiagnosticCode.Unsupported, SequenceKind.Csi);
    }

    private bool TryHandleCsiKey(ReadOnlySpan<byte> parameters, byte final)
    {
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
            _ => (Code?) null
        };

        if (code is null)
        {
            return false;
        }

        if (!TryReadCsiModifiers(parameters, out var modifiers, out var action))
        {
            Report(DiagnosticCode.Malformed, SequenceKind.Csi);
            return true;
        }

        if (final == (byte) 'Z')
        {
            modifiers |= Modifiers.Shift;
        }

        EmitStroke(code.Value, null, 0, modifiers, action);
        return true;
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
            _ => Code.Unknown
        };
        EmitStroke(code, null, native, modifiers);
    }

    private bool TryHandleModifiedOtherKey(int encodedModifiers, int native)
    {
        if (!TryGetModifier(encodedModifiers, out var modifiers) ||
            !Rune.TryCreate(native, out var rune))
        {
            return false;
        }

        var code = native switch
        {
            9 => Code.Tab,
            13 => Code.Enter,
            27 => Code.Escape,
            127 => Code.Backspace,
            _ => Rune.GetUnicodeCategory(rune) == UnicodeCategory.Control
                ? Code.Unknown
                : Code.Character
        };
        EmitStroke(code, code == Code.Character ? rune : null, native, modifiers);
        return true;
    }

    private void HandleSs3(byte final)
    {
        FlushUtf8();
        _ss3Pending = false;
        if (_options.KeyMap.TryGet(
                KeySignatureKind.Ss3,
                [],
                [],
                final,
                out var binding))
        {
            EmitBinding(in binding);
            return;
        }

        if (!_options.UseAnsiKeyGrammar)
        {
            Report(DiagnosticCode.Unsupported, SequenceKind.Escape);
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
            _ => Code.Unknown
        };
        EmitStroke(code, null, code == Code.Unknown ? final : 0);
    }

    private void HandleSequence(
        SequenceKind kind,
        ReadOnlySpan<byte> value,
        StringTerminator terminator)
    {
        FlushUtf8();
        EndX10IfPending();
        EndSs3IfPending();

        if (kind == SequenceKind.Osc && XtermResponses.TryOsc(value, out var response))
        {
            if (_protocolSink is { } protocolSink)
            {
                protocolSink.Response(in response);
            }
            else
            {
                Report(DiagnosticCode.Unsupported, kind);
            }

            return;
        }

        if (kind == SequenceKind.Apc && !value.IsEmpty && value[0] == (byte) 'G')
        {
            if (_protocolSink is { } graphicsSink)
            {
                graphicsSink.Response(Kitty.Response.Parse(value, _options.Limits));
            }
            else
            {
                Report(DiagnosticCode.Unsupported, kind);
            }

            return;
        }

        if (_protocolSink is null)
        {
            Report(DiagnosticCode.Unsupported, kind);
            return;
        }

        _protocolSink.Sequence(new ProtocolSequence(
            kind,
            [],
            [],
            0,
            value,
            terminator));
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

    private void ApplyMetrics(in MetricsResponse response)
    {
        if (response.Kind == ResponseKind.WindowPixels)
        {
            _queriedWindowPixels = response.Size;
        }
        else if (response.Kind == ResponseKind.CellPixels)
        {
            _queriedCellPixels = response.Size;
        }
        else
        {
            Debug.Assert(
                response.Kind == ResponseKind.WindowCells,
                "Metrics responses validate their family at construction.");
            _queriedWindowCells = response.Size;
        }

        RecomputeCellMetrics();
    }

    private void RecomputeCellMetrics()
    {
        var cells = _localCells ?? _queriedWindowCells;
        var pixels = _localPixels ?? _queriedWindowPixels;

        if (cells is { } cellSize &&
            pixels is { } pixelSize &&
            pixelSize.Width >= cellSize.Width &&
            pixelSize.Height >= cellSize.Height)
        {
            _cellMetrics = new Metrics(cellSize, pixelSize);
            return;
        }

        _cellMetrics = _queriedCellPixels is { } cellPixels
            ? new Metrics(cellPixels.Width, cellPixels.Height)
            : null;
    }

    private static bool HasPositive(Size value) =>
        value is { Width: > 0, Height: > 0 };

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
        Modifiers modifiers = Modifiers.None,
        InputAction action = InputAction.Press)
    {
        var stroke = new Stroke(code, character, nativeCode, modifiers, action);
        _sink.Input(in stroke);
    }

    private void EmitBinding(in KeyBinding binding) =>
        EmitStroke(binding.Code, null, 0, binding.Modifiers);

    private void EmitFallbackBinding(in KeyBinding binding)
    {
        _skippedBytes = checked(_skippedBytes + binding.Sequence.Length);
        EmitBinding(in binding);
    }

    private void EmitEscape()
    {
        if (_options.KeyMap.TryGet(
                KeySignatureKind.Control,
                [],
                [],
                0x1b,
                out var binding))
        {
            EmitBinding(in binding);
            return;
        }

        EmitStroke(Code.Escape);
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

    private static bool TryReadCsiModifiers(
        ReadOnlySpan<byte> parameters,
        out Modifiers modifiers,
        out InputAction action)
    {
        modifiers = Modifiers.None;
        action = InputAction.Press;

        if (parameters.IsEmpty)
        {
            return true;
        }

        var semicolon = parameters.IndexOf((byte) ';');

        return semicolon < 0
            ? TryDecimal(parameters, allowEmpty: true, out _)
            : TryDecimal(parameters[..semicolon], allowEmpty: true, out _)
              && TryReadModifiers(parameters[(semicolon + 1)..], out modifiers, out action);
    }

    private const int _maxAssociatedText = 32;

    private void HandleKitty(ReadOnlySpan<byte> parameters)
    {
        if (!TrySplitGroups(
                parameters,
                out var keyGroup,
                out var modifierGroup,
                out var textGroup,
                out var hasText) ||
            !TryReadKey(
                keyGroup,
                out var native,
                out var shifted,
                out var baseLayout) ||
            !TryReadModifiers(modifierGroup, out var modifiers, out var action) ||
            (hasText && !ValidateText(textGroup)))
        {
            Report(DiagnosticCode.Malformed, SequenceKind.Csi);
            return;
        }

        var code = MapKittyCode(native, out var character);
        var stroke = new Stroke(
            code,
            character,
            native,
            modifiers,
            action,
            shifted,
            baseLayout);
        _sink.Input(in stroke);

        if (hasText)
        {
            EmitAssociatedText(textGroup);
        }
    }

    private void EmitAssociatedText(ReadOnlySpan<byte> input)
    {
        while (!input.IsEmpty)
        {
            var separator = input.IndexOf((byte) ':');
            var field = separator < 0 ? input : input[..separator];
            var parsed = TryDecimal(field, allowEmpty: false, out var value);
            var scalar = Rune.TryCreate(value, out var rune);
            Debug.Assert(
                parsed && scalar,
                "Associated text is fully validated before emission.");
            var text = new Text(rune);
            _sink.Input(in text);
            input = separator < 0 ? [] : input[(separator + 1)..];
        }
    }

    private static Code MapKittyCode(int native, out Rune? character)
    {
        character = null;

        switch (native)
        {
            case 0:
                return Code.Unknown;
            case 9:
                return Code.Tab;
            case 13:
                return Code.Enter;
            case 27:
                return Code.Escape;
            case 127:
            case 57347:
                return Code.Backspace;
            case 57344:
                return Code.Escape;
            case 57345:
                return Code.Enter;
            case 57346:
                return Code.Tab;
            case 57348:
                return Code.Insert;
            case 57349:
                return Code.Delete;
            case 57350:
                return Code.Left;
            case 57351:
                return Code.Right;
            case 57352:
                return Code.Up;
            case 57353:
                return Code.Down;
            case 57354:
                return Code.PageUp;
            case 57355:
                return Code.PageDown;
            case 57356:
                return Code.Home;
            case 57357:
                return Code.End;
            case 57358:
                return Code.CapsLock;
            case 57359:
                return Code.ScrollLock;
            case 57360:
                return Code.NumLock;
            case 57361:
                return Code.PrintScreen;
            case 57362:
                return Code.Pause;
            case 57363:
                return Code.Menu;
            case >= 57364 and <= 57375:
                var f1Through12 = native - 57364 + 1;
                var f1Mapped = FunctionKeys.TryGet(f1Through12, out var f1Code);
                Debug.Assert(f1Mapped, "Kitty's named function range maps to F1 through F12.");
                return f1Code;
            case >= 57376 and <= 57398:
                var function = native - 57376 + 13;
                var mapped = FunctionKeys.TryGet(function, out var functionCode);
                Debug.Assert(mapped, "Kitty's named function range maps to F13 through F35.");
                return functionCode;
            default:
                break;
        }

        if (native is >= 57344 and <= 63743 ||
            !Rune.TryCreate(native, out var rune) ||
            Rune.GetUnicodeCategory(rune) == UnicodeCategory.Control)
        {
            return Code.Unknown;
        }

        character = rune;
        return Code.Character;
    }

    private static bool TrySplitGroups(
        ReadOnlySpan<byte> input,
        out ReadOnlySpan<byte> key,
        out ReadOnlySpan<byte> modifiers,
        out ReadOnlySpan<byte> text,
        out bool hasText)
    {
        var first = input.IndexOf((byte) ';');

        if (first < 0)
        {
            key = input;
            modifiers = [];
            text = [];
            hasText = false;
            return true;
        }

        key = input[..first];
        input = input[(first + 1)..];
        var second = input.IndexOf((byte) ';');

        if (second < 0)
        {
            modifiers = input;
            text = [];
            hasText = false;
            return true;
        }

        modifiers = input[..second];
        text = input[(second + 1)..];
        hasText = true;
        return text.IndexOf((byte) ';') < 0;
    }

    private static bool TryReadKey(
        ReadOnlySpan<byte> input,
        out int native,
        out Rune? shifted,
        out Rune? baseLayout)
    {
        shifted = null;
        baseLayout = null;
        var first = input.IndexOf((byte) ':');
        var main = first < 0 ? input : input[..first];

        if (!TryDecimal(main, allowEmpty: false, out native) ||
            (native != 0 && !Rune.TryCreate(native, out _)))
        {
            return false;
        }

        if (first < 0)
        {
            return true;
        }

        input = input[(first + 1)..];
        var second = input.IndexOf((byte) ':');
        var shiftedField = second < 0 ? input : input[..second];

        if (!TryOptionalRune(shiftedField, out shifted))
        {
            return false;
        }

        if (second < 0)
        {
            return true;
        }

        var baseField = input[(second + 1)..];
        return baseField.IndexOf((byte) ':') < 0 &&
               TryOptionalRune(baseField, out baseLayout);
    }

    private static bool TryReadModifiers(
        ReadOnlySpan<byte> input,
        out Modifiers modifiers,
        out InputAction action)
    {
        var separator = input.IndexOf((byte) ':');
        var modifierField = separator < 0 ? input : input[..separator];

        if (!TryDecimal(modifierField, allowEmpty: true, out var encoded))
        {
            modifiers = default;
            action = default;
            return false;
        }

        encoded = modifierField.IsEmpty ? 1 : encoded;

        if (encoded is < 1 or > 256)
        {
            modifiers = default;
            action = default;
            return false;
        }

        modifiers = (Modifiers) (encoded - 1);
        action = InputAction.Press;

        if (separator < 0)
        {
            return true;
        }

        var eventField = input[(separator + 1)..];

        if (eventField.IndexOf((byte) ':') >= 0 ||
            !TryDecimal(eventField, allowEmpty: false, out var eventType) ||
            eventType is < 1 or > 3)
        {
            return false;
        }

        action = (InputAction) (eventType - 1);
        return true;
    }

    private static bool ValidateText(ReadOnlySpan<byte> input)
    {
        var count = 0;

        while (!input.IsEmpty)
        {
            var separator = input.IndexOf((byte) ':');
            var field = separator < 0 ? input : input[..separator];

            if (++count > _maxAssociatedText ||
                !TryDecimal(field, allowEmpty: false, out var value) ||
                !Rune.TryCreate(value, out _) ||
                value < 0x20 ||
                value is >= 0x7f and <= 0x9f)
            {
                return false;
            }

            input = separator < 0 ? [] : input[(separator + 1)..];
        }

        return true;
    }

    private static bool TryOptionalRune(ReadOnlySpan<byte> input, out Rune? rune)
    {
        if (input.IsEmpty)
        {
            rune = null;
            return true;
        }

        if (TryDecimal(input, allowEmpty: false, out var value) &&
            Rune.TryCreate(value, out var parsed))
        {
            rune = parsed;
            return true;
        }

        rune = null;
        return false;
    }

    private static bool TryDecimal(ReadOnlySpan<byte> input, bool allowEmpty, out int value)
    {
        value = 0;

        if (input.IsEmpty)
        {
            return allowEmpty;
        }

        foreach (var item in input)
        {
            if (item is < (byte) '0' or > (byte) '9')
            {
                value = 0;
                return false;
            }

            var digit = item - (byte) '0';

            if (value > int.MaxValue / 10 ||
                (value == int.MaxValue / 10 && digit > int.MaxValue % 10))
            {
                value = 0;
                return false;
            }

            value = (value * 10) + digit;
        }

        return true;
    }

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

    private bool TryHandleMouse(ReadOnlySpan<byte> parameters, bool release)
    {
        if (parameters.IsEmpty && !release)
        {
            _x10Pending = true;
            _x10Length = 0;
            return true;
        }

        if (!TryReadMouse(parameters, out var marker, out var code, out var x, out var y))
        {
            Report(DiagnosticCode.Malformed, SequenceKind.Csi);
            return true;
        }

        if (marker == (byte) '<')
        {
            EmitPointer(code, x, y, release);
            return true;
        }

        if (marker == 0 && !release)
        {
            EmitPointer(code - 32, x, y, release: false);
            return true;
        }

        Report(DiagnosticCode.Malformed, SequenceKind.Csi);
        return true;
    }

    private int ConsumeX10(ReadOnlySpan<byte> value)
    {
        var consumed = 0;

        while (_x10Pending && consumed < value.Length)
        {
            if (_x10Length == _x10.Length)
            {
                EndX10IfPending();
                break;
            }

            _x10[_x10Length++] = value[consumed++];

            if (TryReadX10(out var code, out var x, out var y))
            {
                _x10Pending = false;
                _x10.AsSpan(0, _x10Length).Clear();
                _x10Length = 0;
                EmitPointer(code - 32, x - 32, y - 32, release: false);
            }
        }

        return consumed;
    }

    private bool TryReadX10(out int code, out int x, out int y)
    {
        Span<int> values = stackalloc int[3];
        var position = 0;

        for (var index = 0; index < values.Length; index++)
        {
            var status = Rune.DecodeFromUtf8(
                _x10.AsSpan(position, _x10Length - position),
                out var rune,
                out var consumed);

            if (status == OperationStatus.NeedMoreData)
            {
                code = 0;
                x = 0;
                y = 0;
                return false;
            }

            if (status != OperationStatus.Done)
            {
                EndX10IfPending();
                code = 0;
                x = 0;
                y = 0;
                return false;
            }

            values[index] = rune.Value;
            position += consumed;
        }

        code = values[0];
        x = values[1];
        y = values[2];
        return position == _x10Length;
    }

    private void EndX10IfPending()
    {
        if (!_x10Pending)
        {
            return;
        }

        _x10Pending = false;
        _x10.AsSpan(0, _x10Length).Clear();
        _x10Length = 0;
        Report(DiagnosticCode.Malformed, SequenceKind.Csi);
    }

    private void EmitPointer(int code, int wireX, int wireY, bool release)
    {
        var motion = (code & 32) != 0;
        var low = code & 3;

        if (code is < 0 or > 255 ||
            ((code & 128) != 0 && ((code & 64) != 0 || low > 1)))
        {
            Report(DiagnosticCode.Malformed, SequenceKind.Csi);
            return;
        }

        if (wireX == 0 && wireY == 0 && motion && low == 3)
        {
            var leave = new Pointer(
                null,
                null,
                Buttons.None,
                PointerAction.Leave,
                0,
                0,
                DecodeMouseModifiers(code),
                true,
                false);
            _sink.Input(in leave);
            return;
        }

        if (wireX <= 0 || wireY <= 0)
        {
            Report(DiagnosticCode.Malformed, SequenceKind.Csi);
            return;
        }

        var source = new Point(wireX - 1, wireY - 1);
        Point? cells = source;
        Point? pixels = null;
        var inferred = false;

        if (_pixelMouse)
        {
            pixels = source;
            cells = null;

            if (_cellMetrics is { } metrics && metrics.TryMap(source, out var mapped))
            {
                cells = mapped;
                inferred = true;
            }
        }

        var modifiers = DecodeMouseModifiers(code);
        var buttons = DecodeButtons(code);
        var action = PointerAction.Press;
        var wheelX = 0;
        var wheelY = 0;

        if ((code & 64) != 0)
        {
            action = PointerAction.Wheel;
            buttons = Buttons.None;

            switch (low)
            {
                case 0:
                    wheelY = 1;
                    break;
                case 1:
                    wheelY = -1;
                    break;
                case 2:
                    wheelX = -1;
                    break;
                case 3:
                    wheelX = 1;
                    break;
                default:
                    throw new UnreachableException("A two-bit wheel selector must be bounded.");
            }
        }
        else if (motion)
        {
            action = PointerAction.Move;
        }
        else if (release || low == 3)
        {
            action = PointerAction.Release;
        }

        var pointer = new Pointer(
            cells,
            pixels,
            buttons,
            action,
            wheelX,
            wheelY,
            modifiers,
            motion,
            inferred);
        _sink.Input(in pointer);
    }

    private static Buttons DecodeButtons(int code)
    {
        var selector = code & 3;
        return (code & 64) != 0 || selector == 3
            ? Buttons.None
            : (code & 128) != 0
                ? selector switch
                {
                    0 => Buttons.Back,
                    1 => Buttons.Forward,
                    _ => Buttons.None
                }
                : selector switch
                {
                    0 => Buttons.Primary,
                    1 => Buttons.Middle,
                    2 => Buttons.Secondary,
                    _ => Buttons.None
                };
    }

    private static Modifiers DecodeMouseModifiers(int code)
    {
        var modifiers = Modifiers.None;

        if ((code & 4) != 0)
        {
            modifiers |= Modifiers.Shift;
        }

        if ((code & 8) != 0)
        {
            modifiers |= Modifiers.Alt;
        }

        if ((code & 16) != 0)
        {
            modifiers |= Modifiers.Control;
        }

        return modifiers;
    }

    private static bool TryReadMouse(
        ReadOnlySpan<byte> input,
        out byte marker,
        out int code,
        out int x,
        out int y)
    {
        var parameters = new Parameters(input, 4, int.MaxValue);
        marker = parameters.PrivateMarker;
        x = 0;
        y = 0;
        return ReadMouseField(ref parameters, ParameterSeparator.Semicolon, out code) &&
               ReadMouseField(ref parameters, ParameterSeparator.Semicolon, out x) &&
               ReadMouseField(ref parameters, ParameterSeparator.None, out y) &&
               parameters.Read(out _, out _) == ParameterStatus.End;
    }

    private static bool ReadMouseField(
        ref Parameters parameters,
        ParameterSeparator expected,
        out int value) =>
        parameters.Read(out value, out var separator) == ParameterStatus.Value &&
        separator == expected;

    /// <summary>Accepts borrowed parser text through pending SS3 and X10 state.</summary>
    /// <param name="value">The borrowed text bytes.</param>
    internal void AcceptText(ReadOnlySpan<byte> value)
    {
        if (_x10Pending)
        {
            value = value[ConsumeX10(value)..];
        }

        if (_ss3Pending && !value.IsEmpty)
        {
            HandleSs3(value[0]);
            value = value[1..];
        }

        DecodeText(value);
    }

    /// <summary>Accepts one parser control byte after flushing pending UTF-8.</summary>
    /// <param name="value">The control byte.</param>
    internal void AcceptControl(byte value)
    {
        FlushUtf8();
        HandleControl(value);
    }

    /// <summary>Accepts one parsed escape sequence.</summary>
    /// <param name="intermediates">Borrowed intermediate bytes.</param>
    /// <param name="final">The final byte.</param>
    internal void AcceptEscape(ReadOnlySpan<byte> intermediates, byte final) =>
        HandleEscape(intermediates, final);

    /// <summary>Accepts one parsed CSI sequence.</summary>
    /// <param name="parameters">Borrowed parameter bytes.</param>
    /// <param name="intermediates">Borrowed intermediate bytes.</param>
    /// <param name="final">The final byte.</param>
    internal void AcceptCsi(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final) => HandleCsi(parameters, intermediates, final);

    /// <summary>Accepts one parsed terminal string sequence.</summary>
    /// <param name="kind">The sequence family.</param>
    /// <param name="value">The borrowed bounded payload.</param>
    /// <param name="terminator">The observed string terminator.</param>
    internal void AcceptSequence(
        SequenceKind kind,
        ReadOnlySpan<byte> value,
        StringTerminator terminator) => HandleSequence(kind, value, terminator);

    /// <summary>Accepts one parsed DCS payload.</summary>
    /// <param name="parameters">Borrowed parameter bytes.</param>
    /// <param name="intermediates">Borrowed intermediate bytes.</param>
    /// <param name="final">The DCS final byte.</param>
    /// <param name="value">The borrowed payload.</param>
    /// <param name="terminator">The observed terminator.</param>
    internal void AcceptDcs(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        ReadOnlySpan<byte> value,
        StringTerminator terminator)
    {
        FlushUtf8();
        EndX10IfPending();
        EndSs3IfPending();

        if (XtermDecrqss.TryParse(parameters, intermediates, final, value, out var status))
        {
            if (_protocolSink is { } statusSink)
            {
                statusSink.Response(in status);

                if (status.Name == StatusName.Unknown && status.IsValid)
                {
                    Report(DiagnosticCode.Unsupported, SequenceKind.Dcs);
                }
            }
            else
            {
                Report(DiagnosticCode.Unsupported, SequenceKind.Dcs);
            }

            return;
        }

        if (XtermGetCap.TryParse(
                parameters,
                intermediates,
                final,
                value,
                _options.Limits,
                out var capability))
        {
            if (_protocolSink is { } capabilitySink)
            {
                Debug.Assert(capability is not null, "A successful XTGETTCAP parse owns a response.");
                capabilitySink.Response(capability);
            }
            else
            {
                Report(DiagnosticCode.Unsupported, SequenceKind.Dcs);
            }

            return;
        }

        if (_protocolSink is null)
        {
            Report(DiagnosticCode.Unsupported, SequenceKind.Dcs);
            return;
        }

        _protocolSink.Sequence(new ProtocolSequence(
            SequenceKind.Dcs,
            parameters,
            intermediates,
            final,
            value,
            terminator));
    }

    /// <summary>Accepts one parser diagnostic.</summary>
    /// <param name="value">The structured non-sensitive diagnostic.</param>
    internal void AcceptDiagnostic(in Diagnostic value) => HandleParserDiagnostic(in value);
}
