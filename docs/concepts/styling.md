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
Measure-impact properties are normal-state values only. Render-impact properties
may vary by overlay state. Appearance never controls behavior: `IsEnabled`
determines input acceptance.

## Invalidation and tests

Property metadata declares the earliest affected phase: measure, arrange, or
render. Tests cover registration, theme precedence, local override/clear,
application theme switching, standard theme cells, third-party style properties,
showcase theme toggling, and exact terminal cell output.

## Legacy resources

The older ancestor-inheriting `Style` and `Appearance` resources remain for
compatibility tests of the legacy resolver. New code and showcase styling use
`ControlStyle<TControl>`, `Theme`, and typed style properties exclusively.
