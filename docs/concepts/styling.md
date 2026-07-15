# Styling and visual states

## Styling contract

SharpVision uses typed style properties, type-keyed themes, and per-instance
style overlays. Controls are ordinary mutable objects; a style or theme change
invalidates only dependent controls and only the required phase.

## Style properties

Each styleable value is registered as immutable `StyleProperty<T>` metadata on
its declaring control type. Controls expose conventional CLR properties backed
by public `GetValue`, `SetValue`, and `ClearValue` operations. Local values win
over every themed, per-instance, class-default, and visual-state layer.

Property metadata carries an ordered `ChangeImpact`: `None`, `Render`,
`Arrange`, or `Measure`. `Arrange` invalidates arrange and render; `Measure`
invalidates measure, arrange, and render. Assigning an equivalent local value is
a no-op and raises neither invalidation nor a property-change notification.

Base `Control` registers margin, padding, foreground, background, attributes,
underline, underline color, fill mode, border chrome, and shadow chrome. Derived
controls register additional properties and may publish class defaults (for
example, `Button` rounded border and compact shadow).

## Themes and resolution

`Theme` owns at most one `IControlStyle` per control type. `Application.Theme`
(default `Themes.Dark`) publishes an internal theme context to every attached
control. Resolution applies these layers from lowest to highest priority:

1. registered default;
2. most-derived class default;
3. theme chains for ancestor `IStyleScope` controls, farthest scope to nearest;
4. the descendant's theme chain from `Control` through its runtime type;
5. instance styles for ancestor scopes, farthest scope to nearest;
6. the descendant's per-instance `Control.Style`;
7. explicit local value.

Within each theme or instance-style layer, the resolver first chooses that
layer's best matching visual-state value. It then advances to the next layer.
Layer priority therefore remains authoritative: a focused value in a lower theme
layer cannot override a normal value in a higher instance layer.

Replacing a style in a `Theme` publishes the maximum aggregate impact of the
removed and replacement styles. This preserves the invalidation needed to erase
old geometry even when the replacement itself is render-only.

`Themes.White` and `Themes.Dark` are frozen standard themes built from the
public `Theme` and `ControlStyle<TControl>` API using the portable 16-color
palette.

## Per-instance styles

`Control.Style` is a nullable `IControlStyle` overlay. It normally applies only
to the owning control. When that control implements `IStyleScope`, its style is
also a lower-priority resource for descendants; nearer scopes override farther
scopes, and the descendant's own style wins over every scope. Replacing
`Control.Style` invalidates the maximum aggregate impact of the old and new
styles.

## Visual states

Standard states are normal, hovered, focused, selected, checked, indeterminate,
pressed, and disabled. The hovered overlay applies only to interactive
(focusable) controls; static content such as text and tables is never marked
hovered. Any style property may vary by overlay state; activating such a state
uses the property's declared `ChangeImpact`. Visual overlays never control
behavior: `IsEnabled` determines input acceptance.

Public theme resolution accepts any combination of defined state flags and
rejects unknown bits before evaluating the cascade.

## Invalidation and tests

Property metadata declares the earliest affected phase with `ChangeImpact`.
Tests cover all four impact mappings, replacement impact, layer and state
precedence, equivalent assignment, registration, local override/clear,
application theme switching, standard theme cells, third-party style properties,
showcase theme toggling, and exact terminal cell output.

## Shared chrome

Border, shadow, and opaque body fill rasterize through one internal geometry so
`Button`, `Window`, `Border`, and `Shadow` share a single draw path. A derived
control draws the same chrome through the protected `RenderChrome` method (base
`OnRender` calls it); the base control deflates `ContentBounds` by border
thickness before padding. See [Theming a new control](theming-new-controls.md)
for the full extender surface.
