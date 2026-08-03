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
    private bool? _kittyClipboard;
    private bool? _itermImages;
    private bool _keyboardQueried;
    private bool _graphicsQueried;
    private bool _fenceQueried;
    private bool _usesExplicitOuterProfile;
    private PaletteResponse? _paletteColor;
    private PaletteResponse? _foregroundColor;
    private PaletteResponse? _backgroundColor;
    private MetricsResponse? _windowPixels;
    private MetricsResponse? _cellPixels;
    private MetricsResponse? _windowCells;
    private CapabilityResponse? _capabilityString;

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
    /// <param name="baseline">The non-null description-derived semantic baseline.</param>
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
    public bool IsStarted { get; private set; }

    /// <summary>Gets whether one immutable profile was published.</summary>
    public bool IsComplete { get; private set; }

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
    /// <returns>True when the query batch was written; false when route encoding failed atomically.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The negotiator already started.</exception>
    public bool TryStart(
        IBufferWriter<byte> destination,
        Size? cells,
        Size? pixels,
        MultiplexerRoute? route = null)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (IsStarted)
        {
            throw new InvalidOperationException("The capability negotiator already started.");
        }

        Deadline = _timeProvider.GetUtcNow() + _options.Limits.QueryTimeout;
        _usesExplicitOuterProfile = route?.CanRouteCapabilityQueries == true;
        var supportsStringQueries = route?.SupportsStringTerminatedQueries != false;
        var remaining = _options.Limits.MaxConcurrentQueries;

        // Planning-only environment projection: probes a multiplexer or SSH hop can never carry
        // an answer for should not be written at all, rather than written and then narrowed to
        // Unsupported moments after the round trip is already spent (see #249). This never
        // touches _baseline and is never passed to CapabilityDetector.Detect — Publish still
        // folds _options.Environment through the normal discovery pipeline once, in phase order,
        // so a suppressed probe still ends up Unsupported/Origin.Environment there, not
        // Origin.Query. Skipped when the route can carry capability queries: Publish deliberately
        // uses an empty environment in that case so an inner multiplexer's variables cannot
        // narrow or augment explicit outer evidence, and this projection must respect the same
        // carve-out.
        var planning = _usesExplicitOuterProfile ? _baseline : _baseline.Apply(_options.Environment);

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

        IsStarted = true;
        var preludeQueries = new ArrayBufferWriter<byte>();
        var standardQueries = new ArrayBufferWriter<byte>();
        var preludeWriter = new ProtocolWriter(preludeQueries);
        var writer = new ProtocolWriter(standardQueries);

        if (queryKeyboard)
        {
            Kitty.Keyboard.KittyKeyboard.Query(preludeWriter);
            remaining--;
        }

        Csi.PrimaryDeviceAttributes(writer);
        remaining--;

        if (TryRegister(QueryKind.SecondaryAttributes, ref remaining))
        {
            Csi.SecondaryDeviceAttributes(writer);
        }

        AddModeQuery(
            writer,
            DecPrivateMode.SynchronizedOutput,
            _baseline.SynchronizedOutput,
            _options.Overrides?.SynchronizedOutput,
            ref remaining);
        AddModeQuery(
            writer,
            DecPrivateMode.FocusReporting,
            _baseline.FocusReporting,
            _options.Overrides?.FocusReporting,
            ref remaining);
        AddModeQuery(
            writer,
            DecPrivateMode.BracketedPaste,
            _baseline.BracketedPaste,
            _options.Overrides?.BracketedPaste,
            ref remaining);
        AddModeQuery(
            writer,
            DecPrivateMode.CellMouse,
            _baseline.CellMouse,
            _options.Overrides?.CellMouse,
            ref remaining);
        AddModeQuery(
            writer,
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
            // same way regardless (see #249).
            planning.KittyClipboard,
            _options.Overrides?.KittyClipboard,
            ref remaining);

        if (!HasPositive(pixels) && TryRegister(QueryKind.WindowPixels, ref remaining))
        {
            Csi.ReportWindowPixels(writer);
        }

        if (!HasCellMetrics(cells, pixels) &&
            TryRegister(QueryKind.CellPixels, ref remaining))
        {
            Csi.ReportCellPixels(writer);
        }

        if (!HasPositive(cells) && TryRegister(QueryKind.WindowCells, ref remaining))
        {
            Csi.ReportWindowCells(writer);
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
            // negative-reply form, so a silenced probe there could only ever time out (see #249).
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

        if (supportsStringQueries &&
            ShouldQueryXtermKeyboard() &&
            TryRegister(StatusName.ModifyOtherKeys, ref remaining))
        {
            XtermDecrqss.Query(writer, StatusName.ModifyOtherKeys);
        }

        // Only an approved route can carry an APC probe across a multiplexer. A detected but
        // unroutable multiplexer consumes it instead: tmux parses a bare APC string as a pane
        // title, so the probe would overwrite the user's pane title and could never be answered.
        // Skipping it also avoids spending a query deadline on a reply that cannot arrive.
        var canDeliverApc = route is not null || _options.Multiplexing.Layers.Count == 0;
        var graphics = supportsStringQueries &&
                       canDeliverApc &&
                       remaining != 0 &&
                       ShouldQuery(_baseline.KittyGraphics, _options.Overrides?.KittyGraphics);

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
        }

        // A terminating fence, written last so an in-order terminal answers it only after every
        // other standard query. Only 2 of the other families ever retire early on their own
        // (Keyboard and KittyGraphics, both piggybacked on PrimaryAttributes); every other silent
        // family would otherwise hold the first frame for the full shared deadline. CSI 6n (DSR
        // cursor position) is answered by effectively every terminal that speaks any of the other
        // protocols in this batch, so its reply retiring every still-outstanding family turns the
        // deadline into a genuine backstop instead of the normal path (see #247). A fence-resolved
        // family supplies no evidence and must not be recorded as Origin.Query - see the same rule
        // RetireOutstandingFamilies already follows for ordinary deadline expiration (#248).
        _fenceQueried = TryRegister(QueryKind.CursorPosition, ref remaining);

        if (_fenceQueried)
        {
            Csi.ReportCursorPosition(writer);
        }

        var queryBatch = new ArrayBufferWriter<byte>();
        queryBatch.Write(preludeQueries.WrittenSpan);

        if (graphics)
        {
            Span<byte> queryPixel = [0, 0, 0];
            Kitty.Graphics.KittyGraphicsWriter.Write(
                Kitty.Graphics.Command.Query(31),
                queryPixel,
                queryBatch);
        }

        queryBatch.Write(standardQueries.WrittenSpan);

        if (route?.CanRouteCapabilityQueries == true)
        {
            if (!route.TryWriteCapabilityQueries(destination, queryBatch.WrittenSpan))
            {
                CompletePendingWork(_timeProvider.GetUtcNow());
                return false;
            }
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
        if (!IsStarted)
        {
            throw new InvalidOperationException("The capability negotiator has not started.");
        }

        var now = _timeProvider.GetUtcNow();

        if (response.Kind == ResponseKind.PrivateMode)
        {
            return AcceptPrivateMode(in response, now);
        }

        _ = ExpireIfDeadlineReached(now);
        var match = _tracker.Match(response, now);
        LastDiagnostic = _tracker.LastDiagnostic;

        if (match == QueryMatch.Matched)
        {
            if (response.Kind == ResponseKind.Keyboard)
            {
                _kittyKeyboard = true;
            }
            else if (response.Kind == ResponseKind.PrimaryAttributes &&
                     _keyboardQueried && !_kittyKeyboard.HasValue)
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

            // The fence reply retires every family still outstanding at this instant. It is
            // written last in the batch, so an in-order terminal answers it only after every
            // other reply it is going to send at all (see #247).
            if (response.Kind == ResponseKind.CursorPosition && _fenceQueried)
            {
                RetireOutstandingFamilies(now);
            }

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

        if (!IsStarted)
        {
            throw new InvalidOperationException("The capability negotiator has not started.");
        }

        if (!response.IsValid)
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

        if (!IsStarted)
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

        if (!IsStarted)
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

        if (!IsStarted)
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

        if (!IsStarted)
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

        if (!IsStarted)
        {
            throw new InvalidOperationException("The capability negotiator has not started.");
        }

        var now = _timeProvider.GetUtcNow();
        _ = ExpireIfDeadlineReached(now);
        var match = _tracker.Match(QueryKind.ItermCapabilities, now);
        LastDiagnostic = _tracker.LastDiagnostic;

        if (match == QueryMatch.Matched)
        {
            // The published feature-reporting table assigns code "F" to both FILE and
            // FOCUS_REPORTING (see docs/protocols/iterm2.md), so this positive reply is
            // corroborated — not disambiguated — by QueryEvidenceAdapter's TERM_PROGRAM_VERSION
            // narrowing before it can ever authorize multipart output.
            _itermImages = response.HasFileCode;
            TryPublish();
        }

        return match;
    }

    /// <summary>Publishes conservative evidence when the shared deadline elapsed.</summary>
    /// <returns>Whether this call transitioned negotiation to complete.</returns>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public bool Expire() => !IsStarted
        ? throw new InvalidOperationException("The capability negotiator has not started.")
        : ExpireIfDeadlineReached(_timeProvider.GetUtcNow());

    /// <summary>
    /// Publishes absent evidence immediately when the owning transport closes.
    /// </summary>
    /// <returns>Whether this call transitioned negotiation to complete.</returns>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public bool Complete()
    {
        if (!IsStarted)
        {
            throw new InvalidOperationException("The capability negotiator has not started.");
        }

        if (IsComplete)
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
        SetModeResult(mode, response.IsSupported);
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
        if (IsComplete)
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
        // all, erasing a TERM_PROGRAM=iTerm.app environment hint underneath it — see #248.
        var queries = new QueryResults()
        {
            PaletteColor = _paletteColor,
            ForegroundColor = _foregroundColor,
            BackgroundColor = _backgroundColor,
            WindowPixels = _windowPixels,
            CellPixels = _cellPixels,
            WindowCells = _windowCells,
            SynchronizedOutput = _synchronizedOutput,
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
            _baseline,
            _usesExplicitOuterProfile ? _emptyEnvironment : _options.Environment,
            queries,
            _options.Overrides);
        PublishedResults = queries;
        IsComplete = true;
    }

    private void CompletePendingWork(DateTimeOffset now)
    {
        RetireOutstandingFamilies(now);
        Publish();
    }

    /// <summary>
    /// Retires every still-outstanding query family and DEC private mode without recording any
    /// evidence for them - the shared step behind deadline expiration, transport completion, and
    /// the terminating fence reply (#247). None of these callers observed an actual reply for the
    /// families retired here, so their fields stay unset and <see cref="Publish"/> leaves them
    /// absent rather than inventing <see cref="Origin.Query"/> support or non-support (#248).
    /// </summary>
    private void RetireOutstandingFamilies(DateTimeOffset now)
    {
        _ = _tracker.ExpireAll(now);

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
        _ = _options.Environment.TryGetValue(EnvironmentNames.Term, out var term);
        return Contains(term, "xterm") && !Contains(term, "kitty") &&
               ShouldQuery(_baseline.XtermKeyboard, _options.Overrides?.XtermKeyboard);
    }

    private bool ShouldQueryXtermCapability()
    {
        _ = _options.Environment.TryGetValue(EnvironmentNames.Term, out var term);
        return Contains(term, "xterm") && !Contains(term, "kitty") &&
               _baseline.ColorOrigin is Origin.Default or Origin.Environment &&
               _options.Overrides?.ColorDepth is null;
    }

    private static bool Contains(string? value, string fragment) =>
        value?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true;

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
        if (IsComplete || now < Deadline)
        {
            return false;
        }

        RetireOutstandingFamilies(now);
        TryPublish();
        Debug.Assert(IsComplete, "Atomic batch expiration must publish absent evidence once.");
        return true;
    }

    #endregion
}
