// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Clipboard;

using SharpVision.Terminal.Capabilities;
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
public sealed class KittyClipboardTransaction: IDisposable
{
    private const int _chunkBytes = 4_096;

    private readonly TransferLimits _limits;
    private readonly KittyClipboardOperation _operation;
    private readonly string? _id;
    private readonly bool _listOnly;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, Builder> _builders = [];
    private readonly List<string> _mimeOrder = [];
    private readonly HashSet<string> _closedMimes = [];
    private string? _currentMime;
    private int _totalBytes;

    private KittyClipboardTransaction(
        KittyClipboardOperation operation,
        TransferLimits? limits,
        string? id,
        bool listOnly,
        TimeProvider? timeProvider,
        QueryLimits? queryLimits)
    {
        if (operation is not (KittyClipboardOperation.Read or KittyClipboardOperation.Write))
        {
            throw new ArgumentOutOfRangeException(
                nameof(operation), operation, "A transaction must read or write.");
        }

        if (id is not null && !id.IsIdentifier())
        {
            throw new ArgumentException(
                "A transaction ID contains a forbidden character.",
                nameof(id));
        }

        _operation = operation;
        _limits = limits ?? TransferLimits.Default;
        _id = id;
        _listOnly = listOnly;
        _timeProvider = timeProvider ?? TimeProvider.System;
        // TransferLimits (deliberately scoped to MaxClipboardBytes/MaxMetadataBytes) has no
        // timeout of its own; the deadline is bound by the same QueryLimits used for capability
        // queries.
        Deadline = _timeProvider.GetUtcNow() + (queryLimits ?? QueryLimits.Default).QueryTimeout;
    }

    /// <summary>Gets the current lifecycle state.</summary>
    public KittyClipboardTransactionState State { get; private set; }

    /// <summary>Gets the immutable deadline calculated at construction.</summary>
    public DateTimeOffset Deadline { get; }

    /// <summary>Gets the transferred result after successful completion.</summary>
    public KittyClipboardResult? Result { get; private set; }

    /// <summary>Gets a terminal error status after failure.</summary>
    public KittyClipboardReplyStatus Failure { get; private set; }

    /// <summary>Gets a redacted local protocol diagnostic after failure.</summary>
    public Diagnostic? Diagnostic { get; private set; }

    /// <summary>Creates a bounded clipboard read transaction.</summary>
    /// <param name="limits">Optional immutable transfer limits.</param>
    /// <param name="id">Optional sanitized correlation identifier.</param>
    /// <param name="listOnly">Whether DATA without a MIME field contains a MIME list.</param>
    /// <param name="timeProvider">Optional deterministic clock.</param>
    /// <param name="queryLimits">Optional immutable query limits bounding the transaction deadline.</param>
    /// <returns>The new read transaction.</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> is invalid.</exception>
    public static KittyClipboardTransaction Read(
        TransferLimits? limits = null,
        string? id = null,
        bool listOnly = false,
        TimeProvider? timeProvider = null,
        QueryLimits? queryLimits = null) =>
        new(KittyClipboardOperation.Read, limits, id, listOnly, timeProvider, queryLimits);

    /// <summary>Creates a bounded clipboard write transaction.</summary>
    /// <param name="limits">Optional immutable transfer limits.</param>
    /// <param name="id">Optional sanitized correlation identifier.</param>
    /// <param name="timeProvider">Optional deterministic clock.</param>
    /// <param name="queryLimits">Optional immutable query limits bounding the transaction deadline.</param>
    /// <returns>The new write transaction.</returns>
    /// <exception cref="ArgumentException"><paramref name="id"/> is invalid.</exception>
    public static KittyClipboardTransaction Write(
        TransferLimits? limits = null,
        string? id = null,
        TimeProvider? timeProvider = null,
        QueryLimits? queryLimits = null) =>
        new(KittyClipboardOperation.Write, limits, id, listOnly: false, timeProvider, queryLimits);

    /// <summary>Applies one decoded packet to this transaction.</summary>
    /// <param name="packet">The immutable decoded packet.</param>
    /// <returns>How the packet affected the transaction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="packet"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The transaction is disposed.</exception>
    public KittyClipboardAcceptResult Accept(KittyClipboardPacket packet)
    {
        ObjectDisposedException.ThrowIf(State == KittyClipboardTransactionState.Disposed, this);
        ArgumentNullException.ThrowIfNull(packet);

        if (IsTerminal)
        {
            return KittyClipboardAcceptResult.Ignored;
        }

        if (CheckTimeout())
        {
            return KittyClipboardAcceptResult.Ignored;
        }

        // Correlation is checked before validity so that unrelated malformed
        // traffic cannot fail an ID-bound transaction: a structural failure
        // whose id was already parsed before the error is attributed exactly
        // like a valid packet; one with no recoverable id never matches a
        // bound transaction and is ignored rather than treated as ours.
        if (!string.Equals(_id, packet.Id, StringComparison.Ordinal))
        {
            return KittyClipboardAcceptResult.Ignored;
        }

        if (!packet.Valid)
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

        return _operation == KittyClipboardOperation.Read
            ? AcceptRead(packet)
            : AcceptWrite(packet);
    }

