// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Discovery.Queries;

using Adapters;

using Capabilities;

using Xterm;


/// <summary>
/// Owns one bounded active terminal capability query batch and publishes its immutable evidence.
/// </summary>
internal sealed class ActiveQueryDiscoveryStrategy
{
    #region State and construction

    private static readonly IReadOnlyDictionary<string, string?> _emptyEnvironment =
        new Dictionary<string, string?>();
    private static readonly IReadOnlySet<QueryKind> _cursorFenceExclusions = new HashSet<QueryKind>
    {
        QueryKind.CursorPosition
    };
    private static readonly IReadOnlySet<QueryKind> _localFenceExclusions = new HashSet<QueryKind>
    {
        QueryKind.Keyboard, QueryKind.CursorPosition, QueryKind.WindowPixels,
        QueryKind.CellPixels, QueryKind.WindowCells, QueryKind.StatusString
    };
    private readonly NegotiationOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly QueryTracker _tracker;
    private readonly TerminalCapabilities _baseline;
    private readonly HashSet<int> _pendingModes = [];
    private readonly HashSet<int> _completedModes = [];
    private readonly HashSet<int> _expiredModes = [];
    private bool? _bracketedPaste;
    private bool? _cellMouse;
    private bool? _focusReporting;
    private bool? _kittyKeyboard;
    private bool? _kittyGraphics;
    private bool? _xtermKeyboard;
    private bool? _pixelMouse;
    private bool? _sixel;
    private bool? _synchronizedOutput;
    private bool? _graphemeClustering;
    private bool? _kittyClipboard;
    private bool? _itermImages;
    private bool _keyboardQueried;
    private bool _graphicsQueried;
    private bool _usesExplicitOuterProfile;
    private string? _planningTerminalName;
    private string? _localTerminalName;
    private TerminalCapabilities? _outerBaseline;
    private PaletteResponse? _paletteColor;
    private PaletteResponse? _foregroundColor;
    private PaletteResponse? _backgroundColor;
    private MetricsResponse? _windowPixels;
    private MetricsResponse? _cellPixels;
    private MetricsResponse? _windowCells;
    private CapabilityResponse? _capabilityString;

    /// <summary>Gets the baseline for output families that follow the optional outer route.</summary>
    private TerminalCapabilities OutputBaseline => _outerBaseline ?? _baseline;

    private TerminalCapabilities? Published { get; set; }

    private QueryResults? PublishedResults { get; set; }

