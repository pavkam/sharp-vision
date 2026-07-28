# SharpVision

SharpVision is a retained-mode terminal user interface library for .NET 10. It
provides mutable controls, deterministic layout and input routing, styling,
Unicode-aware rendering, menus, popups, windows, and application hosting.

This package is an alpha prerelease. Install the current version with:

```bash
dotnet add package SharpVision --version 0.2.0-alpha.1
```

The package depends on `SharpVision.Terminal`, which supplies the terminal
protocol, cell-geometry, buffer, input, and rendering foundation.

See the
[documentation](https://github.com/pavkam/sharp-vision/blob/main/docs/index.md)
for complete API contracts and examples, and check the
[protocol coverage matrix](https://github.com/pavkam/sharp-vision/blob/main/docs/protocols/coverage-matrix.md)
before depending on an optional terminal feature.
