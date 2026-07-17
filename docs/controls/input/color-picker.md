# ColorPicker

## ColorPicker contract

`ColorPicker` is a retained
[`CompositeControl`](../composite-control.md#compositecontrol-contract) that
selects the nearest color the active terminal can reproduce. Its constructor
creates one permanent layout tree from `Grid`, `Stack`, `Dock`, `Overlay`,
`Slider`, and focused color surfaces; measure and render never rebuild it.

## Value and capability adaptation

`Value` is a concrete terminal `Color`. A detached picker preserves an assigned
RGB, indexed, or default representation. Once attached, the inherited
application capability profile controls normalization:

| Active depth | Committed representation    | Presentation                                                |
| ------------ | --------------------------- | ----------------------------------------------------------- |
| `TrueColor`  | RGB                         | saturation/value plane, hue ramp, RGB sliders, preview, hex |
| `Indexed256` | nearest index 0 through 255 | responsive 16 by 16 swatch grid                             |
| `Basic16`    | nearest index 0 through 15  | responsive 4 by 4 swatch grid                               |
| `Monochrome` | `Color.Default`             | disabled default-only surface                               |

The picker and frame encoder call the same public
`SharpVision.Terminal.Rendering.Palette` projection algorithm, including the
xterm-compatible cube, grayscale ramp, ANSI reference colors, and ascending
index tie-break. A downgrade is intentionally lossy. Upgrading converts the
currently committed indexed color to its reference RGB value; it never restores
discarded source RGB.

`EffectiveColorDepth` exposes the inherited tier. A changed direct assignment,
user selection, attachment normalization, or runtime capability transition
commits `Value`, synchronizes every retained part, and then raises one
`ValueChanged` event with immutable `ColorChangedEventArgs`. No-op changes are
silent. All attached mutation is dispatcher-affine.

## Layout and input

The true-color branch stretches its saturation/value plane into remaining space.
The hue ramp and horizontal hue `Slider` share one `Overlay`; exact red, green,
and blue sliders occupy retained rows below it. The preview and uppercase
`#RRGGBB` readout occupy the final row. Indexed branches reuse the Canvas
showcase's two-column swatch language when space permits and proportionally map
smaller committed bounds without drawing or hit testing outside them.

The plane is one focus stop. Left/Right adjust saturation by one percentage
point, Up/Down adjust value, and Home/End reach saturation endpoints. Primary
press and captured movement map committed cell coordinates to both normalized
axes. Hue and RGB parts use the complete
[`Slider` input contract](slider.md#input-and-visual-states).

Each palette is one focus stop. Left/Right move one entry, Up/Down move one row,
and Home/End reach the first/last entry. Primary press and captured movement
select the swatch under the responsive grid coordinate. Focus transfer and every
shared unavailability path release capture without an invented selection.

Every color is drawn to the semantic canvas. The picker never emits escape
sequences, and terminal output still passes through capability projection at the
frame boundary.

## Example

```csharp
var picker = new ColorPicker
{
    Value = Color.Rgb(255, 72, 128),
    Width = Length.Cells(40),
    Height = Length.Cells(18),
};

picker.ValueChanged += (_, change) => preview.Background = change.Value;
```

## Test obligations

Tests cover detached assignment, every capability tier, attachment and runtime
normalization, lossy downgrade, upgrade representation, committed event order,
RGB/HSV synchronization, exact preview and selected cells, palette keyboard and
pointer mapping, capture cancellation, focus, disabled state, zero/tiny/resize
containment, randomized tier membership, and dedicated showcase interaction.
