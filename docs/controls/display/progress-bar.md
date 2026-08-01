# ProgressBar

## Overview

`ProgressBar` shows progress as a non-interactive filled bar. It cannot receive
focus, is excluded from hit testing, and owns no children.

## API

| Property                                        | Default      | Purpose                                                                         |
| ----------------------------------------------- | ------------ | ------------------------------------------------------------------------------- |
| `Minimum`, `Maximum`                            | `0`, `1`     | Define the finite, strictly increasing value range.                             |
| `Value`                                         | `0`          | Sets determinate progress; clamped into the current range.                      |
| `IsIndeterminate`                               | `false`      | Switches from value-based fill to the deterministic unknown-duration animation. |
| `Orientation`                                   | `Horizontal` | Chooses left-to-right or bottom-to-top fill.                                    |
| `UseSubCellResolution`                          | `false`      | Uses code-owned fractional block levels for finer-than-cell progress.           |
| `FillColor`, `TrackColor`, `IndeterminateColor` | `null`       | Override the theme colors for each rendered part.                               |
| `FillGlyph`, `TrackGlyph`, `IndeterminateGlyph` | Code-owned   | Override the validated one-cell glyphs; `ResetGlyphs()` restores the defaults.  |

## Behavior

- `Minimum` and `Maximum` are finite `double` endpoints with
  `Minimum < Maximum`. They default to zero and one.
- `Value` must be finite and is clamped into the inclusive range. It defaults
  to zero.
- `IsIndeterminate` selects the deterministic unknown-duration presentation.
- `Orientation` chooses horizontal left-to-right or vertical bottom-to-top
  filling, defaulting to `Horizontal`.
- `UseSubCellResolution` enables the nine code-owned ordered fractional levels.
  It defaults to `false`.
- `FillGlyph`, `TrackGlyph`, and `IndeterminateGlyph` are validated one-cell
  local overrides, and `ResetGlyphs()` clears all three at once.

`ValueChanged` fires for every committed `Value` transition, no matter which
public setter caused it — assigning `Value` directly, or a `Minimum`/`Maximum`
change that clamped it. `PropertyChanged(Value)` and `ValueChanged` always
agree: both observe the same history, and a clamp that leaves `Value` unchanged
raises neither. The event args expose the committed value as `Value`, matching
the other range controls.

A non-finite value throws `ArgumentOutOfRangeException`, and an endpoint that
would make the range empty or reversed throws `ArgumentException` — in both
cases before any mutation. Changing an endpoint clamps the current value in the
same transaction, before any notification fires, so every notification observes
coherent, already-clamped state: `PropertyChanged(Minimum)` or
`PropertyChanged(Maximum)` fires first, followed by `PropertyChanged(Value)`
and `ValueChanged` when the clamp actually changed the value.

Determinate rendering normalizes `(Value - Minimum) / (Maximum - Minimum)` and
fills `floor(normalized * axisCells)` complete cells; at the maximum, every
cell is filled. The remaining cells use the code-owned empty-progress glyph.
Horizontal fill grows from the left, vertical fill from the bottom.

Without a local override, all progress cells resolve from the code-owned glyph
defaults, and a glyph that is unsuitable under the active width policy uses its
code-owned fallback. Replacing the Theme recolors an existing bar without
changing its glyphs. In indeterminate mode, the committed content bounds fill
with the code-owned indeterminate glyph.

Without local color overrides, completed and indeterminate progress use the
theme's accent color (`ThemeColor.Accent`), and incomplete track cells use the
theme's muted color (`ThemeColor.Muted`). The per-part color properties
override those fallbacks while preserving background and attributes.

The intrinsic desired size is ten cells on the main axis and one cell on the
cross axis, and both alignment axes default to `Stretch`. Rendering uses the
resolved visual-state style, draws inside `ContentBounds`, and participates in
shared intrinsic chrome. Zero content bounds draw nothing, and the control
never handles pointer or keyboard input.

## Example

![The ProgressBar control rendered in the live showcase](../../images/controls/progress-bar.png)

```csharp
var bar = new ProgressBar
{
    Maximum = 100,
    Value = 42,
    Orientation = Orientation.Horizontal,
};
```

## Expected behavior

Callers can rely on the following: values and endpoints are validated as
finite, the range stays strictly increasing, and `Value` is always clamped
into it; endpoint changes surface through the documented notification order;
horizontal and vertical bars fill in the documented directions for empty,
partial, and full values, including sub-cell resolution; indeterminate
rendering is deterministic; zero and tiny bounds degrade safely; mutation,
resize, and appearance inheritance behave as documented; the control stays out
of hit testing; and the rendered output matches exact final cells.
`ProgressBarSurfaceTests` demonstrates the terminal-visible determinate and
indeterminate states through a mounted application.
