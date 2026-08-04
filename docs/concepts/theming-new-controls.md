# Theming a new control

## Overview

A new control selects one of the existing semantic `ThemeRole` values, keeps its
control-specific glyphs and internal part geometry in code, and exposes complete
local appearance through the inherited `ControlBase` API. It does not add
selector syntax or a mutable style registry.

Custom controls work with retained objects, direct CLR properties, and the
global semantic theme. They never register selectors or type recipes.

A control that genuinely needs theme-authored structural content beyond the role
profile - a bespoke glyph family or part color a semantic role cannot express -
may claim one namespaced style section (`"vendor.control"`) and read it through
`Theme.GetStyleSection<TSection>(sectionName)` (see
[themes.md](themes.md#semantic-profiles)). This is deliberately narrow: it does
not add a selector language or a mutable registry, and it is the exception, not
the default path most controls in this document should follow.

## Select a semantic role

Every control defaults to `ThemeRole.Control`. Override the protected property
when the control matches one of the library-defined high-level meanings:

```csharp
public sealed class PrimitiveCommandTile : ControlBase
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
caller-authored chrome; otherwise publish one complete Style value and keep one
immutable Style value with a validated `With(...)` method.

For a standard typed style, derive from the generic control seam and pass one
immutable definition to its constructor. The framework publishes `Style`,
`ActualStyle`, caching, notifications, and the optional post-commit callback:

```csharp
public sealed class CommandTile : Control<ButtonStyle>
{
    private static readonly StyleDefinition<ButtonStyle> _styleDefinition =
        StyleDefinitions.Control(
            ThemeRole.Input,
            static profile => new ButtonStyle(ButtonStyle.Standard.Padding, profile),
            static style => style.Appearance,
            static (previous, _, current, _) => previous == current
                ? InvalidationImpact.None
                : InvalidationImpact.Render);

    public CommandTile() : base(_styleDefinition)
    {
    }
}
```

`Pressable<TStyle>`, `CompositeControl<TStyle>`, and `FloatingSurface<TStyle>`
provide the same facade without surrendering their specialized behavior.
Override `OnStyleChanged` only for genuine post-commit work such as normalizing
an animation phase or projecting an aggregate style onto heterogeneous retained
parts.

Use `StyleDefinitions.Part`, `InitializePartStyle`, and `BindStyle` for a named
style forwarded to retained implementation controls. Bind the nullable local
slot, not `Actual`, so a reset never pins a theme-derived value.

Implement `IsCheckedState`, `IsSelectedState`, or `IsIndeterminateState` only
when the control genuinely owns that semantic fact. `Focused` means direct
focus; `FocusWithin` means a descendant has focus.

## Registering a theme-authored style section

A control whose structure needs more than a semantic role - a mark family, a
padding, a glyph sequence - can let a theme author it directly through a
`styles` section, deserialized and memoized by the fully public
`Theme.GetStyleSection<TSection>(sectionName)` (see
[themes.md](themes.md#semantic-profiles)). `ScrollBar`'s own `Chrome`/`Fill`
wiring is the library's worked example: a small JSON DTO, and a resolve step
that checks the section before falling back to the code-owned default.

```csharp
public sealed class WidgetStyleSection
{
    public string? Chrome { get; set; }
}

public static class WidgetTheming
{
    // A third-party section name must be namespaced ("acme.widget", not a
    // bare word) - an unqualified name outside the library's own registered
    // set is rejected as a likely typo.
    public static ScrollBarChrome ResolveChrome(Theme theme, ScrollBarChrome fallback)
    {
        var section = theme.GetStyleSection<WidgetStyleSection>("acme.widget");

        return section?.Chrome is { } chromeName
            ? Enum.Parse<ScrollBarChrome>(chromeName, ignoreCase: true)
            : fallback;
    }
}
```

`GetStyleSection` is the only member this needs, and it is public - no internal
access is required. It deserializes and memoizes the section on first access per
`Theme` instance, and returns `null` for a theme that never authored it; the
caller's own code is what decides the fallback, not the framework. `ScrollBar`
itself (`public sealed class ScrollBar: Control<ScrollBarStyle>`) uses exactly
the `Control<TStyle>` + `StyleDefinition<TStyle>` facade shown earlier in this
page, just with a custom resolve function that calls `GetStyleSection` instead
of the `StyleDefinitions.Control` factory - see `ScrollBarStyle.cs` and its
sibling `ScrollBarStyleSection.cs` for that complete, current implementation.
Constructing a `StyleDefinition<TStyle>` directly (rather than through the
`StyleDefinitions` factory methods) needs its internal constructor, so a
third-party control outside this assembly cannot replicate that exact
resolve-function customization; calling `GetStyleSection` directly from wherever
the control already resolves its appearance (a property getter,
`OnRenderContent`, or a small local helper, as above) is the third-party path.

Prove a JSON-authored member the same way the role-profile fallback is proven: a
themed-load round-trip that asserts the resolved value, and an unauthored-theme
case that asserts the code-owned default survives.

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
