// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

using Xterm;

/// <summary>
/// Tracks a finite number of correlated, typed-identity, and uncorrelated terminal queries.
/// </summary>
[PublicAPI]
public sealed class QueryTracker
{
    private readonly QueryLimits _limits;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<Key, Active> _active = [];
    private readonly Dictionary<long, Key> _tokens = [];
    private readonly Dictionary<Key, History> _history = [];
    private readonly Queue<Key> _historyOrder = [];
    private long _nextToken;

    /// <summary>Initializes a bounded query tracker.</summary>
    /// <param name="limits">Optional immutable protocol limits.</param>
    /// <param name="timeProvider">Optional deterministic clock.</param>
    public QueryTracker(QueryLimits? limits = null, TimeProvider? timeProvider = null)
    {
        _limits = limits ?? QueryLimits.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets the current number of in-flight queries.</summary>
    public int ActiveCount => _active.Count;

    /// <summary>Gets the most recent non-sensitive tracking diagnostic.</summary>
    public Diagnostic? LastDiagnostic { get; private set; }

    /// <summary>Registers one query when bounds and correlation allow it.</summary>
    /// <param name="kind">The expected response family.</param>
    /// <param name="id">A required Kitty ID or null for ordinary uncorrelated families.</param>
    /// <param name="token">Receives the positive cancellation token on success.</param>
    /// <returns>Whether the query was registered.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is unknown.</exception>
    /// <exception cref="ArgumentException">
    /// The correlation ID policy is violated or a DCS family requires its typed overload.
    /// </exception>
    public bool TryRegister(QueryKind kind, string? id, out QueryToken token)
    {
        Validate(kind, id);
        var now = _timeProvider.GetUtcNow();
        return TryRegisterCore(kind, id, now + _limits.QueryTimeout, now, out token);
    }

    /// <summary>Registers one exact DECRQSS selector identity.</summary>
    /// <param name="name">The approved requested status selector.</param>
    /// <param name="token">Receives the positive cancellation token on success.</param>
    /// <returns>Whether the exact selector was registered.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="name"/> is unknown or undefined.</exception>
    public bool TryRegister(StatusName name, out QueryToken token)
    {
        Validate(name);
        var now = _timeProvider.GetUtcNow();
        return TryRegisterCore(
            new Key(QueryKind.StatusString, StatusId(name)),
            now + _limits.QueryTimeout,
            now,
            out token);
    }

    /// <summary>Registers one exact XTGETTCAP capability identity.</summary>
    /// <param name="name">The approved requested capability name.</param>
    /// <param name="token">Receives the positive cancellation token on success.</param>
    /// <returns>Whether the exact name was registered.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="name"/> is undefined.</exception>
    public bool TryRegister(CapabilityName name, out QueryToken token)
    {
        Validate(name);
        var now = _timeProvider.GetUtcNow();
        return TryRegisterCore(
            new Key(QueryKind.CapabilityString, CapabilityId(name)),
            now + _limits.QueryTimeout,
            now,
            out token);
    }

    /// <summary>Registers one batch query with an exact caller-owned exclusive deadline.</summary>
    /// <param name="kind">The expected response family.</param>
    /// <param name="id">A required Kitty ID or null for uncorrelated families.</param>
    /// <param name="deadline">The exact exclusive response deadline.</param>
    /// <param name="token">Receives the positive cancellation token on success.</param>
    /// <returns>Whether the query was registered.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="kind"/> is unknown or <paramref name="deadline"/> is not in the future.
    /// </exception>
    /// <exception cref="ArgumentException">The correlation ID policy is violated.</exception>
    internal bool TryRegister(
        QueryKind kind,
        string? id,
        DateTimeOffset deadline,
        out QueryToken token)
    {
        Validate(kind, id);
        var now = _timeProvider.GetUtcNow();

        return deadline <= now
            ? throw new ArgumentOutOfRangeException(
                nameof(deadline),
                deadline,
                "The exact query deadline must be in the future.")
            : TryRegisterCore(kind, id, deadline, now, out token);
    }

    /// <summary>Registers one exact DECRQSS selector with a caller-owned deadline.</summary>
    internal bool TryRegister(StatusName name, DateTimeOffset deadline, out QueryToken token)
    {
        Validate(name);
        return TryRegisterExact(
            new Key(QueryKind.StatusString, StatusId(name)),
            deadline,
            out token);
    }

    /// <summary>Registers one exact XTGETTCAP name with a caller-owned deadline.</summary>
    internal bool TryRegister(CapabilityName name, DateTimeOffset deadline, out QueryToken token)
    {
        Validate(name);
        return TryRegisterExact(
            new Key(QueryKind.CapabilityString, CapabilityId(name)),
            deadline,
            out token);
    }

    private bool TryRegisterExact(Key key, DateTimeOffset deadline, out QueryToken token)
    {
        var now = _timeProvider.GetUtcNow();
        return deadline <= now
            ? throw new ArgumentOutOfRangeException(
                nameof(deadline),
                deadline,
                "The exact query deadline must be in the future.")
            : TryRegisterCore(key, deadline, now, out token);
    }

    private bool TryRegisterCore(
        QueryKind kind,
        string? id,
        DateTimeOffset deadline,
        DateTimeOffset now,
        out QueryToken token) =>
        TryRegisterCore(new Key(kind, id), deadline, now, out token);

    private bool TryRegisterCore(
        Key key,
        DateTimeOffset deadline,
        DateTimeOffset now,
        out QueryToken token)
    {
        _ = ExpireCore(now);
        PruneHistory(now);

        if (_active.Count >= _limits.MaxConcurrentQueries ||
            _active.ContainsKey(key) ||
            _history.ContainsKey(key))
        {
            LastDiagnostic = CreateDiagnostic(DiagnosticCode.QueryLimit, key.Kind);
            token = default;
            return false;
        }

        var value = checked(++_nextToken);
        token = new QueryToken(value);
        _active.Add(key, new Active(token, deadline));
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
        => Match(kind, _timeProvider.GetUtcNow(), id);

    /// <summary>Matches one response at one caller-observed batch instant.</summary>
    /// <param name="kind">The response family.</param>
    /// <param name="now">The exact response-observation instant.</param>
    /// <param name="id">A Kitty correlation ID or null.</param>
    /// <returns>The match outcome.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is unknown.</exception>
    /// <exception cref="ArgumentException">The correlation ID policy is violated.</exception>
    internal QueryMatch Match(QueryKind kind, DateTimeOffset now, string? id = null)
    {
        Validate(kind, id);
        _ = ExpireCore(now);
        PruneHistory(now);
        return MatchCore(kind, new Key(kind, id), now);
    }

    private QueryMatch MatchCore(QueryKind kind, Key key, DateTimeOffset now)
    {

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

    /// <summary>Matches a typed CSI response and applies Kitty detection ordering.</summary>
    /// <param name="response">The recognized typed response.</param>
    /// <returns>The match outcome for <paramref name="response"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The response kind is not query-trackable.</exception>
    public QueryMatch Match(Response response) =>
        Match(response, _timeProvider.GetUtcNow());

    /// <summary>Matches one typed CSI response at one caller-observed batch instant.</summary>
    /// <param name="response">The recognized typed response.</param>
    /// <param name="now">The exact response-observation instant.</param>
    /// <returns>The match outcome for <paramref name="response"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The response kind is not query-trackable.</exception>
    internal QueryMatch Match(Response response, DateTimeOffset now)
    {
        var kind = response.Kind switch
        {
            ResponseKind.PrimaryAttributes => QueryKind.PrimaryAttributes,
            ResponseKind.SecondaryAttributes => QueryKind.SecondaryAttributes,
            ResponseKind.CursorPosition => QueryKind.CursorPosition,
            ResponseKind.PrivateMode => QueryKind.PrivateMode,
            ResponseKind.ForegroundColor => QueryKind.ForegroundColor,
            ResponseKind.BackgroundColor => QueryKind.BackgroundColor,
            ResponseKind.Keyboard => QueryKind.Keyboard,
            ResponseKind.ModifyOtherKeys => QueryKind.ModifyOtherKeys,
            ResponseKind.PaletteColor or
                ResponseKind.WindowPixels or
                ResponseKind.CellPixels or
                ResponseKind.WindowCells => throw new ArgumentOutOfRangeException(
                    nameof(response),
                    response,
                    "The response requires its typed tracking overload."),
            ResponseKind.None => throw new ArgumentOutOfRangeException(
                nameof(response),
                response,
                "An unrecognized response cannot be tracked."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(response),
                response,
                "The response kind is not query-trackable.")
        };
        _ = ExpireCore(now);
        PruneHistory(now);
        var keyboardUnsupported = kind == QueryKind.PrimaryAttributes &&
                                  CompleteUnsupported(QueryKind.Keyboard, now);
        var graphicsUnsupported = kind == QueryKind.PrimaryAttributes &&
                                  CompleteUnsupportedFamily(QueryKind.KittyGraphics, now);
        var result = MatchCore(kind, new Key(kind, null), now);

        if (graphicsUnsupported)
        {
            LastDiagnostic = CreateDiagnostic(DiagnosticCode.Unsupported, QueryKind.KittyGraphics);
        }
        else if (keyboardUnsupported)
        {
            LastDiagnostic = CreateDiagnostic(DiagnosticCode.Unsupported, QueryKind.Keyboard);
        }

        return result;
    }

    /// <summary>Matches one typed DECRQSS response against its active family.</summary>
    /// <param name="response">The non-empty owned response.</param>
    /// <returns>The match classification.</returns>
    /// <exception cref="ArgumentException"><paramref name="response"/> is empty.</exception>
    public QueryMatch Match(in StatusResponse response) =>
        Match(in response, _timeProvider.GetUtcNow());

    /// <summary>Matches one exact DECRQSS identity at a caller-observed instant.</summary>
    internal QueryMatch Match(in StatusResponse response, DateTimeOffset now)
    {
        if (response.IsEmpty)
        {
            throw new ArgumentException("The response cannot be empty.", nameof(response));
        }

        _ = ExpireCore(now);
        PruneHistory(now);
        return !response.IsValid || response.Name == StatusName.Unknown
            ? Unknown()
            : MatchCore(
                QueryKind.StatusString,
                new Key(QueryKind.StatusString, StatusId(response.Name)),
                now);
    }

    /// <summary>Matches one typed XTGETTCAP response against its active family.</summary>
    /// <param name="response">The non-null owned response.</param>
    /// <returns>The match classification.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    public QueryMatch Match(CapabilityResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return Match(response, _timeProvider.GetUtcNow());
    }

    /// <summary>Matches one exact XTGETTCAP identity at a caller-observed instant.</summary>
    internal QueryMatch Match(CapabilityResponse response, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(response);
        _ = ExpireCore(now);
        PruneHistory(now);

        if (!response.IsValid || response.Items.Count != 1)
        {
            return Unknown();
        }

        var name = response.Items.Keys.Single();
        return MatchCore(
            QueryKind.CapabilityString,
            new Key(QueryKind.CapabilityString, CapabilityId(name)),
            now);
    }

    private QueryMatch Unknown()
    {
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

    /// <summary>Expires every active family at its owning batch's shared deadline.</summary>
    /// <returns>The number of active queries retired by this call.</returns>
    internal int ExpireAll() => ExpireAll(_timeProvider.GetUtcNow());

    /// <summary>Expires every active family at one caller-observed instant.</summary>
    /// <param name="now">The shared batch-expiration instant.</param>
    /// <returns>The number of active queries retired by this call.</returns>
    internal int ExpireAll(DateTimeOffset now)
    {
        Key[] expired = [.. _active.Keys];

        foreach (var key in expired)
        {
            var active = _active[key];
            _ = _active.Remove(key);
            _ = _tokens.Remove(active.Token.Value);
            AddHistory(key, Outcome.TimedOut, now);
        }

        PruneHistory(now);
        return expired.Length;
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

    private bool CompleteUnsupported(QueryKind kind, DateTimeOffset now)
    {
        var key = new Key(kind, null);

        if (!_active.Remove(key, out var active))
        {
            return false;
        }

        _ = _tokens.Remove(active.Token.Value);
        AddHistory(key, Outcome.Unsupported, now);
        return true;
    }

    private bool CompleteUnsupportedFamily(QueryKind kind, DateTimeOffset now)
    {
        var key = _active.Keys.FirstOrDefault(candidate => candidate.Kind == kind);

        if (key.Kind != kind || !_active.Remove(key, out var active))
        {
            return false;
        }

        _ = _tokens.Remove(active.Token.Value);
        AddHistory(key, Outcome.Unsupported, now);
        return true;
    }

    private int ExpireCore(DateTimeOffset now)
    {
        Key[] expired =
        [
            .. _active
                .Where(pair => pair.Value.Deadline <= now)
                .Select(static pair => pair.Key)
        ];

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
            kind is QueryKind.KittyClipboard or
                QueryKind.PaletteColor or
                QueryKind.ForegroundColor or
                QueryKind.BackgroundColor
                ? SequenceKind.Osc
                : kind == QueryKind.KittyGraphics
                    ? SequenceKind.Apc
                : kind is QueryKind.StatusString or QueryKind.CapabilityString
                    ? SequenceKind.Dcs
                    : SequenceKind.Csi,
            0,
            0);

    private static bool IsIdentifier(string value) =>
        value.Length > 0 && value.All(static item => item is
            (>= 'a' and <= 'z') or
            (>= 'A' and <= 'Z') or
            (>= '0' and <= '9') or
            '-' or '_' or '+' or '.');

    private static bool IsGraphicsIdentifier(string value)
    {
        if (value.Length == 0 || value[0] == '0')
        {
            return false;
        }

        uint result = 0;

        foreach (var item in value)
        {
            if (item is < '0' or > '9')
            {
                return false;
            }

            var digit = (uint) (item - '0');

            if (result > (uint.MaxValue - digit) / 10)
            {
                return false;
            }

            result = (result * 10) + digit;
        }

        return result != 0;
    }

    private static void Validate(QueryKind kind, string? id)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The query kind is unknown.");
        }

        if (kind is QueryKind.StatusString or QueryKind.CapabilityString)
        {
            throw new ArgumentException(
                "DECRQSS and XTGETTCAP queries require a typed selector registration.",
                nameof(kind));
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
        else if (kind == QueryKind.KittyGraphics)
        {
            if (id is null || !IsGraphicsIdentifier(id))
            {
                throw new ArgumentException(
                    "A Kitty graphics query requires a canonical numeric correlation ID.",
                    nameof(id));
            }
        }
        else if (id is not null)
        {
            throw new ArgumentException(
                "Only Kitty clipboard and graphics queries accept correlation IDs.",
                nameof(id));
        }
    }

    private static void Validate(StatusName name)
    {
        if (!Enum.IsDefined(name) || name == StatusName.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(name), name, "The status selector is not queryable.");
        }
    }

    private static void Validate(CapabilityName name)
    {
        if (!Enum.IsDefined(name))
        {
            throw new ArgumentOutOfRangeException(nameof(name), name, "The capability name is undefined.");
        }
    }

    private static string StatusId(StatusName name) => name switch
    {
        StatusName.Rendition => "status:rendition",
        StatusName.CursorStyle => "status:cursor-style",
        StatusName.VerticalMargins => "status:vertical-margins",
        StatusName.HorizontalMargins => "status:horizontal-margins",
        StatusName.ModifyOtherKeys => "status:modify-other-keys",
        StatusName.FormatOtherKeys => "status:format-other-keys",
        StatusName.Unknown => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown status is not queryable."),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "The status selector is undefined.")
    };

    private static string CapabilityId(CapabilityName name) => name switch
    {
        CapabilityName.Colors => "capability:colors",
        CapabilityName.TerminalName => "capability:terminal-name",
        CapabilityName.DirectColor => "capability:direct-color",
        CapabilityName.Backspace => "capability:backspace",
        CapabilityName.Enter => "capability:enter",
        CapabilityName.Up => "capability:up",
        CapabilityName.Down => "capability:down",
        CapabilityName.Left => "capability:left",
        CapabilityName.Right => "capability:right",
        CapabilityName.Home => "capability:home",
        CapabilityName.End => "capability:end",
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "The capability name is undefined.")
    };
}
