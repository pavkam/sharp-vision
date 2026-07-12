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

Owned image values are implemented. Semantic frame placements, graphics
backends, the public Image control, and renderer lifecycle remain unsupported
until their typed implementation and acceptance tests land. Cell controls do not
emit graphics escape sequences directly.
