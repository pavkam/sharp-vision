// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using Terminal.Capabilities;
using Terminal.Clipboard;
using Terminal.Kitty.Clipboard;

using KittyClipboardWriter = Terminal.Kitty.Clipboard.KittyClipboardWriter;

/// <summary>Encodes implemented output protocols and posts them through the ordered write path.</summary>
internal sealed class TerminalServices: ITerminalServices, IBell, IClipboard, IDisposable
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
    private DispatcherTimer? _osc52TimeoutTimer;
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
    public bool TitleSupported
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
    bool IClipboard.Supported =>
        _application.Capabilities.KittyClipboard.Authoritative ||
        _application.Capabilities.Osc52.Authoritative;

    /// <inheritdoc/>
    bool IBell.Supported
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

        if (!TitleSupported)
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
        _application.TerminalProfile.AnsiCompatible ||
        (Description.Origin == DescriptionOrigin.BuiltIn &&
         string.Equals(Description.Name, "windows-vt", StringComparison.Ordinal));

    /// <inheritdoc/>
    public void Write(ReadOnlySpan<char> text, Selection selection = Selection.Clipboard)
    {
        if (!((IClipboard) this).Supported)
        {
            return;
        }

        if (_application.Capabilities.KittyClipboard.Authoritative)
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
        if (!((IClipboard) this).Supported)
        {
            return;
        }

        if (_application.Capabilities.KittyClipboard.Authoritative)
        {
            _application.Dispatcher.Post(() => PerformKittyRead(selection));
            return;
        }

        var destination = new ArrayBufferWriter<byte>(8);
        Osc52.Query(new ProtocolWriter(destination), selection);

        // Assigned inside the posted callback, never here: Request is callable from any thread -
        // it only reads capabilities - and the pending state is dispatcher-owned.
        _application.Dispatcher.Post(() => StartOsc52Request(selection));
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

        CancelPendingOsc52Request();

        // Labelled from the wire, not from the pending request. Reading the pending selection
        // instead delivered a reply under the wrong label whenever a second Request superseded the
        // first before the terminal answered: the earlier selection's data arrived tagged with the
        // later selection, which is silent data misattribution for a consumer filling two panes.
        //
        // The reply still completes whatever request is outstanding rather than being matched and
        // dropped on a mismatch. reply.Selection is a usable label but not a reliable key - an
        // empty Pc field decodes to Select, and a multi-character Pc decodes to the first
        // recognized character - so requiring it to match would strand a request the terminal
        // actually answered. One outstanding request is the house design for both protocols.
        var selection = reply.Selection;

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

    /// <summary>Tears down the clipboard work this instance owns.</summary>
    /// <remarks>
    /// Without this the deadline timer outlives the application. DispatcherTimer.Start arms the
    /// underlying ITimer <em>periodically</em>, and once the dispatcher has stopped OnElapsed posts
    /// to it, swallows the ObjectDisposedException, and fires again on the next period forever -
    /// Deliver never runs, so nothing ever reaches CompleteKittyTransaction to dispose it. The live
    /// CLR timer then roots the timer's Tick closure, this instance, the Application, and through it
    /// the dispatcher, renderer, and whole control tree, so disposing the Application alone does not
    /// break the chain. A host running several applications in one process accumulates one such
    /// graph and one live periodic callback per run.
    ///
    /// A request outstanding at shutdown is abandoned the same way a superseded one is: no reply
    /// event fires. The consumer gets no outcome either way, but at least nothing is left armed.
    /// </remarks>
    public void Dispose()
    {
        CancelPendingKittyTransaction();
        CancelPendingOsc52Request();
    }

    /// <summary>Arms one OSC 52 read and its deadline, superseding any request still outstanding.</summary>
    /// <param name="selection">The selection being queried.</param>
    private void StartOsc52Request(Selection selection)
    {
        _application.Dispatcher.VerifyAccess();
        CancelPendingOsc52Request();
        _pendingOsc52Request = true;
        _pendingOsc52Selection = selection;

        // Without a deadline this request could never end. OSC 52 clipboard *reads* are denied by
        // default on stock terminals - xterm's disallowedWindowOps blocks them and kitty's default
        // clipboard_control omits read-clipboard - so "never answered" is the ordinary outcome, not
        // an edge case, and a consumer awaiting the reply event waited forever with no exception,
        // no diagnostic, and no Failure. The Kitty sibling has always had one; this uses the same
        // QueryTimeout so both protocols give up at the same point.
        var timer = new DispatcherTimer(_application.Dispatcher, _application.ClipboardQueryTimeout);
        timer.Tick += (_, _) => OnOsc52Timeout();
        _osc52TimeoutTimer = timer;
        timer.Start();
    }

    private void OnOsc52Timeout()
    {
        if (!_pendingOsc52Request)
        {
            return;
        }

        var selection = _pendingOsc52Selection;
        CancelPendingOsc52Request();

        // Same shape the Kitty TimedOut arm reports: an outcome with no data and no diagnostic,
        // so a consumer distinguishes "no answer" from a terminal-reported failure.
        KittyClipboardReplyReceived?.Invoke(
            this,
            new KittyClipboardReplyEventArgs(selection, null, null, ReplyStatus.None, null));
    }

    private void CancelPendingOsc52Request()
    {
        _osc52TimeoutTimer?.Dispose();
        _osc52TimeoutTimer = null;
        _pendingOsc52Request = false;
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
