# Theming a new control

## Overview

A new control either reuses one of the six well-known style types outright, or
declares a self-contained typed style with its own `styles.*` JSON key. It never
adds selector syntax or a mutable style registry, and it never needs internal
access to compile - both extension paths below are fully public.

Custom controls work with retained objects, direct CLR properties, and the
global semantic theme. They never register selectors or type recipes.

## Reuse an existing well-known type

Most new controls have no structural style members of their own - they just want
to look like an input, a container, a window, a popup, or a tooltip. A control
with no primary `Style` slot at all overrides the protected
`GetDefaultAppearanceStates` hook and returns the matching public `Theme`
property directly:

```csharp
public sealed class PrimitiveCommandTile : ControlBase
{
    protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
        (theme ?? ThemeCatalog.Dark).Input;

    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        _ = canvas.Draw("Run", new Point(ContentBounds.X, ContentBounds.Y), ResolvedStyle);
    }
}
```

The base `ControlBase` implementation returns `theme.Control` - the passive
fallback - so only override this hook when the control matches one of the other
five well-known types. The framework handles theme publication, state
composition, caching, and invalidation on its own; a third-party developer
writes no theme plumbing beyond this one property.

The chosen type covers the control's overall face, border, and shadow. When the
control has an additional semantic part outside those three, expose a validated
`ControlColor` or `ControlDecoration` property with a library-defined semantic
default:

```csharp
public ControlColor Fill { get; set; } = SemanticColor.Accent;
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
    TerminalAttributes.Bold);
```

The derived control may use the protected state seam for per-state variation:

```csharp
tile.SetAppearance(
    VisualState.IsPointerOver,
    new AppearanceOverlay(
        border: new BorderOverlay(foreground: SemanticColor.ActiveBorder)));
```

Complete values set by the derived control override the Theme, and derived state
sets override both. `ActualFace`, `ActualBorder`, and `ActualShadow` stay public
so callers can inspect the fully resolved values.

## A control with its own typed style

A control whose structure needs more than one of the six well-known types
alone - its own padding, glyph family, mark style, or any other structural
member - declares a `sealed record` (or `readonly record struct`) deriving from
`ControlStyle` (or one of its five siblings), and registers a
`static StyleDefinition<TStyle>` for it with `StyleDefinitions.Control<TStyle>`:

```csharp
public sealed record CommandTileStyle : InputStyle
{
    [SetsRequiredMembers]
    public CommandTileStyle(Face face, Border border, Shadow shadow, Thickness padding)
        : base(face, border, shadow, InputStyle.Default.DropDownGlyph) => Padding = padding;

    // Declared BEFORE Definition, and this ordering is load-bearing. Static initializers run in
    // textual order, so a Definition declared first would pass a null Default to a factory that
    // rejects null - from inside the static constructor, surfacing as a TypeInitializationException
    // naming neither member.
    public static CommandTileStyle Default { get; } = new(
        face: new Face(Color.Default, Color.Transparent, TerminalAttributes.None, Underline.None, Color.Default),
        border: new Border(BorderSide.All, BorderGlyphStyle.Heavy, Color.Default, Color.Transparent, TerminalAttributes.None),
        shadow: new Shadow(false, ShadowMode.Composite, default, default, Color.Default, Color.Transparent, TerminalAttributes.None),
        padding: new Thickness(1, 0));

    internal static StyleDefinition<CommandTileStyle> Definition { get; } =
        StyleDefinitions.Control<CommandTileStyle>(
            "acme.commandTile",
            Default,
            static (previous, _, current, _) =>
                previous.Padding != current.Padding
                    ? InvalidationImpact.Measure
                    : previous == current
                        ? InvalidationImpact.None
                        : InvalidationImpact.Render);

    public required Thickness Padding { get; init; }
}

public sealed class CommandTile : ControlBase, IStyled<CommandTileStyle>
{
    private readonly StyleSlot<CommandTileStyle> _style;

    public CommandTile() => _style = InitializeStyle(CommandTileStyle.Definition);

    public CommandTileStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    public CommandTileStyle ActualStyle => _style.Actual;
}
```

