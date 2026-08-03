// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using Terminal.Capabilities;
using Terminal.Clipboard;
using Terminal.Kitty.Clipboard;

using KittyClipboardWriter = Terminal.Kitty.Clipboard.KittyClipboardWriter;

/// <summary>Encodes implemented output protocols and posts them through the ordered write path.</summary>
internal sealed class TerminalServices: ITerminalServices, IBell, IClipboard
{
    // Kitty OSC 5522 metadata reserves ':' and ';' as field and payload separators, so a MIME
    // type cannot carry a ";charset=" parameter the way OSC 52 or HTTP would.
    private const string _textMime = "text/plain";
    private readonly Application _application;
    private readonly Lock _programGate = new();
    private ProgramExpander? _expander;
    private Transaction? _kittyTransaction;
    private DispatcherTimer? _kittyTimeoutTimer;
    private Selection _kittyTransactionSelection;
    private int _kittyIdSequence;
    private bool _pendingOsc52Request;
    private Selection _pendingOsc52Selection;

    public TerminalServices(Application application)
    {
        Debug.Assert(application is not null, "The owning application must be provided.");
        _application = application;
    }

    /// <inheritdoc/>
    public event EventHandler<KittyClipboardReplyEventArgs>? KittyClipboardReplyReceived;

    /// <inheritdoc/>
    public IBell Bell => this;

    /// <inheritdoc/>
    public Description Description => _application.TerminalProfile.Description;

    /// <inheritdoc/>
    public IClipboard Clipboard => this;

