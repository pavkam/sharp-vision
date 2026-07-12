using System.Buffers;
using System.Diagnostics;

using SharpVision.Terminal.Protocols;

namespace SharpVision.Terminal.Capabilities;

/// <summary>Coordinates one bounded terminal capability query batch.</summary>
public sealed class Negotiator
{
    private static readonly int[] _modes = [2026, 1004, 2004, 1006, 1016];

    private readonly NegotiationOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly QueryTracker _tracker;
    private readonly HashSet<int> _pendingModes = [];

    private Capabilities? Published { get; set; }

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

    /// <summary>Gets the published immutable profile.</summary>
    /// <exception cref="InvalidOperationException">Negotiation is incomplete.</exception>
    public Capabilities Capabilities => Published ??
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
}
