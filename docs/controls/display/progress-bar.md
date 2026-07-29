# ProgressBar

## ProgressBar contract

`ProgressBar` displays a non-interactive visual progress indicator as a filled
bar. It cannot receive focus, is excluded from hit testing, and owns no
children.

## API

| Property                                        | Default      | Purpose                                                                         |
| ----------------------------------------------- | ------------ | ------------------------------------------------------------------------------- |
| `Minimum`, `Maximum`                            | `0`, `1`     | Define the finite, strictly increasing value range.                             |
| `Value`                                         | `0`          | Selects determinate progress and clamps into the current range.                 |
| `IsIndeterminate`                               | `false`      | Replaces value-based fill with the deterministic unknown-duration presentation. |
| `Orientation`                                   | `Horizontal` | Chooses left-to-right or bottom-to-top fill.                                    |
| `UseSubCellResolution`                          | `false`      | Uses code-owned fractional block levels for finer terminal-cell progress.       |
| `FillColor`, `TrackColor`, `IndeterminateColor` | `null`       | Override theme colors for each rendered part.                                   |
| `FillGlyph`, `TrackGlyph`, `IndeterminateGlyph` | Code-owned   | Override validated one-cell glyphs; `ResetGlyphs()` restores defaults.          |

## Behavior

- `Minimum` and `Maximum` are finite `double` endpoints with
  `Minimum < Maximum`. Their defaults are zero and one.
- `Value` is finite and clamps into the inclusive range. Its default is zero.
- `IsIndeterminate` selects the deterministic unknown-duration presentation.
- `Orientation` controls horizontal left-to-right or vertical bottom-to-top
  filling. Default is `Horizontal`.
- `UseSubCellResolution` selects the nine code-owned ordered fractional levels.
  Its default is `false`.
- `FillGlyph`, `TrackGlyph`, and `IndeterminateGlyph` are validated one-cell
  local overrides. `ResetGlyphs()` clears all three overrides.

`ValueChanged` fires for every committed `Value` transition regardless of which
public setter caused it — `Value` directly, or `Minimum`/`Maximum` clamping it.
`PropertyChanged(Value)` and `ValueChanged` always agree: both observe the same
history, and a clamp that leaves `Value` unchanged raises neither. The event
args expose the committed value as `Value`, matching the other range controls.

Non-finite values throw `ArgumentOutOfRangeException`. An endpoint that would
make the range empty or reversed throws `ArgumentException`. All validation
occurs before mutation. Changing an endpoint clamps the current value in the
same transaction before any notification fires, so `Minimum`/`Maximum`'s own
`PropertyChanged` and the subsequent `Value` notifications all observe coherent,
already-clamped state; `PropertyChanged(Minimum)` or `PropertyChanged(Maximum)`
fires first, followed by `PropertyChanged(Value)` and `ValueChanged` when
clamping changed it.

Determinate rendering normalizes `(Value - Minimum) / (Maximum - Minimum)` and
fills `floor(normalized * axisCells)` complete cells. The maximum fills every
cell. Remaining cells use the code-owned empty-progress glyph. Horizontal fill
starts at the left; vertical fill starts at the bottom.

Without a local override, all progress cells resolve from the code-owned
progress glyph defaults. A glyph that is unsuitable under the active width
policy uses its code-owned fallback. Theme replacement recolors an existing bar
without changing its glyphs. Indeterminate rendering fills the committed content
bounds with the code-owned indeterminate glyph.

Without local overrides, completed and indeterminate progress use the focused
theme foreground while incomplete track cells use `Theme.Muted`. These part
properties override those fallbacks while preserving background and attributes.

The intrinsic desired size is ten cells on the main axis and one cell on the
cross axis, and both alignment axes default to `Stretch`. Rendering uses the
resolved visual-state style, draws inside `ContentBounds`, and participates in
shared intrinsic chrome. Zero content bounds draw nothing. The control never
handles pointer or keyboard input.

## Example

```csharp
var bar = new ProgressBar
{
    Maximum = 100,
    Value = 42,
    Orientation = Orientation.Horizontal,
};
```

## Test obligations

Cover finite validation, range ordering, value clamping, endpoint notification
visibility, horizontal and vertical fill direction, empty/partial/full values,
sub-cell resolution, deterministic indeterminate rendering, zero/tiny bounds,
mutation, resize, appearance inheritance, non-interactive hit testing, and final
cells. `ProgressBarSurfaceTests` must prove terminal-visible determinate and
indeterminate states through a mounted application.
