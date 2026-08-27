# Images

## Overview

`SharpVision.Terminal.Graphics.ImageSource` is an immutable, finite,
transport-neutral image source. Construction copies the caller's bytes before
returning, the value never exposes its backing array, and mutating the caller's
buffer afterward cannot change what is rendered.

The first source representations are:

- sRGB RGBA with exactly four bytes per pixel in red, green, blue, alpha order;
- encoded PNG with a validated signature, IHDR, chunk boundaries and type codes,
  per-chunk CRCs, critical-chunk ordering, palette legality, consecutive IDAT
  data, IEND termination, and positive pixel dimensions.

PNG validation establishes safe ownership and dimensions. The decoder converts
non-interlaced, 8- or 16-bit grayscale, RGB, indexed, grayscale-alpha, and RGBA
sources to straight RGBA8888 for raster-only backends; indexed sources remain 8
bits per index. A 16-bit-per-channel sample narrows to 8 bits by keeping its
most significant byte. It applies indexed alpha tables and the exact transparent
grayscale or RGB sample from `tRNS` as specified by the
[PNG Third Edition transparency chunk](https://www.w3.org/TR/png-3/#11tRNS),
comparing the full-width sample so a 16-bit source is never matched against a
narrowed value.

> [!IMPORTANT]
>
> **Implementation gap:** the decoder does not cover interlaced (Adam7) or
> sub-8-bit-per-channel (1, 2, or 4 bits) PNG sources. Structural validation
> accepts them, so `ImageSource.FromPng` succeeds and the failure surfaces later
> as a decode rejection: on a sixel-only terminal such a placement keeps its
> cell fallback and reports a `GraphicsDiagnostic`, while the Kitty and iTerm2
> backends transmit the same bytes untouched.

The Kitty and iTerm2 backends may transmit owned PNG directly when their
capability and route are proved.

On a terminal whose only graphics protocol is sixel, a supported PNG placement
is decoded and rendered through sixel after the Image control paints its
ordinary-cell fallback. A hosted `Application`/`ConsoleApplication` raises
`GraphicsDiagnostic` after any frame that leaves a placement falling back,
carrying an immutable snapshot with one `GraphicsPlacementDiagnostic` per
placement and its `ImageIdentity` and `GraphicsPlacementSkipReason` — so the
degradation is observable instead of silent. A directly owned `Renderer`,
outside the hosted path, reads the same immutable snapshot from every successful
render's `Metrics.GraphicsDiagnostics`. Reasons distinguish an unsupported image
representation, a deauthorized protocol, and otherwise-supported image data
whose crop, geometry, or placement mode cannot be encoded. A placement that only
fails the remaining bounded frame output budget reports that limit separately.
Diagnostic construction rejects the empty image identity and undefined reasons.
The valid default `Metrics` value exposes an empty diagnostic snapshot, never
null.

## Bounds and validation

`Graphics.ImageLimits` applies before any allocation or ownership transfer. The
defaults permit at most 16,384 pixels on either axis, 67,108,864 total pixels,
and 256 MiB of source bytes. Applications may supply smaller positive limits. A
pixel limit cannot exceed the area addressable by its dimension limit.

RGBA dimensions, the exact byte count, and checked multiplication are validated
before copying. PNG chunk lengths are treated as untrusted big-endian values;
every chunk CRC covers its type and data according to the
[PNG Third Edition CRC algorithm](https://www.w3.org/TR/png-3/#5CRC-algorithm).
The same specification's
[chunk naming rules](https://www.w3.org/TR/png-3/#5Chunk-naming-conventions)
make unknown critical chunks fatal while unknown ancillary chunks remain
skippable. Chunk type codes contain only ASCII letters and keep their reserved
third byte uppercase. The shared structural pass also requires one leading IHDR,
no duplicate or late PLTE/tRNS chunks, consecutive IDAT chunks, and one final
IEND. PLTE contains 1 to 256 complete RGB entries, appears only for color types
2, 3, or 6, and cannot exceed an indexed source's bit-depth range. Truncated,
overflowing, trailing, corrupted, structurally invalid, and policy-exceeding
containers fail without publishing an image. Raster decoding requires exactly
the declared scanline bytes after decompression; shorter or longer payloads are
rejected rather than partially decoded.

Every image receives a stable, nonzero, process-local identity. The identity is
semantic cache input - it is not a terminal protocol identifier and not a
content hash. Remote image and placement identifiers remain renderer-owned.

## Copy boundary

`ImageSource.CopyTo` validates the complete caller destination before copying,
so a destination that is too short is left unchanged. Internal synchronous
encoders may borrow the immutable source bytes only for the duration of the
call; no span may cross an asynchronous transport or dispatcher boundary.

## Supported backends

Owned image values, semantic frame placements, the transactional renderer
lifecycle, the Kitty, sixel, and iTerm2 backends, and evidence-based protocol
selection are implemented and connected through `Application`. Backend selection
happens lazily at the first render, after negotiated profile publication and the
first resize barrier. Kitty accepts RGBA and PNG; sixel accepts RGBA and the
supported decoded PNG subset with measured cell-pixel geometry; iTerm2 3.5+
multipart accepts complete PNG with contain or stretch under an explicit
override. A detected multiplexer route is always passed to selection, including
when passthrough is unauthorized, so direct graphics cannot leak around the
policy. `TerminalOptions.Multiplexing` carries that routing policy independently
of `Negotiation`, so a host that pins `Profile` or `Capabilities` to avoid
probing (which discards the rest of `Negotiation`) still routes graphics through
an approved passthrough instead of silently degrading to the unsafe direct path.

The public [`Image` control](../controls/display/image.md#overview) paints a
deterministic cell fallback and records only a semantic placement. Exact resize
metrics are inherited through the control tree before layout and are passed to
the renderer for the same frame. `Application` awaits finite graphics shutdown
before `Session` disposes the borrowed transport; Kitty deletes and flushes
remote state first, and a cleanup failure cannot skip transport or host-lease
disposal.

Once a backend family is selected, it is fixed for that `Application` lifetime.
Every selected backend still revalidates the current profile on each frame:
revocation removes retained Kitty state or requests complete non-retained cell
repair, and emits no new graphics. A later profile cannot switch the selected
backend family; constructing a new `Application` performs fresh selection. If
authoritative evidence for the already-selected family returns, that family may
resume through its ordinary full-repair path. A renderer that has no backend yet
is not held to cell-only fallback forever: the first later profile that proves
authoritative graphics support promotes it to that backend family, the same as
if the evidence had been available when the renderer was constructed.

## Expected behavior

| Layer       | Required evidence                                                                                      |
| ----------- | ------------------------------------------------------------------------------------------------------ |
| Unit        | Bounds, copy validation, borrowed ownership, immutable placement values, and backend selection.        |
| Rendering   | Cell fallback, placement provenance, occlusion, revocation, repair, and exact backend bytes.           |
| Integration | Resize metrics, negotiated publication, lazy selection, ordered graphics output, and bounded shutdown. |

- RGBA and PNG support boundaries hold independently of each other.
- Missing metrics, unauthorized multiplexers, unsupported stretch modes,
  cancellation, write and flush failure, and cleanup after partial remote state
  all behave as described above.
