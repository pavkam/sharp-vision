# ColorPicker

## ColorPicker contract

`ColorPicker` is a retained
[`CompositeControl`](../composite-control.md#compositecontrol-contract) that
authors one stable RGB color independently of terminal output depth. Its
constructor creates one permanent layout tree from `Grid`, `Stack`, `Dock`,
`Overlay`, `Slider`, and focused color surfaces; measure and render never
rebuild it.

## API

`Value` is a concrete RGB or terminal-default `Color`. Attachment and runtime
capability changes never rewrite the authored value:

| Active depth                       | Committed representation | Presentation                                                |
| ---------------------------------- | ------------------------ | ----------------------------------------------------------- |
| `TrueColor`/`Indexed256`/`Basic16` | RGB                      | saturation/value plane, hue ramp, RGB sliders, preview, hex |
| `Monochrome`                       | RGB or `Color.Default`   | disabled default-only surface                               |

Only the frame encoder projects RGB through the xterm-compatible cube, grayscale
ramp, or ANSI reference colors. A presentation downgrade is lossy on the wire
but never changes `Value`, so a later capability upgrade uses the original
authored RGB automatically.

`EffectiveColorDepth` exposes the inherited tier. A changed direct assignment or
user selection commits `Value`, synchronizes every retained part, and then
raises one `ValueChanged` event with immutable `ColorChangedEventArgs`.
Capability changes alter presentation without raising a value event. No-op
changes are silent. All attached mutation is dispatcher-affine.

## Layout and input

The RGB editor stretches its saturation/value plane into remaining space. The
hue ramp and horizontal hue `Slider` share one `Overlay`; exact red, green, and
blue sliders occupy retained rows below it. The selected-color preview and
uppercase `#RRGGBB` readout share one final-row surface, with contrast-aware
text drawn over the selected color.

The plane is one focus stop. Left/Right adjust saturation by one percentage
point, Up/Down adjust value, and Home/End reach saturation endpoints. Primary
press and captured movement map committed cell coordinates to both normalized
axes. Hue and RGB parts use the complete
[`Slider` input contract](slider.md#input-and-visual-states).

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

picker.ValueChanged += (_, change) =>
{
    var face = preview.Face;
    preview.Face = new Face(
        face.Foreground,
        change.Value,
        face.Attributes,
        face.Underline,
        face.UnderlineColor);
};
```

## Test obligations

Tests cover detached assignment, every capability tier, RGB preservation across
attachment and runtime changes, committed event order, RGB/HSV synchronization,
exact preview cells, plane keyboard and pointer mapping, capture cancellation,
focus, disabled state, zero/tiny/resize containment, and dedicated showcase
interaction.
