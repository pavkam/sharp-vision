# Memory ownership

## Memory ownership contract

Borrowed spans are valid only for the duration of a synchronous call. Owned
memory has an explicit owner and disposal point. Pooled storage is never exposed
after return, frame completion, or disposal.

| Data                    | Owner                        | Lifetime                           |
| ----------------------- | ---------------------------- | ---------------------------------- |
| Transport read buffer   | Transport                    | Until decoder call returns         |
| Decoder state           | Decoder instance             | Until reset/disposal               |
| Decoded immutable event | Event value or owned payload | Through dispatcher delivery        |
| Front frame             | Renderer                     | Until a later successful commit    |
| Back frame              | Frame builder                | Until commit/abandon               |
| Grapheme arena          | Frame/screen owner           | While referenced by owned cells    |
| Encoded write batch     | Transport operation          | Until asynchronous write completes |

## Span and asynchronous boundaries

Protocol encoders write synchronously to caller spans or `IBufferWriter<byte>`.
Any data crossing an `await`, queue, callback, or dispatcher boundary must be an
owned immutable value or copy. A `ReadOnlyMemory<T>` API documents whether the
caller or library owns its backing storage.

## Pool safety

Owners clear sensitive clipboard/credential buffers before returning them when
the pool contract permits. Disposal is idempotent. Debug assertions verify
ownership state, continuation-cell references, and non-overlapping active
leases; public APIs still throw for caller misuse.

## Allocation contract

Steady-state parsing, measuring, damage scanning, and frame encoding allocate no
object per byte, Rune, grapheme, or cell. Performance tests measure allocation
and peak retained memory for representative frames and bounded large payloads.
