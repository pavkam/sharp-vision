# iTerm2 proprietary protocols

## Overview

Primary sources:

- [iTerm2 Inline Images Protocol](https://iterm2.com/documentation-images.html)
  defines original `File`, iTerm2 3.5 multipart transfer, arguments, cell units,
  inline behavior, ST/BEL termination, and the 1,048,576-byte sequence limit;
- [iTerm2 Proprietary Escape Codes](https://iterm2.com/documentation-escape-codes.html)
  defines the OSC 1337 family;
- [iTerm2 Feature Reporting](https://iterm2.com/feature-reporting/) defines
  `Capabilities`, `TERM_FEATURES`, and the `FILE` feature boundary.

Sources accessed 2026-07-20.

iTerm2 accepts BEL or ST for OSC 1337. SharpVision emits canonical 7-bit OSC
with ST (`ESC \`) only. The generic bounded parser continues to recognize and
own both terminators for inbound diagnostic observation and recovery.

## Implemented multipart writer

SharpVision implements the ordered multipart form introduced in iTerm2 3.5:

```text
OSC 1337;MultipartFile=size=B;width=N;height=N;preserveAspectRatio=P;inline=1 ST
OSC 1337;FilePart=BASE64 ST
OSC 1337;FileEnd ST
```

`B` is the exact owned PNG byte count. Bare positive `N` values specify terminal
cells. Contain uses `preserveAspectRatio=1`; stretch uses 0. `inline=1` is
always present so data can never become a file download. The optional `name`
argument is omitted. The older single-sequence `File` form is not emitted and no
legacy compatibility is claimed.

Every complete OSC frame, including introducer and ST, is at most 1,048,576
bytes. Raw part size is derived from Base64 and exact frame overhead. Under
tmux, the inner bound is reduced using exact per-layer passthrough framing and
ESC-doubling math so every outer envelope also remains within route policy. The
whole multipart transaction is staged under a separate finite caller bound;
limit or destination failure never exposes a partial transaction. An authorized
route too small for metadata, one Base64 group, or FileEnd leaves the placement
on cell fallback rather than failing the render.

The writer accepts structurally validated owned PNG only. It never decodes PNG
or accepts RGBA. The protocol cannot express a clipped pixel source or
aspect-preserving cover crop, so only a complete PNG source with contain or
stretch is eligible. Cover and partial-source placements retain cell fallback.
Profile mutation, annotations, shell integration, clipboard streaming, generic
file transfer, and every other OSC 1337 feature remain unsupported.

## Non-retained backend and selection

iTerm2 inline images have no retained remote identity. The backend anchors each
image with pane-local CUP, emits multipart frames after ordinary cells, and
restores the semantic frame cursor. Movement, removal, PNG-to-unsupported
replacement, resize, or invalidation requests full cell reconstruction before
target placements are repainted. Cell damage intersecting an unchanged image
repaints it and the transitive closure of later overlapping placements in
original paint order; unrelated damage is byte-quiet. A later overlap that is
not encodable keeps itself and every affected lower placement on ordinary cell
fallback. Failed transport output invalidates the transaction and the next
render reconstructs cells and images. Shutdown is byte-quiet because no remote
IDs or delete command exist.

Direct output is supported. An explicitly authorized tmux route wraps
MultipartFile, every FilePart, and FileEnd independently. CUP remains
pane-local. GNU Screen and inactive, hidden, or operation-unauthorized routes
are unavailable.

Backend selection is evidence-based. Kitty query or override evidence wins
globally because its retained backend supports RGBA and PNG. Without Kitty, one
shared non-retained backend walks placements in original paint order: sixel
handles RGBA only with supported query/override evidence and exact metrics;
iTerm2 handles compatible PNG only when `ItermImages` is Supported with Override
origin. This keeps mixed RGBA/PNG order intact and lets PNG remain viable when
sixel metrics are absent.

`ItermImages=true` is an explicit assertion that the destination implements the
iTerm2 3.5-or-newer multipart protocol. Query, database, and tentative
`TERM_PROGRAM` evidence do not authorize output. Application host selection
creates the backend lazily after profile and resize publication and consumes
semantic placements from the public Image control.

> [!IMPORTANT]
>
> **Implementation gap:** Capability discovery does not consume iTerm2
> `Capabilities` or `TERM_FEATURES` `FILE` evidence. Direct iTerm2 image output
> therefore requires an explicit `ItermImages=true` override even when a local
> terminal could provide authoritative FILE support evidence. Issue #230 tracks
> consuming this evidence.

Unsupported source, fitting, route, or evidence keeps the control's previously
painted cell fallback.

## Security and tests

Exact-byte tests freeze metadata order, exact size, cell dimensions,
preserve-aspect behavior, inline-only policy, omitted name, PNG Base64,
multipart boundaries, and canonical ST. They prove official sequence math, exact
nested tmux bounds, route-aware large-payload reconstruction, atomic output
bounds, destination exception fidelity, tiny-route fallback, and rejection of
RGBA, cover, partial sources, legacy `File`, BEL output, and Screen.

The generic router accepts bounded OSC 1337 with ST or BEL at every split and
recovers following input after overflow. Backend and real renderer tests cover
cursor restoration, stale-pixel repair, intersecting and unrelated damage,
transitive same- and mixed-protocol overlap repaint, unsupported-upper fallback,
allocation-free synchronous phases, byte-quiet cleanup, independently routed
tmux frames, transport failure, and full retry. Selector tests freeze
Kitty-over-fallback priority, origin requirements, route authorization, mixed
paint order, and missing-metric PNG viability. Application/public-control
coverage shares the final-byte, conservative-route, and failure-safe shutdown
tests with the other graphics backends.

## Sources

- [iTerm2 Inline Images Protocol](https://iterm2.com/documentation-images.html)
- [iTerm2 Proprietary Escape Codes](https://iterm2.com/documentation-escape-codes.html)
- [iTerm2 Feature Reporting](https://iterm2.com/feature-reporting/)

Sources accessed 2026-07-28.

## Expected behavior

| Layer     | Required evidence                                                             |
| --------- | ----------------------------------------------------------------------------- |
| Writer    | Exact multipart metadata, Base64 chunks, limits, terminators, and validation. |
| Selection | Explicit 3.5+ evidence, metrics/stretch constraints, and authorized routes.   |
| Rendering | Upload/paint order, damage repair, failure retry, cleanup, and final bytes.   |
