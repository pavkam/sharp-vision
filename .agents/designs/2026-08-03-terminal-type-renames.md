# Purpose-Revealing Terminal Type Names

## Objective

Rename the remaining generically named `SharpVision.Terminal` types so each
public or internal name identifies its purpose without requiring namespace
resolution. This is an intentional alpha API break with no aliases, forwarding
types, obsolete wrappers, or migration layer.

The release version becomes `0.8.0-alpha.1`. The API snapshot gate retains only
the new version's current surface; every older snapshot directory is removed.

## Naming decisions

Existing aliases are promoted when they already describe the type accurately.
Names that need more context use subsystem and responsibility rather than only
the vendor name.

| Current type                | Replacement                 |
| --------------------------- | --------------------------- |
| `Protocols.Writer`          | `ProtocolWriter`            |
| `Iterm.Writer`              | `ItermWriter`               |
| `Sixel.Writer`              | `SixelWriter`               |
| `Kitty.Graphics.Writer`     | `KittyGraphicsWriter`       |
| `Kitty.Clipboard.Writer`    | `KittyClipboardWriter`      |
| `Rendering.Encoder`         | `FrameEncoder`              |
| `Sixel.Encoder`             | `SixelEncoder`              |
| `Graphics.Format`           | `ImageFormat`               |
| `Kitty.Graphics.Format`     | `KittyGraphicsFormat`       |
| `Graphics.Limits`           | `ImageLimits`               |
| `Geometry.Metrics`          | `CellMetrics`               |
| `Rendering.Metrics`         | `RenderMetrics`             |
| `Runtime.Options`           | `TerminalOptions`           |
| `Input.Options`             | `InputOptions`              |
| `Rendering.Palette`         | `TerminalPalette`           |
| `Sixel.Palette`             | `SixelPalette`              |
| `Multiplexing.Policy`       | `MultiplexingPolicy`        |
| `Unicode.Policy`            | `UnicodePolicy`             |
| `Multiplexing.Operation`    | `MultiplexingOperation`     |
| `Terminfo.Operation`        | `TerminfoOperation`         |
| `Kitty.Clipboard.Operation` | `KittyClipboardOperation`   |
| `Kitty.Graphics.Response`   | `KittyGraphicsResponse`     |
| `Xterm.Response`            | `XtermCapabilitiesResponse` |
| `Input.Text`                | `TerminalText`              |
| `Capabilities.Capabilities` | `TerminalCapabilities`      |
| `Rendering.Attributes`      | `TerminalAttributes`        |
| `Rendering.Canvas`          | `TerminalCanvas`            |
| `Kitty.Graphics.Medium`     | `KittyGraphicsMedium`       |

Every declaration moves to a same-named file. Namespaces stay unchanged because
the Kitty protocol-family split has already landed and the remaining defect is
the type identifier itself.

## Migration boundaries

All production references, XML documentation, tests, examples, package-consumer
fixtures, normative documentation, and repository guidance use the replacement
names directly. Alias directives made obsolete by the new declarations are
deleted. Unrelated aliases that distinguish UI concepts remain untouched.

No behavior, wire grammar, validation, visibility, member signature other than
the renamed containing type, or ownership rule changes. Historical Git history
is the migration record; the shipped assemblies contain only the new names.

## Version and API surface

`OverallVersion`, package version, assembly version derivation, and file version
derivation advance through the existing `Directory.Build.props` mechanism to
`0.8.0-alpha.1`. The compatibility test remains a current-surface regression
gate, but its `Snapshots` directory contains only the two approved
`0.8.0-alpha.1` baselines. No historical alpha snapshots remain.

The reviewed snapshot difference must consist only of the intended containing
type renames and references to those renamed types. An added, removed, or
otherwise changed member is a defect.

## Verification

The rename first proves its reach by making focused builds or tests fail on the
old identifiers. After every reference is migrated, verification covers:

1. Searches showing the retired fully qualified type names and obsolete aliases
   are absent from active source, tests, examples, and normative documentation.
2. Focused Terminal and UI tests exercising protocol writers, input routing,
   rendering, graphics, capabilities, runtime options, and Unicode policy.
3. Manual review of both generated `0.8.0-alpha.1` API snapshots.
4. `make format`, `make lint`, `make build`, and `make test` with zero warnings,
   zero errors, and the configured minimum test discovery.
