using SharpVision.Terminal.Protocols;

namespace SharpVision.Terminal.Capabilities;

/// <summary>
/// Identifies a bounded terminal query response family.
/// </summary>
public enum QueryKind
{
    /// <summary>Primary device attributes.</summary>
    PrimaryAttributes,

    /// <summary>Secondary device attributes.</summary>
    SecondaryAttributes,

    /// <summary>A cursor position report.</summary>
    CursorPosition,

    /// <summary>A DEC private mode report.</summary>
    PrivateMode,

    /// <summary>A default foreground color reply.</summary>
    ForegroundColor,

    /// <summary>A default background color reply.</summary>
    BackgroundColor,

    /// <summary>A correlated Kitty clipboard response.</summary>
    KittyClipboard,
}

/// <summary>
/// Identifies the outcome of matching an incoming response.
/// </summary>
public enum QueryMatch
{
    /// <summary>The response completed an active query.</summary>
    Matched,

    /// <summary>The response duplicated a recently completed query.</summary>
    Duplicate,

    /// <summary>The response followed timeout or cancellation.</summary>
    Late,

    /// <summary>No active or recent query matches the response.</summary>
    Unknown,
}

/// <summary>
/// Identifies one active query without exposing internal correlation state.
/// </summary>
/// <param name="Value">The positive tracker-local token.</param>
public readonly record struct QueryToken(long Value);

