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

Scope ancestry follows `Control.Parent` through every registered ownership slot.
Private content, presentation hosts, popup edges, and framework parts do not
disappear from the cascade merely because their owner exposes no public
`Children` collection.

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

Border, shadow, and opaque body fill are intrinsic `Control` chrome. There are
no `Border` or `Shadow` wrapper controls. Every control exposes the properties,
and `BorderThickness` always reserves layout; visible chrome requires a render
path that calls `RenderChrome` or a specialized `ControlChrome` equivalent.

| Properties                                                                | Defaults                    | Contract                                                                   |
| ------------------------------------------------------------------------- | --------------------------- | -------------------------------------------------------------------------- |
| `BorderThickness`                                                         | Zero edges                  | Zero-or-one physical edges; `Measure` impact because layout reserves them. |
| `BorderGlyphs`, `BorderColor`, `BorderAttributes`                         | Default glyphs, null styles | Validated one-cell glyphs and render-only border appearance.               |
| `HasShadow`, `ShadowMode`, `ShadowOffset`                                 | `false`, composite, `(0,0)` | Render-only visual overflow; it never enlarges layout or hit targets.      |
| `ShadowGlyph`, `ShadowForeground`, `ShadowBackground`, `ShadowAttributes` | `▓`, null styles            | Validated one-cell glyph and render-only shadow appearance.                |

These are registered base defaults. Effective values still resolve through the
complete cascade. Standard themes, for example, supply semantic body background,
border color, and shadow foreground values even though the base metadata for
those colors is null; Button and Window add their own class defaults.

`BorderThickness` is reserved by the base measure/arrange pipeline before
`Padding`; `ContentBounds` is therefore the border-then-padding-deflated content
box. Combined measure insets saturate, partial physical edges reserve only their
active cells, and a theme-resolved thickness change remeasures the control.

Base `OnRender` calls protected `RenderChrome`, which rasterizes the body,
per-side border, and shadow through `ControlChrome`. A derived control that
fully overrides `OnRender` must call `RenderChrome` before custom content when
it wants those intrinsic visuals; layout still reserves a configured border when
it deliberately does not. On the base path, shadow expands `VisualBounds` by the
signed `ShadowOffset`, remains clipped by ancestor canvases, and reserves no
layout, child space, or hit target. Button intentionally translates its face and
owned content while pressed.

Base chrome draws the translated shadow first, then clears the body when
`FillMode` is opaque or `Background` resolves from any cascade layer, and draws
the border last. Composite mode restyles existing cells in the shadow footprint;
block mode replaces them with `ShadowGlyph`. A `(0,0)` offset leaves the
translated footprint wholly inside the excluded body and is therefore invisible.
Partial borders draw only enabled physical edges, with corners only where
adjoining edges meet. If the active ambiguous-width policy would make a
configured glyph wide, rendering repairs it to portable ASCII `+`, `-`, `|`, or
`#` rather than splitting a cell.

`Button` does not use the base options verbatim: its specialized `ControlChrome`
call translates the pressed face/content, preserves the shadow gap, and resolves
the detached shadow from normal appearance. `Window` retains its bespoke titled
uniform frame and draws its optional shadow explicitly; it does not express that
frame through `BorderThickness` or call base `RenderChrome`.

Sealed bespoke renderers such as `Text`, `FigletText`, and `TextInput` do not
automatically paint base chrome. Use an ordinary chrome-rendering container such
as `Dock` when callers need to frame or shadow one of those controls.

Intrinsic chrome does not create an ownership edge. When chrome needs its own
margin, style scope, bounds, routed ancestry, or lifetime, compose an ordinary
container such as `Dock` as the distinct node and set the intrinsic properties
on that container. See
[Custom components](custom-components.md#chrome-and-custom-rendering) and
[Theming a new control](theming-new-controls.md) for the extender surface.
