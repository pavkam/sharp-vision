# Kitty graphics protocol

## Kitty graphics contract

Primary source:
[Kitty terminal graphics protocol](https://sw.kovidgoyal.net/kitty/graphics-protocol/),
accessed 2026-07-20. A command is APC `ESC _ G`, comma-separated control data,
an optional semicolon plus Base64 data, and ST `ESC \`.

`Kitty.Graphics.Command` and `Kitty.Graphics.Writer` provide the typed
direct-data surface. Image identifiers and placement identifiers are canonical
nonzero unsigned 32-bit decimal values: leading zeroes, signs, and overflow are
rejected. The official direct RGB query uses `f=24`; transmission accepts only
RGBA `f=32` and PNG `f=100`. RGB transmission and zlib compression are explicit
unsupported values. File, temporary-file, and shared-memory media are rejected
before output because they cross path and external-lifetime trust boundaries.
Animation and Unicode placeholder presentation are outside this contract.

## Supported features

Typed and implemented behavior includes:

- the official one-pixel direct RGB query;
- strict bounded typed response parsing and numeric correlation;
- direct RGBA and PNG upload, retained placement/update, and deletion;
- renderer-owned finite image and placement identifiers;
- 4,096-byte maximum encoded data chunks with metadata-minimal continuation
  chunks;
- transactional upload, cell, placement, and removal ordering;
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

Replies are APC payloads beginning with `G`. Only one `i` field and optional `p`
field are accepted, both canonical nonzero ASCII decimal values within `uint`.
The nonempty response text must be printable ASCII and is copied within
`Protocols.Limits.MaxMetadataBytes`. `Response.Message` exposes that bounded but
untrusted terminal text to an explicit caller. Diagnostics and `ToString()`
never include it. Malformed, duplicate-field, overflowing, unknown-field, late,
and unrelated replies cannot consume another transaction.

## Upload and placement

Direct data is Base64 encoded into APC payloads no longer than 4,096 bytes. A
non-final chunk therefore contains 3,072 raw bytes and has a Base64 length
divisible by four. After the first chunk, only `m` and optional `q` metadata are
emitted. One image finishes all chunks before another graphics command begins.
The backend uses quiet mode 2 and never opens a terminal-supplied path.

`Writer.WriteEncoded` is a checked public convenience, not an unchecked framing
seam. It accepts only query or transmit commands, decodes at most 3,072 bytes,
requires an exact canonical re-encoding, and validates the decoded query, RGBA,
or complete PNG shape before mutating the destination. Complete raw transmission
always validates payload shape before chunking; there is no public validation
bypass.

Placements carry the exact source pixel rectangle, destination cell width and
height, stable image/placement pair, deterministic frame-order z-index, and
`C=1` so the command itself does not advance the cursor. Kitty placement is
anchored at the current cursor, so the backend emits absolute CUP to the
pane-local destination before each APC. After all placements it emits one
absolute CUP to the semantic `Frame.Cursor.Position`. It does not consume the
terminal save/restore slot. Kitty terminals are VT-compatible; this CUP
requirement is part of backend selection.

Updating the same semantic placement reuses its image/placement pair. Removing
one placement of a shared image uses `a=d,d=i,i=...,p=...`. Removing the last
use sends `a=d,d=I,i=...`, which frees image data and all its placements.

## Bounds, ownership, and failure

The default image and placement identifier spaces each contain 4,096 active
values. `Graphics.Limits` bounds image dimensions and owned source bytes. The
backend applies a 16 MiB complete prepared-output limit while staging, grows one
reusable pooled writer only within that limit, and rejects exhaustion before
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
transport. Application implements that ordering with a finite cleanup timeout:
it awaits hard/soft deletes and flush, then disposes Session's borrowed
transport and the host lease. A renderer cleanup failure is retained as the
lifetime diagnostic but cannot skip those later disposal boundaries.

Application selects Kitty lazily at the first render after profile and resize
publication. The public Image control contributes only semantic fallback cells
and a placement. Later cell paint, including Window and elevated Popup output,
makes the whole intersected placement ineffective and causes retained removal.

## Multiplexers

Graphics passthrough requires an explicit outer profile, active visibility
policy, and `Multiplexing.Operation.Graphics`. Each APC upload chunk or delete
is wrapped independently through every tmux layer so a multi-chunk image does
not become one oversized envelope. Placement CUP and the final cursor-restoring
CUP remain ordinary pane-local bytes; only the Kitty APC between them is sent
through passthrough. GNU screen remains unavailable because its conservative
route is CSI-only. An unauthorized, Screen-containing, or oversized route
rejects backend selection or preparation without destination mutation.

## Security and tests

Exact-byte tests cover the query, valid RGBA/PNG payloads across chunk
boundaries, placements, updates, soft and hard deletion, response quiet modes,
and rejected malformed, noncanonical, or shape-invalid Base64. Successful and
printable-error replies, malformed duplicate recovery, and enabled 8-bit APC
framing run at every possible transport split; numeric overflow, canonical IDs,
bounds, duplicate correlation, and redaction are also covered. Real
backend-to-renderer tests prove image caching, byte-quiet commit, stable ID
reuse, last-use deletion, cursor restoration, uncertain tombstone recovery,
allocation-free cleanup commit, per-delete tmux routing, Screen rejection, and
explicit shutdown after success or partial failure.

Local emulator evidence on 2026-07-20 found Kitty 0.46.2 installed, but the test
process had no `KITTY_PID` and standard input was not a TTY. A live frontend
smoke test was therefore unavailable and no GUI was launched; exact-byte,
fragmentation, renderer, and pseudoterminal evidence remain the verification for
this change.

## Sources

- [Kitty terminal graphics protocol](https://sw.kovidgoyal.net/kitty/graphics-protocol/)
  defines APC framing, query, direct media, chunking, placement, response, and
  deletion.

Source accessed 2026-07-28.

## Expected behavior

| Layer         | Required evidence                                                                       |
| ------------- | --------------------------------------------------------------------------------------- |
| Writer/parser | Exact query/upload/place/delete bytes, every split, correlation, bounds, and recovery.  |
| Renderer      | ID lifetime, chunks, commit/tombstone order, occlusion, revocation, retry, and cleanup. |
| Integration   | Authorized multiplexer route, cell fallback, resize metrics, final bytes, and shutdown. |
