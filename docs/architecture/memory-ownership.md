# Memory ownership

## Memory ownership contract

Borrowed spans are valid only for the duration of a synchronous call. Owned
memory has an explicit owner and disposal point. Pooled storage is never exposed
after return, frame completion, or disposal.

| Data                    | Owner                        | Lifetime                         |
| ----------------------- | ---------------------------- | -------------------------------- |
| Transport read buffer   | Runtime session              | Until decoder call returns       |
| Decoder state           | Decoder instance             | Until reset/disposal             |
| Decoded immutable event | Event value or owned payload | Through dispatcher delivery      |
| Front frame             | Renderer                     | Until a later successful commit  |
| Back frame              | Frame builder                | Until commit/abandon             |
| Grapheme arena          | Frame/screen owner           | While referenced by owned cells  |
| Image source bytes      | Immutable graphics image     | Image value lifetime             |
| Encoded write batch     | Renderer                     | Until write and flush complete   |
| Child control           | Parent `Control` slot        | Until removal/owner disposal     |
| Routed event snapshot   | Router                       | Until synchronous dispatch ends  |
| UI input record         | `Application`                | Until dispatcher delivery        |
| UI back frame           | `Application`                | Until render completion/disposal |

The Phase 2 `Parser` invokes `ISequenceSink` synchronously. Every span supplied
to a sink is borrowed only until that callback returns. A sink that queues or
retains a value must copy it. Parser disposal clears and returns its pooled
terminal-string storage.

`Input.Decoder` borrows each transport span only until `Decode` returns. Key,
text, pointer, and focus callbacks contain values only. During bracketed paste,
the decoder owns one finite pooled buffer, clears it on overflow, completion,
truncation, and disposal, and copies normalized UTF-8 into the delivered
`Paste`. Its `ReadOnlyMemory<byte>` therefore remains owned and stable after
later decoder calls; no pooled array or transport memory escapes.

`Osc52.Decode` and `KittyPacket.Parse` copy successfully decoded payloads into
owned arrays. A completed `KittyTransaction` transfers its accumulated MIME
buffers into `KittyResult`; the result owner must dispose it, which clears every
buffer. Temporary Base64 and transaction buffers are returned with clearing.

A rendering `Frame` owns its pooled cells and UTF-8 arena until disposal.
`CellInfo` contains metadata only; `CopyGrapheme` is the caller-owned byte
boundary. `Canvas` borrows its frame and becomes unusable when that frame is
disposed. `Frame.Clear` clears active text bytes for reuse, while disposal
clears the full rented arrays—including hyperlink references—before pool return.

`Renderer` owns its committed front-frame copy and one finite pooled byte
buffer. The caller retains ownership of every back frame and transport. Back
frame memory is borrowed until `RenderAsync` completes; `ITransport.WriteAsync`
borrows renderer memory until its returned operation completes and must either
transfer the complete memory or throw. Renderer disposal never disposes a
borrowed transport.

`Terminal.Graphics.Image` copies RGBA or structurally validated PNG bytes into
private immutable storage. Public callers recover bytes only through a complete
copy into caller-owned memory. Synchronous terminal encoders may borrow the
internal source span only until they return; a renderer must copy encoded output
into its owned finite batch before awaiting transport I/O.

`Runtime.Session` rents one finite read buffer for the event loop and clears it
before pool return. `IResizeSource` returns immutable `Dimensions` values and
retains no caller memory. The session owns disposal of its transport and resize
source; `StreamTransport` in turn owns its streams unless `leaveOpen` is true.

Every `SharpVision.Controls.Control` owns one central registry of ordered visual
slots. `Container.Children` exposes only its public container-child slot; the
current foundation also registers the List presentation host and private
container/editor scrollbar rails in distinct item-host or framework-part slots.
Content, composition-root, and item-visual roles are reserved by the same
registry for the role migration. Normal and popup are independent render layers,
not ownership roles. Until popup owners register dedicated popup-layer slots, a
`Popup` promotes its ordinary ownership edge for rendering and hit testing
without changing parentage. Removal transfers the now-detached control back to
the caller, while owner disposal recursively disposes every remaining slot
member. Attachment borrows one dispatcher reference for the lifetime of the
attachment. A control subscribes only to its direct `Style`; replacement,
detachment where applicable, and disposal remove owned registrations
deterministically.

Routed input snapshots both ancestry and matching handler registrations before
invocation. The router owns its rented arrays only through synchronous preview,
target, and bubble delivery and clears them before pool return. Handlers must
copy any data they retain. Terminal `Paste` payloads are already immutable owned
values and may cross the dispatcher queue without borrowing decoder storage.

`SharpVision.Runtime.Application` owns its dispatcher, terminal session,
renderer, focus/capture managers, current UI back frame, transport, and resize
source. Its bounded input queue stores immutable value records. Resize storms
use one newest-value slot rather than one allocation per notification. A back
frame remains application-owned until its asynchronous renderer lease completes;
only then may it be disposed or replaced.

## Span and asynchronous boundaries

Text layout borrows `ReadOnlySpan<char>` only for one synchronous format call
and writes immutable `Line` values into caller-owned storage. `Text` owns and
reuses its line array; its public `ReadOnlyMemory<Line>` view remains valid only
until the next successful layout. Every registered slot owns its attached
controls exclusively. Complete-slot and capacity-one replacement validate the
whole candidate snapshot before detaching any previous control.

Protocol encoders write synchronously to caller spans or `IBufferWriter<byte>`.
Any data crossing an `await`, queue, callback, or dispatcher boundary must be an
owned immutable value or copy. A `ReadOnlyMemory<T>` API documents whether the
caller or library owns its backing storage. `StreamTransport` borrows input and
output streams until disposal, serializes writes and flushes, and disposes both
only when constructed without `leaveOpen`.

## Pool safety

Owners clear sensitive clipboard/credential buffers before returning them when
the pool contract permits. Disposal is idempotent. Debug assertions verify
ownership state, continuation-cell references, and non-overlapping active
leases; public APIs still throw for caller misuse.

## Allocation contract

Steady-state parsing, unchanged measure/arrange, routed-event delivery, damage
scanning, and frame encoding allocate no object per byte, Rune, grapheme, or
cell. A warmed unchanged
[`Renderer.RenderAsync`](rendering-pipeline.md#commit-and-invalidation) call
allocates zero managed bytes. Performance tests measure allocation and peak
retained memory for representative frames, control trees, routes, dispatcher
posts, and bounded large payloads.
