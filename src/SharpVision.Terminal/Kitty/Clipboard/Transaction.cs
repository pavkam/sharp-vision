// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Clipboard;

using SharpVision.Terminal.Clipboard;

/// <summary>
/// Enforces bounded Kitty OSC 5522 read or write response ordering.
/// </summary>
/// <remarks>
/// Instances are single-threaded. Matching uses the optional sanitized ID;
/// unrelated packets are ignored. A successful result transfers owned data to
/// <see cref="Result"/>, which the caller must dispose.
/// </remarks>
[PublicAPI]
public sealed class Transaction: IDisposable
{
    private const int _chunkBytes = 4_096;

    private readonly Limits _limits;
    private readonly Operation _operation;
    private readonly string? _id;
    private readonly bool _listOnly;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, Builder> _builders = [];
    private readonly List<string> _mimeOrder = [];
    private readonly HashSet<string> _closedMimes = [];
    private string? _currentMime;
    private int _totalBytes;

    private Transaction(
        Operation operation,
        Limits? limits,
        string? id,
        bool listOnly,
        TimeProvider? timeProvider)
    {
        if (operation is not (Operation.Read or Operation.Write))
        {
            throw new ArgumentOutOfRangeException(
                nameof(operation), operation, "A transaction must read or write.");
        }

        if (id is not null && !IsIdentifier(id))
        {
            throw new ArgumentException(
                "A transaction ID contains a forbidden character.",
                nameof(id));
        }

        _operation = operation;
        _limits = limits ?? Limits.Default;
        _id = id;
        _listOnly = listOnly;
        _timeProvider = timeProvider ?? TimeProvider.System;
        Deadline = _timeProvider.GetUtcNow() + _limits.QueryTimeout;
    }

    /// <summary>Gets the current lifecycle state.</summary>
    public TransactionState State { get; private set; }

    /// <summary>Gets the immutable deadline calculated at construction.</summary>
    public DateTimeOffset Deadline { get; }

    /// <summary>Gets the transferred result after successful completion.</summary>
    public Result? Result { get; private set; }

    /// <summary>Gets a terminal error status after failure.</summary>
    public ReplyStatus Failure { get; private set; }

    /// <summary>Gets a redacted local protocol diagnostic after failure.</summary>
    public Diagnostic? Diagnostic { get; private set; }

    /// <summary>Creates a bounded clipboard read transaction.</summary>
    /// <param name="limits">Optional immutable protocol limits.</param>
    /// <param name="id">Optional sanitized correlation identifier.</param>
    /// <param name="listOnly">Whether DATA without a MIME field contains a MIME list.</param>
    /// <param name="timeProvider">Optional deterministic clock.</param>
    /// <returns>The new read transaction.</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> is invalid.</exception>
    public static Transaction Read(
        Limits? limits = null,
        string? id = null,
        bool listOnly = false,
        TimeProvider? timeProvider = null) =>
        new(Operation.Read, limits, id, listOnly, timeProvider);

    /// <summary>Creates a bounded clipboard write transaction.</summary>
    /// <param name="limits">Optional immutable protocol limits.</param>
    /// <param name="id">Optional sanitized correlation identifier.</param>
    /// <param name="timeProvider">Optional deterministic clock.</param>
    /// <returns>The new write transaction.</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> is invalid.</exception>
    public static Transaction Write(
        Limits? limits = null,
        string? id = null,
        TimeProvider? timeProvider = null) =>
        new(Operation.Write, limits, id, listOnly: false, timeProvider);

    /// <summary>Applies one decoded packet to this transaction.</summary>
    /// <param name="packet">The immutable decoded packet.</param>
    /// <returns>How the packet affected the transaction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="packet"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The transaction is disposed.</exception>
    public AcceptResult Accept(Packet packet)
    {
        ObjectDisposedException.ThrowIf(State == TransactionState.Disposed, this);
        ArgumentNullException.ThrowIfNull(packet);

        if (IsTerminal)
        {
            return AcceptResult.Ignored;
        }

        if (CheckTimeout())
        {
            return AcceptResult.Ignored;
        }

        // Correlation is checked before validity so that unrelated malformed
        // traffic cannot fail an ID-bound transaction: a structural failure
        // whose id was already parsed before the error is attributed exactly
        // like a valid packet; one with no recoverable id never matches a
        // bound transaction and is ignored rather than treated as ours.
        if (!string.Equals(_id, packet.Id, StringComparison.Ordinal))
        {
            return AcceptResult.Ignored;
        }

        if (!packet.IsValid)
        {
            return Fail(packet.Diagnostic ?? Unexpected());
        }

        if (packet.Operation != _operation)
        {
            return Fail(Unexpected());
        }

        if (IsError(packet.ReplyStatus))
        {
            Failure = packet.ReplyStatus;
            return Fail(Unexpected());
        }

        return _operation == Operation.Read
            ? AcceptRead(packet)
            : AcceptWrite(packet);
    }

    /// <summary>Cancels an active transaction and clears temporary data.</summary>
    /// <exception cref="ObjectDisposedException">The transaction is disposed.</exception>
    public void Cancel()
    {
        ObjectDisposedException.ThrowIf(State == TransactionState.Disposed, this);

        if (IsTerminal)
        {
            return;
        }

        ClearBuilders();
        State = TransactionState.Cancelled;
    }