`"acme.commandTile"` is this style's own `styles.*` key, following the same
`"vendor.control"` convention a theme author uses for any third-party section
(see [themes.md](themes.md#style-types)). A third-party style is the one case
that passes its key explicitly, because a dot cannot appear in a type name. A
library-owned style passes no key at all: the overloads without one derive it
from the style type's own name, so `ButtonStyle` owns `button` and
`ScrollBarStyle` owns `scrollBar`. `Default` is this type's own code-owned
fallback - the value every theme that never authors this key resolves to.
`StyleDefinitions.Control<TStyle>` builds a **self-contained root**: it does not
inherit another type's theme customization. That is the trade-off to weigh, not
just a description - a root resolves entirely from its own key, so a theme
authoring only `styles.control` moves nothing about it. Prefer the one-hop
fallback form below whenever the control has a sensible parent type. This is the
fully public extension path - no internal access is required, and a theme author
can restyle every member (including `Padding`) directly through this key's own
JSON, per-state, the same way any well-known style's JSON works.

`CommandTile` derives directly from `ControlBase` here, but the same
`IStyled<TStyle>` declaration plus `InitializeStyle`/`StyleSlot<TStyle>`
forwarding works identically no matter which base a control actually derives
from - `InputBase`, `CompositeControlBase`, `FloatingSurfaceBase`, or
otherwise - since `InitializeStyle` lives on the non-generic `ControlBase`
itself; see [Appearance](styling.md#overview) for the full mechanism. Pass a
private method as `InitializeStyle`'s optional `changed` callback only for
genuine post-commit work such as normalizing an animation phase or projecting an
aggregate style onto heterogeneous retained parts - there is no virtual
`OnStyleChanged` to override.

Use `StyleDefinitions.Part`, `InitializePartStyle`, and `BindStyle` for a named
style forwarded to retained implementation controls. Bind the nullable local
slot, not `Actual`, so a reset never pins a theme-derived value.

Implement `IsCheckedState`, `IsSelectedState`, or `IsIndeterminateState` only
when the control genuinely owns that semantic fact. `Focused` means direct
focus; `FocusWithin` means a descendant has focus.

**One-hop fallback to an existing type's theme customization** (rather than a
self-contained root) is how every library leaf control - `Button`, `CheckBox`,
`RadioButton`, `ScrollBar`, and the rest - resolves today
(`StyleDefinitions.Control<TStyle, TFallback>`), so a restyled `"input"` or
`"control"` section automatically reaches every control that falls back to it
without hand-listing each one. That factory needs a
`Func<Theme, StyleStates<TFallback>>` produced from `Theme`'s own internal
resolution primitive, so it is presently usable only by controls compiled inside
the SharpVision assembly. A self-contained root (as above) is the supported path
for a control outside the assembly; it does not lose theme responsiveness, it
just does not automatically follow another type's restyle.

## What goes wrong

Three mistakes account for most of the time lost giving a control its own typed
style. All three compile, and the first two also pass a test that looks like it
covers them.

**Structural members do not travel on the appearance path.** A control's
resolved `AppearanceStates` - what `GetDefaultAppearanceStates` returns, and
what `Theme.Control`, `Theme.Input`, and their four siblings expose - carries
each state's `Face`, `Border`, and `Shadow`, and nothing else. Every other
member a style declares (a glyph family, a padding, an extra semantic color) is
dropped on the way. Those members reach the screen only if the render code reads
them off `ActualStyle` - the way `Text` resolves `ActualStyle.EllipsisGlyph`
inside its own render pass rather than expecting the appearance states to carry
it.

A control that only overrides the appearance hook has no `ActualStyle` to read,
so a style type with members beyond face, border, and shadow needs a real style
slot - `InitializeStyle` - and not just the hook.

**A style-layer assertion does not prove the control uses the style.** A test
that resolves the definition and asserts on the resolved value passes whether or
not the render code ever reads it: revert a call site to a hardcoded glyph and
the test stays green. The assertion that separates the two is one on the
rendered cell. Apply the theme, render, and read the cell back.

**Choosing a fallback type is a layout decision, not a color one.**
`ContainerStyle` is the intuitive parent for anything box-shaped, but its
default border encloses all four sides, and enabled border sides reserve layout
through the base box model. Falling back to it rather than `ControlStyle`
therefore changes the control's measured size by a cell per edge, before any
theme is involved. Pick the fallback whose default geometry matches the control,
then its colors.

## Rendering and proof

Controls render through the canvas and never emit terminal bytes. Reusable
canonical glyphs and their one-cell fallbacks stay code-owned. A style type may
choose a well-known base type's standard border family or shadow geometry, but a
control-specific symbol is never a theme key.

A shipped external control needs consumer and surface proof: it compiles
publicly without internals access, validates its arguments, invalidates
correctly, resolves its style after a theme swap, honors state-specific local
overrides, falls back to one-cell glyphs, and is verified through final cells
rather than private resolver calls.

## Expected behavior

| Layer    | Observable evidence                                                                                |
| -------- | -------------------------------------------------------------------------------------------------- |
| Consumer | External derivation compiles using only public/protected members and a public `StyleDefinition`.   |
| Unit     | Local complete values, partial state sets, reset, validation, invalidation, and theme replacement. |
| Surface  | Normal, interactive, combined, disabled, fallback-glyph, and theme-swap cells.                     |

1. The normal theme-resolved appearance renders correctly without any local
   repair values.
2. Each state-specific contribution applies on its own and in combination with
   the others.
3. Caller-assigned local values remain authoritative across theme replacement.
