# Styling and visual states

## Styling contract

SharpVision uses typed style properties, type-keyed themes, and per-instance
style overlays. Controls are ordinary mutable objects; a style or theme change
invalidates only dependent controls and only the required phase.

## Style properties

Each styleable value is registered as immutable `StyleProperty<T>` metadata on
its declaring control type. Controls expose conventional CLR properties backed
by protected `GetValue`, `SetValue`, and public `ClearValue` operations. Local
values win over every themed, per-instance, class-default, and visual-state
layer.

Base `Control` registers margin, padding, foreground, background, attributes,
underline, underline color, fill mode, border chrome, and shadow chrome. Derived
controls register additional properties and may publish class defaults (for
example, `Button` rounded border and compact shadow).

## Themes and resolution

`Theme` owns at most one `IControlStyle` per control type. `Application.Theme`
(default `Themes.Dark`) publishes an internal theme context to every attached
control. Resolution order for each property and visual state is:

1. registered default;
2. most-derived class default;
3. theme chain from `Control` through the runtime type;
4. per-instance `Control.Style` overlay (that control only);
5. active visual-state overlays (hovered, focused, checked, pressed, disabled);
6. explicit local value.

`Themes.White` and `Themes.Dark` are frozen standard themes built from the
public `Theme` and `ControlStyle<TControl>` API using the portable 16-color
palette.

## Per-instance styles

`Control.Style` is a nullable `IControlStyle` overlay. It applies only to the
owning control and does not flow to descendants. List-owned items resolve
`ControlStyle<List>` theme and owner-instance values in addition to their own
theme chain so row selection styling remains coherent.

## Visual states

Standard states are normal, hovered, pressed, focused, checked, and disabled.
The hovered overlay applies only to interactive (focusable) controls; static
content such as text and tables is never marked hovered. Measure-impact
properties are normal-state values only. Render-impact properties may vary by
overlay state. Visual overlays never control behavior: `IsEnabled` determines
input acceptance.

Public theme resolution accepts any combination of defined state flags and
rejects unknown bits before evaluating the cascade.

The standard base `Control` focus state does not add underline. Focus is
expressed by the control type at the semantic surface that represents its
interaction: `Button` and `ComboBox` use an Accent border, `ScrollBar` uses an
Accent rail, and choice controls use an Accent mark. Pressed and checked states
likewise avoid a generic selection-background overlay. `CheckBox` and
`RadioButton` therefore preserve the parent background while checked, and an
indeterminate `CheckBox` uses the Warning role for its mark. A custom control
that needs focus presentation defines its own type style rather than decorating
every cell inherited from `Control`.

## Invalidation and tests

Property metadata declares the earliest affected phase: measure, arrange, or
render. Tests cover registration, theme precedence, local override/clear,
application theme switching, standard theme cells, third-party style properties,
showcase theme toggling, and exact terminal cell output.

## Shared chrome

Border, shadow, and opaque body fill rasterize through one internal geometry.
Every `Control` owns this chrome directly; there are no `Border` or `Shadow`
wrapper controls. A derived control draws the shared chrome through the
protected `RenderChrome` method (base `OnRender` calls it), while the base
control expands its visual bounds for shadow overflow without changing desired
size, arranged bounds, child slots, or pointer hit testing.

`BorderThickness` defaults to zero and reserves the enabled edges during measure
and arrange before padding. Each edge is either zero or one cell; larger values
are rejected before mutation. `BorderGlyphs` defaults to `Glyphs.Default`, while
`BorderColor` and `BorderAttributes` default to null and inherit the resolved
body style. Border thickness invalidates measure; the remaining border
properties invalidate render. A custom-rendering leaf that needs a separate
visible frame is wrapped in an ordinary chrome-rendering container such as
`Dock`, with the intrinsic border properties set on that container.

`HasShadow` enables the overflow and defaults to `false` on `Control`.
`ShadowMode` selects composite styling or block-glyph replacement,
`ShadowOffset` supplies the signed cell translation, and `ShadowGlyph` supplies
the printable one-cell block Rune. The base defaults are composite mode, zero
offset, and dark shade `▓`; derived controls such as `Button` and `Window` may
publish different class defaults. `ShadowForeground`, `ShadowBackground`, and
`ShadowAttributes` style only the shadow. An explicit `ShadowBackground`
replaces the background of shadow cells while composite mode preserves their
graphemes and complete wide-cell ownership. When `ShadowBackground` is null, a
generic `Background` supplies the same opaque shadow fallback; when both are
null, composite shadowing preserves the destination background. Unsupported wide
block glyphs use the documented fixed-cell fallback under the inherited
ambiguous-width policy.

All shadow properties invalidate render only. `ShadowMode` rejects undefined
values, and `ShadowGlyph` rejects control or non-one-cell Runes before mutation.
Signed offsets can overflow on any side: the shadow is clipped by the inherited
ancestor clip and frame bounds, never participates in layout, and never expands
pointer hit testing.

The base control also deflates `ContentBounds` by border thickness before
padding. See [Theming a new control](theming-new-controls.md) for the full
extender surface.