    /// <summary>Cancels an active transaction and clears temporary data.</summary>
    /// <exception cref="ObjectDisposedException">The transaction is disposed.</exception>
    public void Cancel()
    {
        ObjectDisposedException.ThrowIf(State == KittyClipboardTransactionState.Disposed, this);

        if (IsTerminal)
        {
            return;
        }

        ClearBuilders();
        State = KittyClipboardTransactionState.Cancelled;
    }

    /// <summary>Checks the injected clock and times out an expired transaction.</summary>
    /// <returns><see langword="true"/> when this call caused timeout.</returns>
    /// <exception cref="ObjectDisposedException">The transaction is disposed.</exception>
    public bool CheckTimeout()
    {
        ObjectDisposedException.ThrowIf(State == KittyClipboardTransactionState.Disposed, this);

        if (IsTerminal || _timeProvider.GetUtcNow() < Deadline)
        {
            return false;
        }

        ClearBuilders();
        State = KittyClipboardTransactionState.TimedOut;
        return true;
    }

    /// <summary>Clears temporary data and makes further use invalid.</summary>
    public void Dispose()
    {
        if (State == KittyClipboardTransactionState.Disposed)
        {
            return;
        }

        ClearBuilders();
        State = KittyClipboardTransactionState.Disposed;
    }

    /// <summary>Returns a structural description without ID, MIME, or data.</summary>
    /// <returns>A redacted transaction description.</returns>
    public override string ToString() =>
        $"KittyClipboardTransaction operation={_operation} state={State} bytes={_totalBytes}";

    private bool IsTerminal => State is
        KittyClipboardTransactionState.Completed or
        KittyClipboardTransactionState.Failed or
        KittyClipboardTransactionState.Cancelled or
        KittyClipboardTransactionState.TimedOut or
        KittyClipboardTransactionState.Disposed;

    private KittyClipboardAcceptResult AcceptData(KittyClipboardPacket packet)
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
        State = KittyClipboardTransactionState.Receiving;

        return KittyClipboardAcceptResult.Accepted;
    }

    private KittyClipboardAcceptResult AcceptRead(KittyClipboardPacket packet)
    {
        if (packet.ReplyStatus == KittyClipboardReplyStatus.Ok)
        {
            if (State != KittyClipboardTransactionState.Created)
            {
                return Fail(Unexpected());
            }

            State = KittyClipboardTransactionState.Accepted;
            return KittyClipboardAcceptResult.Accepted;
        }

        return packet.ReplyStatus == KittyClipboardReplyStatus.Data
            ? State is KittyClipboardTransactionState.Accepted or KittyClipboardTransactionState.Receiving
                ? AcceptData(packet)
                : Fail(Unexpected())
            : packet.ReplyStatus == KittyClipboardReplyStatus.Done &&
              State is KittyClipboardTransactionState.Accepted or KittyClipboardTransactionState.Receiving
                ? Complete()
                : Fail(Unexpected());
    }

    private KittyClipboardAcceptResult AcceptWrite(KittyClipboardPacket packet) =>
        packet.ReplyStatus == KittyClipboardReplyStatus.Done && State == KittyClipboardTransactionState.Created
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

    private KittyClipboardAcceptResult Complete()
    {
        Debug.Assert(!IsTerminal, "Only an active transaction can complete.");

        var items = new KittyClipboardMimeData[_mimeOrder.Count];

        for (var index = 0; index < _mimeOrder.Count; index++)
        {
            var mime = _mimeOrder[index];
            items[index] = new KittyClipboardMimeData(mime, _builders[mime].ToArray());
        }

        ClearBuilders();
        Result = new KittyClipboardResult(items);
        State = KittyClipboardTransactionState.Completed;

        return KittyClipboardAcceptResult.Completed;
    }

    private KittyClipboardAcceptResult Fail(Diagnostic diagnostic)
    {
        ClearBuilders();
        Diagnostic = diagnostic;
        State = KittyClipboardTransactionState.Failed;

        return KittyClipboardAcceptResult.Failed;
    }

    private static bool IsError(KittyClipboardReplyStatus status) => status is
        KittyClipboardReplyStatus.Io or
        KittyClipboardReplyStatus.Invalid or
        KittyClipboardReplyStatus.Unavailable or
        KittyClipboardReplyStatus.Denied or
        KittyClipboardReplyStatus.Busy;


    private static Diagnostic Unexpected() =>
        new(DiagnosticCode.UnexpectedPacket, SequenceKind.Osc, 0, 0);
}
