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

Sources accessed 2026-07-20; Feature Reporting re-verified 2026-08-02.

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
legacy compatibility is claimed: the 3.5 multipart form supersedes it, and its
single all-in-one payload could not be budgeted across multiplexer envelopes.

Every complete OSC frame, including introducer and ST, is at most 1,048,576
bytes. Raw part size is derived from Base64 and exact frame overhead. Under
tmux, the inner bound is reduced using exact per-layer passthrough framing and
ESC-doubling math so every outer envelope also remains within route policy. The
whole multipart transaction is staged under a separate finite caller bound;
limit or destination failure never exposes a partial transaction. An authorized
route too small for metadata, one Base64 group, or FileEnd leaves the placement
on cell fallback rather than failing the render.

The writer itself accepts structurally validated owned PNG only; it never
decodes the payload it transmits. An RGBA `ImageSource`, which has no dedicated
iTerm2 wire format, is PNG-encoded on demand at the non-retained backend
boundary — never inside the writer — before it ever reaches `ItermWriter`, so
the writer's PNG-only contract is unchanged. The protocol cannot express a
clipped pixel source or aspect-preserving cover crop, so only a complete PNG or
RGBA source with contain or stretch is eligible. Cover and partial-source
placements retain cell fallback regardless of source format. Profile mutation,
annotations, shell integration, clipboard streaming, generic file transfer, and
every other OSC 1337 feature stay out of scope: they manage the user's terminal
application and filesystem rather than present application output.

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
iTerm2 handles compatible PNG or RGBA (PNG-encoded on demand) only when
`ItermImages` is Supported with Query or Override origin. This keeps mixed
RGBA/PNG order intact and lets PNG or RGBA remain viable when sixel metrics are
absent.

`ItermImages=true` is an explicit assertion that the destination implements the
iTerm2 3.5-or-newer multipart protocol. Database and tentative `TERM_PROGRAM`
evidence do not authorize output — only an explicit override, or a positive
`OSC 1337 ; Capabilities` reply carrying the `FILE` code (see below), can.

Capability discovery consumes `OSC 1337 ; Capabilities` as query-origin
evidence: `ActiveQueryDiscoveryStrategy` emits the query whenever the planning
projection of `ItermImages` (baseline capabilities with environment evidence
already applied, but never assigned back to the baseline itself) is
`Unknown`/`Tentative` with no override, and parses the reply's concatenated
feature codes for a bare `F`. Under a multiplexer, environment evidence already
narrows `ItermImages` to `Unsupported`, so the probe is not written at all — a
multiplexer cannot carry it and would only ever time out. A terminal that is
asked and stays silent leaves `ItermImages` as absent query evidence rather than
resolving it to an explicit `false`: `Origin.Query` means a bounded terminal
query supplied the evidence, and a timeout supplied none. Coercing silence to
`Unsupported`/`Query` used to overwrite a genuine `TERM_PROGRAM=iTerm.app`
`Tentative`/`Environment` hint with the (identical, in this case)
`Unsupported`/`Query` conclusion — strictly worse information, since it asserted
"confirmed unsupported" where the truth was "unconfirmed". The batch also
carries a terminating fence (a trailing `CSI 6n`) that retires every
still-unanswered family, including this one, without granting it query-origin
evidence — see
[Discovery pipeline](../architecture/discovery-pipeline.md#overview).
`TERM_FEATURES` is not separately consumed: it would only ever set the same
`Tentative`/`Environment` hint that `TERM_PROGRAM == "iTerm.app"` already sets,
whose sole function is making the query fire, so a second environment variable
doing the identical job was judged not worth adding.

> [!NOTE]
>
> The published feature table assigns code `F` to both `FILE` and
> `FOCUS_REPORTING` with no documented disambiguation (see Sources below) — this
> library cannot tell which meaning a bare `F` in the reply denotes from the
> code alone. `FocusReporting` is unaffected because it has its own unambiguous
> DEC private mode query; only `ItermImages` reads the `F` code, and only in
> combination with the version corroborator described next.

Because of that ambiguity, and because Feature Reporting predates iTerm2 3.5's
multipart protocol while `FILE` documents only the legacy single-sequence form
SharpVision never emits, a bare `FILE`/`F` reply is corroborated — never
disambiguated — by `TERM_PROGRAM_VERSION`. `QueryEvidenceAdapter` downgrades an
otherwise-Supported `ItermImages` value to `Unsupported` when
`TERM_PROGRAM_VERSION` parses below `3.5`; an absent, unparseable, or `>= 3.5`
version leaves Query (or a later Override) evidence untouched. This is narrowing
only — the version can withhold Supported evidence but can never by itself grant
it, consistent with `CapabilitySupport`'s own contract that `Tentative` "must
not enable it."

Application host selection creates the backend lazily after profile and resize
publication and consumes semantic placements from the public Image control.

Unsupported source, fitting, route, or evidence keeps the control's previously
painted cell fallback.

## Security and tests

Exact-byte tests freeze metadata order, exact size, cell dimensions,
preserve-aspect behavior, inline-only policy, omitted name, PNG Base64,
multipart boundaries, and canonical ST. They prove official sequence math, exact
nested tmux bounds, route-aware large-payload reconstruction, atomic output
bounds, destination exception fidelity, tiny-route fallback, on-demand RGBA PNG
encoding reaching the same multipart transaction as an owned PNG source, and
rejection of cover, partial-source, legacy `File`, BEL output, and Screen.

The generic router accepts bounded OSC 1337 with ST or BEL at every split and
recovers following input after overflow. Backend and real renderer tests cover
cursor restoration, stale-pixel repair, intersecting and unrelated damage,
transitive same- and mixed-protocol overlap repaint, unsupported-upper fallback,
allocation-free synchronous phases, byte-quiet cleanup, independently routed
tmux frames, transport failure, and full retry. Selector tests freeze
Kitty-over-fallback priority, origin requirements (including that Query now
authorizes iTerm2 output the same way it already does Kitty and sixel), route
authorization, mixed paint order, and missing-metric PNG/RGBA viability.
`ActiveQueryDiscoveryStrategy` tests cover the Capabilities query gate, `FILE`
code parsing, and silent-terminal negative inference; `CapabilityDetector` tests
cover the `TERM_PROGRAM_VERSION` narrowing corroborator.
Application/public-control coverage shares the final-byte, conservative-route,
and failure-safe shutdown tests with the other graphics backends.

## Sources

- [iTerm2 Inline Images Protocol](https://iterm2.com/documentation-images.html)
- [iTerm2 Proprietary Escape Codes](https://iterm2.com/documentation-escape-codes.html)
- [iTerm2 Feature Reporting](https://iterm2.com/feature-reporting/)

Sources accessed 2026-07-28; Feature Reporting re-verified 2026-08-02.

## Expected behavior

| Layer     | Required evidence                                                                            |
| --------- | -------------------------------------------------------------------------------------------- |
| Writer    | Exact multipart metadata, Base64 chunks, limits, terminators, and validation.                |
| Selection | Query- or Override-origin 3.5+ evidence, metrics/stretch constraints, and authorized routes. |
| Rendering | Upload/paint order, damage repair, failure retry, cleanup, and final bytes.                  |
