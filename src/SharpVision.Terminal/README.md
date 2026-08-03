# SharpVision.Terminal

SharpVision.Terminal is the low-level terminal foundation used by SharpVision.
It provides incremental protocol handling, Unicode cell geometry, input
decoding, frame buffers, damage tracking, and deterministic terminal output for
.NET 10.

This package is published before `SharpVision`, which depends on its public
terminal runtime and protocol surface. Applications normally receive it
transitively through `SharpVision`; reference it directly only when building
lower-level terminal infrastructure.

See the
[terminal protocol specifications](https://github.com/pavkam/sharp-vision/blob/main/docs/protocols/index.md)
and the
[protocol coverage matrix](https://github.com/pavkam/sharp-vision/blob/main/docs/protocols/coverage-matrix.md)
for the exact implemented surface and fallback behavior.
