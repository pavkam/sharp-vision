# Theming a new control

## Overview

A new control either reuses one of the six well-known style types outright, or
declares a typed style with a declared one-hop fallback to one of them. It never
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
    public PrimitiveCommandTile() => EnableChromeAuthoring();

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
private ControlColor _fill = SemanticColor.Accent;

public ControlColor Fill
{
    get => _fill;
    set
    {
        if (value.IsLiteral && value.Literal.IsTransparent)
        {
            throw new ArgumentException("Fill cannot be transparent.", nameof(value));
        }

        _ = SetProperty(ref _fill, value, InvalidationImpact.Render);
    }
}

protected override InvalidationImpact GetThemeChangeImpact(
    Theme? previous,
    Theme? current,
    Face? previousParentAmbientFace,
    Face? currentParentAmbientFace) =>
    MaximumImpact(
        base.GetThemeChangeImpact(
            previous,
            current,
            previousParentAmbientFace,
            currentParentAmbientFace),
        Fill.Resolve(previous) != Fill.Resolve(current)
            ? InvalidationImpact.Render
            : InvalidationImpact.None);
```

Resolve that value through the attached `Theme` during rendering and compare its
resolved colors in `GetThemeChangeImpact`, as above; the raw semantic token does
not change when two themes map it to different colors. Assign a concrete `Color`
instead when the caller's choice must survive theme changes. Background channels
may use `Color.Transparent`; glyph-painting foreground channels must reject it
before mutation.

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
member - declares a `sealed record` deriving from `ControlStyle` (or one of its
five siblings), and registers a `static StyleDefinition<TStyle>` for it with a
declared **one-hop fallback** to whichever of the six well-known types is the
closest semantic match -
`StyleDefinitions.Control<TStyle, TFallback>(fallbackTo, complete, compare)`.
This is the one factory every leaf control style in the library calls today -
`Button`, `CheckBox`, `RadioButton`, `ScrollBar`, and the rest - so a restyled
`"input"` or `"control"` role section automatically reaches every control that
falls back to it, with nothing to hand-list per control:

```csharp
public sealed record CommandTileStyle : ControlStyle
{
    [SetsRequiredMembers]
    public CommandTileStyle(Face face, Border border, Shadow shadow, Thickness padding)
        : base(face, border, shadow) => Padding = padding;

    internal static StyleDefinition<CommandTileStyle> Definition { get; } =
        StyleDefinitions.Control(
            static theme => theme.GetStyleSet(InputStyle.Default),
            Complete,
            static (previous, _, current, _) =>
                previous.Padding != current.Padding
                    ? InvalidationImpact.Measure
                    : previous == current
                        ? InvalidationImpact.None
                        : InvalidationImpact.Render);

    private static CommandTileStyle Complete(InputStyle input, VisualState state, Theme theme) =>
        new(input.Face, input.Border, input.Shadow, padding: new Thickness(1, 0));

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

`CommandTileStyle` declares no `styles.*` key of its own at all: a theme's
`styles` object is closed to exactly the six well-known role sections, so a leaf
control's only sources of appearance are its code-owned `Complete` logic, the
fallback type's own resolved states (`InputStyle`'s `"input"` role section
here), and a locally assigned `Style`. `Complete` runs once per resolved state,
completing the fallback's contribution into this type's own shape; a state the
fallback does not author still runs `Complete` against the fallback's own
`Normal`, which is the only place a per-state code-owned default - a checked
accent color, a glyph-family lookup through `theme.Glyphs` - can be expressed.
`fallbackTo` may resolve any public per-state set: one of the six well-known
types through `Theme.GetStyleSet`, or one of `Theme`'s four derived interaction
sets - `GetInteractiveControlStyleSet`, `GetInteractiveRowStyleSet`,
`GetFocusableContainerStyleSet`, and `GetFocusableControlStyleSet`. Choose a
derived set the same way a library leaf style does: the Interactive pair for a
borderless control that owns direct interaction outright (Row alone keeps the
passive background under pointer hover, for a row whose selection owns the fill
instead), and the narrower Focusable pair - Container or Control geometry - for
a control that is merely a direct focus target whose own content already owns
hover, press, and selection more specifically.

`CommandTile` derives directly from `ControlBase` here, but the same
`IStyled<TStyle>` declaration plus `InitializeStyle`/`StyleSlot<TStyle>`
forwarding works identically no matter which base a control actually derives
from - `InputBase`, `CompositeControlBase`, `FloatingSurfaceBase`, or
otherwise - since `InitializeStyle` lives on the non-generic `ControlBase`
itself; see [Appearance](styling.md#overview) for the full mechanism. Pass a
private method as `InitializeStyle`'s optional `changed` callback only for
genuine post-commit work such as normalizing an animation phase or projecting an
aggregate style onto heterogeneous retained parts - there is no virtual
`OnStyleChanged` to override. The factory is fully public and requires no
internal access - a third-party control registers its own `StyleDefinition`
exactly the way `CommandTileStyle` does above.

The style slot recursively tracks every `ControlColor` and `ControlDecoration`
member, including members nested in faces, borders, shadows, and custom style
fragments. A theme replacement that changes any resolved paint value therefore
requests render even when the definition's callback sees identical raw semantic
tokens. The callback still owns structural classification: return `Measure` or
`Arrange` for geometry changes and `Render` for non-semantic visual members.
Standalone control properties are outside a style slot, so their control keeps
the explicit `GetThemeChangeImpact` comparison shown above.

Use `StyleDefinitions.Part`, `InitializePartStyle`, and `BindStyle` for a named
style forwarded to retained implementation controls. Bind the nullable local
slot, not `Actual`, so a reset never pins a theme-derived value. The framework
rejects a control definition passed to `InitializePartStyle` and a part
definition passed to `InitializeStyle`; role mismatches are authoring errors,
not a way to suppress appearance ownership. A primary-named style that only
projects values onto heterogeneous retained parts uses
`StyleDefinitions.Aggregate` with `InitializeStyle`, as `ColorPickerStyle` does.
Aggregate styles retain conventional `Style`/`ActualStyle` naming without
claiming the aggregate control's own face, border, or shadow.

Bindings are scoped to retained ancestry. Removing or reparenting the target
releases its upstream edge automatically, after which the target may accept its
own local style or a new owner's binding. Source updates preflight and commit
the complete transitive graph before callbacks publish; exceptions do not leave
later targets stale, and reentrant commits supersede older notifications.

Implement `IsCheckedState`, `IsSelectedState`, or `IsIndeterminateState` only
when the control genuinely owns that semantic fact. `Focused` means direct
focus; `FocusWithin` means a descendant has focus.

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

A resolved-appearance assertion sits between those two layers.
`ResolveAppearance(theme, visualState)` resolves through the control's own
hooks - appearance selection, state folding, ambient inheritance, and semantic
literals - without a mounted application, so a unit test can prove which
appearance the control selects under an explicit theme and state. It still does
not prove the render pass reads a structural member; that proof stays with the
rendered cell.

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
