# Kitty graphics protocol

## Overview

Primary source:
[Kitty terminal graphics protocol](https://sw.kovidgoyal.net/kitty/graphics-protocol/),
accessed 2026-08-27. A command is APC `ESC _ G`, comma-separated control data,
an optional semicolon plus Base64 data, and ST `ESC \`.

`Kitty.Graphics.KittyGraphicsCommand` and `Kitty.Graphics.KittyGraphicsWriter`
provide the typed direct-data surface. Image identifiers and placement
identifiers are canonical nonzero unsigned 32-bit decimal values: leading
zeroes, signs, and overflow are rejected. The official direct RGB query uses
`f=24`; transmission accepts RGB `f=24`, RGBA `f=32`, and PNG `f=100`. Raw RGB
or RGBA transmission may additionally request zlib `o=z` compression:
`KittyGraphicsWriter.WriteTransmission` compresses the caller's raw payload with
a fixed compression level (matching `Graphics.Png`'s deterministic PNG encoding
technique) before chunking the compressed bytes for the wire; `Write` and
`WriteEncoded` are unaffected, since they already receive framed wire bytes with
no raw/compressed distinction to make. File, temporary-file, and shared-memory
media are rejected before output because they depend on filesystem paths and
externally managed lifetimes that SharpVision does not trust.

> [!IMPORTANT]
>
> **Implementation gap:** Frame transmission (`a=f`) and animation playback
> control (`a=a`) have a typed, validated, byte-exact wire surface — see
> [Frame transmission and animation control](#frame-transmission-and-animation-control-protocol-surface-only)
> — but the renderer does not consume it: `KittyGraphicsBackend.Prepare` and the
> `Frame`/`Placement` diff model that drives it have no concept of a retained
> animated image, so every placement remains a cursor-anchored single-image
> direct-data upload. Driving actual animated rendering — backend and renderer
> integration plus a higher-level animated `Image` control — remains a
> documented follow-up.

## Supported features

Typed and implemented behavior includes:

- the official one-pixel direct RGB query;
- strict bounded typed response parsing and numeric correlation;
- direct RGB, RGBA, and PNG upload, retained placement/update, and deletion;
- optional zlib-compressed direct RGB or RGBA transmission;
- terminal-allocated image identifiers correlated through finite client image
  numbers, plus renderer-owned finite placement identifiers;
- 4,096-byte maximum encoded data chunks with metadata-minimal continuation
  chunks;
- transactional upload, cell, placement, and removal ordering;
- Kitty 0.28 Unicode-placeholder placement integrated with cell damage and
  vertical scrolling after terminal image-id assignment;
- explicit asynchronous remote cleanup before local renderer disposal; and
- explicitly authorized tmux passthrough.

`Graphics.ImageSource` copies validated source data. `Frame` retains ordered
semantic `Graphics.Placement` values. A renderer with no selected graphics
backend emits only ordinary cells, so those cells are the deterministic fallback
rather than an implicit raster conversion.

## Detection and responses

When standard-query capacity remains after existing query families, capability
negotiation writes the official query `ESC_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA ESC\`
before primary DA. Any strictly valid response with image ID 31 proves support,
including an error response, because the response itself proves that the
terminal understood the graphics command. Primary DA is the ordering barrier: an
unanswered active graphics query becomes unsupported query evidence when DA
arrives.

Replies are APC payloads beginning with `G`. A required `i` field and optional
`p` and `I` fields are accepted, each a canonical nonzero ASCII decimal value
within `uint`. `I` echoes back the client-assigned image number for a
number-created image, alongside the terminal-assigned `i` (for example
`i=99,I=13;OK`); `KittyGraphicsResponse.ImageNumber` exposes it, zero when
absent. The nonempty response text must be printable ASCII and is copied within
`Kitty.KittyMetadataLimits.MaxMetadataBytes`. `KittyGraphicsResponse.Message`
exposes that bounded but untrusted terminal text to an explicit caller.
Diagnostics and `ToString()` never include it. Malformed, duplicate-field,
overflowing, unknown-field, late, and unrelated replies cannot consume another
transaction.

## Upload and placement

Direct data is Base64 encoded into APC payloads no longer than 4,096 bytes. A
non-final chunk therefore contains 3,072 raw bytes and has a Base64 length
divisible by four. After the first chunk, only `m` and optional `q` metadata are
emitted. One image finishes all chunks before another graphics command begins.
The backend never opens a terminal-supplied path. Retained uploads use Kitty's
capital `I` image-number field rather than claiming a process-local `i` image
id, and transmit with quiet mode 0 while still number-addressed so the
terminal's `OK` response - the only way a client using `I=` ever learns the
terminal-assigned `i=` id - is not suppressed; once addressing has switched to
the terminal-assigned id, quiet mode 2 is used, since no further correlation
reply is needed. Kitty creates a fresh image even when another client used the
same number. The acknowledgement is routed through `Application` and `Renderer`
to the retained backend, which correlates the echoed `I` number and switches
later placement and cleanup commands to the terminal-assigned `i` id. This
prevents one client from replacing or deleting another client's image merely
because both began allocating at one.

A retiring image's still-outstanding client number can be reused immediately by
a new image rather than waiting for the terminal's own confirmation of the old
upload. Because later replies are correlated only by that shared number, a
stale reply belonging to the retiring image is indistinguishable from one meant
for its replacement; the backend tracks each such handoff and drops exactly one
stale reply per outstanding transfer - success or failure - rather than risk
corrupting the replacement's assigned id or wrongly diagnosing a healthy upload
as terminal-rejected. A number can be handed off more than once before any of
the intermediate replies arrive, and each handoff owes its own drop.

`KittyGraphicsWriter.WriteEncoded` is a checked public convenience, not a way
around validation. It accepts only query, transmit, or frame transmission
commands, decodes at most 3,072 bytes, requires an exact canonical re-encoding,
and validates the decoded query, RGB/RGBA, or complete PNG shape before mutating
the destination. Complete raw transmission always validates payload shape before
chunking; there is no public validation bypass. Neither `WriteEncoded` nor
`Write` accepts a zlib-compressed command: only `WriteTransmission` compresses
the raw payload before framing, so those two lower-level entry points reject
`o=z` commands outright rather than emit metadata that contradicts the bytes
they were given.

Placements carry the exact source pixel rectangle, destination cell width and
height, stable image/placement pair, deterministic frame-order z-index, and
`C=1` for cursor-anchored placement. Until the terminal acknowledges a
number-addressed upload with its assigned image id, the backend emits absolute
CUP to the pane-local destination before each APC and restores the semantic
`Frame.Cursor.Position` afterwards. It does not consume the terminal
save/restore slot. Kitty terminals are VT-compatible; this CUP requirement is
part of backend selection.

After acknowledgement, an eligible contain-mode placement switches to Kitty 0.28
Unicode placeholders. Kitty specifies that virtual placements always preserve
the source aspect ratio, so cover and stretch placements remain cursor-anchored
rather than silently acquiring contain semantics on a later frame. A virtual
placement command uses `U=1`; its ordinary cell stream then writes U+10EEEE
followed by explicit row and column diacritics from Kitty's fixed table. Image
ids above 24 bits add the table entry selected by the high byte as a third
diacritic. The low 24 image-id bits are carried as exact RGB foreground and the
placement id as exact RGB underline color. An indexed-256 profile uses the exact
low-byte palette indexes for both identifiers. These are protocol fields, so
they bypass ordinary color projection: SharpVision never quantizes them.
Basic-16 and monochrome profiles, indexed identifiers above 255, placement
identifiers above the representable range, or dimensions beyond the 297-entry
coordinate table retain cursor-anchored placement.

The placeholder projection is transactional overlay state rather than semantic
frame content. It participates in damage equality, replacement ordering, and
vertical-scroll detection while preserving the semantic underlay background for
transparent image pixels. Uploads and `U=1` virtual-placement preludes precede
placeholder cells; non-placeholder placements follow cell output; removals stay
last. Overlapping placeholders resolve in semantic placement order. A failed
write invalidates both the backend overlay and cell front before reconstruction.

Updating the same semantic placement reuses its image/placement pair. Removing
one placement of a shared image uses `a=d,d=n,I=...,p=...` before
acknowledgement (deleting by number) and `a=d,d=i,i=...,p=...` afterwards
(deleting by the terminal-assigned id). Removing the last use follows the same
reference transition, using `d=N` before acknowledgement and `d=I` afterwards,
freeing that exact image and all its placements. When a frame removes the final
visible Kitty placement, the renderer also performs a complete cell
reconstruction. Its standard clear-screen operation clears retained raster state
before replacement cells are painted, while the exact hard deletes still release
the corresponding stored image data.

The following two sequences show the actor interplay for the mechanisms above:
detecting support before any image is uploaded, and correlating a
number-addressed upload to its terminal-assigned id.

```mermaid
sequenceDiagram
    participant Application
    participant Terminal

    Note over Application,Terminal: One atomic query batch: the graphics probe<br/>precedes Primary Device Attributes in the same write
    Application->>Terminal: APC graphics query i=31,s=1,v=1,a=q,t=d,f=24;AAAA
    Application->>Terminal: CSI Primary Device Attributes (ordering barrier)
    opt Terminal understands the graphics command
        Terminal-->>Application: APC reply i=31,... (any strictly valid reply,<br/>even an error, proves support)
    end
    Terminal-->>Application: Primary DA reply
    Note over Application: A graphics query still unanswered when the DA reply<br/>arrives is recorded as unsupported
```

```mermaid
sequenceDiagram
    participant KittyGraphicsBackend
    participant Terminal
    participant Application
    participant Renderer

    KittyGraphicsBackend->>Terminal: transmit, number-addressed (I=n, quiet=0)
    Terminal-->>Application: APC reply i=<id>,I=n;OK
    Application->>Renderer: AcceptKittyGraphicsResponse(response)
    Renderer->>KittyGraphicsBackend: Accept(response)
    Note over KittyGraphicsBackend: Queued; the next prepared frame correlates<br/>I=n to i=<id> and switches addressing to the id (quiet=2)
    KittyGraphicsBackend->>Terminal: later placement/delete addressed by i (quiet=2)
```

## Frame transmission and animation control (protocol surface only)

`KittyGraphicsCommand.TransmitFrame` builds `a=f` frame data commands and
`KittyGraphicsCommand.Animate` builds `a=a` playback control commands;
`KittyGraphicsWriter` encodes both with the same validated, byte-exact, chunked
framing as `Transmit`. This is a typed protocol surface only: no renderer or
backend consumes these commands yet, so constructing and writing them produces
correct wire bytes with no visible effect until backend and renderer integration
lands.

`TransmitFrame` reuses `f` (format), `t=d` (direct medium only), and `s`/`v`
(raw RGB/RGBA frame-rectangle dimensions, omitted for PNG) exactly like
`Transmit`, chunked through the same 3,072-byte raw/4,096-byte encoded bounds.
Unlike a plain transmission, every frame continuation chunk re-asserts `a=f`
alongside `m`/`q`, because the protocol's default action for an unmarked
continuation chunk is a plain transmission, not a frame. Frame-specific fields
are optional and omitted at their protocol default: `c` is the nonzero base
frame this frame composites onto (default: transparent black), `x`/`y` is the
non-negative pixel offset where the frame data is placed within the image
(default: `0,0`), and `z` is the gap in milliseconds before the next frame,
where zero is ignored and a negative value creates a gapless frame. Every
`TransmitFrame` call creates a new frame; editing an already-transmitted frame
(the protocol's `r` key) is not exposed.

`Animate` emits `i` (image identifier) or `I` (image number) depending on
`UsesImageNumber`, exactly like every other action, alongside `s` (the playback
sub-action: `1` stops, `2` runs but waits for new frames, `3` runs), and emits
`v` (the raw Kitty loop count — zero is ignored, one plays infinitely, and a
larger value plays that many loops minus one) only when nonzero. Selecting a
specific frame as current (the protocol's `a=a` `c` key) is not exposed.
`Animate` never carries payload data, matching `Place` and `Delete`.

Primary source for frame and animation key semantics: the "Animation" section of
the
[Kitty terminal graphics protocol](https://sw.kovidgoyal.net/kitty/graphics-protocol/),
verified 2026-08-26 against the current published specification text.

## Bounds, ownership, and failure

The default image and placement identifier spaces each contain 4,096 active
values. `Graphics.ImageLimits` bounds image dimensions and owned source bytes.
The backend applies a 16 MiB complete prepared-output limit while staging, grows
one reusable pooled writer only within that limit, and rejects exhaustion before
transport I/O. Every successfully prepared transaction is committed exactly once
after byte-quiet completion or successful write and flush; `Changed` controls
byte emission and metrics, not transaction completion.

A preparation failure before any prepared transaction exists returns newly
rented identifiers. After preparation succeeds, cancellation, encoding, write,
or flush failure makes newly rented image IDs and image/placement pairs
uncertain: they remain reserved and cannot be reused. The next transaction emits
hard deletes for uncertain images and soft deletes for uncovered uncertain
placements. Only a successful flush and commit returns those IDs. Another
failure retains the tombstones and repeats cleanup, while the finite identifier
spaces keep uncertainty bounded.

`Renderer.ShutdownAsync(ITransport, CancellationToken)` is the normal shutdown
boundary. It serializes against rendering and disposal, prepares hard deletes
for all committed and uncertain images plus soft deletes for any uncertain
placement not covered by those images, writes and flushes them through the
borrowed transport, then releases local frames, identifiers, backend state, and
pooled bytes. Cancellation and I/O failures still release local state and
preserve the original exception. Repeated shutdown is byte-quiet. Parameterless
`Dispose()` is emergency local cleanup and cannot send terminal bytes. The
runtime host must invoke asynchronous renderer shutdown before disposing its
transport.

> [!WARNING]
>
> Disposing the renderer first permanently forfeits remote cleanup. `Dispose()`
> marks the renderer disposed, and a later `ShutdownAsync` observes that and
> returns immediately without emitting the hard and soft deletes — so uploaded
> images and their reserved identifiers stay live in the terminal for the rest
> of the terminal session. Always await `ShutdownAsync` before disposal when the
> transport is still usable.

Application implements that ordering with a finite cleanup timeout: it awaits
hard/soft deletes and flush, then disposes Session's borrowed transport and the
host lease. A renderer cleanup failure is retained as the lifetime diagnostic
but cannot skip those later disposal boundaries.

Application selects Kitty lazily at the first render after profile and resize
publication. The public Image control contributes only semantic fallback cells
and a placement. Later cell paint, including Window and elevated Popup output,
makes the whole intersected placement ineffective and causes retained removal.

## Multiplexers

Graphics passthrough requires an explicit outer profile, active visibility
policy, and `Multiplexing.MultiplexingOperation.Graphics`. Each APC upload chunk
or delete is wrapped independently through every tmux layer so a multi-chunk
image does not become one oversized envelope. Placement CUP and the final
cursor-restoring CUP remain ordinary pane-local bytes; only the Kitty APC
between them is sent through passthrough. GNU screen remains unavailable because
its conservative route is CSI-only. An unauthorized, Screen-containing, or
oversized route rejects backend selection or preparation without destination
mutation.

## Security and tests

Exact-byte tests cover the query, valid RGB/RGBA/PNG payloads across chunk
boundaries, zlib-compressed transmission and its determinism, placements,
updates, soft and hard deletion, frame transmission with and without composition
fields, animation playback control sub-actions and loop count, response quiet
modes, and rejected malformed, noncanonical, or shape-invalid Base64. Successful
and printable-error replies, malformed duplicate recovery, and enabled 8-bit APC
framing run at every possible transport split; numeric overflow, canonical IDs,
bounds, duplicate correlation, and redaction are also covered. Real
backend-to-renderer tests prove image caching, byte-quiet commit, stable ID
reuse, last-use deletion, cursor restoration, uncertain tombstone recovery,
ambiguous stale-reply correlation after number reuse, exact placeholder
identity colors and diacritics, placeholder scrolling, allocation-free cleanup
commit, per-delete tmux routing, Screen rejection, and explicit shutdown after
success or partial failure.

Local emulator evidence on 2026-07-20 found Kitty 0.46.2 installed, but the test
process had no `KITTY_PID` and standard input was not a TTY. A live frontend
smoke test was therefore unavailable and no GUI was launched; exact-byte,
fragmentation, renderer, and pseudoterminal evidence remain the verification for
this change.

## Sources

- [Kitty terminal graphics protocol](https://sw.kovidgoyal.net/kitty/graphics-protocol/)
  defines APC framing, query, direct media, chunking, placement, response, and
  deletion.

Source accessed 2026-08-29. Unicode-placeholder behavior is specified by the
source's “Unicode placeholders” section, introduced in Kitty 0.28.0.

## Expected behavior

| Layer         | Required evidence                                                                       |
| ------------- | --------------------------------------------------------------------------------------- |
| Writer/parser | Exact query/upload/place/delete bytes, every split, correlation, bounds, and recovery.  |
| Renderer      | ID lifetime, chunks, placeholders, damage, commit/tombstone order, retry, and cleanup.  |
| Integration   | Authorized multiplexer route, cell fallback, resize metrics, final bytes, and shutdown. |