    /// <summary>Initializes one bounded strategy over the conservative capability baseline.</summary>
    /// <param name="options">The non-null owned negotiation policy.</param>
    /// <param name="timeProvider">The deadline clock, or null for system time.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public ActiveQueryDiscoveryStrategy(
        NegotiationOptions options,
        TimeProvider? timeProvider = null) : this(
        options,
        TerminalCapabilities.Conservative,
        timeProvider)
    {
    }

    /// <summary>Initializes one bounded strategy over an already-resolved description baseline.</summary>
    /// <param name="options">The non-null owned negotiation policy.</param>
    /// <param name="baseline">The non-null nearest-connection description baseline for raw modes, keys, and geometry.</param>
    /// <param name="timeProvider">The deadline clock, or null for system time.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="baseline"/> is null.</exception>
    public ActiveQueryDiscoveryStrategy(
        NegotiationOptions options,
        TerminalCapabilities baseline,
        TimeProvider? timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(baseline);
        _options = options;
        _baseline = baseline;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _tracker = new QueryTracker(options.Limits, _timeProvider);
    }

    /// <summary>Gets whether the query batch was emitted.</summary>
    public bool Started { get; private set; }

    /// <summary>
    /// Gets whether the written batch included the CSI 6n cursor-position request. A
    /// low caller-supplied <see cref="QueryLimits.MaxConcurrentQueries"/> budget can crowd the
    /// fence out of the batch entirely; callers that gate CSI 1;&lt;mod&gt;R disambiguation on the
    /// negotiation window must also gate on this, or a modified F3 keystroke arriving during that
    /// window would be misclassified as a reply to a query that was never actually sent.
    /// </summary>
    public bool FenceQueried { get; private set; }

    /// <summary>Gets whether one immutable profile was published.</summary>
    public bool Completed { get; private set; }

    /// <summary>Gets whether response correlation or private-mode work remains active.</summary>
    public bool HasPendingWork => _tracker.ActiveCount != 0 || _pendingModes.Count != 0;

    /// <summary>Gets the shared response deadline after startup.</summary>
    public DateTimeOffset Deadline { get; private set; }

    /// <summary>Gets the latest redacted response-classification diagnostic.</summary>
    public Diagnostic? LastDiagnostic { get; private set; }

    /// <summary>Gets the published immutable profile.</summary>
    /// <exception cref="InvalidOperationException">Negotiation is incomplete.</exception>
    public TerminalCapabilities Capabilities => Published ??
                                                throw new InvalidOperationException(
                                                    "Negotiation has not published a profile.");

    /// <summary>Gets the owned standard-query evidence after publication.</summary>
    /// <exception cref="InvalidOperationException">Negotiation is incomplete.</exception>
    public QueryResults Results => PublishedResults ??
                              throw new InvalidOperationException(
                                  "Negotiation has not published query evidence.");

    #endregion

    #region Startup

    /// <summary>Writes a bounded startup batch and reports whether it reached the destination atomically.</summary>
    /// <param name="destination">The non-null synchronous byte destination.</param>
    /// <param name="cells">Optional locally observed text-area cells.</param>
    /// <param name="pixels">Optional locally observed text-area pixels.</param>
    /// <param name="route">Optional explicit typed outer-terminal query route.</param>
    /// <param name="describedTerminalName">
    /// The connection's own resolved description name, used only as a planning-name fallback
    /// when neither a route nor <c>TERM</c> supplies one (see the built-in Windows VT carve-out
    /// below).
    /// </param>
    /// <returns>True when the query batch was written; false when route encoding failed atomically.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The negotiator already started.</exception>
    public bool TryStart(
        IBufferWriter<byte> destination,
        Size? cells,
        Size? pixels,
        MultiplexerRoute? route = null,
        string? describedTerminalName = null)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (Started)
        {
            throw new InvalidOperationException("The capability negotiator already started.");
        }

        Deadline = _timeProvider.GetUtcNow() + _options.Limits.QueryTimeout;
        _usesExplicitOuterProfile = route?.CanRouteCapabilityQueries == true;

        _outerBaseline = _usesExplicitOuterProfile ? route!.Policy.OuterProfile!.Capabilities : null;
        _localTerminalName = _options.Environment.TryGetValue(EvidenceEnvironmentVars.Term, out var term) &&
                             !string.IsNullOrEmpty(term)
            ? term
            : describedTerminalName;
        _planningTerminalName = _usesExplicitOuterProfile
            ? route!.Policy.OuterProfile!.Description.Name
            : _localTerminalName;
        var supportsStringQueries = route?.SupportsStringTerminatedQueries != false;
        var remaining = _options.Limits.MaxConcurrentQueries;

        // Routed clipboard and image queries consult outer evidence. Raw modes, keyboard, and
        // geometry keep the nearest connection's baseline and identity, including the built-in
        // windows-vt fallback when native Windows has no TERM environment variable.
        var planning = _usesExplicitOuterProfile ? OutputBaseline : _baseline.Apply(_options.Environment);

        var queryKeyboard = remaining >= 2 &&
                            ShouldQuery(
                                _baseline.KittyKeyboard,
                                _options.Overrides?.KittyKeyboard);
        _keyboardQueried = queryKeyboard;
        var keyboard = !queryKeyboard ||
                       _tracker.TryRegister(QueryKind.Keyboard, null, Deadline, out _);
        var attributes = _tracker.TryRegister(
            QueryKind.PrimaryAttributes,
            null,
            Deadline,
            out _);
        Debug.Assert(
            keyboard && attributes,
            "A fresh tracker must admit the selected bounded query families.");

        if (!keyboard || !attributes)
        {
            throw new InvalidOperationException(
                "The selected query capacity could not be registered.");
        }

        Started = true;
        var preludeQueries = new ArrayBufferWriter<byte>();
        var standardQueries = new ArrayBufferWriter<byte>();
        var localQueries = new ArrayBufferWriter<byte>();
        var preludeWriter = new ProtocolWriter(preludeQueries);
        var writer = new ProtocolWriter(standardQueries);
        var localWriter = _usesExplicitOuterProfile ? new ProtocolWriter(localQueries) : writer;

        if (queryKeyboard)
        {
            Kitty.Keyboard.KittyKeyboard.Query(_usesExplicitOuterProfile ? localWriter : preludeWriter);
            remaining--;
        }

        // Reserve DA1 before optional families spend the remaining capacity. Its bytes follow
        // the other queries addressed to the same terminal, making its reply their fence.
        remaining--;

        if (TryRegister(QueryKind.SecondaryAttributes, ref remaining))
        {
            Csi.SecondaryDeviceAttributes(writer);
        }

        AddModeQuery(
            localWriter,
            DecPrivateMode.SynchronizedOutput,
            _baseline.SynchronizedOutput,
            _options.Overrides?.SynchronizedOutput,
            ref remaining);
        AddModeQuery(
            localWriter,
            DecPrivateMode.GraphemeClustering,
            _baseline.GraphemeClustering,
            _options.Overrides?.GraphemeClustering,
            ref remaining);
        AddModeQuery(
            localWriter,
            DecPrivateMode.FocusReporting,
            _baseline.FocusReporting,
            _options.Overrides?.FocusReporting,
            ref remaining);
        AddModeQuery(
            localWriter,
            DecPrivateMode.BracketedPaste,
            _baseline.BracketedPaste,
            _options.Overrides?.BracketedPaste,
            ref remaining);
        AddModeQuery(
            localWriter,
            DecPrivateMode.CellMouse,
            _baseline.CellMouse,
            _options.Overrides?.CellMouse,
            ref remaining);
        AddModeQuery(
            localWriter,
            DecPrivateMode.PixelMouse,
            _baseline.PixelMouse,
            _options.Overrides?.PixelMouse,
            ref remaining);
        AddModeQuery(
            writer,
            DecPrivateMode.ClipboardPasteEvents,
            // The planning projection, not _baseline: under a multiplexer or SSH,
            // EnvironmentEvidenceAdapter already narrows this to Unsupported, so writing the
            // probe would only spend a round trip a multiplexer cannot carry and SSH answers the
            // same way regardless.
            planning.KittyClipboard,
            _options.Overrides?.KittyClipboard,
            ref remaining);

        if (!HasPositive(pixels) && TryRegister(QueryKind.WindowPixels, ref remaining))
        {
            Csi.ReportWindowPixels(localWriter);
        }

        if (!HasCellMetrics(cells, pixels) &&
            TryRegister(QueryKind.CellPixels, ref remaining))
        {
            Csi.ReportCellPixels(localWriter);
        }

        if (!HasPositive(cells) && TryRegister(QueryKind.WindowCells, ref remaining))
        {
            Csi.ReportWindowCells(localWriter);
        }

        if (supportsStringQueries && TryRegister(QueryKind.PaletteColor, ref remaining))
        {
            Osc.QueryPalette(writer, 0);
        }

        if (supportsStringQueries && TryRegister(QueryKind.ForegroundColor, ref remaining))
        {
            Osc.QueryForeground(writer);
        }

        if (supportsStringQueries && TryRegister(QueryKind.BackgroundColor, ref remaining))
        {
            Osc.QueryBackground(writer);
        }

        if (supportsStringQueries &&
            // The planning projection: under a multiplexer, EnvironmentEvidenceAdapter already
            // narrows ItermImages to Unsupported, and OSC 1337 is iTerm2-proprietary with no
            // negative-reply form, so a silenced probe there could only ever time out.
            ShouldQuery(planning.ItermImages, _options.Overrides?.ItermImages) &&
            TryRegister(QueryKind.ItermCapabilities, ref remaining))
        {
            Osc.QueryItermCapabilities(writer);
        }

        if (supportsStringQueries &&
            ShouldQueryXtermCapability() &&
            TryRegister(CapabilityName.DirectColor, ref remaining))
        {
            CapabilityName[] names = [CapabilityName.DirectColor];
            XtermGetCap.Query(writer, names);
        }

        if ((_usesExplicitOuterProfile || supportsStringQueries) &&
            ShouldQueryXtermKeyboard() &&
            TryRegister(StatusName.ModifyOtherKeys, ref remaining))
        {
            XtermDecrqss.Query(localWriter, StatusName.ModifyOtherKeys);
        }

        // Only an approved route can carry an APC probe across a multiplexer. A detected but
        // unroutable multiplexer consumes it instead: tmux parses a bare APC string as a pane
        // title, so the probe would overwrite the user's pane title and could never be answered.
        // Skipping it also avoids spending a query deadline on a reply that cannot arrive.
        var canDeliverApc = route is not null || _options.Multiplexing.Layers.Count == 0;
        var graphics = supportsStringQueries &&
                       canDeliverApc &&
                       remaining != 0 &&
                       ShouldQuery(OutputBaseline.KittyGraphics, _options.Overrides?.KittyGraphics);

        if (graphics)
        {
            var registered = _tracker.TryRegister(
                QueryKind.KittyGraphics,
                "31",
                Deadline,
                out _);
            Debug.Assert(registered, "Remaining capacity must admit one graphics query.");

            if (!registered)
            {
                throw new InvalidOperationException("The Kitty graphics query could not be registered.");
            }

            _graphicsQueried = true;
            remaining--;
        }

        // DA1 fences this terminal's standard queries and graphics prelude. On a route it
        // says nothing about raw queries sent to the nearest layer, which remain independent.
        Csi.PrimaryDeviceAttributes(writer);

        // Cursor position is last on a direct connection, or last in the local group before
        // an outer batch. Its reply is byte-identical to modified F3, so it only resolves itself
        // and can never prove another query stayed silent. DA1 also never retires this family.
        FenceQueried = TryRegister(QueryKind.CursorPosition, ref remaining);

        if (FenceQueried)
        {
            Csi.ReportCursorPosition(localWriter);
        }

        var queryBatch = new ArrayBufferWriter<byte>();
        queryBatch.Write(preludeQueries.WrittenSpan);

        if (graphics)
        {
            Span<byte> queryPixel = [0, 0, 0];
            Kitty.Graphics.KittyGraphicsWriter.Write(
                Kitty.Graphics.KittyGraphicsCommand.Query(31),
                queryPixel,
                queryBatch);
        }

        queryBatch.Write(standardQueries.WrittenSpan);

        if (route?.CanRouteCapabilityQueries == true)
        {
            // Encode both destinations before writing anything. An outer-envelope limit must
            // not leave a partial local query batch on the transport.
            var routedQueries = new ArrayBufferWriter<byte>();

            if (!route.TryWriteCapabilityQueries(routedQueries, queryBatch.WrittenSpan))
            {
                CompletePendingWork(_timeProvider.GetUtcNow());
                return false;
            }

            destination.Write(localQueries.WrittenSpan);
            destination.Write(routedQueries.WrittenSpan);
        }
        else
        {
            destination.Write(queryBatch.WrittenSpan);
        }

        return true;
    }

    #endregion

    #region XtermCapabilitiesResponse acceptance and completion

    /// <summary>Matches one recognized response and publishes when all queries complete.</summary>
    /// <param name="response">The owned typed terminal response.</param>
    /// <returns>The active, duplicate, late, or unknown match classification.</returns>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public QueryMatch Accept(in XtermCapabilitiesResponse response)
    {
        if (!Started)
        {
            throw new InvalidOperationException("The capability negotiator has not started.");
        }

        var now = _timeProvider.GetUtcNow();

        if (response.Kind == ResponseKind.PrivateMode)
        {
            return AcceptPrivateMode(in response, now);
        }

        _ = ExpireIfDeadlineReached(now);
        // QueryTracker's typed DA1 shortcut also resolves a same-terminal keyboard prelude.
        // An outer DA1 has no authority over the local keyboard query; fence it explicitly below.
        var match = _usesExplicitOuterProfile && response.Kind == ResponseKind.PrimaryAttributes
            ? _tracker.Match(QueryKind.PrimaryAttributes, now)
            : _tracker.Match(response, now);
        LastDiagnostic = _tracker.LastDiagnostic;

        if (match == QueryMatch.Matched)
        {
            if (response.Kind == ResponseKind.Keyboard)
            {
                _kittyKeyboard = true;
            }
            else if (response.Kind == ResponseKind.PrimaryAttributes &&
                     !_usesExplicitOuterProfile && _keyboardQueried && !_kittyKeyboard.HasValue)
            {
                _kittyKeyboard = false;
            }

            if (response.Kind == ResponseKind.PrimaryAttributes)
            {
                _sixel = response.Values.Span.Contains(4);
            }

            if (response.Kind == ResponseKind.PrimaryAttributes &&
                _graphicsQueried && !_kittyGraphics.HasValue)
            {
                _kittyGraphics = false;
            }

            if (response.Kind == ResponseKind.PrimaryAttributes)
            {
                // Retire only families addressed to the terminal that answered DA1. Local
                // mode, keyboard, and geometry replies can still be in flight on an outer route.
                RetireFencedFamilies(now);
            }

            // A CSI 6n reply only ever resolves its own tracked family here (via the _tracker.Match
            // call above). It deliberately does not retire any other still-outstanding family: the
            // reply grammar is byte-identical to a modified F3 keystroke, which a user or replayed
            // typeahead can deliver at any point in the shared deadline window, so a match here is
            // never trustworthy proof that every other family stayed silent. Every other family
            // still resolves through its own matching reply, the DA1 fence above, or the shared
            // deadline.
            TryPublish();
        }

        return match;
    }

    /// <summary>Matches one strict numeric Kitty graphics APC response.</summary>
    /// <param name="response">The non-null owned response.</param>
    /// <returns>The active, duplicate, late, or unknown match classification.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public QueryMatch Accept(Kitty.Graphics.KittyGraphicsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!Started)
        {
            throw new InvalidOperationException("The capability negotiator has not started.");
        }

        if (!response.Valid)
        {
            LastDiagnostic = response.Diagnostic;
            return QueryMatch.Unknown;
        }

        var now = _timeProvider.GetUtcNow();
        _ = ExpireIfDeadlineReached(now);
        var id = response.ImageId.ToString(CultureInfo.InvariantCulture);
        var match = _tracker.Match(QueryKind.KittyGraphics, now, id);
        LastDiagnostic = _tracker.LastDiagnostic;

        if (match == QueryMatch.Matched)
        {
            _kittyGraphics = true;
            TryPublish();
        }

        return match;
    }

    /// <summary>Matches one validated terminal color response.</summary>
    /// <param name="response">The immutable color response.</param>
    /// <returns>The active, duplicate, late, or unknown match classification.</returns>
    /// <exception cref="ArgumentException"><paramref name="response"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public QueryMatch Accept(in PaletteResponse response)
    {
        if (response.IsEmpty)
        {
            throw new ArgumentException("The response cannot be empty.", nameof(response));
        }

        if (!Started)
        {
            throw new InvalidOperationException("The capability negotiator has not started.");
        }

        var now = _timeProvider.GetUtcNow();
        _ = ExpireIfDeadlineReached(now);

        // Startup requests OSC 4 index zero specifically. Other valid palette
        // replies remain observable to the router but cannot consume that query.
        if (response.Kind == ResponseKind.PaletteColor && response.Index != 0)
        {
            LastDiagnostic = null;
            return QueryMatch.Unknown;
        }

        var match = _tracker.Match(ToQueryKind(response.Kind, palette: true), now);
        LastDiagnostic = _tracker.LastDiagnostic;

        if (match == QueryMatch.Matched)
        {
            if (response.Kind == ResponseKind.PaletteColor)
            {
                _paletteColor = response;
            }
            else if (response.Kind == ResponseKind.ForegroundColor)
            {
                _foregroundColor = response;
            }
            else
            {
                Debug.Assert(
                    response.Kind == ResponseKind.BackgroundColor,
                    "Palette responses validate their family at construction.");
                _backgroundColor = response;
            }

            TryPublish();
        }

        return match;
    }

    /// <summary>Matches one validated terminal metrics response.</summary>
    /// <param name="response">The immutable metrics response.</param>
    /// <returns>The active, duplicate, late, or unknown match classification.</returns>
    /// <exception cref="ArgumentException"><paramref name="response"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public QueryMatch Accept(in MetricsResponse response)
    {
        if (response.IsEmpty)
        {
            throw new ArgumentException("The response cannot be empty.", nameof(response));
        }

        if (!Started)
        {
            throw new InvalidOperationException("The capability negotiator has not started.");
        }

        var now = _timeProvider.GetUtcNow();
        _ = ExpireIfDeadlineReached(now);
        var match = _tracker.Match(ToQueryKind(response.Kind, palette: false), now);
        LastDiagnostic = _tracker.LastDiagnostic;

        if (match == QueryMatch.Matched)
        {
            if (response.Kind == ResponseKind.WindowPixels)
            {
                _windowPixels = response;
            }
            else if (response.Kind == ResponseKind.CellPixels)
            {
                _cellPixels = response;
            }
            else
            {
                Debug.Assert(
                    response.Kind == ResponseKind.WindowCells,
                    "Metrics responses validate their family at construction.");
                _windowCells = response;
            }

            TryPublish();
        }

        return match;
    }

    /// <summary>Matches one validated DECRQSS response.</summary>
    /// <param name="response">The non-empty owned status response.</param>
    /// <returns>The match classification.</returns>
    /// <exception cref="ArgumentException"><paramref name="response"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public QueryMatch Accept(in StatusResponse response)
    {
        if (response.IsEmpty)
        {
            throw new ArgumentException("The response cannot be empty.", nameof(response));
        }

        if (!Started)
        {
            throw new InvalidOperationException("The capability negotiator has not started.");
        }

        var now = _timeProvider.GetUtcNow();
        _ = ExpireIfDeadlineReached(now);

        var match = _tracker.Match(in response, now);
        LastDiagnostic = _tracker.LastDiagnostic;

        if (match == QueryMatch.Matched)
        {
            _xtermKeyboard = true;
            TryPublish();
        }

        return match;
    }

    /// <summary>Matches one validated XTGETTCAP response.</summary>
    /// <param name="response">The non-null owned response.</param>
    /// <returns>The match classification.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public QueryMatch Accept(CapabilityResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!Started)
        {
            throw new InvalidOperationException("The capability negotiator has not started.");
        }

        var now = _timeProvider.GetUtcNow();
        _ = ExpireIfDeadlineReached(now);

        var match = _tracker.Match(response, now);
        LastDiagnostic = _tracker.LastDiagnostic;

        if (match == QueryMatch.Matched)
        {
            _capabilityString = response;
            TryPublish();
        }

        return match;
    }

    /// <summary>Matches one validated iTerm2 OSC 1337 Capabilities feature-reporting reply.</summary>
    /// <param name="response">The non-null owned response.</param>
    /// <returns>The active, duplicate, late, or unknown match classification.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public QueryMatch Accept(ItermCapabilitiesResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!Started)
        {
            throw new InvalidOperationException("The capability negotiator has not started.");
        }

        var now = _timeProvider.GetUtcNow();
        _ = ExpireIfDeadlineReached(now);
        var match = _tracker.Match(QueryKind.ItermCapabilities, now);
        LastDiagnostic = _tracker.LastDiagnostic;

        if (match == QueryMatch.Matched)
        {
            // The published table assigns F to both FILE and FOCUS_REPORTING. Its absence proves
            // FILE was not advertised, but its presence cannot identify which Boolean produced
            // the token and therefore cannot authorize the multipart protocol SharpVision emits.
            _itermImages = response.HasFileCode ? null : false;
            TryPublish();
        }

        return match;
    }

    /// <summary>Publishes conservative evidence when the shared deadline elapsed.</summary>
    /// <returns>Whether this call transitioned negotiation to complete.</returns>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public bool Expire() => !Started
        ? throw new InvalidOperationException("The capability negotiator has not started.")
        : ExpireIfDeadlineReached(_timeProvider.GetUtcNow());

    /// <summary>
    /// Publishes absent evidence immediately when the owning transport closes.
    /// </summary>
    /// <returns>Whether this call transitioned negotiation to complete.</returns>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public bool Complete()
    {
        if (!Started)
        {
            throw new InvalidOperationException("The capability negotiator has not started.");
        }

        if (Completed)
        {
            return false;
        }

        CompletePendingWork(_timeProvider.GetUtcNow());
        return true;
    }

    private QueryMatch AcceptPrivateMode(in XtermCapabilitiesResponse response, DateTimeOffset now)
    {
        _ = ExpireIfDeadlineReached(now);
        var values = response.Values.Span;

        if (values.Length != 2)
        {
            LastDiagnostic = null;
            return QueryMatch.Unknown;
        }

        var mode = values[0];

        if (_completedModes.Contains(mode))
        {
            LastDiagnostic = CreateDiagnostic(DiagnosticCode.DuplicateResponse);
            return QueryMatch.Duplicate;
        }

        if (_expiredModes.Contains(mode))
        {
            LastDiagnostic = CreateDiagnostic(DiagnosticCode.LateResponse);
            return QueryMatch.Late;
        }

        if (!_pendingModes.Remove(mode))
        {
            LastDiagnostic = null;
            return QueryMatch.Unknown;
        }

        LastDiagnostic = null;
        _ = _completedModes.Add(mode);

        // DECRPM value 3 ("permanently set") means usable for every mode except 2026: that
        // mode's *value* encodes "an update is currently in progress" (DECSET begins, DECRST
        // ends), so a terminal claiming it is permanently set would never present a frame.
        // Treat that specific reply as unusable rather than as proof of a working toggle.
        var supported = response.Supported && (mode != DecPrivateMode.SynchronizedOutput || values[1] != 3);

        SetModeResult(mode, supported);
        TryPublish();
        return QueryMatch.Matched;
    }

    private void SetModeResult(int mode, bool supported)
    {
        switch (mode)
        {
            case DecPrivateMode.SynchronizedOutput:
                _synchronizedOutput = supported;
                break;
            case DecPrivateMode.GraphemeClustering:
                _graphemeClustering = supported;
                break;
            case DecPrivateMode.FocusReporting:
                _focusReporting = supported;
                break;
            case DecPrivateMode.BracketedPaste:
                _bracketedPaste = supported;
                break;
            case DecPrivateMode.CellMouse:
                _cellMouse = supported;
                break;
            case DecPrivateMode.PixelMouse:
                _pixelMouse = supported;
                break;
            case DecPrivateMode.ClipboardPasteEvents:
                _kittyClipboard = supported;
                break;
            default:
                throw new UnreachableException("Only selected modes can be completed.");
        }
    }

    #endregion

    #region Publication

    private void TryPublish()
    {
        if (Completed)
        {
            return;
        }

        if (_tracker.ActiveCount != 0 || _pendingModes.Count != 0)
        {
            return;
        }

        Publish();
    }

    private void Publish()
    {
        // An unanswered OSC 1337 Capabilities probe (deadline expiry, fence retirement, or
        // transport EOF) leaves _itermImages null here deliberately. Coercing it to an explicit
        // false would publish Unsupported/Origin.Query for a query that supplied no evidence at
        // all, erasing a TERM_PROGRAM=iTerm.app environment hint underneath it.
        var queries = new QueryResults()
        {
            PaletteColor = _paletteColor,
            ForegroundColor = _foregroundColor,
            BackgroundColor = _backgroundColor,
            WindowPixels = _windowPixels,
            CellPixels = _cellPixels,
            WindowCells = _windowCells,
            SynchronizedOutput = _synchronizedOutput,
            GraphemeClustering = _graphemeClustering,
            FocusReporting = _focusReporting,
            BracketedPaste = _bracketedPaste,
            PixelMouse = _pixelMouse,
            CellMouse = _cellMouse,
            KittyKeyboard = _kittyKeyboard,
            KittyGraphics = _kittyGraphics,
            KittyClipboard = _kittyClipboard,
            Sixel = _sixel,
            ItermImages = _itermImages,
            XtermKeyboard = _xtermKeyboard,
            CapabilityString = _capabilityString
        };
        Published = CapabilityDetector.Detect(
            OutputBaseline,
            _usesExplicitOuterProfile ? _emptyEnvironment : _options.Environment,
            queries,
            _options.Overrides);
        if (_usesExplicitOuterProfile)
        {
            // Evidence must follow delivery: these operations are always written raw to the
            // nearest layer, so the outer profile cannot authorize them or suppress local support.
            var local = CapabilityDetector.Detect(_baseline, _options.Environment, queries, _options.Overrides);
            Published = Published with
            {
                SynchronizedOutput = local.SynchronizedOutput,
                GraphemeClustering = local.GraphemeClustering,
                FocusReporting = local.FocusReporting,
                BracketedPaste = local.BracketedPaste,
                CellMouse = local.CellMouse,
                PixelMouse = local.PixelMouse,
                KittyKeyboard = local.KittyKeyboard,
                XtermKeyboard = local.XtermKeyboard
            };
        }

        PublishedResults = queries;
        Completed = true;
    }

    private void CompletePendingWork(DateTimeOffset now)
    {
        RetireOutstandingFamilies(now);
        Publish();
    }

    /// <summary>
    /// Retires every still-outstanding query family and DEC private mode without recording any
    /// evidence for them - the shared step behind deadline expiration and transport completion.
    /// Neither caller observed an actual reply for the families retired here, so their fields stay
    /// unset and <see cref="Publish"/> leaves them absent rather than inventing
    /// <see cref="Origin.Query"/> support or non-support. A matched cursor-position reply
    /// deliberately does not call this: that reply's grammar cannot be told apart from an
    /// unrelated keystroke, so it is not evidence that any other family stayed silent.
    /// </summary>
    private void RetireOutstandingFamilies(DateTimeOffset now)
    {
        _ = _tracker.ExpireAll(now);
        RetirePendingModes();
    }

    /// <summary>
    /// Retires unanswered queries fenced by this terminal's DA1, without inventing evidence.
    /// Local families survive an outer fence; cursor position always resolves independently.
    /// </summary>
    private void RetireFencedFamilies(DateTimeOffset now)
    {
        _ = _tracker.RetireActiveFamiliesExcept(
            _usesExplicitOuterProfile ? _localFenceExclusions : _cursorFenceExclusions, now);

        if (_usesExplicitOuterProfile)
        {
            // Mode 5522 follows the outer clipboard route; every other mode stays local.
            if (_pendingModes.Remove(DecPrivateMode.ClipboardPasteEvents))
            {
                _ = _expiredModes.Add(DecPrivateMode.ClipboardPasteEvents);
            }
        }
        else
        {
            RetirePendingModes();
        }
    }

    private void RetirePendingModes()
    {
        foreach (var mode in _pendingModes)
        {
            _ = _expiredModes.Add(mode);
        }

        _pendingModes.Clear();
    }

    #endregion

    #region Query planning and registration

    private static Diagnostic CreateDiagnostic(DiagnosticCode code) =>
        new(code, SequenceKind.Csi, offset: 0, discardedBytes: 0);

    private static QueryKind ToQueryKind(ResponseKind kind, bool palette)
    {
        if (palette)
        {
            if (kind == ResponseKind.PaletteColor)
            {
                return QueryKind.PaletteColor;
            }

            if (kind == ResponseKind.ForegroundColor)
            {
                return QueryKind.ForegroundColor;
            }

            if (kind == ResponseKind.BackgroundColor)
            {
                return QueryKind.BackgroundColor;
            }
        }
        else
        {
            if (kind == ResponseKind.WindowPixels)
            {
                return QueryKind.WindowPixels;
            }

            if (kind == ResponseKind.CellPixels)
            {
                return QueryKind.CellPixels;
            }

            if (kind == ResponseKind.WindowCells)
            {
                return QueryKind.WindowCells;
            }
        }

        throw new UnreachableException("Typed responses validate their family at construction.");
    }

    private void AddModeQuery(
        ProtocolWriter writer,
        int mode,
        Feature baseline,
        bool? overrideValue,
        ref int remaining)
    {
        if (remaining == 0 || !ShouldQuery(baseline, overrideValue))
        {
            return;
        }

        _ = _pendingModes.Add(mode);
        Csi.QueryPrivateMode(writer, mode);
        remaining--;
    }

    private bool TryRegister(QueryKind kind, ref int remaining)
    {
        if (remaining == 0)
        {
            return false;
        }

        var registered = _tracker.TryRegister(kind, null, Deadline, out _);
        Debug.Assert(registered, "A fresh startup family must fit the selected global budget.");

        if (!registered)
        {
            throw new InvalidOperationException("The selected query family could not be registered.");
        }

        remaining--;
        return true;
    }

    private bool TryRegister(StatusName name, ref int remaining)
    {
        if (remaining == 0)
        {
            return false;
        }

        var registered = _tracker.TryRegister(name, Deadline, out _);
        Debug.Assert(registered, "A fresh DECRQSS selector must fit the selected global budget.");

        if (!registered)
        {
            throw new InvalidOperationException("The selected DECRQSS selector could not be registered.");
        }

        remaining--;
        return true;
    }

    private bool TryRegister(CapabilityName name, ref int remaining)
    {
        if (remaining == 0)
        {
            return false;
        }

        var registered = _tracker.TryRegister(name, Deadline, out _);
        Debug.Assert(registered, "A fresh XTGETTCAP name must fit the selected global budget.");

        if (!registered)
        {
            throw new InvalidOperationException("The selected XTGETTCAP name could not be registered.");
        }

        remaining--;
        return true;
    }

    private static bool ShouldQuery(Feature baseline, bool? overrideValue) =>
        !overrideValue.HasValue && baseline.State is CapabilitySupport.Unknown or CapabilitySupport.Tentative;

    private bool ShouldQueryXtermKeyboard()
    {
        var term = _localTerminalName;
        return IsXtermLikeHint(term) &&
               ShouldQuery(_baseline.XtermKeyboard, _options.Overrides?.XtermKeyboard);
    }

    private bool ShouldQueryXtermCapability()
    {
        var term = _planningTerminalName;
        return IsXtermLikeHint(term) &&
               // Database is included alongside Default and Environment because a terminfo entry
               // such as xterm-256color only proves indexed support: it says nothing about direct
               // color one way or the other, so a live XTGETTCAP reply is still free to raise it,
               // the same way QueryEvidenceAdapter.RefineColor already treats Database evidence as
               // upgradable rather than settled.
               OutputBaseline.ColorOrigin is Origin.Default or Origin.Environment or Origin.Database &&
               _options.Overrides?.ColorDepth is null &&
               !_options.Environment.ContainsKey(EvidenceEnvironmentVars.NoColor);
    }

    // "windows-vt" is SharpVision's own built-in description name, selected only after
    // confirming ENABLE_VIRTUAL_TERMINAL_PROCESSING succeeded (see WindowsVtProvider). It never
    // contains "xterm", so it needs its own explicit carve-out alongside the environment-hint
    // test rather than being folded into it - see the related handling in TryStart above.
    private static bool IsXtermLikeHint(string? term) =>
        TerminalNames.IsXtermFamily(term) ||
        string.Equals(term, "windows-vt", StringComparison.Ordinal);

    private static bool HasPositive(Size? value) =>
        value is { Width: > 0, Height: > 0 };

    private static bool HasCellMetrics(Size? cells, Size? pixels) =>
        cells is { Width: > 0, Height: > 0 } cellSize &&
        pixels is { Width: > 0, Height: > 0 } pixelSize &&
        pixelSize.Width >= cellSize.Width &&
        pixelSize.Height >= cellSize.Height;

    #endregion

    #region Deadline expiration

    private bool ExpireIfDeadlineReached(DateTimeOffset now)
    {
        if (Completed || now < Deadline)
        {
            return false;
        }

        RetireOutstandingFamilies(now);
        TryPublish();
        Debug.Assert(Completed, "Atomic batch expiration must publish absent evidence once.");
        return true;
    }

    #endregion
}
