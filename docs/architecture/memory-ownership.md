# Memory ownership

## Overview

Borrowed spans are valid only for the duration of a synchronous call. Owned
memory has an explicit owner and disposal point. Pooled storage is never exposed
after return, frame completion, or disposal.

| Data                    | Owner                        | Lifetime                         |
| ----------------------- | ---------------------------- | -------------------------------- |
| Transport read buffer   | Runtime session              | Until the drained read completes |
| Decoder state           | Decoder instance             | Until reset/disposal             |
| Decoded immutable event | Event value or owned payload | Through dispatcher delivery      |
| Terminal context        | Session and application      | One immutable snapshot at a time |
| Backend identity        | Shared immutable instance    | Fixed for application lifetime   |
| Discovery input         | Discovery context            | One detection pass               |
| Query lifecycle state   | Active-query strategy        | Startup publication or disposal  |
| Front frame             | Renderer                     | Until a later successful commit  |
| Back frame              | Frame builder                | Until commit/abandon             |
| Grapheme arena          | Frame/screen owner           | While referenced by owned cells  |
| Image source bytes      | Immutable graphics image     | Image value lifetime             |
| Image placements        | Rendering frame              | Frame clear, copy, or disposal   |
| Graphics backend state  | Renderer                     | Renderer disposal                |
| Encoded write batch     | Renderer                     | Until write and flush complete   |
| Child control           | Parent `ControlBase` slot    | Until removal/owner disposal     |
| Routed event snapshot   | Router                       | Until synchronous dispatch ends  |
| UI input record         | `Application`                | Until dispatcher delivery        |
| UI back frame           | `Application`                | Until render completion/disposal |

```mermaid
flowchart LR
    Transport["Session-owned read buffer"] -->|borrowed span| Parser["Parser and decoder"]
    Parser -->|copy before return| Record["Owned immutable record"]
    Record --> Queue["Bounded application queue"]
    Queue --> Dispatcher["Synchronous dispatcher delivery"]
    Dispatcher --> Controls["Retained control tree"]

    Back["Application-owned back frame"] -->|borrowed until completion| Renderer
    Renderer --> Front["Renderer-owned committed front frame"]
    Renderer -->|borrowed until write completes| TransportWrite["Transport output"]
```

Every asynchronous boundary receives owned storage or a copy. A borrowed span
ends with its synchronous call, and borrowed frame or transport memory stays
alive until the returned asynchronous operation completes.

`ProtocolParser` invokes `ISequenceSink` synchronously. Every span supplied to a
sink is borrowed only until that callback returns, so a sink that queues or
retains a value must copy it. Parser disposal clears and returns its pooled
terminal-string storage.

`Input.InputDecoder` borrows each transport span only until `Decode` returns.
Key, text, pointer, and focus callbacks carry values only. During bracketed
paste, the decoder owns one finite pooled buffer, clears it on overflow,
completion, truncation, and disposal, and copies normalized UTF-8 into the
delivered `Paste`. The `Paste` value's `ReadOnlyMemory<byte>` is therefore owned
and stays stable across later decoder calls; no pooled array or transport memory
escapes.

`Osc52.Decode` and `Kitty.Clipboard.Packet.Parse` copy successfully decoded
payloads into owned arrays. A completed `Kitty.Clipboard.Transaction` transfers
its accumulated MIME buffers into `Kitty.Clipboard.KittyClipboardResult`; the
result publishes an immutable item collection, its owner must dispose it, and
disposal clears every transferred buffer. Temporary Base64 and transaction
buffers are returned to their pools with clearing.