    /// <inheritdoc/>
    public bool IsTitleSupported
    {
        get
        {
            if (UsesAnsiTitle)
            {
                return true;
            }

            lock (_programGate)
            {
                return Expander().HasPair("TS", "fsl");
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Reports authoritative evidence for either clipboard protocol: Kitty OSC 5522 or OSC 52.
    /// Environment and default evidence never authorize clipboard output.
    /// </remarks>
    bool IClipboard.IsSupported =>
        _application.Capabilities.KittyClipboard.IsAuthoritative ||
        _application.Capabilities.Osc52.IsAuthoritative;

    /// <inheritdoc/>
    bool IBell.IsSupported
    {
        get
        {
            lock (_programGate)
            {
                return Expander().Has("bel");
            }
        }
    }

    /// <inheritdoc/>
    public void Ring()
    {
        var destination = new ArrayBufferWriter<byte>();

        lock (_programGate)
        {
            if (!Expander().TryWrite("bel", [], destination))
            {
                return;
            }
        }

        _application.PostOutOfBand(destination.WrittenMemory);
    }

    /// <inheritdoc/>
    public void SetTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        if (!IsTitleSupported)
        {
            return;
        }

        var byteCount = Encoding.UTF8.GetByteCount(title);
        var destination = new ArrayBufferWriter<byte>(byteCount + 8);
        var rented = ArrayPool<byte>.Shared.Rent(Math.Max(1, byteCount));

        try
        {
            var written = Encoding.UTF8.GetBytes(title, rented);
            Osc.Title(new ProtocolWriter(destination), rented.AsSpan(0, written));

            if (!UsesAnsiTitle)
            {
                WriteDescribedTitle(rented.AsSpan(0, written));
            }
            else
            {
                _application.PostOutOfBand(destination.WrittenMemory);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    private void WriteDescribedTitle(ReadOnlySpan<byte> title)
    {
        ReadOnlyMemory<byte> prefix;
        ReadOnlyMemory<byte> suffix;

        lock (_programGate)
        {
            if (!Expander().TryExpandPair("TS", "fsl", out prefix, out suffix))
            {
                return;
            }
        }

        var destination = new ArrayBufferWriter<byte>(prefix.Length + title.Length + suffix.Length);
        destination.Write(prefix.Span);
        destination.Write(title);
        destination.Write(suffix.Span);
        _application.PostOutOfBand(destination.WrittenMemory);
    }

    private ProgramExpander Expander()
    {
        // The expander owns bounded interpreter state, so it is rebuilt only when the active
        // profile would actually produce different output. Capability refinement replaces the
        // profile object on every negotiation step without touching the compiled description.
        Debug.Assert(_programGate.IsHeldByCurrentThread, "Program expansion is serialized by its owner.");
        var profile = _application.TerminalProfile;

        if (_expander is null || !_expander.AppliesTo(profile))
        {
            _expander = profile.CreateProgramExpander();
        }

        return _expander;
    }

    private bool UsesAnsiTitle =>
        _application.TerminalProfile.IsAnsiCompatibility ||
        (Description.Origin == DescriptionOrigin.BuiltIn &&
         string.Equals(Description.Name, "windows-vt", StringComparison.Ordinal));

    /// <inheritdoc/>
    public void Write(ReadOnlySpan<char> text, Selection selection = Selection.Clipboard)
    {
        if (!((IClipboard) this).IsSupported)
        {
            return;
        }

        if (_application.Capabilities.KittyClipboard.IsAuthoritative)
        {
            var byteCount = Encoding.UTF8.GetByteCount(text);
            var buffer = new byte[byteCount];
            _ = Encoding.UTF8.GetBytes(text, buffer);
            _application.Dispatcher.Post(() => PerformKittyWrite(buffer, selection));
            return;
        }

        var destination = new ArrayBufferWriter<byte>(Encoding.UTF8.GetByteCount(text) + 16);
        var rented = ArrayPool<byte>.Shared.Rent(Math.Max(1, Encoding.UTF8.GetByteCount(text)));

        try
        {
            var written = Encoding.UTF8.GetBytes(text, rented);
            Osc52.Write(new ProtocolWriter(destination), selection, rented.AsSpan(0, written));
            _application.PostOutOfBand(destination.WrittenMemory);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    /// <inheritdoc/>
    public void Request(Selection selection = Selection.Clipboard)
    {
        if (!((IClipboard) this).IsSupported)
        {
            return;
        }

        if (_application.Capabilities.KittyClipboard.IsAuthoritative)
        {
            _application.Dispatcher.Post(() => PerformKittyRead(selection));
            return;
        }

        var destination = new ArrayBufferWriter<byte>(8);
        Osc52.Query(new ProtocolWriter(destination), selection);
        _application.Dispatcher.Post(() =>
        {
            _pendingOsc52Request = true;
            _pendingOsc52Selection = selection;
        });
        _application.PostOutOfBand(destination.WrittenMemory);
    }

    /// <summary>Feeds a decoded OSC 52 reply to a pending request, ignoring unsolicited replies.</summary>
    /// <param name="reply">The immutable decoded clipboard reply.</param>
    internal void ReceiveClipboardReply(ClipboardReply reply)
    {
        _application.Dispatcher.VerifyAccess();

        // A query-status reply is the terminal echoing a request shape, not an actual data or
        // error outcome, so it never completes a pending request.
        if (!_pendingOsc52Request || reply.Status == ClipboardStatus.Query)
        {
            return;
        }

        _pendingOsc52Request = false;
        var selection = _pendingOsc52Selection;

        var eventArgs = reply.Status == ClipboardStatus.Success
            ? new KittyClipboardReplyEventArgs(selection, null, reply.Data, ReplyStatus.None, null)
            : new KittyClipboardReplyEventArgs(
                selection,
                null,
                null,
                ReplyStatus.None,
                new Diagnostic(DiagnosticCode.Malformed, SequenceKind.Osc, offset: 0, discardedBytes: 0));

        KittyClipboardReplyReceived?.Invoke(this, eventArgs);
    }

    /// <summary>Feeds a decoded Kitty OSC 5522 packet to the live transaction, if any.</summary>
    /// <param name="packet">The non-null owned decoded packet.</param>
    internal void ReceiveKittyClipboardPacket(Packet packet)
    {
        _application.Dispatcher.VerifyAccess();

        if (_kittyTransaction is not { } transaction)
        {
            return;
        }

        var result = transaction.Accept(packet);

        if (result is AcceptResult.Completed or AcceptResult.Failed)
        {
            CompleteKittyTransaction(transaction);
        }
    }

    private void PerformKittyWrite(byte[] text, Selection selection)
    {
        var id = NextKittyId();
        var transaction = Transaction.Write(id: id, timeProvider: _application.Dispatcher.TimeProvider);
        StartKittyTransaction(transaction, selection);

        var destination = new ArrayBufferWriter<byte>(text.Length + 64);
        var writer = new ProtocolWriter(destination);
        var idBytes = Encoding.ASCII.GetBytes(id);
        KittyClipboardWriter.WriteStart(writer, selection, id: idBytes);
        KittyClipboardWriter.WriteData(writer, Encoding.ASCII.GetBytes(_textMime), text);
        KittyClipboardWriter.WriteEnd(writer);
        _application.PostOutOfBand(destination.WrittenMemory);
    }

    private void PerformKittyRead(Selection selection)
    {
        var id = NextKittyId();
        var transaction = Transaction.Read(id: id, timeProvider: _application.Dispatcher.TimeProvider);
        StartKittyTransaction(transaction, selection);

        var destination = new ArrayBufferWriter<byte>(64);
        var writer = new ProtocolWriter(destination);
        var idBytes = Encoding.ASCII.GetBytes(id);
        KittyClipboardWriter.Read(writer, Encoding.ASCII.GetBytes(_textMime), selection, id: idBytes);
        _application.PostOutOfBand(destination.WrittenMemory);
    }

    private string NextKittyId()
    {
        _kittyIdSequence++;
        return string.Create(CultureInfo.InvariantCulture, $"sv{_kittyIdSequence}");
    }

    private void StartKittyTransaction(Transaction transaction, Selection selection)
    {
        CancelPendingKittyTransaction();
        _kittyTransaction = transaction;
        _kittyTransactionSelection = selection;
        ScheduleKittyTimeout(transaction);
    }

    private void CancelPendingKittyTransaction()
    {
        _kittyTimeoutTimer?.Dispose();
        _kittyTimeoutTimer = null;

        if (_kittyTransaction is { } previous)
        {
            // A superseded request is silently abandoned: the caller already moved on by issuing
            // a new Write or Request, so no reply event fires for the stale one.
            previous.Cancel();
            previous.Dispose();
        }

        _kittyTransaction = null;
    }

    private void ScheduleKittyTimeout(Transaction transaction)
    {
        var remaining = transaction.Deadline - _application.Dispatcher.TimeProvider.GetUtcNow();

        if (remaining < TimeSpan.FromMilliseconds(1))
        {
            remaining = TimeSpan.FromMilliseconds(1);
        }

        var timer = new DispatcherTimer(_application.Dispatcher, remaining);
        timer.Tick += (_, _) => OnKittyTimeout(transaction);
        _kittyTimeoutTimer = timer;
        timer.Start();
    }

    private void OnKittyTimeout(Transaction transaction)
    {
        if (!ReferenceEquals(transaction, _kittyTransaction))
        {
            return;
        }

        _ = transaction.CheckTimeout();
        CompleteKittyTransaction(transaction);
    }

    private void CompleteKittyTransaction(Transaction transaction)
    {
        if (!ReferenceEquals(transaction, _kittyTransaction))
        {
            return;
        }

        _kittyTimeoutTimer?.Dispose();
        _kittyTimeoutTimer = null;
        _kittyTransaction = null;
        var selection = _kittyTransactionSelection;

        switch (transaction.State)
        {
            case TransactionState.Completed:
                KittyClipboardReplyReceived?.Invoke(
                    this,
                    new KittyClipboardReplyEventArgs(
                        selection,
                        transaction.Result,
                        null,
                        ReplyStatus.None,
                        null));
                break;
            case TransactionState.Failed:
                KittyClipboardReplyReceived?.Invoke(
                    this,
                    new KittyClipboardReplyEventArgs(
                        selection,
                        null,
                        null,
                        transaction.Failure,
                        transaction.Diagnostic));
                break;
            case TransactionState.TimedOut:
                KittyClipboardReplyReceived?.Invoke(
                    this,
                    new KittyClipboardReplyEventArgs(selection, null, null, ReplyStatus.None, null));
                break;
            case TransactionState.Created:
            case TransactionState.Accepted:
            case TransactionState.Receiving:
            case TransactionState.Cancelled:
            case TransactionState.Disposed:
                // Cancelled locally (superseded) reaches here only through defensive symmetry;
                // CancelPendingKittyTransaction never calls this method for that case, and the
                // remaining states are unreachable once Accept or CheckTimeout returned terminal.
                break;
            default:
                throw new UnreachableException();
        }

        transaction.Dispose();
    }
}
