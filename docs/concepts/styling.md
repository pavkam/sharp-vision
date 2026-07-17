# Appearance

## Styling contract

SharpVision controls use direct CLR configuration and immutable theme palettes.
There is no style-property registry, type-style cascade, or mutable per-instance
style object.

`Foreground`, `Background`, underline, border, and shadow properties are local
control configuration. Their colour values are `ThemeColor?`: a concrete
terminal colour or a semantic `ColorRole`. `ThemeColor` resolves only while an
appearance is rendered.

## Visual states

`Appearance` holds a compact optional overlay, and `SetAppearance` assigns an
overlay for one `VisualState`. Overlays are applied in fixed order:

```text
PointerOver → FocusWithin → Focused → Current → Selected → Checked
→ Indeterminate → Pressed → Disabled
```

Only text values inherit from a parent normal appearance. Background, border,
and shadow do not inherit. Set `AppearanceBoundary` to stop ambient text
inheritance.

A null `Background` preserves cells already present in the canvas.
`Color.Default` is an opaque terminal default colour and is not transparency.

## Shared chrome

Border, shadow, and background are intrinsic control appearance. The framework
renders shadow and body fill before normal content, then overlays the border
after normal children. Custom `OnRenderContent` code never calls a chrome
helper.