    /// <summary>Checks the injected clock and times out an expired transaction.</summary>
    /// <returns><see langword="true"/> when this call caused timeout.</returns>
    /// <exception cref="ObjectDisposedException">The transaction is disposed.</exception>
    public bool CheckTimeout()
    {
        ObjectDisposedException.ThrowIf(State == TransactionState.Disposed, this);

        if (IsTerminal || _timeProvider.GetUtcNow() < Deadline)
        {
            return false;
        }

        ClearBuilders();
        State = TransactionState.TimedOut;
        return true;
    }

    /// <summary>Clears temporary data and makes further use invalid.</summary>
    public void Dispose()
    {
        if (State == TransactionState.Disposed)
        {
            return;
        }

        ClearBuilders();
        State = TransactionState.Disposed;
    }

    /// <summary>Returns a structural description without ID, MIME, or data.</summary>
    /// <returns>A redacted transaction description.</returns>
    public override string ToString() =>
        $"Transaction operation={_operation} state={State} bytes={_totalBytes}";

    private bool IsTerminal => State is
        TransactionState.Completed or
        TransactionState.Failed or
        TransactionState.Cancelled or
        TransactionState.TimedOut or
        TransactionState.Disposed;

    private AcceptResult AcceptData(Packet packet)
    {
        if (packet.Data.Length > _chunkBytes)
        {
            return Fail(new Diagnostic(
                DiagnosticCode.StringLimit,
                SequenceKind.Osc,
                0,
                packet.Data.Length));
        }

        string mime;

        if (packet.Mime.IsEmpty)
        {
            if (!_listOnly)
            {
                return Fail(Unexpected());
            }

            mime = string.Empty;
        }
        else
        {
            mime = Encoding.UTF8.GetString(packet.Mime.Span);
        }

        if (_currentMime is not null && !string.Equals(_currentMime, mime, StringComparison.Ordinal))
        {
            _ = _closedMimes.Add(_currentMime);
        }

        if (_closedMimes.Contains(mime))
        {
            return Fail(Unexpected());
        }

        if (packet.Data.Length > _limits.MaxClipboardBytes - _totalBytes)
        {
            return Fail(new Diagnostic(
                DiagnosticCode.StringLimit,
                SequenceKind.Osc,
                0,
                packet.Data.Length));
        }

        if (!_builders.TryGetValue(mime, out var builder))
        {
            builder = new Builder();
            _builders.Add(mime, builder);
            _mimeOrder.Add(mime);
        }

        builder.Append(packet.Data.Span);
        _currentMime = mime;
        _totalBytes += packet.Data.Length;
        State = TransactionState.Receiving;

        return AcceptResult.Accepted;
    }

    private AcceptResult AcceptRead(Packet packet)
    {
        if (packet.ReplyStatus == ReplyStatus.Ok)
        {
            if (State != TransactionState.Created)
            {
                return Fail(Unexpected());
            }

            State = TransactionState.Accepted;
            return AcceptResult.Accepted;
        }

        return packet.ReplyStatus == ReplyStatus.Data
            ? State is TransactionState.Accepted or TransactionState.Receiving
                ? AcceptData(packet)
                : Fail(Unexpected())
            : packet.ReplyStatus == ReplyStatus.Done &&
              State is TransactionState.Accepted or TransactionState.Receiving
                ? Complete()
                : Fail(Unexpected());
    }

    private AcceptResult AcceptWrite(Packet packet) =>
        packet.ReplyStatus == ReplyStatus.Done && State == TransactionState.Created
            ? Complete()
            : Fail(Unexpected());

    private void ClearBuilders()
    {
        foreach (var builder in _builders.Values)
        {
            builder.Dispose();
        }

        _builders.Clear();
        _mimeOrder.Clear();
        _closedMimes.Clear();
        _currentMime = null;
        _totalBytes = 0;
    }

    private AcceptResult Complete()
    {
        Debug.Assert(!IsTerminal, "Only an active transaction can complete.");

        var items = new MimeData[_mimeOrder.Count];

        for (var index = 0; index < _mimeOrder.Count; index++)
        {
            var mime = _mimeOrder[index];
            items[index] = new MimeData(mime, _builders[mime].ToArray());
        }

        ClearBuilders();
        Result = new Result(items);
        State = TransactionState.Completed;

        return AcceptResult.Completed;
    }

    private AcceptResult Fail(Diagnostic diagnostic)
    {
        ClearBuilders();
        Diagnostic = diagnostic;
        State = TransactionState.Failed;

        return AcceptResult.Failed;
    }

    private static bool IsError(ReplyStatus status) => status is
        ReplyStatus.Io or
        ReplyStatus.Invalid or
        ReplyStatus.Unavailable or
        ReplyStatus.Denied or
        ReplyStatus.Busy;

    private static bool IsIdentifier(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (var item in value)
        {
            if (item is not (
                (>= 'a' and <= 'z') or
                (>= 'A' and <= 'Z') or
                (>= '0' and <= '9') or
                '-' or '_' or '+' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    private static Diagnostic Unexpected() =>
        new(DiagnosticCode.UnexpectedPacket, SequenceKind.Osc, 0, 0);
}
