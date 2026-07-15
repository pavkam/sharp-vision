// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;



/// <summary>Coordinates one bounded terminal capability query batch.</summary>
public sealed class Negotiator
{
    private static readonly int[] _modes = [2026, 1004, 2004, 1006, 1016];

    private readonly NegotiationOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly QueryTracker _tracker;
    private readonly HashSet<int> _pendingModes = [];
    private readonly HashSet<int> _completedModes = [];
    private readonly HashSet<int> _expiredModes = [];
    private bool? _bracketedPaste;
    private bool? _cellMouse;
    private bool? _focusReporting;
    private bool? _kittyKeyboard;
    private bool? _pixelMouse;
    private bool? _synchronizedOutput;
    private bool _keyboardQueried;

    private TerminalCapabilities? Published { get; set; }

    /// <summary>Initializes one bounded negotiator.</summary>
    /// <param name="options">The non-null owned negotiation policy.</param>
    /// <param name="timeProvider">The deadline clock, or null for system time.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public Negotiator(
        NegotiationOptions options,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _tracker = new QueryTracker(options.Limits, _timeProvider);
    }

    /// <summary>Gets whether the query batch was emitted.</summary>
    public bool IsStarted { get; private set; }

    /// <summary>Gets whether one immutable profile was published.</summary>
    public bool IsComplete { get; private set; }

    /// <summary>Gets the shared response deadline after startup.</summary>
    public DateTimeOffset Deadline { get; private set; }

    /// <summary>Gets the latest redacted response-classification diagnostic.</summary>
    public Diagnostic? LastDiagnostic { get; private set; }

    /// <summary>Gets the published immutable profile.</summary>
    /// <exception cref="InvalidOperationException">Negotiation is incomplete.</exception>
    public TerminalCapabilities Capabilities => Published ??
        throw new InvalidOperationException("Negotiation has not published a profile.");

    /// <summary>Writes the complete bounded startup query batch.</summary>
    /// <param name="destination">The non-null synchronous byte destination.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The negotiator already started.</exception>
    public void Start(IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (IsStarted)
        {
            throw new InvalidOperationException("The capability negotiator already started.");
        }

        var capacity = _options.Limits.MaxConcurrentQueries;
        var queryKeyboard = capacity >= 2;
        _keyboardQueried = queryKeyboard;
        var keyboard = !queryKeyboard ||
            _tracker.TryRegister(QueryKind.Keyboard, null, out _);
        var attributes = _tracker.TryRegister(QueryKind.PrimaryAttributes, null, out _);
        Debug.Assert(
            keyboard && attributes,
            "A fresh tracker must admit the selected bounded query families.");

        if (!keyboard || !attributes)
        {
            throw new InvalidOperationException(
                "The selected query capacity could not be registered.");
        }

        IsStarted = true;
        Deadline = _timeProvider.GetUtcNow() + _options.Limits.QueryTimeout;
        var writer = new Writer(destination);

        if (queryKeyboard)
        {
            Keyboard.Query(writer);
        }

        Csi.PrimaryDeviceAttributes(writer);
        var remaining = capacity - (queryKeyboard ? 2 : 1);

        foreach (var mode in _modes.Take(remaining))
        {
            _ = _pendingModes.Add(mode);
            Csi.QueryPrivateMode(writer, mode);
        }
    }

    /// <summary>Matches one recognized response and publishes when all queries complete.</summary>
    /// <param name="response">The owned typed terminal response.</param>
    /// <returns>The active, duplicate, late, or unknown match classification.</returns>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public QueryMatch Accept(in Response response)
    {
        if (!IsStarted)
        {
            throw new InvalidOperationException("The capability negotiator has not started.");
        }

        if (response.Kind == ResponseKind.PrivateMode)
        {
            return AcceptPrivateMode(in response);
        }

        var match = _tracker.Match(response);
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

            TryPublish();
        }

        return match;
    }

    /// <summary>Publishes conservative evidence when the shared deadline elapsed.</summary>
    /// <returns>Whether this call transitioned negotiation to complete.</returns>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    public bool Expire()
    {
        if (!IsStarted)
        {
            throw new InvalidOperationException("The capability negotiator has not started.");
        }

        if (IsComplete || _timeProvider.GetUtcNow() < Deadline)
        {
            return false;
        }

        _ = _tracker.Expire();

        foreach (var mode in _pendingModes)
        {
            _ = _expiredModes.Add(mode);
        }

        _pendingModes.Clear();
        TryPublish();
        return true;
    }

    /// <summary>
    /// Publishes absent evidence immediately when the owning transport closes.
    /// </summary>
    /// <returns>Whether this call transitioned negotiation to complete.</returns>
    /// <exception cref="InvalidOperationException">The negotiator has not started.</exception>
    internal bool Complete()
    {
        if (!IsStarted)
        {
            throw new InvalidOperationException("The capability negotiator has not started.");
        }

        if (IsComplete)
        {
            return false;
        }

        foreach (var mode in _pendingModes)
        {
            _ = _expiredModes.Add(mode);
        }

        _pendingModes.Clear();
        Publish();
        return true;
    }

    private QueryMatch AcceptPrivateMode(in Response response)
    {
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
            case 2026:
                _synchronizedOutput = supported;
                break;
            case 1004:
                _focusReporting = supported;
                break;
            case 2004:
                _bracketedPaste = supported;
                break;
            case 1006:
                _cellMouse = supported;
                break;
            case 1016:
                _pixelMouse = supported;
                break;
            default:
                throw new UnreachableException("Only selected modes can be completed.");
        }
    }

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
        var queries = new Queries()
        {
            SynchronizedOutput = _synchronizedOutput,
            FocusReporting = _focusReporting,
            BracketedPaste = _bracketedPaste,
            PixelMouse = _pixelMouse,
            CellMouse = _cellMouse,
            KittyKeyboard = _kittyKeyboard,
        };
        Published = Detector.Detect(
            _options.Environment,
            queries,
            _options.Overrides);
        IsComplete = true;
    }

    private static Diagnostic CreateDiagnostic(DiagnosticCode code) =>
        new(code, SequenceKind.Csi, offset: 0, discardedBytes: 0);
}
