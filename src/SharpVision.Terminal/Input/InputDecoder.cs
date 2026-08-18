// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;

using Protocols;

using Xterm;

/// <summary>
/// Incrementally decodes UTF-8 and legacy VT keyboard input into stable values.
/// </summary>
/// <remarks>
/// The decoder is single-threaded. Input bytes and parser callback spans are
/// borrowed only for each synchronous call; emitted values retain none of them.
/// </remarks>
[PublicAPI]
public sealed class InputDecoder: IDisposable
{
    private readonly IInputSink _sink;
    private readonly IProtocolSink? _protocolSink;
    private readonly InputOptions _options;
    private readonly ProtocolParser _parser;
    private readonly PasteAccumulator _pasteAccumulator;
    private KeySequenceMatcher? _keyMatcher;
    private byte[]? _keyReplay;
    private readonly TimeProvider _timeProvider;
    private readonly Utf8TextAccumulator _utf8;
    private DateTimeOffset _escapeDeadline;
    private readonly CellMetricsResolver _cellMetricsResolver;
    private readonly MouseDecoder _mouseDecoder;
    private readonly Kitty.Keyboard.KittyKeyDecoder _kittyKeyDecoder;
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
    public InputDecoder(
        IInputSink sink,
        InputOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
        _protocolSink = sink as IProtocolSink;
        _options = options ?? InputOptions.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _parser = new ProtocolParser(_options.ParserLimits);
        _pasteAccumulator = new PasteAccumulator(_options.MaxPasteBytes);
        _keyMatcher = _options.KeyMap.FallbackBindings.Count == 0
            ? null
            : new KeySequenceMatcher(_options.KeyMap.FallbackBindings);
        _keyReplay = _keyMatcher is null ? null : new byte[_keyMatcher.MaximumLength];
        _cellMetricsResolver = new CellMetricsResolver(_options.CellMetrics);
        _mouseDecoder = new MouseDecoder(
            _sink,
            _cellMetricsResolver,
            _options.PixelMouse,
            _options.MouseCoordinates == MouseCoordinates.Utf8,
            Report);
        _kittyKeyDecoder = new Kitty.Keyboard.KittyKeyDecoder(_sink, Report);
        _utf8 = new Utf8TextAccumulator(rune => EmitText(rune));
        _csiHandlers =
        [
            TryHandleXtermMetricsCsi,
            TryHandleXtermCsi,
            TryHandleKittyCsi,
            TryHandleFocusCsi,
            TryHandlePasteBeginCsi,
            TryHandleMouseCsi,
            TryHandleKeyMapCsi,
            TryHandleLegacyCsiKey,
            TryHandleAnsiGrammarCsi
        ];
        _sequenceHandlers =
        [
            TryHandleOscSequence,
            TryHandleOsc52Sequence,
            TryHandleItermCapabilitiesSequence,
            TryHandleKittyClipboardSequence,
            TryHandleKittyGraphicsSequence,
            TryHandleProtocolSequence
        ];
        _dcsHandlers =
        [
            TryHandleDecrqssDcs,
            TryHandleGetCapDcs,
            TryHandleProtocolDcs
        ];
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

            if (_pasteAccumulator.Active)
            {
                DecodeCoreByte(value, ref adapter);
                position++;
                continue;
            }

            if (_keyMatcher is not null &&
                (_keyMatcher.Pending || CanStartMatcher(value)))
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

            // Every kind of pending state below must be quiescent before a run of ordinary
            // ground-state text bytes can be hazarded to the parser in one call instead of one
            // byte at a time: a configured fallback matcher, a dangling UTF-8 continuation, a
            // lone-Escape or SS3 disambiguation, and a pending X10 mouse continuation byte all
            // give individual bytes special meaning that a multi-byte batch would bypass. The
            // per-byte IsGroundTextCandidate check inside the scan then stops the run the instant
            // an unsafe byte would be next, so nothing unsafe ever reaches the batched call.
            if (_keyMatcher is null &&
                !_utf8.HasPending &&
                !_escapePending &&
                !_ss3Pending &&
                !_mouseDecoder.Pending &&
                _parser.IsGround &&
                IsGroundTextCandidate(value))
            {
                var start = position++;

                while (position < input.Length && IsGroundTextCandidate(input[position]))
                {
                    position++;
                }

                ParseCore(input[start..position], ref adapter);
                continue;
            }

            DecodeCoreByte(value, ref adapter);
            position++;
        }
    }

    /// <summary>Gets the pending lone-Escape ambiguity deadline, or null when no Escape is
    /// pending. The read loop mirrors this into a wake-up so <see cref="ExpireEscape"/> runs
    /// even when no further byte ever arrives.</summary>
    public DateTimeOffset? PendingEscapeDeadline => _escapePending ? _escapeDeadline : null;

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

        while (_keyMatcher is { Pending: true })
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

        if (_mouseDecoder.Pending)
        {
            _mouseDecoder.EndIfPending();
        }

        if (_pasteAccumulator.Active)
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

    /// <summary>Gets the total number of <see cref="ProtocolParser.Parse{TSink}"/> calls issued
    /// since construction, for ground-state run-batching test seams.</summary>
    internal int ParseCallCount { get; private set; }

    /// <summary>Gets the total number of UTF-8 accumulator text-processing steps performed since
    /// construction, for ground-state run-batching test seams.</summary>
    internal int TextAccumulationCallCount { get; private set; }

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
        if (_pasteAccumulator.Active)
        {
            _skippedBytes = checked(_skippedBytes + 1);
            ProcessPaste(value);
            return;
        }

        if (!_utf8.HasPending &&
            !_escapePending &&
            !_ss3Pending &&
            !_mouseDecoder.Pending &&
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

            // Legacy terminals encode Alt plus non-ASCII text as ESC then UTF-8. Arming
            // _nextTextModifiers is only safe when this byte will actually reach EmitText: a
            // pending SS3 or X10 mouse continuation consumes the byte itself before the UTF-8
            // accumulator ever sees it, which would otherwise leave the flag armed indefinitely
            // and attach Alt to an unrelated later keystroke. The range is also narrowed
            // to valid UTF-8 lead bytes (0xC2..0xF4): a continuation byte (0x80..0xBF) can never
            // begin a scalar, so treating one as the start of Alt+text would swallow the Escape
            // and still never produce text.
            if (value is >= 0xc2 and <= 0xf4 && !_ss3Pending && !_mouseDecoder.Pending)
            {
                _skippedBytes = checked(_skippedBytes + 1);
                _nextTextModifiers = Modifiers.Alt;
            }
            else
            {
                ParseCore("\u001b"u8, ref adapter);
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

            // 0x7f is a legal X10 mouse field byte (coordinate 95, via value + 32) and the only
            // out-of-band byte this fast path injects that a pending report can legally contain.
            // Feed it to the mouse decoder instead of destroying the in-progress report and
            // falling through to a phantom Backspace plus a leaked literal character.
            if (_mouseDecoder.Pending)
            {
                Span<byte> pending = [value];
                _ = _mouseDecoder.ConsumeX10(pending);
                return;
            }

            _utf8.Flush();
            HandleControl(value);
            return;
        }

        Span<byte> one = [value];
        ParseCore(one, ref adapter);
    }

    private bool CanStartMatcher(byte value) =>
        !_utf8.HasPending &&
        !_escapePending &&
        !_ss3Pending &&
        !_mouseDecoder.Pending &&
        _parser.IsGround &&
        value != ControlBytes.Escape;

    /// <summary>
    /// Gets whether one byte can safely join a batched ground-state text run. This mirrors
    /// <see cref="ProtocolParser"/>'s own text-run membership test plus the two eight-bit
    /// spellings <see cref="DecodeCoreByte"/> intercepts ahead of the parser: 0x8f and 0x9b are
    /// otherwise ordinary high bytes, but when the active key map structurally requires their
    /// eight-bit SS3 or CSI spelling, they must reach that interception one byte at a time rather
    /// than being folded into a batched <see cref="ProtocolParser.Parse{TSink}"/> call.
    /// </summary>
    /// <param name="value">The candidate byte.</param>
    private bool IsGroundTextCandidate(byte value) =>
        value is > 0x1f and not 0x7f &&
        (!_options.ParserLimits.AcceptEightBitControls || value is < 0x80 or > 0x9f) &&
        !(value == 0x8f && _options.KeyMap.RequiresEightBitSs3) &&
        !(value == 0x9b && _options.KeyMap.RequiresEightBitCsi);

    /// <summary>Routes every call into <see cref="ProtocolParser.Parse{TSink}"/> through one
    /// counted call site, so both the batched ground-state text run and every other existing
    /// single-byte call share one seam for verifying that batching actually reduces call
    /// count.</summary>
    /// <param name="value">The borrowed bytes to parse.</param>
    /// <param name="adapter">The synchronous callback adapter.</param>
    private void ParseCore(ReadOnlySpan<byte> value, ref Adapter adapter)
    {
        ParseCallCount++;
        _parser.Parse(value, ref adapter);
    }

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

            if (!matcher.Pending && !CanStartMatcher(value))
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

    /// <summary>Attempts one candidate handler for a parsed CSI sequence.</summary>
    /// <param name="parameters">The borrowed CSI parameter bytes.</param>
    /// <param name="intermediates">The borrowed CSI intermediate bytes.</param>
    /// <param name="final">The CSI final byte.</param>
    /// <returns>True when this handler claimed and fully processed the sequence.</returns>
    private delegate bool CsiHandler(ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> intermediates, byte final);

    /// <summary>
    /// Handlers tried in order for every CSI sequence. Precedence is data, not statement order:
    /// a CSI ending in <c>u</c> is inspected by both <see cref="TryHandleXtermCsi"/> (the Kitty
    /// enhancement-flags query reply, <c>CSI ? &lt;flags&gt; u</c>) and
    /// <see cref="TryHandleKittyCsi"/> (a Kitty keyboard event report, <c>CSI &lt;code&gt;u</c>
    /// with no private marker) — the former runs first and only claims its own marker/shape, so
    /// the latter still sees every report the former does not recognize.
    /// </summary>
    private readonly CsiHandler[] _csiHandlers;

    private void HandleCsi(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final)
    {
        _utf8.Flush();
        _mouseDecoder.EndIfPending();
        EndSs3IfPending();

        foreach (var handler in _csiHandlers)
        {
            if (handler(parameters, intermediates, final))
            {
                return;
            }
        }
    }

    private bool TryHandleXtermMetricsCsi(ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> intermediates, byte final)
    {
        if (!XtermResponses.TryMetricsCsi(parameters, intermediates, final, out var metrics))
        {
            return false;
        }

        if (_protocolSink is { } protocolSink)
        {
            protocolSink.Dispatch(in metrics);
            _cellMetricsResolver.Apply(in metrics);
        }
        else
        {
            Report(DiagnosticCode.Unsupported, SequenceKind.Csi);
        }

        return true;
    }

    private bool TryHandleXtermCsi(ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> intermediates, byte final)
    {
        if (!XtermResponses.TryCsi(parameters, intermediates, final, out var response))
        {
            return false;
        }

        if (_protocolSink is { } protocolSink)
        {
            protocolSink.Response(in response);
        }
        else
        {
            Report(DiagnosticCode.Unsupported, SequenceKind.Csi);
        }

        return true;
    }

    private bool TryHandleKittyCsi(ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> intermediates, byte final)
    {
        if (!intermediates.IsEmpty || final != (byte) 'u')
        {
            return false;
        }

        _kittyKeyDecoder.Handle(parameters);
        return true;
    }

    private bool TryHandleFocusCsi(ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> intermediates, byte final)
    {
        if (!intermediates.IsEmpty || !parameters.IsEmpty || final is not ((byte) 'I' or (byte) 'O'))
        {
            return false;
        }

        var focus = new TerminalFocus(final == (byte) 'I');
        _sink.Input(in focus);
        return true;
    }

    private bool TryHandlePasteBeginCsi(ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> intermediates, byte final)
    {
        if (!intermediates.IsEmpty ||
            final != (byte) '~' ||
            !TryReadSingle(parameters, out var native) ||
            native != 200)
        {
            return false;
        }

        BeginPaste();
        return true;
    }

    private bool TryHandleMouseCsi(ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> intermediates, byte final) =>
        intermediates.IsEmpty &&
        final is (byte) 'M' or (byte) 'm' &&
        _mouseDecoder.TryHandleMouse(parameters, final == (byte) 'm');

    private bool TryHandleKeyMapCsi(ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> intermediates, byte final)
    {
        if (!_options.KeyMap.TryGet(KeySignatureKind.Csi, parameters, intermediates, final, out var binding))
        {
            return false;
        }

        EmitBinding(in binding);
        return true;
    }

    private bool TryHandleLegacyCsiKey(ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> intermediates, byte final) =>
        intermediates.IsEmpty && TryHandleCsiKey(parameters, final);

    /// <summary>The terminal handler: always claims the sequence, either via the ANSI grammar
    /// fallback or by reporting it unsupported/malformed.</summary>
    private bool TryHandleAnsiGrammarCsi(ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> intermediates, byte final)
    {
        if (!_options.UseAnsiKeyGrammar || !intermediates.IsEmpty)
        {
            Report(DiagnosticCode.Unsupported, SequenceKind.Csi);
            return true;
        }

        if (final != (byte) '~')
        {
            Report(DiagnosticCode.Unsupported, SequenceKind.Csi);
            return true;
        }

        Span<int> values = stackalloc int[3];

        if (!TryReadParameters(parameters, values, out var count))
        {
            Report(DiagnosticCode.Malformed, SequenceKind.Csi);
            return true;
        }

        if (count == 3 && values[0] == 27 && TryHandleModifiedOtherKey(values[1], values[2]))
        {
            return true;
        }

        HandleTilde(values, count);
        return true;
    }

    /// <summary>Maps a CSI or SS3 cursor/function-key final byte shared by both grammars.</summary>
    /// <param name="final">The final byte.</param>
    /// <param name="code">The mapped code, or <see cref="Code.Unknown"/> when unmapped.</param>
    /// <returns>True when <paramref name="final"/> is one of the ten shared final bytes.</returns>
    private static bool TryMapCursorKey(byte final, out Code code)
    {
        switch (final)
        {
            case (byte) 'A':
                code = Code.Up;
                return true;
            case (byte) 'B':
                code = Code.Down;
                return true;
            case (byte) 'C':
                code = Code.Right;
                return true;
            case (byte) 'D':
                code = Code.Left;
                return true;
            case (byte) 'H':
                code = Code.Home;
                return true;
            case (byte) 'F':
                code = Code.End;
                return true;
            case (byte) 'P':
                code = Code.F1;
                return true;
            case (byte) 'Q':
                code = Code.F2;
                return true;
            case (byte) 'R':
                code = Code.F3;
                return true;
            case (byte) 'S':
                code = Code.F4;
                return true;
            default:
                code = Code.Unknown;
                return false;
        }
    }

    private bool TryHandleCsiKey(ReadOnlySpan<byte> parameters, byte final)
    {
        // CSI Z (cursor back-tab / Shift+Tab) has no SS3 equivalent, so it stays CSI-only. An
        // unmapped final byte returns false rather than Code.Unknown: CSI sits in an extensible
        // dispatch chain (terminfo KeyMap, then the ANSI grammar fallback) that must still get a
        // chance to claim it, unlike SS3 which has no further fallback.
        Code? code = final == (byte) 'Z'
            ? Code.Tab
            : TryMapCursorKey(final, out var mapped) ? mapped : null;

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

        // Unlike CSI, SS3 has no further fallback handler once this table is exhausted, so an
        // unmapped final byte still becomes a real stroke — Code.Unknown carrying the native byte
        // for diagnostics — instead of silently dropping the input.
        var mapped = TryMapCursorKey(final, out var code);
        EmitStroke(code, null, mapped ? 0 : final);
    }

    /// <summary>Attempts one candidate handler for a parsed OSC/APC/PM string sequence.</summary>
    /// <param name="kind">The sequence family.</param>
    /// <param name="value">The borrowed sequence payload.</param>
    /// <param name="terminator">The observed string terminator.</param>
    /// <returns>True when this handler claimed and fully processed the sequence.</returns>
    private delegate bool SequenceHandler(SequenceKind kind, ReadOnlySpan<byte> value, StringTerminator terminator);

    /// <summary>Handlers tried in order for every OSC/APC/PM sequence.</summary>
    private readonly SequenceHandler[] _sequenceHandlers;

    private void HandleSequence(
        SequenceKind kind,
        ReadOnlySpan<byte> value,
        StringTerminator terminator)
    {
        _utf8.Flush();
        _mouseDecoder.EndIfPending();
        EndSs3IfPending();

        foreach (var handler in _sequenceHandlers)
        {
            if (handler(kind, value, terminator))
            {
                return;
            }
        }
    }

    private bool TryHandleOscSequence(SequenceKind kind, ReadOnlySpan<byte> value, StringTerminator terminator)
    {
        if (kind != SequenceKind.Osc || !XtermResponses.TryOsc(value, out var response))
        {
            return false;
        }

        if (_protocolSink is { } protocolSink)
        {
            protocolSink.Dispatch(in response);
        }
        else
        {
            Report(DiagnosticCode.Unsupported, kind);
        }

        return true;
    }

    private bool TryHandleOsc52Sequence(SequenceKind kind, ReadOnlySpan<byte> value, StringTerminator terminator)
    {
        if (kind != SequenceKind.Osc || !value.StartsWith("52;"u8))
        {
            return false;
        }

        if (_protocolSink is { } clipboardSink)
        {
            clipboardSink.Dispatch(Clipboard.Osc52.Decode(value, _options.TransferLimits.MaxClipboardBytes));
        }
        else
        {
            Report(DiagnosticCode.Unsupported, kind);
        }

        return true;
    }

    private bool TryHandleItermCapabilitiesSequence(
        SequenceKind kind,
        ReadOnlySpan<byte> value,
        StringTerminator terminator)
    {
        if (kind != SequenceKind.Osc || !XtermResponses.TryOscItermCapabilities(value, out var response))
        {
            return false;
        }

        if (_protocolSink is { } capabilitiesSink)
        {
            capabilitiesSink.Dispatch(response);
        }
        else
        {
            Report(DiagnosticCode.Unsupported, kind);
        }

        return true;
    }

    private bool TryHandleKittyClipboardSequence(
        SequenceKind kind,
        ReadOnlySpan<byte> value,
        StringTerminator terminator)
    {
        if (kind != SequenceKind.Osc || !value.StartsWith("5522;"u8))
        {
            return false;
        }

        if (_protocolSink is { } kittyClipboardSink)
        {
            kittyClipboardSink.Dispatch(Kitty.Clipboard.KittyClipboardPacket.Parse(value, _options.TransferLimits));
        }
        else
        {
            Report(DiagnosticCode.Unsupported, kind);
        }

        return true;
    }

    private bool TryHandleKittyGraphicsSequence(
        SequenceKind kind,
        ReadOnlySpan<byte> value,
        StringTerminator terminator)
    {
        if (kind != SequenceKind.Apc || value.IsEmpty || value[0] != (byte) 'G')
        {
            return false;
        }

        if (_protocolSink is { } graphicsSink)
        {
            graphicsSink.Dispatch(Kitty.Graphics.KittyGraphicsResponse.Parse(value, _options.KittyMetadataLimits));
        }
        else
        {
            Report(DiagnosticCode.Unsupported, kind);
        }

        return true;
    }

    /// <summary>The terminal handler: always claims the sequence, either forwarding it to the
    /// protocol sink or reporting it unsupported.</summary>
    private bool TryHandleProtocolSequence(SequenceKind kind, ReadOnlySpan<byte> value, StringTerminator terminator)
    {
        if (_protocolSink is null)
        {
            Report(DiagnosticCode.Unsupported, kind);
            return true;
        }

        _protocolSink.Sequence(new ProtocolSequence(kind, [], [], 0, value, terminator));
        return true;
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
        var text = new TerminalText(rune);
        _sink.Input(in text);
    }

    private void EmitStroke(
        Code code,
        Rune? character = null,
        int nativeCode = 0,
        Modifiers modifiers = Modifiers.None,
        KeyAction action = KeyAction.Press)
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
        out KeyAction action)
    {
        modifiers = Modifiers.None;
        action = KeyAction.Press;

        if (parameters.IsEmpty)
        {
            return true;
        }

        var semicolon = parameters.IndexOf((byte) ';');
        var keycode = semicolon < 0 ? parameters : parameters[..semicolon];

        // This leading field is a plain CSI parameter, not a Kitty colon sub-parameter - TryDecimal
        // alone would silently accept "1:2" as the value "1" with the trailing ":2" simply unread,
        // turning a malformed or private-use sequence into a plausible synthetic key stroke instead
        // of the diagnostic it should report.
        var hasSubParameter = keycode.IndexOf((byte) ':') >= 0;

        return !hasSubParameter && (semicolon < 0
            ? Kitty.Keyboard.KittyKeyDecoder.TryDecimal(keycode, allowEmpty: true, out _)
            : Kitty.Keyboard.KittyKeyDecoder.TryDecimal(keycode, allowEmpty: true, out _)
              && Kitty.Keyboard.KittyKeyDecoder.TryReadModifiers(parameters[(semicolon + 1)..], out modifiers, out action));
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
        if (_mouseDecoder.Pending)
        {
            value = value[_mouseDecoder.ConsumeX10(value)..];
        }

        if (_ss3Pending && !value.IsEmpty)
        {
            HandleSs3(value[0]);
            value = value[1..];
        }

        TextAccumulationCallCount++;
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

    /// <summary>Attempts one candidate handler for a parsed DCS sequence.</summary>
    /// <param name="parameters">The borrowed DCS parameter bytes.</param>
    /// <param name="intermediates">The borrowed DCS intermediate bytes.</param>
    /// <param name="final">The DCS final byte.</param>
    /// <param name="value">The borrowed DCS payload.</param>
    /// <param name="terminator">The observed string terminator.</param>
    /// <returns>True when this handler claimed and fully processed the sequence.</returns>
    private delegate bool DcsHandler(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        ReadOnlySpan<byte> value,
        StringTerminator terminator);

    /// <summary>Handlers tried in order for every DCS sequence.</summary>
    private readonly DcsHandler[] _dcsHandlers;

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

        foreach (var handler in _dcsHandlers)
        {
            if (handler(parameters, intermediates, final, value, terminator))
            {
                return;
            }
        }
    }

    private bool TryHandleDecrqssDcs(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        ReadOnlySpan<byte> value,
        StringTerminator terminator)
    {
        if (!XtermDecrqss.TryParse(parameters, intermediates, final, value, out var status))
        {
            return false;
        }

        if (_protocolSink is { } statusSink)
        {
            statusSink.Dispatch(in status);

            if (status.Name == StatusName.Unknown && status.Valid)
            {
                Report(DiagnosticCode.Unsupported, SequenceKind.Dcs);
            }
        }
        else
        {
            Report(DiagnosticCode.Unsupported, SequenceKind.Dcs);
        }

        return true;
    }

    private bool TryHandleGetCapDcs(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        ReadOnlySpan<byte> value,
        StringTerminator terminator)
    {
        if (!XtermGetCap.TryParse(parameters, intermediates, final, value, _options.QueryLimits, out var capability))
        {
            return false;
        }

        if (_protocolSink is { } capabilitySink)
        {
            Debug.Assert(capability is not null, "A successful XTGETTCAP parse owns a response.");
            capabilitySink.Dispatch(capability);
        }
        else
        {
            Report(DiagnosticCode.Unsupported, SequenceKind.Dcs);
        }

        return true;
    }

    /// <summary>The terminal handler: always claims the sequence, either forwarding it to the
    /// protocol sink or reporting it unsupported.</summary>
    private bool TryHandleProtocolDcs(
        ReadOnlySpan<byte> parameters,
        ReadOnlySpan<byte> intermediates,
        byte final,
        ReadOnlySpan<byte> value,
        StringTerminator terminator)
    {
        if (_protocolSink is null)
        {
            Report(DiagnosticCode.Unsupported, SequenceKind.Dcs);
            return true;
        }

        _protocolSink.Sequence(new ProtocolSequence(SequenceKind.Dcs, parameters, intermediates, final, value, terminator));
        return true;
    }

    /// <summary>Accepts one parser diagnostic.</summary>
    /// <param name="value">The structured non-sensitive diagnostic.</param>
    internal void AcceptDiagnostic(in Diagnostic value) => HandleParserDiagnostic(in value);
}
