# SharpVision.Terminal

SharpVision.Terminal is the low-level terminal foundation used by SharpVision.
It provides incremental protocol handling, Unicode cell geometry, input
decoding, frame buffers, damage tracking, and deterministic terminal output for
.NET 10.

This project is currently non-packable and has no published NuGet package. Use a
repository project reference when building terminal infrastructure directly. The
`SharpVision` UI package is intended to bring this layer transitively once a
matching terminal package is published.

See the
[terminal protocol specifications](https://github.com/pavkam/sharp-vision/blob/main/docs/protocols/index.md)
and the
[protocol coverage matrix](https://github.com/pavkam/sharp-vision/blob/main/docs/protocols/coverage-matrix.md)
for the exact implemented surface and fallback behavior.
