// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

using Input;

using MultiplexerRoute = Multiplexing.Route;

/// <summary>Routes one terminal byte stream into typed input and protocol events.</summary>
[PublicAPI]
public sealed class ProtocolRouter: IDisposable
{
    private readonly IProtocolSink _sink;
    private readonly Decoder _decoder;
    private readonly MultiplexerRoute? _multiplexerRoute;
    private readonly byte[]? _multiplexerCandidate;
    private int _multiplexerLength;
    private int _multiplexerDiscardEscapes;
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
        Options? options = null,
        TimeProvider? timeProvider = null) : this(
        sink,
        options,
        timeProvider,
        route: null)
    {
    }

    /// <summary>Initializes a router with one explicit bounded multiplexer reply route.</summary>
    /// <param name="sink">The non-null synchronous protocol sink.</param>
    /// <param name="route">The non-null explicit active multiplexer route.</param>
    /// <param name="options">Finite input policy, or null for defaults.</param>
    /// <param name="timeProvider">The Escape deadline clock, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> or <paramref name="route"/> is null.</exception>
    internal ProtocolRouter(
        IProtocolSink sink,
        MultiplexerRoute route,
        Options? options = null,
        TimeProvider? timeProvider = null) : this(
        sink,
        options,
        timeProvider,
        route ?? throw new ArgumentNullException(nameof(route)))
    {
    }

    private ProtocolRouter(
        IProtocolSink sink,
        Options? options,
        TimeProvider? timeProvider,
        MultiplexerRoute? route)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
        _decoder = new Decoder(sink, options, timeProvider);

        if (route?.Policy.IsActive == true)
        {
            _multiplexerRoute = route;
            _multiplexerCandidate = new byte[route.Policy.MaxEnvelopeBytes];
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
            _decoder.Decode(input);
            return;
        }

        RouteMultiplexerInput(input);
    }

    /// <summary>Expires a pending lone Escape when its deadline elapsed.</summary>
    /// <returns>Whether an Escape key was emitted.</returns>
    public bool ExpireEscape() => _decoder.ExpireEscape();

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
        Debug.Assert(_multiplexerCandidate is not null, "An active route owns bounded candidate storage.");
        var prefix = _multiplexerRoute.ReplyPrefix;

        foreach (var value in input)
        {
            var currentRawOffset = _rawOffset;
            _rawOffset = checked(_rawOffset + 1);

            if (_multiplexerDiscarding)
            {
                DiscardMultiplexerByte(value);
                continue;
            }

            if (_multiplexerLength == 0 && value != prefix[0])
            {
                DecodeByte(value);
                continue;
            }

            if (_multiplexerLength == _multiplexerCandidate.Length)
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
                _multiplexerCandidateStart = currentRawOffset;
            }

            _multiplexerCandidate[_multiplexerLength++] = value;

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
                _decoder.Decode(reply.Span);
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
        _decoder.Decode(single);
    }

    private void FlushMultiplexerCandidate()
    {
        if (_multiplexerLength == 0)
        {
            return;
        }

        Debug.Assert(_multiplexerCandidate is not null, "A retained candidate owns storage.");
        var candidate = _multiplexerCandidate.AsSpan(0, _multiplexerLength);
        _decoder.Decode(candidate);
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
        var requiredTerminators = _multiplexerRoute.Policy.Kind == Multiplexing.Kind.Screen &&
                                  candidate.Length > 3 &&
                                  candidate[0] == 0x1b &&
                                  candidate[1] == (byte) 'P' &&
                                  candidate[2] == 0x1b &&
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

        if (value == 0x1b)
        {
            _multiplexerDiscardEscapes++;
            return;
        }

        if (value == (byte) '\\' && _multiplexerDiscardEscapes > 0)
        {
            var terminates = _multiplexerRoute.Policy.Kind == Multiplexing.Kind.Screen ||
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
            if (candidate[index - 1] == 0x1b && candidate[index] == (byte) '\\')
            {
                count++;
            }
        }

        return count;
    }

    #endregion
}
