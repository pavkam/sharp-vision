// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using Terminal.Capabilities;
using Terminal.Clipboard;
using Terminal.Kitty.Clipboard;
using Terminal.Multiplexing;

using KittyClipboardWriter = Terminal.Kitty.Clipboard.KittyClipboardWriter;

/// <summary>Encodes implemented output protocols and posts them through the ordered write path.</summary>
internal sealed class TerminalServices: ITerminalServices, IBell, IClipboard, INotifications, IDisposable
{
    // Kitty OSC 5522 metadata reserves ':' and ';' as field and payload separators, so a MIME
    // type cannot carry a ";charset=" parameter the way OSC 52 or HTTP would.
    private const string _textMime = "text/plain";
    private readonly Application _application;
    private readonly MultiplexerRoute? _multiplexerRoute;
    private readonly Lock _programGate = new();
    private ProgramExpander? _expander;
    private KittyClipboardTransaction? _kittyPasteTransaction;
    private DispatcherTimer? _kittyPasteTimeoutTimer;
    private byte[] _kittyPastePassword = [];
    private Selection _kittyPasteSelection;
    private KittyClipboardTransaction? _kittyTransaction;
    private DispatcherTimer? _kittyTimeoutTimer;
    private Selection _kittyTransactionSelection;
    private int _kittyIdSequence;
    private DispatcherTimer? _osc52TimeoutTimer;
    private bool _pendingOsc52Request;
    private Selection _pendingOsc52Selection;
    private int _osc52ReplyDebt;

    /// <summary>Initializes terminal services against the application's immutable route.</summary>
    /// <param name="application">The non-null owning application.</param>
    /// <param name="multiplexerRoute">The explicit multiplexer route, or null.</param>
    public TerminalServices(Application application, MultiplexerRoute? multiplexerRoute)
    {
        Debug.Assert(application is not null, "The owning application must be provided.");
        _application = application;
        _multiplexerRoute = multiplexerRoute;
    }

    /// <inheritdoc/>
    public event EventHandler<ClipboardPasteEventArgs>? ClipboardPasteReceived;

    /// <inheritdoc/>
    public event EventHandler<KittyClipboardReplyEventArgs>? KittyClipboardReplyReceived;

    /// <inheritdoc/>
    public IBell Bell => this;

    /// <inheritdoc/>
    public Description Description => _application.TerminalProfile.Description;

    /// <inheritdoc/>
    public IClipboard Clipboard => this;

    /// <inheritdoc/>
    public INotifications Notifications => this;

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
        (_multiplexerRoute is null || _multiplexerRoute.CanRouteClipboard) &&
        (_application.Capabilities.KittyClipboard.Authoritative ||
         _application.Capabilities.Osc52.Authoritative);

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
    /// <remarks>
    /// Reports authoritative evidence for desktop notifications. There is no reliable environment
    /// or query signal for this protocol, so only an explicit
    /// <see cref="CapabilityOverrides.Notifications"/> opt-in can ever make this true.
    /// </remarks>
    bool INotifications.IsSupported => _application.Capabilities.Notifications.Authoritative;

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

