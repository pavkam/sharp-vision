# Theming a new control

## Theming contract

A new control selects one existing semantic `ThemeRole`, keeps control-specific
glyphs and internal part geometry in code, and exposes complete local appearance
through the inherited `Control` API. It does not add selector syntax, a mutable
style registry, or a control-type theme key.

Custom controls use retained objects, direct CLR properties, and the global
semantic theme. They do not register selectors, type recipes, or theme keys.

## Select a semantic role

Every control defaults to `ThemeRole.Control`. Override the protected property
when the control has one of the library-defined high-level meanings:

```csharp
public sealed class CommandTile : Control
{
    protected override ThemeRole ThemeRole => ThemeRole.Input;

    protected override void OnRenderContent(Canvas canvas)
    {
        _ = canvas.Draw("Run", new Point(ContentBounds.X, ContentBounds.Y), ResolvedStyle);
    }
}
```

The framework handles theme publication, state composition, caching, and
invalidation. A third-party developer does not write theme plumbing.

Use the role only for the control's overall face, border, and shadow. For an
additional semantic part, expose a validated `ColorValue` or `AttributeValue`
property with a library-defined semantic default:

```csharp
public ColorValue Fill { get; set; } = ThemeColor.Accent;
```

Resolve that value through the attached `Theme` during rendering. Use a concrete
`Color` assignment when the caller must always win across theme changes.
Background channels may use `Color.Transparent`; glyph-painting foreground
channels must reject it before mutation.

## Local customization

Inside a derived control, assign protected complete composites when that
control's public layout contract owns the whole result:

```csharp
tile.Border = new Border(
    BorderSide.All,
    BorderGlyphStyle.Rounded,
    Color.Rgb(114, 167, 255),
    Color.Transparent,
    Attributes.Bold);
```

The derived control may use the protected state seam for variation:

```csharp
tile.SetAppearance(
    VisualState.PointerOver,
    new AppearanceSet(
        border: new BorderSet(foreground: ThemeColor.ActiveBorder)));
```

Derived-control complete values override the Theme. Derived state sets override
both. `ActualFace`, `ActualBorder`, and `ActualShadow` remain public so callers
can inspect the fully resolved values. Republish raw chrome only when the custom
control intentionally supports arbitrary caller-authored chrome; otherwise
publish one complete Style value and retain partial StyleSet values for Theme
composition.

Implement `IsCheckedState`, `IsSelectedState`, or `IsIndeterminateState` only
when the control owns that semantic fact. `Focused` means direct focus;
`FocusWithin` means descendant focus.

## Rendering and proof

Controls render through the canvas and never emit terminal bytes. Reusable
canonical glyphs and their one-cell fallbacks remain code-owned. Theme profiles
may choose a semantic role's standard border family or shadow geometry, but a
control-specific symbol is not a theme key.

A shipped external control needs consumer and surface proof: public compilation
without internals access, argument validation, invalidation, role resolution
after a theme swap, state-specific local overrides, glyph fallback, and final
cells rather than private resolver calls.

## Expected behavior

| Layer    | Required evidence                                                                                  |
| -------- | -------------------------------------------------------------------------------------------------- |
| Consumer | External derivation compiles using only protected/public members and the selected semantic role.   |
| Unit     | Local complete values, partial state sets, reset, validation, invalidation, and theme replacement. |
| Surface  | Normal, interactive, combined, disabled, fallback-glyph, and theme-swap cells.                     |

1. Prove the normal semantic profile without local repair values.
2. Prove each state-specific contribution independently and in combination.
3. Prove caller local values remain authoritative across theme replacement.
