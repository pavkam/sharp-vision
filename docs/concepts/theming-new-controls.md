# Theming a new control

## Overview

A new control selects one of the existing semantic `ThemeRole` values, keeps its
control-specific glyphs and internal part geometry in code, and exposes complete
local appearance through the inherited `Control` API. It does not add selector
syntax, a mutable style registry, or a control-type theme key.

Custom controls work with retained objects, direct CLR properties, and the
global semantic theme. They never register selectors, type recipes, or theme
keys.

## Select a semantic role

Every control defaults to `ThemeRole.Control`. Override the protected property
when the control matches one of the library-defined high-level meanings:

```csharp
public sealed class CommandTile : Control
{
    protected override ThemeRole ThemeRole => ThemeRole.Input;

    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        _ = canvas.Draw("Run", new Point(ContentBounds.X, ContentBounds.Y), ResolvedStyle);
    }
}
```

The framework handles theme publication, state composition, caching, and
invalidation on its own; a third-party developer writes no theme plumbing.

The role covers the control's overall face, border, and shadow. When the control
has an additional semantic part, expose a validated `ColorValue` or
`AttributeValue` property with a library-defined semantic default:

```csharp
public ColorValue Fill { get; set; } = ThemeColor.Accent;
```

Resolve that value through the attached `Theme` during rendering. Assign a
concrete `Color` instead when the caller's choice must survive theme changes.
Background channels may use `Color.Transparent`; glyph-painting foreground
channels must reject it before mutation.

## Local customization

Inside a derived control, assign the protected complete composites when the
control's public layout contract owns the whole result:

```csharp
tile.Border = new Border(
    BorderSide.All,
    BorderGlyphStyle.Rounded,
    Color.Rgb(114, 167, 255),
    Color.Transparent,
    Attributes.Bold);
```

The derived control may use the protected state seam for per-state variation:

```csharp
tile.SetAppearance(
    VisualState.PointerOver,
    new AppearanceSet(
        border: new BorderSet(foreground: ThemeColor.ActiveBorder)));
```

Complete values set by the derived control override the Theme, and derived state
sets override both. `ActualFace`, `ActualBorder`, and `ActualShadow` stay public
so callers can inspect the fully resolved values. Republish the raw chrome
properties only when the custom control intentionally supports arbitrary
caller-authored chrome; otherwise publish one complete Style value and keep
partial StyleSet values for Theme composition.

Implement `IsCheckedState`, `IsSelectedState`, or `IsIndeterminateState` only
when the control genuinely owns that semantic fact. `Focused` means direct
focus; `FocusWithin` means a descendant has focus.

## Rendering and proof

Controls render through the canvas and never emit terminal bytes. Reusable
canonical glyphs and their one-cell fallbacks stay code-owned. Theme profiles
may choose a semantic role's standard border family or shadow geometry, but a
control-specific symbol is never a theme key.

A shipped external control needs consumer and surface proof: it compiles
publicly without internals access, validates its arguments, invalidates
correctly, resolves its role after a theme swap, honors state-specific local
overrides, falls back to one-cell glyphs, and is verified through final cells
rather than private resolver calls.

## Expected behavior

| Layer    | Observable evidence                                                                                |
| -------- | -------------------------------------------------------------------------------------------------- |
| Consumer | External derivation compiles using only protected/public members and the selected semantic role.   |
| Unit     | Local complete values, partial state sets, reset, validation, invalidation, and theme replacement. |
| Surface  | Normal, interactive, combined, disabled, fallback-glyph, and theme-swap cells.                     |

1. The normal semantic profile renders correctly without any local repair
   values.
2. Each state-specific contribution applies on its own and in combination with
   the others.
3. Caller-assigned local values remain authoritative across theme replacement.
