# SharpVision.Terminal

SharpVision.Terminal is the low-level terminal foundation used by SharpVision.
It provides incremental protocol handling, Unicode cell geometry, input
decoding, frame buffers, damage tracking, and deterministic terminal output for
.NET 10.

This package is an alpha prerelease. Install the current version with:

```bash
dotnet add package SharpVision.Terminal --version 0.2.0-alpha.1
```

Applications normally reference the `SharpVision` package, which brings this
package in transitively. Reference `SharpVision.Terminal` directly when building
terminal infrastructure without the UI control layer.

See the
[terminal protocol specifications](https://github.com/pavkam/sharp-vision/blob/main/docs/protocols/index.md)
and the
[protocol coverage matrix](https://github.com/pavkam/sharp-vision/blob/main/docs/protocols/coverage-matrix.md)
for the exact implemented surface and fallback behavior.
