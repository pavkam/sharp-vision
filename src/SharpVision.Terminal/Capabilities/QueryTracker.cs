// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

using SharpVision.Terminal.Protocols;

/// <summary>
/// Tracks a finite number of correlated and uncorrelated terminal queries.
/// </summary>
public sealed class QueryTracker
{
    private readonly Limits _limits;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<Key, Active> _active = [];
    private readonly Dictionary<long, Key> _tokens = [];
    private readonly Dictionary<Key, History> _history = [];
    private readonly Queue<Key> _historyOrder = [];
    private long _nextToken;

    /// <summary>Initializes a bounded query tracker.</summary>
    /// <param name="limits">Optional immutable protocol limits.</param>
    /// <param name="timeProvider">Optional deterministic clock.</param>
    public QueryTracker(Limits? limits = null, TimeProvider? timeProvider = null)
    {
        _limits = limits ?? Limits.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

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
        DateTimeOffset now = _timeProvider.GetUtcNow();
        _ = ExpireCore(now);
        PruneHistory(now);
        Key key = new(kind, id);

        if (_active.Count >= _limits.MaxConcurrentQueries ||
            _active.ContainsKey(key) ||
            _history.ContainsKey(key))
        {
            LastDiagnostic = CreateDiagnostic(DiagnosticCode.QueryLimit, kind);
            token = default;
            return false;
        }

        long value = checked(++_nextToken);
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
        DateTimeOffset now = _timeProvider.GetUtcNow();
        _ = ExpireCore(now);
        PruneHistory(now);
        Key key = new(kind, id);

        if (_active.Remove(key, out Active active))
        {
            _ = _tokens.Remove(active.Token.Value);
            AddHistory(key, Outcome.Completed, now);
            LastDiagnostic = null;
            return QueryMatch.Matched;
        }

        if (_history.TryGetValue(key, out History history))
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

    /// <summary>Matches a typed CSI response and applies Kitty detection ordering.</summary>
    /// <param name="response">The recognized typed response.</param>
    /// <returns>The match outcome for <paramref name="response"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The response kind is not query-trackable.</exception>
    public QueryMatch Match(Response response)
    {
        QueryKind kind = response.Kind switch
        {
            ResponseKind.PrimaryAttributes => QueryKind.PrimaryAttributes,
            ResponseKind.SecondaryAttributes => QueryKind.SecondaryAttributes,
            ResponseKind.CursorPosition => QueryKind.CursorPosition,
            ResponseKind.PrivateMode => QueryKind.PrivateMode,
            ResponseKind.ForegroundColor => QueryKind.ForegroundColor,
            ResponseKind.BackgroundColor => QueryKind.BackgroundColor,
            ResponseKind.Keyboard => QueryKind.Keyboard,
            ResponseKind.None => throw new ArgumentOutOfRangeException(
                nameof(response),
                response,
                "An unrecognized response cannot be tracked."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(response),
                response,
                "The response kind is not query-trackable."),
        };
        bool keyboardUnsupported = kind == QueryKind.PrimaryAttributes &&
            CompleteUnsupported(QueryKind.Keyboard);
        QueryMatch result = Match(kind);

        if (keyboardUnsupported)
        {
            LastDiagnostic = CreateDiagnostic(DiagnosticCode.Unsupported, QueryKind.Keyboard);
        }

        return result;
    }

    /// <summary>Cancels one active token and retains a bounded late-reply guard.</summary>
    /// <param name="token">The tracker-local token.</param>
    /// <returns>Whether an active query was cancelled.</returns>
    public bool Cancel(QueryToken token)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        _ = ExpireCore(now);
        PruneHistory(now);

        if (!_tokens.Remove(token.Value, out Key key) || !_active.Remove(key))
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
        DateTimeOffset now = _timeProvider.GetUtcNow();
        int expired = ExpireCore(now);
        PruneHistory(now);
        return expired;
    }

    private void AddHistory(Key key, Outcome outcome, DateTimeOffset now)
    {
        while (_history.Count >= _limits.MaxConcurrentQueries && _historyOrder.Count > 0)
        {
            Key oldest = _historyOrder.Dequeue();
            _ = _history.Remove(oldest);
        }

        _history[key] = new History(outcome, now + _limits.QueryTimeout);
        _historyOrder.Enqueue(key);
    }

    private bool CompleteUnsupported(QueryKind kind)
    {
        Key key = new(kind, null);

        if (!_active.Remove(key, out Active active))
        {
            return false;
        }

        _ = _tokens.Remove(active.Token.Value);
        AddHistory(key, Outcome.Unsupported, _timeProvider.GetUtcNow());
        return true;
    }

    private int ExpireCore(DateTimeOffset now)
    {
        Key[] expired = [.. _active
            .Where(pair => pair.Value.Deadline <= now)
            .Select(static pair => pair.Key)];

        foreach (Key key in expired)
        {
            Active active = _active[key];
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
            Key key = _historyOrder.Peek();

            if (!_history.TryGetValue(key, out History history))
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

}
