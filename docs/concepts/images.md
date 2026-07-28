# Images

## Image ownership contract

`SharpVision.Terminal.Graphics.Image` is an immutable, finite, transport-neutral
image source. Construction copies caller bytes before returning. The value never
exposes its backing array, and later caller mutation cannot change rendering.

The first source representations are:

- sRGB RGBA with exactly four bytes per pixel in red, green, blue, alpha order;
- encoded PNG with a validated signature, IHDR, chunk boundaries, IDAT presence,
  IEND termination, and positive pixel dimensions.

PNG validation establishes safe ownership and dimensions; it is not raster
decoding. Sixel therefore consumes RGBA only. Kitty and iTerm2 backends may
transmit owned PNG directly when their capability and route are proved.

## Bounds and validation

`Graphics.Limits` applies before allocation or ownership transfer. Defaults
permit at most 16,384 pixels on either axis, 67,108,864 total pixels, and 256
MiB of source bytes. Applications may supply smaller positive limits. A pixel
limit cannot exceed the area addressable by its dimension limit.

RGBA dimensions, exact byte count, and checked multiplication are validated
before copying. PNG chunk lengths are treated as untrusted big-endian values;
truncated, overflowing, trailing, structurally invalid, or policy-exceeding
containers fail without publishing an image.

Every image receives a stable, nonzero process-local identity. The identity is
semantic cache input, not a terminal protocol identifier and not a content hash.
Remote image and placement identifiers remain renderer-owned.

## Copy boundary

`Image.CopyTo` validates the complete caller destination before copying. A short
destination is unchanged. Internal synchronous encoders may borrow the immutable
source bytes only for the duration of the call; no span may cross an
asynchronous transport or dispatcher boundary.

## Current support boundary

Owned image values, semantic frame placements, transactional renderer lifecycle,
Kitty/sixel/iTerm2 backends, and evidence-based protocol selection are
implemented and connected through Application. Backend selection occurs lazily
at the first render, after negotiated profile publication and the first resize
barrier. Kitty accepts RGBA and PNG; sixel accepts RGBA only with measured
cell-pixel geometry; iTerm2 3.5+ multipart accepts complete PNG with contain or
stretch under explicit override. A detected multiplexer route is always passed
to selection, including when passthrough is unauthorized, so direct graphics
cannot leak around the policy.

The public [`Image` control](../controls/display/image.md#image-contract) paints
deterministic cell fallback and records only a semantic placement. Exact resize
metrics are inherited through the control tree before layout and are passed to
the renderer for the same frame. Application awaits finite graphics shutdown
before Session disposes the borrowed transport; Kitty deletes and flushes remote
state first, and a cleanup failure cannot skip transport or host-lease disposal.

Backend family selection is fixed for one Application lifetime. Every selected
backend still revalidates the current profile on each frame: revocation removes
retained Kitty state or requests complete non-retained cell repair and emits no
new graphics. A later profile cannot promote a cell-only renderer or switch the
selected backend family; constructing a new Application performs fresh
selection. If authoritative evidence for the already-selected family returns,
that family may resume through its ordinary full-repair path.

## Test obligations

| Layer       | Required evidence                                                                                      |
| ----------- | ------------------------------------------------------------------------------------------------------ |
| Unit        | Bounds, copy validation, borrowed ownership, immutable placement values, and backend selection.        |
| Rendering   | Cell fallback, placement provenance, occlusion, revocation, repair, and exact backend bytes.           |
| Integration | Resize metrics, negotiated publication, lazy selection, ordered graphics output, and bounded shutdown. |

- Cover RGBA and PNG support boundaries independently.
- Cover missing metrics, unauthorized multiplexers, unsupported stretch modes,
  cancellation, write/flush failure, and cleanup after partial remote state.
