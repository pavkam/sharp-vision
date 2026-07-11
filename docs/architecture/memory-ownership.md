# Memory ownership

## Memory ownership contract

Borrowed spans are valid only for the duration of a synchronous call. Owned
memory has an explicit owner and disposal point. Pooled storage is never exposed
after return, frame completion, or disposal.

| Data                    | Owner                        | Lifetime                        |
| ----------------------- | ---------------------------- | ------------------------------- |
| Transport read buffer   | Transport                    | Until decoder call returns      |
| Decoder state           | Decoder instance             | Until reset/disposal            |
| Decoded immutable event | Event value or owned payload | Through dispatcher delivery     |
| Front frame             | Renderer                     | Until a later successful commit |
| Back frame              | Frame builder                | Until commit/abandon            |
| Grapheme arena          | Frame/screen owner           | While referenced by owned cells |
| Encoded write batch     | Renderer                     | Until write and flush complete  |

The Phase 2 `Parser` invokes `ISequenceSink` synchronously. Every span supplied
to a sink is borrowed only until that callback returns. A sink that queues or
retains a value must copy it. Parser disposal clears and returns its pooled
terminal-string storage.

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

## Span and asynchronous boundaries

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

Steady-state parsing, measuring, damage scanning, and frame encoding allocate no
object per byte, Rune, grapheme, or cell. A warmed unchanged
[`Renderer.RenderAsync`](rendering-pipeline.md#commit-and-invalidation) call
allocates zero managed bytes. Performance tests measure allocation and peak
retained memory for representative frames and bounded large payloads.
