// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

using Input;

using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;

/// <summary>Routes one terminal byte stream into typed input and protocol events.</summary>
[PublicAPI]
[MustDisposeResource]
public sealed class ProtocolRouter: IDisposable
{
    private readonly IProtocolSink _sink;
    private readonly InputDecoder _decoder;
    private readonly MultiplexerRoute? _multiplexerRoute;
    private byte[]? _multiplexerCandidate;
    private int _multiplexerLength;
    private long _multiplexerDiscardEscapes;
    private int _multiplexerDiscardTerminators;
    private long _multiplexerCandidateStart;
    private long _multiplexerDiscardStart;
    private long _multiplexerDiscardedBytes;
    private long _rawOffset;
    private bool _multiplexerDiscarding;

    #region Construction

    /// <summary>Initializes a router with bounded decoder policy.</summary>
    /// <param name="sink">The non-null synchronous protocol sink.</param>
    /// <param name="options">Finite input policy, or null for defaults.</param>
    /// <param name="timeProvider">The Escape deadline clock, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is null.</exception>
    public ProtocolRouter(
        IProtocolSink sink,
        InputOptions? options = null,
        TimeProvider? timeProvider = null) : this(
        sink,
        options,
        timeProvider,
        route: null)
    {
    }

    /// <summary>Initializes a router with one explicit bounded multiplexer protocol route.</summary>
    /// <param name="sink">The non-null synchronous protocol sink.</param>
    /// <param name="route">The non-null explicit active multiplexer route.</param>
    /// <param name="options">Finite input policy, or null for defaults.</param>
    /// <param name="timeProvider">The Escape deadline clock, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> or <paramref name="route"/> is null.</exception>
    internal ProtocolRouter(
        IProtocolSink sink,
        MultiplexerRoute route,
        InputOptions? options = null,
        TimeProvider? timeProvider = null) : this(
        sink,
        options,
        timeProvider,
        route ?? throw new ArgumentNullException(nameof(route)))
    {
    }

    private ProtocolRouter(
        IProtocolSink sink,
        InputOptions? options,
        TimeProvider? timeProvider,
        MultiplexerRoute? route)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
        _decoder = new InputDecoder(sink, options, timeProvider);