/// <summary>
/// Tracks a finite number of correlated and uncorrelated terminal queries.
/// </summary>
/// <param name="limits">Optional immutable protocol limits.</param>
/// <param name="timeProvider">Optional deterministic clock.</param>
public sealed class QueryTracker(
    Limits? limits = null,
    TimeProvider? timeProvider = null)
{
    private readonly Limits _limits = limits ?? Limits.Default;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly Dictionary<Key, Active> _active = [];
    private readonly Dictionary<long, Key> _tokens = [];
    private readonly Dictionary<Key, History> _history = [];
    private readonly Queue<Key> _historyOrder = [];
    private long _nextToken;

    /// <summary>Gets the current number of in-flight queries.</summary>
    public int ActiveCount => _active.Count;

    /// <summary>Gets the most recent non-sensitive tracking diagnostic.</summary>
    public Diagnostic? LastDiagnostic { get; private set; }

    /// <summary>Registers one query when bounds and correlation allow it.</summary>
    /// <param name="kind">The expected response family.</param>
    /// <param name="id">A required Kitty ID or null for uncorrelated families.</param>
    /// <param name="token">Receives the positive cancellation token on success.</param>
    /// <returns>Whether the query was registered.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is unknown.</exception>
    /// <exception cref="ArgumentException">The correlation ID policy is violated.</exception>
    public bool TryRegister(QueryKind kind, string? id, out QueryToken token)
    {
        Validate(kind, id);
        var now = _timeProvider.GetUtcNow();
        _ = ExpireCore(now);
        PruneHistory(now);
        var key = new Key(kind, id);

        if (_active.Count >= _limits.MaxConcurrentQueries ||
            _active.ContainsKey(key) ||
            _history.ContainsKey(key))
        {
            LastDiagnostic = CreateDiagnostic(DiagnosticCode.QueryLimit, kind);
            token = default;
            return false;
        }

        var value = checked(++_nextToken);
        token = new QueryToken(value);
        _active.Add(key, new Active(token, now + _limits.QueryTimeout));
        _tokens.Add(value, key);
        LastDiagnostic = null;
        return true;
    }

    /// <summary>Matches one response against active and recent queries.</summary>
    /// <param name="kind">The response family.</param>
    /// <param name="id">A Kitty correlation ID or null.</param>
    /// <returns>The match outcome.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is unknown.</exception>
    /// <exception cref="ArgumentException">The correlation ID policy is violated.</exception>
    public QueryMatch Match(QueryKind kind, string? id = null)
    {
        Validate(kind, id);
        var now = _timeProvider.GetUtcNow();
        _ = ExpireCore(now);
        PruneHistory(now);
        var key = new Key(kind, id);

        if (_active.Remove(key, out var active))
        {
            _ = _tokens.Remove(active.Token.Value);
            AddHistory(key, Outcome.Completed, now);
            LastDiagnostic = null;
            return QueryMatch.Matched;
        }

        if (_history.TryGetValue(key, out var history))
        {
            if (history.Outcome == Outcome.Completed)
            {
                LastDiagnostic = CreateDiagnostic(DiagnosticCode.DuplicateResponse, kind);
                return QueryMatch.Duplicate;
            }

            LastDiagnostic = CreateDiagnostic(DiagnosticCode.LateResponse, kind);
            return QueryMatch.Late;
        }

        LastDiagnostic = null;
        return QueryMatch.Unknown;
    }

    /// <summary>Cancels one active token and retains a bounded late-reply guard.</summary>
    /// <param name="token">The tracker-local token.</param>
    /// <returns>Whether an active query was cancelled.</returns>
    public bool Cancel(QueryToken token)
    {
        var now = _timeProvider.GetUtcNow();
        _ = ExpireCore(now);
        PruneHistory(now);

        if (!_tokens.Remove(token.Value, out var key) || !_active.Remove(key))
        {
            return false;
        }

        AddHistory(key, Outcome.Cancelled, now);
        return true;
    }

    /// <summary>Expires every query whose injected-clock deadline elapsed.</summary>
    /// <returns>The number of queries expired by this call.</returns>
    public int Expire()
    {
        var now = _timeProvider.GetUtcNow();
        var expired = ExpireCore(now);
        PruneHistory(now);
        return expired;
    }

    private void AddHistory(Key key, Outcome outcome, DateTimeOffset now)
    {
        while (_history.Count >= _limits.MaxConcurrentQueries && _historyOrder.Count > 0)
        {
            var oldest = _historyOrder.Dequeue();
            _ = _history.Remove(oldest);
        }

        _history[key] = new History(outcome, now + _limits.QueryTimeout);
        _historyOrder.Enqueue(key);
    }

    private int ExpireCore(DateTimeOffset now)
    {
        var expired = _active
            .Where(pair => pair.Value.Deadline <= now)
            .Select(static pair => pair.Key)
            .ToArray();

        foreach (var key in expired)
        {
            var active = _active[key];
            _ = _active.Remove(key);
            _ = _tokens.Remove(active.Token.Value);
            AddHistory(key, Outcome.TimedOut, now);
        }

        return expired.Length;
    }

    private void PruneHistory(DateTimeOffset now)
    {
        while (_historyOrder.Count > 0)
        {
            var key = _historyOrder.Peek();

            if (!_history.TryGetValue(key, out var history))
            {
                _ = _historyOrder.Dequeue();
                continue;
            }

            if (history.Until > now)
            {
                break;
            }

            _ = _historyOrder.Dequeue();
            _ = _history.Remove(key);
        }
    }

    private static Diagnostic CreateDiagnostic(DiagnosticCode code, QueryKind kind) =>
        new(
            code,
            kind == QueryKind.KittyClipboard ? SequenceKind.Osc : SequenceKind.Csi,
            0,
            0);

    private static bool IsIdentifier(string value) =>
        value.Length > 0 && value.All(static item => item is
            (>= 'a' and <= 'z') or
            (>= 'A' and <= 'Z') or
            (>= '0' and <= '9') or
            '-' or '_' or '+' or '.');

    private static void Validate(QueryKind kind, string? id)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The query kind is unknown.");
        }

        if (kind == QueryKind.KittyClipboard)
        {
            if (id is null || !IsIdentifier(id))
            {
                throw new ArgumentException(
                    "A Kitty clipboard query requires a valid correlation ID.",
                    nameof(id));
            }
        }
        else if (id is not null)
        {
            throw new ArgumentException(
                "Only Kitty clipboard queries accept correlation IDs.",
                nameof(id));
        }
    }

    private readonly record struct Key(QueryKind Kind, string? Id);

    private readonly record struct Active(QueryToken Token, DateTimeOffset Deadline);

    private readonly record struct History(Outcome Outcome, DateTimeOffset Until);

    private enum Outcome
    {
        Completed,
        Cancelled,
        TimedOut,
    }
}