`DiscoveryContext` owns an immutable baseline plus copied environment, query,
and override references for one semantic discovery pass. Source adapters retain
no caller dictionary, native description buffer, or raw terminal payload.
`TerminalBackendResolver` publishes immutable redacted evidence and one shared
backend instance. `TerminalContext` owns one immutable `TerminalProfile`
reference and that fixed backend reference; capability refinement creates a new
context without replacing the backend identity. The
[discovery contract](discovery-pipeline.md#immutable-input-and-adapters) owns
redaction and publication ordering.

`ActiveQueryDiscoveryStrategy` owns its `QueryTracker`, pending family sets,
typed response values, shared deadline, and unpublished evidence until it
publishes one immutable result. `Negotiator` and `NegotiationSink` borrow and
forward that state; they do not own a second lifecycle. Typed responses copied
by the parser and router remain owned after the session read buffer is reused.

A rendering `Frame` owns its pooled cells and UTF-8 arena until disposal.
`CellInfo` contains metadata only, and `CopyGrapheme` is the boundary where
bytes cross into caller-owned storage. `Canvas` borrows its frame and becomes
unusable when that frame is disposed. `Frame.Clear` clears active text bytes so
the arena can be reused, while disposal clears the full rented arrays —
including hyperlink references — before returning them to the pool.

`Renderer` owns its committed front-frame copy, one finite pooled byte buffer,
and one bounded description-program interpreter. The committed frame is never
handed to a caller: damage tracking compares every target against it, so
external mutation would desynchronize the frame from the physical terminal, and
external disposal would permanently break a live renderer.
`Renderer.AttachCommittedFrame` instead links it to a target frame, which reads
it only through `Canvas.HasPreviousFrame` and `Canvas.CopyFromPrevious`. That
copy takes complete grapheme owners only — a wide cluster straddling the region
is written blank rather than split — validates geometry compatibility, and
preflights the destination arena, so a rejected copy leaves the target unchanged
and repeated copies cannot exceed the advertised bound. The renderer's
preallocated transaction snapshot retains only owned immutable static-variable
values and rolls back on failed encoding or output. The caller retains ownership
of every back frame and transport. Back-frame memory is borrowed until
`RenderAsync` completes; `ITransport.WriteAsync` borrows renderer memory until
its returned operation completes and must either transfer the complete memory or
throw. Renderer disposal never disposes a borrowed transport.

`Terminal.Graphics.ImageSource` copies RGBA or structurally validated PNG bytes
into private immutable storage. Public callers recover bytes only through a
complete copy into caller-owned memory. Synchronous terminal encoders may borrow
the internal source span only until they return; a renderer must copy encoded
output into its owned finite batch before awaiting transport I/O.

A rendering frame retains immutable images through a finite, cleared, pooled
placement array. A nonempty placement value contains only the image identity and
positive source and destination geometry; it never exposes image bytes. The
valid default/`Empty` sentinel retains no image and cannot enter the active
span. Clone and copy own independent arrays, preserve stable order, and may
share the immutable image objects. Frame clear and disposal clear active
placement references before pool return.

The renderer exclusively owns an optional `IGraphicsBackend`. Backend
preparation may allocate, but it completes before terminal I/O; the backend's
upload, placement, and removal writers synchronously borrow the renderer batch.
Prepared backend state commits only after the complete shared cell-and-graphics
batch flushes; after encoding, write, flush, or cancellation uncertainty it is
instead discarded and fully invalidated. Identifiers newly referenced by a
possibly partial failed batch stay reserved in finite uncertain-image and
uncertain-placement collections. A later committed cleanup returns them only
after the exact remote deletes flush; local disposal returns them without
claiming remote cleanup. No graphics backend retains a borrowed frame or
transport after its call. Renderer disposal releases the front frame before
graphics-backend cleanup and releases its pooled batch in a `finally` path, so a
graphics-backend cleanup exception propagates without retaining local pooled
ownership or leaving the writer gate acquired; a subsequent disposal is a no-op.

`Runtime.Session` rents one finite read buffer for the event loop and clears it
before pool return. `ITransport.ReadAsync` borrows that destination until its
returned operation completes, and cancellation is only a request, so the event
loop drains the outstanding read before releasing the rental. Every event-loop
exit — orderly closure, cancellation, a sink or optional-mode failure, or a
transport fault — cancels the linked token, awaits the read to terminal
completion, and only then returns the cleared array. The drain is bounded by
`Options.CleanupTimeout`: a transport that neither completes nor honors
cancellation within that budget permanently forfeits that one array instead of
the session publishing storage the transport can still write into. The session
also observes its abandoned read, resize, and negotiation-deadline tasks, so a
late failure cannot resurface as an unobserved task exception. `IResizeSource`
returns immutable `Dimensions` values and retains no caller memory. The session
owns disposal of its transport and resize source; `StreamTransport` in turn owns
its streams unless `leaveOpen` is true.

Every `SharpVision.Controls.Control` owns one central registry of ordered visual
slots. `Container.Children` exposes only its public container-child slot.
`ListView`, `Menu`, and `Table` register private item-presentation hosts, while
container and editor scrollbar rails use distinct framework-part slots.
`ContentControl` registers the capacity-one public content role. `PressableBase`
does not inherit `ContentControl`: it registers its own capacity-one slot for a
lazily materialized owned caption child, reachable only through the `Text`
string property, never as `Children`. `ComboBox` registers exactly one private
popup-layer framework-part slot, and `Popup.Content` owns its private ListView,
so no control ever has two parents. `CompositeControlBase` owns a permanent
composition root, while `ItemsControl` owns one private presentation host whose
children are the realized item visuals. Normal and popup are independent render
layers, not ownership roles; a `Popup` still promotes an ordinary legacy
ownership edge when necessary. Removal transfers the now-detached control back
to the caller, while owner disposal recursively disposes every remaining slot
member. Attachment borrows one dispatcher reference for the lifetime of the
attachment. A control subscribes only to its direct `Style`; replacement,
detachment where applicable, and disposal remove the owned registrations
deterministically.

`FloatingSurfaceBase` retains one public content edge and at most one live
application-owned modal scope. Window and Popup add no proxy ownership edge:
dialogs are direct Window identities, and Flyout and Tooltip are direct Popup
identities. Screen presentation owns the concrete Window or dialog in its
private Overlay; completion exits modality, removes that same object, and then
disposes it. Tooltip attachment owns the Tooltip itself in one keyed popup-layer
framework slot, which remains reusable after the association is cleared.

Intrinsic border and shadow chrome are style state on the decorated
`ControlBase`. They allocate no child, add no registry edge, and do not change
parentage, routed ancestry, style scope, or disposal ownership. Shadow overflow
is drawn only into the borrowed frame canvas for the current render, under the
[intrinsic chrome clipping contract](../controls/control.md#intrinsic-appearance).
When chrome requires distinct bounds, margin, style, ancestry, or lifetime, an
ordinary container such as `Dock` owns the decorated content through its normal
registry slot and is disposed by the same tree rules as every other container.

Routed input snapshots both the ancestry and the matching handler registrations
before invocation. The router owns its rented arrays only through synchronous
preview, target, and bubble delivery and clears them before pool return.
Handlers must copy any data they retain. Terminal `Paste` payloads are already
immutable owned values and may cross the dispatcher queue without borrowing
decoder storage.

`SharpVision.Application` owns its dispatcher, terminal session, renderer,
focus/capture managers, current UI back frame, transport, and resize source. Its
bounded input queue stores immutable value records. Resize storms use one
newest-value slot rather than one allocation per notification. A back frame
remains application-owned until its asynchronous renderer lease completes; only
then may it be disposed or replaced.

## Span and asynchronous boundaries

Text layout borrows a `ReadOnlySpan<char>` only for one synchronous format call
and writes immutable `Line` values into caller-owned storage. `Text` owns and
reuses its line array, so its public `ReadOnlyMemory<Line>` view remains valid
only until the next successful layout. Every registered slot owns its attached
controls exclusively. Complete-slot and capacity-one replacement validate the
whole candidate snapshot before detaching any previous control.

Protocol encoders write synchronously to caller spans or an
`IBufferWriter<byte>`. Any data crossing an `await`, queue, callback, or
dispatcher boundary must be an owned immutable value or a copy. A
`ReadOnlyMemory<T>` API documents whether the caller or the library owns its
backing storage. `StreamTransport` borrows its input and output streams until
disposal and serializes writes and flushes. Stream ownership is decided per
stream: the `leaveOpen` overload applies one decision to both streams, while
`StreamTransport(input, output, leaveInputOpen, leaveOutputOpen)` lets a
transport own the device it was handed while borrowing another. A stream
supplied as both input and output is closed at most once. A host that opens its
own input device must claim that stream, because a single shared flag would
either leak the opened device or close a stream the transport never owned.

## Pool safety

Owners clear sensitive clipboard and credential buffers before returning them
when the pool contract permits. Disposal is idempotent. Debug assertions verify
ownership state, continuation-cell references, and non-overlapping active
leases; public APIs still throw for caller misuse.

## Allocation rules

Steady-state parsing, unchanged measure/arrange, routed-event delivery, damage
scanning, and frame encoding allocate no object per byte, Rune, grapheme, or
cell. A warmed unchanged or profile-driven cursor/style
[`Renderer.RenderAsync`](rendering-pipeline.md#commit-and-terminal-state-invalidation)
call allocates zero managed bytes. Performance tests measure allocation and peak
retained memory for representative frames, control trees, routes, dispatcher
posts, and bounded large payloads.

## Expected behavior

The ownership rules above are backed by evidence at three layers:

| Layer       | Required evidence                                                                               |
| ----------- | ----------------------------------------------------------------------------------------------- |
| Unit        | Copy/borrow boundaries, disposal, pool return, sensitive clearing, and post-disposal rejection. |
| Allocation  | Warmed parsing, layout, routing, rendering, and encoding meet their allocation contracts.       |
| Integration | No pooled, borrowed, frame-owned, or caller-owned storage crosses its documented lifetime.      |
