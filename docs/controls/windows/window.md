# Window

## Window contract

`Window` is a [`ContentControl`](../content-control.md#contentcontrol-contract)
that frames its inherited `Content` as a titled terminal application surface. It
renders its own border and optional Turbo Vision-style shadow without changing
the content's ordinary box-model or input routing.

## API

- Inherited `Content` uses managed capacity-one ownership and is arranged inside
  the one-cell physical frame. Collapsed content contributes neither desired
  size nor margin; its stale desired size and bounds are cleared through the
  ordinary collapsed-content transactions. Replacement leaves previous content
  detached and caller-owned; disposing the Window disposes only currently
  assigned content and publishes the committed `Content == null` transition.
- `Title` is non-null content written on the top edge and clipped before either
  corner can be overwritten; `TitlePlacement` aligns it left, center, or right
  inside those corners.
- `Glyphs`, `BorderColor`, `Background`, and `Attributes` define the frame
  chrome and its body surface.
- `HasShadow`, `ShadowMode`, `ShadowOffset`, and `ShadowGlyph` select composite
  darkening or a block-glyph shadow outside the window body.
- `CanMove` (default `true`) enables title-bar drag-to-reposition when the
  window is a child of a `Canvas`. A primary pointer press on the top edge
  captures the pointer; move events update `Canvas.SetLeft` and `Canvas.SetTop`
  in real time; release ends the drag.
- `CanClose` (default `false`) renders a close glyph ("✕") at the top-right
  corner of the border. A primary pointer press on the glyph raises the
  `Closing` event. The close glyph requires at least four columns of width to
  appear.

## Lifecycle and interaction

When a Window becomes visible, it automatically focuses the first focusable
descendant control. This mirrors the behavior of Delphi VCL and WinForms dialogs
where showing a form transfers focus to its content. If no focusable descendant
exists, the Window itself receives focus.

Content remains in the surrounding control tree and receives normal focus,
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
    Content = root,
    Width = Length.Percent(100),
    Height = Length.Percent(100),
};
```

## Test obligations

Cover title clipping, content measurement, collapsed geometry, and arrangement,
each glyph family, surface color, composite and block shadow placement, tiny
bounds, ownership, default/cancel discovery across private slots, terminal
resize, and final semantic frame cells.
