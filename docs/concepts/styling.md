# Appearance

## Styling contract

SharpVision controls use direct CLR configuration and immutable theme palettes.
There is no style-property registry, type-style cascade, or mutable per-instance
style object.

The active theme also owns a complete semantic glyph palette. A control renders
an explicit local glyph when one exists and otherwise resolves the corresponding
`Theme.Glyphs` member immediately before drawing. Glyph values never cascade by
control type, and changing `Application.Theme` repaints existing controls
without reconstructing the tree.

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

Physical `PointerOver` state remains observable on every control in the hit
ancestry. The built-in hover overlay ordinarily paints only controls whose
effective `CanFocus` is true, so passive text, containers, table chrome, and
separators do not acquire an interactive accent. A focus-owning `List` remains
neutral while its targeted internal item wrapper paints the row hover surface.
An explicit `PointerOver` appearance set by a caller or derived control remains
authoritative for any other deliberate passive hover treatment.

A null `Background` preserves cells already present in the canvas.
`Color.Default` is an opaque terminal default colour and is not transparency.

## Shared chrome

Border, shadow, and background are intrinsic control appearance. The framework
renders shadow and body fill before normal content, then overlays the border
after normal children. Custom `OnRenderContent` code never calls a chrome
helper.