        if (route?.Policy.Active == true)
        {
            _multiplexerRoute = route;
        }
    }

    #endregion

    #region Routing and lifetime

    /// <summary>Routes one borrowed transport fragment synchronously.</summary>
    /// <param name="input">The borrowed transport bytes.</param>
    public void Route(ReadOnlySpan<byte> input)
    {
        if (_multiplexerRoute is null)
        {
            InvokeDecoder(input);
            return;
        }

        RouteMultiplexerInput(input);
    }

    /// <summary>Gets the number of times the underlying decoder has been invoked, for tests
    /// that verify ordinary pass-through input is batched into as few decode calls as
    /// possible instead of dispatched one byte at a time.</summary>
    internal int DecoderInvocationCount { get; private set; }

    /// <summary>Enables the cursor and functional-key grammar guaranteed by a successfully acquired
    /// Kitty disambiguation lease.</summary>
    internal void EnableKittyKeyboardDisambiguation() =>
        _decoder.EnableKittyKeyboardDisambiguation();

    /// <summary>Marks a DSR cursor-position query (<c>CSI 6n</c>) as genuinely outstanding, so the
    /// byte-identical modified F3 keystroke shape is trusted as that reply until the query
    /// completes.</summary>
    internal void EnableCursorPositionQuery() =>
        _decoder.EnableCursorPositionQuery();

    /// <summary>Marks the outstanding DSR cursor-position query as no longer pending, restoring
    /// modified F3 key delivery.</summary>
    internal void DisableCursorPositionQuery() =>
        _decoder.DisableCursorPositionQuery();

    private void InvokeDecoder(ReadOnlySpan<byte> input)
    {
        DecoderInvocationCount++;
        _decoder.Decode(input);
    }

    /// <summary>Gets the pending lone-Escape ambiguity deadline, or null when none is pending.</summary>
    public DateTimeOffset? PendingEscapeDeadline => _decoder.PendingEscapeDeadline;

    /// <summary>Gets the pending fallback key-sequence ambiguity deadline, or null when none is
    /// pending.</summary>
    public DateTimeOffset? PendingKeyMatcherDeadline => _decoder.PendingKeyMatcherDeadline;

    /// <summary>Expires a pending lone Escape when its deadline elapsed.</summary>
    /// <returns>Whether an Escape key was emitted.</returns>
    public bool ExpireEscape() => _decoder.ExpireEscape();

    /// <summary>Expires a pending fallback key-sequence match when its deadline elapsed.</summary>
    /// <returns>Whether a fallback key sequence was resolved.</returns>
    public bool ExpireKeyMatcher() => _decoder.ExpireKeyMatcher();

    /// <summary>Completes pending input and protocol framing once.</summary>
    public void Complete()
    {
        if (_multiplexerDiscarding)
        {
            FinishMultiplexerDiscard();
        }
        else if (HasCompleteMultiplexerPrefix())
        {
            RejectMultiplexerCandidate();
        }
        else
        {
            FlushMultiplexerCandidate();
        }

        _decoder.Complete();
    }

    /// <summary>Releases parser and input-decoder storage.</summary>
    public void Dispose()
    {
        if (_multiplexerCandidate is not null)
        {
            Array.Clear(_multiplexerCandidate);
        }

        _multiplexerLength = 0;
        _multiplexerDiscarding = false;
        _multiplexerDiscardEscapes = 0;
        _multiplexerDiscardTerminators = 0;
        _multiplexerCandidateStart = 0;
        _multiplexerDiscardStart = 0;
        _multiplexerDiscardedBytes = 0;
        _rawOffset = 0;

        _decoder.Dispose();
    }

    /// <summary>Updates ordered pixel-to-cell inference from local geometry.</summary>
    /// <param name="cells">The non-negative text-area cell dimensions.</param>
    /// <param name="pixels">Optional non-negative text-area pixel dimensions.</param>
    internal void SetGeometry(Size cells, Size? pixels) =>
        _decoder.SetGeometry(cells, pixels);

    private void RouteMultiplexerInput(ReadOnlySpan<byte> input)
    {
        Debug.Assert(_multiplexerRoute is not null, "Multiplexer input requires an active route.");
        var prefix = _multiplexerRoute.ReplyPrefix;
        var index = 0;

        while (index < input.Length)
        {
            // Outside a candidate and outside discard recovery, a byte can only route two
            // ways: straight through to the decoder, or as the first byte of a possible
            // wrapped reply. Bytes destined for the decoder never need to be inspected one at
            // a time to make that call, so scan the run up front and hand it to the decoder as
            // a single span instead of one single-byte Decode() call per byte.
            if (_multiplexerLength == 0 && !_multiplexerDiscarding)
            {
                var runStart = index;

                while (index < input.Length && input[index] != prefix[0])
                {
                    index++;
                }

                if (index > runStart)
                {
                    var run = input[runStart..index];
                    _rawOffset = checked(_rawOffset + run.Length);
                    InvokeDecoder(run);
                    continue;
                }
            }

            var value = input[index];
            var currentRawOffset = _rawOffset;
            _rawOffset = checked(_rawOffset + 1);
            index++;

            if (_multiplexerDiscarding)
            {
                DiscardMultiplexerByte(value);
                continue;
            }

            if (_multiplexerCandidate is not null && _multiplexerLength == _multiplexerCandidate.Length)
            {
                if (HasCompleteMultiplexerPrefix())
                {
                    BeginMultiplexerDiscard();
                    DiscardMultiplexerByte(value);
                    continue;
                }

                FlushMultiplexerCandidate();

                if (value != prefix[0])
                {
                    DecodeByte(value);
                    continue;
                }
            }

            if (_multiplexerLength == 0)
            {
                // The first candidate byte of a possible wrapped reply — this is the earliest
                // point storage could possibly be needed, so the bounded buffer is rented here
                // instead of unconditionally at construction. A session that never
                // receives anything starting with the reply prefix never allocates it.
                _multiplexerCandidate ??= new byte[_multiplexerRoute.Policy.MaxEnvelopeBytes];
                _multiplexerCandidateStart = currentRawOffset;
            }

            _multiplexerCandidate![_multiplexerLength++] = value;

            if (_multiplexerLength <= prefix.Length)
            {
                if (!prefix.StartsWith(_multiplexerCandidate.AsSpan(0, _multiplexerLength)))
                {
                    FlushMultiplexerCandidate();
                }

                continue;
            }

            var candidate = _multiplexerCandidate.AsSpan(0, _multiplexerLength);

            if (!_multiplexerRoute.MayEnd(candidate))
            {
                continue;
            }

            if (_multiplexerRoute.TryUnwrapReply(candidate, out var reply))
            {
                var envelopeLength = _multiplexerLength;
                InvokeDecoder(reply.Span);
                _decoder.AdvanceTransportOffset(envelopeLength - reply.Length);
                candidate.Clear();
                _multiplexerLength = 0;
                _multiplexerCandidateStart = 0;
            }
            else if (_multiplexerRoute.IsCompleteRecoveryEnvelope(candidate))
            {
                RejectMultiplexerCandidate();
            }
        }
    }

    private void DecodeByte(byte value)
    {
        Span<byte> single = [value];
        InvokeDecoder(single);
    }

    private void FlushMultiplexerCandidate()
    {
        if (_multiplexerLength == 0)
        {
            return;
        }

        Debug.Assert(_multiplexerCandidate is not null, "A retained candidate owns storage.");
        var candidate = _multiplexerCandidate.AsSpan(0, _multiplexerLength);
        InvokeDecoder(candidate);
        candidate.Clear();
        _multiplexerLength = 0;
        _multiplexerCandidateStart = 0;
    }

    private bool HasCompleteMultiplexerPrefix()
    {
        if (_multiplexerRoute is null || _multiplexerCandidate is null)
        {
            return false;
        }

        var prefix = _multiplexerRoute.ReplyPrefix;
        return _multiplexerLength >= prefix.Length &&
               _multiplexerCandidate.AsSpan(0, prefix.Length).SequenceEqual(prefix);
    }

    private void RejectMultiplexerCandidate()
    {
        Debug.Assert(_multiplexerCandidate is not null, "A rejected route candidate owns bounded storage.");
        var diagnostic = new Diagnostic(
            DiagnosticCode.Unsupported,
            SequenceKind.Dcs,
            _multiplexerCandidateStart,
            discardedBytes: _multiplexerLength);
        _sink.Input(in diagnostic);
        _decoder.AdvanceTransportOffset(_multiplexerLength);
        _multiplexerCandidate.AsSpan(0, _multiplexerLength).Clear();
        _multiplexerLength = 0;
        _multiplexerCandidateStart = 0;
    }

    private void BeginMultiplexerDiscard()
    {
        Debug.Assert(_multiplexerRoute is not null, "Overflow recovery requires an active route.");
        Debug.Assert(_multiplexerCandidate is not null, "Overflow recovery requires bounded storage.");
        var candidate = _multiplexerCandidate.AsSpan(0, _multiplexerLength);
        var requiredTerminators = _multiplexerRoute.Policy.Kind == MultiplexerKind.Screen &&
                                  candidate.Length > 3 &&
                                  candidate[0] == ControlBytes.Escape &&
                                  candidate[1] == (byte) 'P' &&
                                  candidate[2] == ControlBytes.Escape &&
                                  candidate[3] is (byte) 'P' or (byte) ']'
            ? 2
            : 1;
        var observedTerminators = CountScreenTerminators(candidate);
        _multiplexerDiscardTerminators = Math.Max(1, requiredTerminators - observedTerminators);
        _multiplexerDiscardEscapes = 0;
        _multiplexerDiscarding = true;
        _multiplexerDiscardStart = _multiplexerCandidateStart;
        _multiplexerDiscardedBytes = _multiplexerLength;
        _multiplexerCandidate.AsSpan(0, _multiplexerLength).Clear();
        _multiplexerLength = 0;
        _multiplexerCandidateStart = 0;
    }

    private void DiscardMultiplexerByte(byte value)
    {
        Debug.Assert(_multiplexerRoute is not null, "Discard recovery requires an active route.");
        _multiplexerDiscardedBytes = checked(_multiplexerDiscardedBytes + 1);

        if (value == ControlBytes.Escape)
        {
            _multiplexerDiscardEscapes = checked(_multiplexerDiscardEscapes + 1);
            return;
        }

        if (value == (byte) '\\' && _multiplexerDiscardEscapes > 0)
        {
            var terminates = _multiplexerRoute.Policy.Kind == MultiplexerKind.Screen ||
                             (_multiplexerDiscardEscapes & 1) != 0;

            if (terminates && --_multiplexerDiscardTerminators == 0)
            {
                FinishMultiplexerDiscard();
            }
        }

        _multiplexerDiscardEscapes = 0;
    }

    private void FinishMultiplexerDiscard()
    {
        var diagnostic = new Diagnostic(
            DiagnosticCode.Unsupported,
            SequenceKind.Dcs,
            _multiplexerDiscardStart,
            _multiplexerDiscardedBytes);
        _sink.Input(in diagnostic);
        _decoder.AdvanceTransportOffset(_multiplexerDiscardedBytes);
        _multiplexerDiscarding = false;
        _multiplexerDiscardEscapes = 0;
        _multiplexerDiscardTerminators = 0;
        _multiplexerDiscardStart = 0;
        _multiplexerDiscardedBytes = 0;
    }

    private static int CountScreenTerminators(ReadOnlySpan<byte> candidate)
    {
        var count = 0;

        for (var index = 1; index < candidate.Length; index++)
        {
            if (candidate[index - 1] == ControlBytes.Escape && candidate[index] == (byte) '\\')
            {
                count++;
            }
        }

        return count;
    }

    #endregion
}
