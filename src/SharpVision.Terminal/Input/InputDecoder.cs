// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;

using Protocols;

using Xterm;

using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;

/// <summary>
/// Incrementally decodes UTF-8 and legacy VT keyboard input into stable values.
/// </summary>
/// <remarks>
/// The decoder is single-threaded. Input bytes and parser callback spans are
/// borrowed only for each synchronous call; emitted values retain none of them.
/// </remarks>
[PublicAPI]
[MustDisposeResource]
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
    private DateTimeOffset _keyMatcherDeadline;
    private readonly CellMetricsResolver _cellMetricsResolver;
    private readonly MouseDecoder _mouseDecoder;
    private readonly Kitty.Keyboard.KittyKeyDecoder _kittyKeyDecoder;
    private Modifiers _nextTextModifiers;
    private long _skippedBytes;
    private bool _completed;
    private bool _disposed;
    private bool _escapePending;
    private bool _kittyKeyboardDisambiguationEnabled;
    private bool _cursorPositionQueryPending;
    private bool _ss3Pending;
    private bool _pendingContinuationJustInterrupted;

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

    /// <summary>Enables the cursor and functional-key grammar guaranteed by a successfully acquired
    /// Kitty disambiguation lease.</summary>
    internal void EnableKittyKeyboardDisambiguation() =>
        _kittyKeyboardDisambiguationEnabled = true;

    /// <summary>Marks a DSR cursor-position query (<c>CSI 6n</c>) as genuinely outstanding, so the
    /// byte-identical <c>CSI 1;&lt;mod&gt;R</c> shape is trusted as that reply instead of the
    /// legacy-grammar encoding of a modified F3 keystroke.</summary>
    internal void EnableCursorPositionQuery() =>
        _cursorPositionQueryPending = true;

    /// <summary>Marks the outstanding DSR cursor-position query as no longer pending, so
    /// <c>CSI 1;&lt;mod&gt;R</c> is decoded as a modified F3 keystroke again instead of being
    /// claimed as a cursor-position reply.</summary>
    internal void DisableCursorPositionQuery() =>
        _cursorPositionQueryPending = false;

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
                var status = AddToMatcher(
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

    /// <summary>Gets the pending fallback key-sequence ambiguity deadline, or null when no
    /// fallback key match is pending. The read loop mirrors this into a wake-up so
    /// <see cref="ExpireKeyMatcher"/> runs even when no further byte ever arrives.</summary>
    public DateTimeOffset? PendingKeyMatcherDeadline => _keyMatcher is { Pending: true } ? _keyMatcherDeadline : null;

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
        _mouseDecoder.EndIfPending();
        EndSs3IfPending();
        EmitEscape();
        return true;
    }

    /// <summary>Commits a pending fallback key match to its longest completed binding after its
    /// ambiguity deadline.</summary>
    /// <returns>Whether a fallback key sequence was resolved.</returns>
    /// <exception cref="ObjectDisposedException">The decoder is disposed.</exception>
    public bool ExpireKeyMatcher()
    {
        ThrowIfDisposed();

        if (_keyMatcher is not { Pending: true } || _timeProvider.GetUtcNow() < _keyMatcherDeadline)
        {
            return false;
        }

        var adapter = new Adapter(this);
        CompleteKeyMatcher(ref adapter);
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
        CompleteKeyMatcher(ref completionAdapter);

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
        // A literal Escape arriving while an SS3 (ESC O) or X10 mouse (ESC [ M) continuation is
        // still pending abandons that continuation: unlike every other transition point in this
        // file (HandleControl, HandleEscape, HandleCsi, HandleSequence, AcceptDcs, ExpireEscape,
        // Complete), a bare Escape from Ground was the one place that left the pending flag
        // dangling instead of resetting it here and reporting the abandonment exactly once. The
        // latch set below lets the Alt-detection guard a few bytes down still treat this next byte
        // as if the continuation were live, so the byte-for-byte Stroke/Text outcome for it is
        // unchanged; the latch is cleared once that byte has been fully processed.
        if (_ss3Pending || _mouseDecoder.Pending)
        {
            _mouseDecoder.EndIfPending();
            EndSs3IfPending();
            _pendingContinuationJustInterrupted = true;
        }

        _escapePending = true;
        _escapeDeadline = _timeProvider.GetUtcNow().Add(_options.EscapeTimeout);
    }

    /// <summary>Routes one byte into the active fallback matcher, arming its ambiguity deadline
    /// exactly once at the transition into <see cref="KeySequenceMatchStatus.Pending"/> — a byte
    /// that merely extends an already-pending match never re-arms it, mirroring how
    /// <see cref="BeginEscape"/> stamps <see cref="_escapeDeadline"/> only when a lone Escape
    /// first arrives.</summary>
    private KeySequenceMatchStatus AddToMatcher(
        byte value,
        out KeyBinding binding,
        out int replayOffset,
        out int replayLength)
    {
        Debug.Assert(_keyMatcher is not null, "Only an active matcher can consume a byte.");
        var matcher = _keyMatcher;
        var wasPending = matcher.Pending;
        var status = matcher.Add(value, out binding, out replayOffset, out replayLength);

        if (status == KeySequenceMatchStatus.Pending && !wasPending)
        {
            _keyMatcherDeadline = _timeProvider.GetUtcNow().Add(_options.KeyMatcherTimeout);
        }

        return status;
    }

    /// <summary>Drains the active fallback matcher's retained prefix as its longest completed
    /// binding or exact replay, shared by <see cref="Complete"/> at transport EOF and
    /// <see cref="ExpireKeyMatcher"/> at the ambiguity deadline.</summary>
    private void CompleteKeyMatcher(ref Adapter adapter)
    {
        while (_keyMatcher is { Pending: true })
        {
            var status = _keyMatcher.Complete(
                out var binding,
                out var replayOffset,
                out var replayLength);

            if (status == KeySequenceMatchStatus.Match)
            {
                EmitFallbackBinding(in binding);
                RematchMatcher(replayOffset, replayLength, ref adapter);
            }
            else
            {
                ReplayMatcherToCore(replayOffset, replayLength, ref adapter);
            }
        }
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

        var wasEscapePending = _escapePending;

        if (_escapePending)
        {
            if (value == ControlBytes.Escape)
            {
                _skippedBytes = checked(_skippedBytes + 1);
                EmitEscape();

                // A repeated Escape is not itself a candidate for the Alt-detection guard below (it
                // never reaches that check), so it must leave any latch armed by an earlier
                // interruption untouched rather than consuming it: the byte that actually needs the
                // latch's protection is whichever non-Escape byte finally follows this run of
                // repeated Escapes, however many there are. BeginEscape still re-arms the latch
                // itself if *this* Escape newly abandons a still-live SS3/mouse continuation.
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
            // and still never produce text. _pendingContinuationJustInterrupted covers the same
            // hazard for the one byte immediately following an Escape that itself just abandoned a
            // pending SS3/mouse continuation: by the time this byte is examined, BeginEscape has
            // already reset _ss3Pending/_mouseDecoder.Pending to false (so a third interruption is
            // not mistaken for a second), so those two flags alone can no longer see the danger
            // this latch preserves.
            if (value is >= 0xc2 and <= 0xf4 &&
                !_ss3Pending &&
                !_mouseDecoder.Pending &&
                !_pendingContinuationJustInterrupted)
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

        // The latch only ever needs to survive for the single byte immediately following the
        // Escape that set it, whose full processing (including whatever it just fed into the real
        // parser above) has now finished; clear it so it cannot affect any later, unrelated byte.
        if (wasEscapePending)
        {
            _pendingContinuationJustInterrupted = false;
        }
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

            var status = AddToMatcher(
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
    /// the latter still sees every report the former does not recognize. A CSI ending in
    /// <c>R</c> with no private marker and exactly two positive parameters is likewise
    /// byte-identical between a DSR cursor-position reply and a modified F3 keystroke;
    /// <see cref="TryHandleXtermCsi"/> only claims that shape while a cursor-position query is
    /// genuinely outstanding, so <see cref="TryHandleLegacyCsiKey"/> still sees it otherwise.
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

        // CSI 1;<mod>R is byte-identical between a real DSR cursor-position reply and the
        // legacy-grammar encoding of a modified F3 keystroke (Shift/Ctrl/Alt+F3). No parse-level
        // discriminator can tell them apart, so this only trusts the shape as a reply while a
        // cursor-position query is genuinely outstanding; otherwise it falls through to
        // TryHandleLegacyCsiKey below, which maps the same final byte to Code.F3.
        if (response.Kind == ResponseKind.CursorPosition && !_cursorPositionQueryPending)
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

    private bool TryHandleLegacyCsiKey(ReadOnlySpan<byte> parameters, ReadOnlySpan<byte> intermediates, byte final)
    {
        if (!intermediates.IsEmpty)
        {
            return false;
        }

        // Deliberately excludes UseAnsiKeyGrammar: that flag only controls whether this
        // legacy-shape handler fires at all, not what bit 3 of the modifier byte means. The
        // disambiguation lease alone (with no event sub-parameter present) is enough to call this
        // Kitty grammar: the lease is already the signal the decoder trusts to accept this
        // otherwise-ambiguous shape as a real Kitty keystroke, so reading its modifier byte with
        // legacy ctlseqs.txt semantics afterward would be internally inconsistent - a byte
        // sequence decoding differently before vs after Kitty-keyboard negotiation is intentional
        // here, not accidental.
        var isKittyGrammar = _kittyKeyboardDisambiguationEnabled || HasKittyEventType(parameters);

        return (_options.UseAnsiKeyGrammar || isKittyGrammar) &&
            TryHandleCsiKey(parameters, final, isKittyGrammar);
    }

    // Kitty reuses the legacy CSI cursor-key finals when progressive event reporting is active.
    // Repeat and release carry a distinguishing event sub-parameter, while press may omit its
    // default event type entirely. The Session therefore explicitly enables the ambiguous press
    // grammar only after its disambiguation lease reaches the terminal.
    private static bool HasKittyEventType(ReadOnlySpan<byte> parameters)
    {
        var separator = parameters.IndexOf((byte) ';');
        return separator >= 0 && parameters[(separator + 1)..].IndexOf((byte) ':') >= 0;
    }

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

        // Kitty reports repeat/release on tilde-form functional keys (Delete, Insert,
        // PageUp/Down, F5-F12) as a colon-separated event type appended to the modifier field -
        // e.g. "3;1:2~" for a Delete repeat. That colon is only valid in this second field, so it
        // is decoded here before falling through to the plain-modifier reader below, which would
        // otherwise reject any colon outright.
        if (TryReadTildeEventParameters(parameters, out var eventNative, out var eventModifiers, out var eventAction))
        {
            HandleTilde(eventNative, eventModifiers, eventAction);
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

        // Same Kitty-grammar signal TryHandleLegacyCsiKey uses for the cursor/function-key form:
        // the disambiguation lease alone, with no event sub-parameter present, is enough to treat
        // bit 3 of the modifier field as Super rather than legacy Meta.
        var isKittyGrammar = _kittyKeyboardDisambiguationEnabled || HasKittyEventType(parameters);

        if (count is < 1 or > 2 || values[0] < 0 ||
            !TryGetModifier(count == 2 ? values[1] : 1, out var modifiers, isKittyGrammar))
        {
            Report(DiagnosticCode.Malformed, SequenceKind.Csi);
            return true;
        }

        HandleTilde(values[0], modifiers, KeyAction.Press);
        return true;
    }

    /// <summary>Attempts to read a tilde-form functional key's native code together with a
    /// Kitty-style colon-delimited modifier/event-type field (e.g. "3;1:2" for a Delete repeat).
    /// Returns false whenever the modifier field carries no colon, leaving the plain
    /// legacy-modifier path above to handle it instead.</summary>
    private static bool TryReadTildeEventParameters(
        ReadOnlySpan<byte> parameters,
        out int native,
        out Modifiers modifiers,
        out KeyAction action)
    {
        native = 0;
        modifiers = Modifiers.None;
        action = KeyAction.Press;

        var semicolon = parameters.IndexOf((byte) ';');

        if (semicolon < 0)
        {
            return false;
        }

        var nativeField = parameters[..semicolon];
        var modifierField = parameters[(semicolon + 1)..];

        if (modifierField.IndexOf((byte) ':') < 0 || modifierField.IndexOf((byte) ';') >= 0)
        {
            return false;
        }

        if (!Kitty.Keyboard.KittyKeyDecoder.TryDecimal(nativeField, allowEmpty: false, out native, out var nativeSeparator) ||
            nativeSeparator != ParameterSeparator.None ||
            !Kitty.Keyboard.KittyKeyDecoder.TryReadModifiers(modifierField, out modifiers, out action))
        {
            return false;
        }

        // See the matching remap in TryReadCsiModifiers: Kitty's own bit 3 is Super, but this
        // legacy ANSI grammar (guarded by UseAnsiKeyGrammar above) defines bit 3 as Meta - except
        // reaching here already guarantees HasKittyEventType(parameters), where bit 3 is Super.
        if (!HasKittyEventType(parameters))
        {
            RemapLegacySuperToMeta(ref modifiers);
        }

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

    private bool TryHandleCsiKey(ReadOnlySpan<byte> parameters, byte final, bool isKittyGrammar)
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

        if (!TryReadCsiModifiers(parameters, isKittyGrammar, out var modifiers, out var action))
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

    private void HandleTilde(int native, Modifiers modifiers, KeyAction action)
    {
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
        EmitStroke(code, null, native, modifiers, action);
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
        if (kind != SequenceKind.Osc ||
            !(value.StartsWith("4;"u8) || value.StartsWith("10;rgb:"u8) || value.StartsWith("11;rgb:"u8)))
        {
            return false;
        }

        if (!XtermResponses.TryOsc(value, out var response))
        {
            Report(DiagnosticCode.Malformed, kind);
            return true;
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

    private static bool TryGetModifier(int value, out Modifiers modifiers, bool isKittyGrammar = false)
    {
        value = value < 0 ? 1 : value;
        var flags = value - 1;

        // This legacy ctlseqs.txt modifier code has its own 4-bit layout (Shift=1, Alt=2,
        // Control=4, Meta=8) which only coincidentally aligns with the Modifiers enum's bit
        // layout in bits 0-2; bit 3 means Meta here, not Super, so it cannot be cast directly -
        // except under Kitty grammar, where the wire encoding is reused but bit 3 already means
        // Super (see the matching remap in TryReadCsiModifiers).
        modifiers = (Modifiers) (flags & 0b0111) | ((flags & 0b1000) != 0 ? Modifiers.Super : Modifiers.None);

        if (!isKittyGrammar)
        {
            RemapLegacySuperToMeta(ref modifiers);
        }

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
        bool isKittyGrammar,
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

        if (hasSubParameter)
        {
            return false;
        }

        if (semicolon < 0)
        {
            return Kitty.Keyboard.KittyKeyDecoder.TryDecimal(keycode, allowEmpty: true, out _);
        }

        if (!Kitty.Keyboard.KittyKeyDecoder.TryDecimal(keycode, allowEmpty: true, out _) ||
            !Kitty.Keyboard.KittyKeyDecoder.TryReadModifiers(parameters[(semicolon + 1)..], out modifiers, out action))
        {
            return false;
        }

        // KittyKeyDecoder.TryReadModifiers decodes into Kitty's own CSI-u bit layout, where bit 3
        // is Super. Legacy-grammar CSI modifiers reuse the same wire encoding but define bit 3 as
        // Meta (per ctlseqs.txt), so remap it here rather than in the shared Kitty decoder - but
        // only when this sequence is not itself Kitty grammar (event sub-parameter present, or the
        // disambiguation lease active), since then bit 3 already means Super.
        if (!isKittyGrammar)
        {
            RemapLegacySuperToMeta(ref modifiers);
        }

        return true;
    }

    private static void RemapLegacySuperToMeta(ref Modifiers modifiers)
    {
        if ((modifiers & Modifiers.Super) != 0)
        {
            modifiers = (modifiers & ~Modifiers.Super) | Modifiers.Meta;
        }
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
