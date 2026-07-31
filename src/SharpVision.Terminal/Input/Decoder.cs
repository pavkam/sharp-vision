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
    private readonly IInputSink _sink;
    private readonly IProtocolSink? _protocolSink;
    private readonly Options _options;
    private readonly Parser _parser;
    private readonly PasteAccumulator _pasteAccumulator;
    private KeySequenceMatcher? _keyMatcher;
    private byte[]? _keyReplay;
    private readonly TimeProvider _timeProvider;
    private readonly Utf8TextAccumulator _utf8;
    private DateTimeOffset _escapeDeadline;
    private readonly CellMetricsResolver _cellMetricsResolver;
    private readonly MouseDecoder _mouseDecoder;
    private readonly Kitty.KittyKeyDecoder _kittyKeyDecoder;
    private Modifiers _nextTextModifiers;
    private long _skippedBytes;
    private bool _completed;
    private bool _disposed;
    private bool _escapePending;
    private bool _ss3Pending;

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
        _pasteAccumulator = new PasteAccumulator(_options.MaxPasteBytes);
        _keyMatcher = _options.KeyMap.FallbackBindings.Count == 0
            ? null
            : new KeySequenceMatcher(_options.KeyMap.FallbackBindings);
        _keyReplay = _keyMatcher is null ? null : new byte[_keyMatcher.MaximumLength];
        _cellMetricsResolver = new CellMetricsResolver(_options.CellMetrics);
        _mouseDecoder = new MouseDecoder(_sink, _cellMetricsResolver, _options.PixelMouse, Report);
        _kittyKeyDecoder = new Kitty.KittyKeyDecoder(_sink, Report);
        _utf8 = new Utf8TextAccumulator(rune => EmitText(rune));
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

            if (_pasteAccumulator.IsActive)
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
        _utf8.Flush();
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

        _utf8.Flush();

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

        if (_mouseDecoder.IsPending)
        {
            _mouseDecoder.EndIfPending();
        }

        if (_pasteAccumulator.IsActive)
        {
            _pasteAccumulator.Reset();
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
        _cellMetricsResolver.SetGeometry(cells, pixels);
    }

    /// <summary>Accounts for raw transport bytes intentionally removed before protocol decoding.</summary>
    /// <param name="count">The non-negative number of skipped raw bytes.</param>
    internal void AdvanceTransportOffset(long count)
    {
        Debug.Assert(count >= 0, "Skipped transport byte counts cannot be negative.");
        _skippedBytes = checked(_skippedBytes + count);
    }

    /// <summary>Gets the currently owned fallback matcher and its replay workspace, or null when
    /// neither is active, for GC-reachability test seams.</summary>
    internal (KeySequenceMatcher Matcher, byte[] Replay)? OwnedKeyMatcherState =>
        _keyMatcher is { } matcher && _keyReplay is { } replay ? (matcher, replay) : null;

    /// <summary>Clears pending bytes and returns parser-owned pooled storage.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _utf8.Clear();
        _mouseDecoder.Clear();
        _pasteAccumulator.Dispose();

        if (_keyReplay is { } keyReplay)
        {
            keyReplay.AsSpan().Clear();
            _keyReplay = null;
        }

        _keyMatcher?.Dispose();
        _keyMatcher = null;

        _parser.Dispose();
    }

    private void BeginEscape()
    {
        _escapePending = true;
        _escapeDeadline = _timeProvider.GetUtcNow().Add(_options.EscapeTimeout);
    }

    private void DecodeCoreByte(byte value, ref Adapter adapter)
    {
        if (_pasteAccumulator.IsActive)
        {
            _skippedBytes = checked(_skippedBytes + 1);
            ProcessPaste(value);
            return;
        }

        if (!_utf8.HasPending &&
            !_escapePending &&
            !_ss3Pending &&
            !_mouseDecoder.IsPending &&
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
            if (value == ControlBytes.Escape)
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

        if (_parser.IsGround && value == ControlBytes.Escape)
        {
            BeginEscape();
            return;
        }

        if (_parser.IsGround && value == 0x7f)
        {
            _skippedBytes = checked(_skippedBytes + 1);
            _utf8.Flush();
            HandleControl(value);
            return;
        }

        Span<byte> one = [value];
        _parser.Parse(one, ref adapter);
    }

    private bool CanStartMatcher(byte value) =>
        !_utf8.HasPending &&
        !_escapePending &&
        !_ss3Pending &&
        !_mouseDecoder.IsPending &&
        _parser.IsGround &&
        value != ControlBytes.Escape;

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

    private void HandleControl(byte value)
    {
        _mouseDecoder.EndIfPending();

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
        _utf8.Flush();
        _mouseDecoder.EndIfPending();
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
        _utf8.Flush();
        _mouseDecoder.EndIfPending();
        EndSs3IfPending();

        if (XtermResponses.TryMetricsCsi(parameters, intermediates, final, out var metrics))
        {
            if (_protocolSink is { } protocolSink)
            {
                protocolSink.Response(in metrics);
                _cellMetricsResolver.Apply(in metrics);
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
            _kittyKeyDecoder.Handle(parameters);
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
            _mouseDecoder.TryHandleMouse(parameters, final == (byte) 'm'))
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
        _utf8.Flush();
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
        _utf8.Flush();
        _mouseDecoder.EndIfPending();
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
        _utf8.Flush();
        _mouseDecoder.EndIfPending();
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
                ControlBytes.Escape,
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
            ? Kitty.KittyKeyDecoder.TryDecimal(parameters, allowEmpty: true, out _)
            : Kitty.KittyKeyDecoder.TryDecimal(parameters[..semicolon], allowEmpty: true, out _)
              && Kitty.KittyKeyDecoder.TryReadModifiers(parameters[(semicolon + 1)..], out modifiers, out action);
    }

    private void BeginPaste() => _pasteAccumulator.Begin();

    private void ProcessPaste(byte value)
    {
        if (_pasteAccumulator.Process(value))
        {
            FinishPaste();
        }
    }

    private void FinishPaste()
    {
        if (_pasteAccumulator.Overflowed)
        {
            Report(
                DiagnosticCode.StringLimit,
                SequenceKind.Csi,
                _pasteAccumulator.DiscardedBytes);
        }
        else
        {
            var owned = NormalizeUtf8(_pasteAccumulator.Buffered);
            var paste = Paste.Take(owned);
            _sink.Input(paste);
        }

        _pasteAccumulator.Reset();
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

    /// <summary>Accepts borrowed parser text through pending SS3 and X10 state.</summary>
    /// <param name="value">The borrowed text bytes.</param>
    internal void AcceptText(ReadOnlySpan<byte> value)
    {
        if (_mouseDecoder.IsPending)
        {
            value = value[_mouseDecoder.ConsumeX10(value)..];
        }

        if (_ss3Pending && !value.IsEmpty)
        {
            HandleSs3(value[0]);
            value = value[1..];
        }

        _utf8.Process(value);
    }

    /// <summary>Accepts one parser control byte after flushing pending UTF-8.</summary>
    /// <param name="value">The control byte.</param>
    internal void AcceptControl(byte value)
    {
        _utf8.Flush();
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
        _utf8.Flush();
        _mouseDecoder.EndIfPending();
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
