# SharpVision.Terminal

SharpVision.Terminal is the low-level terminal foundation used by SharpVision.
It provides incremental protocol handling, Unicode cell geometry, input
decoding, frame buffers, damage tracking, and deterministic terminal output for
.NET 10.

Applications normally reference the `SharpVision` package, which brings this
package in transitively. Reference `SharpVision.Terminal` directly when building
terminal infrastructure without the UI control layer.