    /// <inheritdoc/>
    public void Notify(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (!((INotifications) this).IsSupported)
        {
            return;
        }

        var byteCount = Encoding.UTF8.GetByteCount(body);
        var destination = new ArrayBufferWriter<byte>(byteCount + 8);
        var rented = ArrayPool<byte>.Shared.Rent(Math.Max(1, byteCount));

        try
        {
            var written = Encoding.UTF8.GetBytes(body, rented);
            Osc.Notify(new ProtocolWriter(destination), rented.AsSpan(0, written));

            if (TryRouteNotification(destination.WrittenMemory, out var routed))
            {
                _application.PostOutOfBand(routed);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    /// <inheritdoc/>
    public void Notify(string title, string body)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(body);

        if (!((INotifications) this).IsSupported)
        {
            return;
        }

        var titleByteCount = Encoding.UTF8.GetByteCount(title);
        var bodyByteCount = Encoding.UTF8.GetByteCount(body);
        var destination = new ArrayBufferWriter<byte>(titleByteCount + bodyByteCount + 16);
        var titleRented = ArrayPool<byte>.Shared.Rent(Math.Max(1, titleByteCount));
        var bodyRented = ArrayPool<byte>.Shared.Rent(Math.Max(1, bodyByteCount));

        try
        {
            var titleWritten = Encoding.UTF8.GetBytes(title, titleRented);
            var bodyWritten = Encoding.UTF8.GetBytes(body, bodyRented);
            Osc.Notify(
                new ProtocolWriter(destination),
                titleRented.AsSpan(0, titleWritten),
                bodyRented.AsSpan(0, bodyWritten));

            if (TryRouteNotification(destination.WrittenMemory, out var routed))
            {
                _application.PostOutOfBand(routed);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(titleRented, clearArray: true);
            ArrayPool<byte>.Shared.Return(bodyRented, clearArray: true);
        }
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
        if (!((IClipboard) this).IsSupported)
        {
            return;
        }

        if (_application.Capabilities.KittyClipboard.Authoritative)
        {
            var byteCount = Encoding.UTF8.GetByteCount(text);
            var buffer = new byte[byteCount];
            _ = Encoding.UTF8.GetBytes(text, buffer);
            try
            {
                _application.Dispatcher.Post(() => PerformKittyWrite(buffer, selection));
            }
            catch (ObjectDisposedException)
            {
            }

            return;
        }

        var destination = new ArrayBufferWriter<byte>(Encoding.UTF8.GetByteCount(text) + 16);
        var rented = ArrayPool<byte>.Shared.Rent(Math.Max(1, Encoding.UTF8.GetByteCount(text)));

        try
        {
            var written = Encoding.UTF8.GetBytes(text, rented);
            Osc52.Write(
                new ProtocolWriter(destination),
                selection,
                rented.AsSpan(0, written),
                _application.TransferLimits.MaxClipboardBytes);
            if (TryRouteClipboard(destination.WrittenMemory, out var routed))
            {
                _application.PostOutOfBand(routed);
            }
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

        if (_application.Capabilities.KittyClipboard.Authoritative)
        {
            try
            {
                _application.Dispatcher.Post(() => PerformKittyRead(selection));
            }
            catch (ObjectDisposedException)
            {
            }

            return;
        }

        try
        {
            _application.Dispatcher.Post(() => PerformOsc52Request(selection));
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>Feeds a decoded OSC 52 reply to a pending request, ignoring unsolicited replies.</summary>
    /// <param name="reply">The immutable decoded clipboard reply.</param>
    internal void ReceiveClipboardReply(ClipboardReply reply)
    {
        _application.Dispatcher.VerifyAccess();

        // A query-status reply is the terminal echoing a request shape, not an actual data or
        // error outcome, so it never completes a pending request.
        if (reply.Status == ClipboardStatus.Query)
        {
            return;
        }

        // OSC 52 carries no correlation id, so a superseded request's own stale reply cannot be
        // told apart from the live request's reply by inspecting the reply alone. Replies arrive
        // in receipt order (the same assumption the terminal-answers-in-order case relies on), so
        // the next N non-Query replies after a supersession are exactly the stale answers owed by
        // the N requests that were superseded before they were answered - drop them here without
        // touching the pending state or its live deadline timer.
        if (_osc52ReplyDebt > 0)
        {
            _osc52ReplyDebt--;
            return;
        }

        if (!_pendingOsc52Request)
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
            ? new KittyClipboardReplyEventArgs(selection, null, reply.Data, KittyClipboardReplyStatus.None, null)
            : new KittyClipboardReplyEventArgs(
                selection,
                null,
                null,
                KittyClipboardReplyStatus.None,
                new Diagnostic(DiagnosticCode.Malformed, SequenceKind.Osc, offset: 0, discardedBytes: 0));

        KittyClipboardReplyReceived?.Invoke(this, eventArgs);
    }

    /// <summary>Feeds a decoded Kitty OSC 5522 packet to the live transaction, if any.</summary>
    /// <param name="packet">The non-null owned decoded packet.</param>
    internal void ReceiveKittyClipboardPacket(KittyClipboardPacket packet)
    {
        _application.Dispatcher.VerifyAccess();

        if (_kittyTransaction is { } transaction)
        {
            var result = transaction.Accept(packet);

            if (result is KittyClipboardAcceptResult.Completed or KittyClipboardAcceptResult.Failed)
            {
                CompleteKittyTransaction(transaction);
            }

            if (result != KittyClipboardAcceptResult.Ignored)
            {
                return;
            }
        }

        if (!_application.ClipboardPasteEventsEnabled ||
            packet.Id is not null ||
            packet.Operation != KittyClipboardOperation.Read)
        {
            return;
        }

        if (packet.ReplyStatus == KittyClipboardReplyStatus.Ok && !packet.Password.IsEmpty)
        {
            CancelPendingKittyPasteTransaction();
            _kittyPasteTransaction = KittyClipboardTransaction.Read(
                limits: _application.TransferLimits,
                listOnly: true,
                timeProvider: _application.Dispatcher.TimeProvider,
                queryLimits: _application.ClipboardQueryLimits);
            _kittyPasteSelection = packet.Selection;
            _kittyPastePassword = packet.Password.ToArray();
            ScheduleKittyPasteTimeout(_kittyPasteTransaction);
        }

        if (_kittyPasteTransaction is not { } pasteTransaction ||
            !_kittyPastePassword.AsSpan().SequenceEqual(packet.Password.Span))
        {
            CancelPendingKittyPasteTransaction();
            return;
        }

        var pasteResult = pasteTransaction.Accept(packet);

        if (pasteResult == KittyClipboardAcceptResult.Completed)
        {
            CompleteKittyPasteTransaction(pasteTransaction);
        }
        else if (pasteResult == KittyClipboardAcceptResult.Failed)
        {
            CancelPendingKittyPasteTransaction();
        }
    }

    private void PerformKittyWrite(byte[] text, Selection selection)
    {
        var id = NextKittyId();
        var transaction = KittyClipboardTransaction.Write(
            id: id,
            timeProvider: _application.Dispatcher.TimeProvider,
            limits: _application.TransferLimits,
            queryLimits: _application.ClipboardQueryLimits);
        var destination = new ArrayBufferWriter<byte>(text.Length + 64);
        var writer = new ProtocolWriter(destination);
        var idBytes = Encoding.ASCII.GetBytes(id);
        KittyClipboardWriter.WriteStart(writer, selection, id: idBytes);
        KittyClipboardWriter.WriteData(
            writer,
            Encoding.ASCII.GetBytes(_textMime),
            text,
            _application.TransferLimits.MaxClipboardBytes);
        KittyClipboardWriter.WriteEnd(writer);

        if (!TryRouteClipboard(destination.WrittenMemory, out var routed))
        {
            transaction.Dispose();
            return;
        }

        StartKittyTransaction(transaction, selection);
        _application.PostOutOfBand(routed);
    }

    private void PerformKittyRead(Selection selection)
    {
        var id = NextKittyId();
        var transaction = KittyClipboardTransaction.Read(
            id: id,
            timeProvider: _application.Dispatcher.TimeProvider,
            limits: _application.TransferLimits,
            queryLimits: _application.ClipboardQueryLimits);
        var destination = new ArrayBufferWriter<byte>(64);
        var writer = new ProtocolWriter(destination);
        var idBytes = Encoding.ASCII.GetBytes(id);
        KittyClipboardWriter.Read(writer, Encoding.ASCII.GetBytes(_textMime), selection, id: idBytes);

        if (!TryRouteClipboard(destination.WrittenMemory, out var routed))
        {
            transaction.Dispose();
            return;
        }

        StartKittyTransaction(transaction, selection);
        _application.PostOutOfBand(routed);
    }

    private string NextKittyId()
    {
        _kittyIdSequence++;
        return string.Create(CultureInfo.InvariantCulture, $"sv{_kittyIdSequence}");
    }

    private void StartKittyTransaction(KittyClipboardTransaction transaction, Selection selection)
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
    ///
    /// The caller must invoke this on the owning dispatcher thread. Every other mutator of this
    /// pending clipboard state - <see cref="ReceiveClipboardReply"/>,
    /// <see cref="ReceiveKittyClipboardPacket"/>, and <see cref="StartOsc52Request"/> - asserts
    /// that with <see cref="Dispatcher.VerifyAccess"/> because a live query's deadline
    /// <see cref="DispatcherTimer"/> ticks <see cref="OnKittyTimeout"/> or
    /// <see cref="OnOsc52Timeout"/> on that same thread; calling this from any other thread would
    /// race those ticks on the same in-flight, single-threaded transaction with no lock between
    /// them.
    /// </remarks>
    public void Dispose()
    {
        DisposedOnDispatcherThreadForTests = _application.Dispatcher.CheckAccess();
        CancelPendingKittyPasteTransaction();
        CancelPendingKittyTransaction();
        CancelPendingOsc52Request();
    }

    /// <summary>Gets whether the most recent <see cref="Dispose"/> call observed itself running on
    /// the owning dispatcher thread. A regression seam only, proving shutdown cleanup never
    /// touches this dispatcher-owned clipboard state from another thread; see the threading
    /// requirement documented on <see cref="Dispose"/>.</summary>
    internal bool DisposedOnDispatcherThreadForTests { get; private set; }

    /// <summary>Gets whether an application-issued Kitty clipboard transaction retains its
    /// deadline timer. This regression seam proves shutdown releases the timer without reflecting
    /// over private runtime state.</summary>
    internal bool HasPendingKittyTimeoutForTests => _kittyTimeoutTimer is not null;

    /// <summary>Arms one OSC 52 read and its deadline, superseding any request still outstanding.</summary>
    /// <param name="selection">The selection being queried.</param>
    private void StartOsc52Request(Selection selection)
    {
        _application.Dispatcher.VerifyAccess();

        // Only a real supersession - overwriting a request that is still outstanding - owes a
        // stale reply. A timeout already cleared _pendingOsc52Request before this runs, so a fresh
        // Request issued after the previous one gave up does not accrue phantom debt that could
        // wrongly swallow a later, unrelated live reply.
        if (_pendingOsc52Request)
        {
            _osc52ReplyDebt++;
        }

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

    /// <summary>Atomically orders pending OSC 52 state with its matching terminal query.</summary>
    /// <param name="selection">The selection being queried.</param>
    private void PerformOsc52Request(Selection selection)
    {
        _application.Dispatcher.VerifyAccess();
        var destination = new ArrayBufferWriter<byte>(8);
        Osc52.Query(new ProtocolWriter(destination), selection);

        if (!TryRouteClipboard(destination.WrittenMemory, out var routed))
        {
            return;
        }

        StartOsc52Request(selection);
        _application.PostOutOfBand(routed);
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
            new KittyClipboardReplyEventArgs(selection, null, null, KittyClipboardReplyStatus.None, null));
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

    private void ScheduleKittyTimeout(KittyClipboardTransaction transaction)
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

    private void OnKittyTimeout(KittyClipboardTransaction transaction)
    {
        if (!ReferenceEquals(transaction, _kittyTransaction))
        {
            return;
        }

        _kittyTimeoutTimer?.Dispose();
        _kittyTimeoutTimer = null;

        if (transaction.CheckTimeout() || transaction.State is
            KittyClipboardTransactionState.Completed or
            KittyClipboardTransactionState.Failed or
            KittyClipboardTransactionState.Cancelled or
            KittyClipboardTransactionState.TimedOut)
        {
            CompleteKittyTransaction(transaction);
            return;
        }

        ScheduleKittyTimeout(transaction);
    }

    private void CompleteKittyTransaction(KittyClipboardTransaction transaction)
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
            case KittyClipboardTransactionState.Completed:
                KittyClipboardReplyReceived?.Invoke(
                    this,
                    new KittyClipboardReplyEventArgs(
                        selection,
                        transaction.Result,
                        null,
                        KittyClipboardReplyStatus.None,
                        null));
                break;
            case KittyClipboardTransactionState.Failed:
                KittyClipboardReplyReceived?.Invoke(
                    this,
                    new KittyClipboardReplyEventArgs(
                        selection,
                        null,
                        null,
                        transaction.Failure,
                        transaction.Diagnostic));
                break;
            case KittyClipboardTransactionState.TimedOut:
                KittyClipboardReplyReceived?.Invoke(
                    this,
                    new KittyClipboardReplyEventArgs(selection, null, null, KittyClipboardReplyStatus.None, null));
                break;
            case KittyClipboardTransactionState.Created:
            case KittyClipboardTransactionState.Accepted:
            case KittyClipboardTransactionState.Receiving:
            case KittyClipboardTransactionState.Cancelled:
            case KittyClipboardTransactionState.Disposed:
                // Cancelled locally (superseded) reaches here only through defensive symmetry;
                // CancelPendingKittyTransaction never calls this method for that case, and the
                // remaining states are unreachable once Accept or CheckTimeout returned terminal.
                break;
            default:
                throw new UnreachableException();
        }

        transaction.Dispose();
    }

    private void ScheduleKittyPasteTimeout(KittyClipboardTransaction transaction)
    {
        var remaining = transaction.Deadline - _application.Dispatcher.TimeProvider.GetUtcNow();

        if (remaining < TimeSpan.FromMilliseconds(1))
        {
            remaining = TimeSpan.FromMilliseconds(1);
        }

        var timer = new DispatcherTimer(_application.Dispatcher, remaining);
        timer.Tick += (_, _) => OnKittyPasteTimeout(transaction);
        _kittyPasteTimeoutTimer = timer;
        timer.Start();
    }

    private void OnKittyPasteTimeout(KittyClipboardTransaction transaction)
    {
        if (!ReferenceEquals(transaction, _kittyPasteTransaction))
        {
            return;
        }

        _kittyPasteTimeoutTimer?.Dispose();
        _kittyPasteTimeoutTimer = null;

        if (transaction.CheckTimeout())
        {
            CancelPendingKittyPasteTransaction();
            return;
        }

        ScheduleKittyPasteTimeout(transaction);
    }

    private void CompleteKittyPasteTransaction(KittyClipboardTransaction transaction)
    {
        Debug.Assert(ReferenceEquals(transaction, _kittyPasteTransaction), "Only the active paste transaction completes.");
        ClipboardPasteEventArgs? eventArgs = null;

        try
        {
            var item = transaction.Result?.Items.Count == 1
                ? transaction.Result.Items[0]
                : null;

            if (item?.Mime == ".")
            {
                var value = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                    .GetString(item.Data.Span);
                var mimeTypes = value.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
                eventArgs = new ClipboardPasteEventArgs(
                    _kittyPasteSelection,
                    mimeTypes,
                    _kittyPastePassword);
            }
        }
        catch (DecoderFallbackException)
        {
        }
        finally
        {
            transaction.Result?.Dispose();
            CancelPendingKittyPasteTransaction();
        }

        if (eventArgs is not null)
        {
            ClipboardPasteReceived?.Invoke(this, eventArgs);
        }
    }

    private void CancelPendingKittyPasteTransaction()
    {
        _kittyPasteTimeoutTimer?.Dispose();
        _kittyPasteTimeoutTimer = null;

        if (_kittyPasteTransaction is { } transaction)
        {
            transaction.Cancel();
            transaction.Dispose();
        }

        _kittyPasteTransaction = null;
        _kittyPastePassword.AsSpan().Clear();
        _kittyPastePassword = [];
    }

    private bool TryRouteClipboard(ReadOnlyMemory<byte> commands, out ReadOnlyMemory<byte> routed)
    {
        if (_multiplexerRoute is null)
        {
            routed = commands;
            return true;
        }

        var destination = new ArrayBufferWriter<byte>(commands.Length + 16);
        var remaining = commands.Span;

        while (!remaining.IsEmpty)
        {
            var terminator = remaining.IndexOf("\u001b\\"u8);

            if (terminator < 0)
            {
                routed = default;
                return false;
            }

            var packetLength = terminator + 2;

            if (!_multiplexerRoute.TryWriteClipboard(destination, remaining[..packetLength]))
            {
                routed = default;
                return false;
            }

            remaining = remaining[packetLength..];
        }

        routed = destination.WrittenMemory.ToArray();
        return true;
    }

    private bool TryRouteNotification(ReadOnlyMemory<byte> command, out ReadOnlyMemory<byte> routed)
    {
        if (_multiplexerRoute is null)
        {
            routed = command;
            return true;
        }

        var destination = new ArrayBufferWriter<byte>(command.Length + 16);

        if (!_multiplexerRoute.TryWriteNotification(destination, command.Span))
        {
            routed = default;
            return false;
        }

        routed = destination.WrittenMemory.ToArray();
        return true;
    }
}
