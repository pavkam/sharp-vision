// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

using Discovery.Queries;

using Xterm;


/// <summary>Coordinates one bounded terminal capability query batch.</summary>
[PublicAPI]
public sealed class Negotiator
{
    private readonly ActiveQueryDiscoveryStrategy _strategy;

    /// <summary>Initializes one bounded negotiator.</summary>
    /// <param name="options">The non-null owned negotiation policy.</param>
    /// <param name="timeProvider">The deadline clock, or null for system time.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public Negotiator(
        NegotiationOptions options,
        TimeProvider? timeProvider = null) =>
        _strategy = new ActiveQueryDiscoveryStrategy(options, timeProvider);

    /// <summary>Initializes one bounded negotiator over an already-resolved description baseline.</summary>
    /// <param name="options">The non-null owned negotiation policy.</param>
    /// <param name="baseline">The non-null description-derived semantic baseline.</param>
    /// <param name="timeProvider">The deadline clock, or null for system time.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="baseline"/> is null.</exception>
    internal Negotiator(
        NegotiationOptions options,
        TerminalCapabilities baseline,
        TimeProvider? timeProvider) =>
        _strategy = new ActiveQueryDiscoveryStrategy(options, baseline, timeProvider);

    /// <summary>Gets whether the query batch was emitted.</summary>
    public bool Started => _strategy.Started;

    /// <summary>Gets whether the written batch included the CSI 6n cursor-position completion
    /// fence. A low caller-supplied query budget can crowd the fence out of the batch entirely.
    /// </summary>
    internal bool FenceQueried => _strategy.FenceQueried;

    /// <summary>Gets whether one immutable profile was published.</summary>
    public bool Completed => _strategy.Completed;

    /// <summary>Gets whether response correlation or private-mode work remains active.</summary>
    internal bool HasPendingWork => _strategy.HasPendingWork;

    /// <summary>Gets the shared response deadline after startup.</summary>
    public DateTimeOffset Deadline => _strategy.Deadline;

    /// <summary>Gets the latest redacted response-classification diagnostic.</summary>
    public Diagnostic? LastDiagnostic => _strategy.LastDiagnostic;

    /// <summary>Gets the published immutable profile.</summary>
    /// <exception cref="InvalidOperationException">Negotiation is incomplete.</exception>
    public TerminalCapabilities Capabilities => _strategy.Capabilities;

    /// <summary>Gets the owned standard-query evidence after publication.</summary>
    /// <exception cref="InvalidOperationException">Negotiation is incomplete.</exception>
    public QueryResults Results => _strategy.Results;

    /// <summary>Writes the complete bounded startup query batch.</summary>
    /// <param name="destination">The non-null synchronous byte destination.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The negotiator already started.</exception>
    public void Start(IBufferWriter<byte> destination) =>
        _ = _strategy.TryStart(destination, cells: null, pixels: null);

    /// <summary>Writes a bounded startup batch after applying local geometry evidence.</summary>
    /// <param name="destination">The non-null synchronous byte destination.</param>
    /// <param name="localCells">Optional locally observed text-area cells.</param>
    /// <param name="localPixels">Optional locally observed text-area pixels.</param>
    /// <param name="route">Optional explicit typed outer-terminal query route.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The negotiator already started.</exception>
    internal void Start(
        IBufferWriter<byte> destination,
        Size? localCells,
        Size? localPixels,
        MultiplexerRoute? route = null) =>
        _ = _strategy.TryStart(destination, localCells, localPixels, route);

    /// <summary>Writes a bounded startup batch and reports whether it reached the destination atomically.</summary>
    /// <param name="destination">The non-null synchronous byte destination.</param>
    /// <param name="localCells">Optional locally observed text-area cells.</param>
    /// <param name="localPixels">Optional locally observed text-area pixels.</param>
    /// <param name="route">Optional explicit typed outer-terminal query route.</param>
    /// <param name="describedTerminalName">The connection's own resolved description name.</param>
    /// <returns>True when the query batch was written; false when route encoding failed atomically.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The negotiator already started.</exception>
    internal bool TryStart(
        IBufferWriter<byte> destination,
        Size? localCells,
        Size? localPixels,
        MultiplexerRoute? route = null,
        string? describedTerminalName = null) =>
        _strategy.TryStart(destination, localCells, localPixels, route, describedTerminalName);

    /// <summary>Matches one recognized response and publishes when all queries complete.</summary>
    /// <param name="response">The owned typed terminal response.</param>
    /// <returns>The active, duplicate, late, or unknown match classification.</returns>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public QueryMatch Accept(in XtermCapabilitiesResponse response) => _strategy.Accept(in response);

    /// <summary>Matches one strict numeric Kitty graphics APC response.</summary>
    /// <param name="response">The non-null owned response.</param>
    /// <returns>The active, duplicate, late, or unknown match classification.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public QueryMatch Accept(Kitty.Graphics.KittyGraphicsResponse response) => _strategy.Accept(response);

    /// <summary>Matches one validated terminal color response.</summary>
    /// <param name="response">The immutable color response.</param>
    /// <returns>The active, duplicate, late, or unknown match classification.</returns>
    /// <exception cref="ArgumentException"><paramref name="response"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public QueryMatch Accept(in PaletteResponse response) => _strategy.Accept(in response);

    /// <summary>Matches one validated terminal metrics response.</summary>
    /// <param name="response">The immutable metrics response.</param>
    /// <returns>The active, duplicate, late, or unknown match classification.</returns>
    /// <exception cref="ArgumentException"><paramref name="response"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public QueryMatch Accept(in MetricsResponse response) => _strategy.Accept(in response);

    /// <summary>Matches one validated DECRQSS response.</summary>
    /// <param name="response">The non-empty owned status response.</param>
    /// <returns>The match classification.</returns>
    /// <exception cref="ArgumentException"><paramref name="response"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public QueryMatch Accept(in StatusResponse response) => _strategy.Accept(in response);

    /// <summary>Matches one validated XTGETTCAP response.</summary>
    /// <param name="response">The non-null owned response.</param>
    /// <returns>The active, duplicate, late, or unknown match classification.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public QueryMatch Accept(CapabilityResponse response) => _strategy.Accept(response);

    /// <summary>Matches one validated iTerm2 OSC 1337 Capabilities feature-reporting reply.</summary>
    /// <param name="response">The non-null owned response.</param>
    /// <returns>The active, duplicate, late, or unknown match classification.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public QueryMatch Accept(ItermCapabilitiesResponse response) => _strategy.Accept(response);

    /// <summary>Publishes conservative evidence when the shared deadline elapsed.</summary>
    /// <returns>Whether this call transitioned negotiation to complete.</returns>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public bool Expire() => _strategy.Expire();

    /// <summary>
    /// Publishes absent evidence immediately when the owning transport closes.
    /// </summary>
    /// <returns>Whether this call transitioned negotiation to complete.</returns>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    internal bool Complete() => _strategy.Complete();
}
