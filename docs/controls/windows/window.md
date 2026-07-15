# Window

## Window contract

`Window` frames one owned child as a titled terminal application surface. It
renders its own border and optional Turbo Vision-style shadow without changing
the child's ordinary box-model or input routing.

## API

- `Child` uses managed capacity-one ownership and is arranged inside the
  one-cell physical frame.
- `Title` is non-null content written on the top edge and clipped before either
  corner can be overwritten; `TitlePlacement` aligns it left, center, or right
  inside those corners.
- `Glyphs`, `BorderColor`, `Background`, and `Attributes` define the frame
  chrome and its body surface.
- `HasShadow`, `ShadowMode`, `ShadowOffset`, and `ShadowGlyph` select composite
  darkening or a block-glyph shadow outside the window body.

## Lifecycle and interaction

Windows do not introduce a special activation, modality, move, or resize model:
their child remains in the surrounding control tree and receives normal focus,
keyboard, pointer, resize, and clipping behavior. This makes `Window` useful as
a composable visual surface inside an `Overlay` or `Canvas`. If an unhandled
Enter or Escape bubbles through the window, its first available `Button` with
`IsDefault` or `IsCancel` respectively receives the conventional fallback
activation. Discovery follows deterministic ownership order through every slot,
including private non-container branches; it never assumes descendants appear in
`Container.Children`.

The showcase includes rounded, paired-line, and portable ASCII frames with left,
centered, and right titles so the chrome choices are visible side by side.

## Example

```csharp
var window = new Window
{
    Title = "SharpVision Showcase",
    Child = root,
    Width = Length.Percent(100),
    Height = Length.Percent(100),
};
```

## Test obligations

Cover title clipping, child measurement and arrangement, each glyph family,
surface color, composite and block shadow placement, tiny bounds, ownership,
default/cancel discovery across private slots, terminal resize, and final
semantic frame cells.
