# SharpVision

SharpVision is a retained-mode terminal user interface library for .NET 10. It
provides mutable controls, deterministic layout and input routing, styling,
Unicode-aware rendering, menus, popups, windows, and application hosting.

This project is a prerelease at version `1.6.0-beta.4` and may change before the
stable API.

The package depends on `SharpVision.Terminal`, which supplies the terminal
protocol, cell-geometry, buffer, input, and rendering foundation, and installs
it transitively.

See the
[documentation](https://github.com/pavkam/sharp-vision/blob/main/docs/index.md)
for complete API contracts and examples, and check the
[protocol coverage matrix](https://github.com/pavkam/sharp-vision/blob/main/docs/protocols/coverage-matrix.md)
before depending on an optional terminal feature.
