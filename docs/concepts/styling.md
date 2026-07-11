# Styling and visual states

## Styling contract

Styles are mutable resources with change notification; controls are ordinary
mutable objects and no virtual-tree reconciliation is involved. A style change
invalidates only dependent controls and only the required phase.

## Values and scope

Styles can provide foreground/background, text attributes, border glyphs/colors,
padding, and control-specific appearance. A null `Control.Style` inherits the
nearest ancestor style; a non-null style replaces that resource scope. Unset
appearance fields and explicit terminal defaults are distinct.

The base control converts foreground, background, and attributes into terminal
cell style. Concrete Phase 5 controls consume resolved border and style-padding
fields according to their own drawing and box contracts; style padding does not
silently replace the base `Control.Padding` property.

Text foreground/background/attribute values and Border
border-color/background/attribute values are nullable direct overrides. Null
inherits the fully resolved active appearance; explicit terminal defaults and
`Attributes.None` replace inherited values. Geometry-affecting padding
invalidates measure, while display-only overrides invalidate render.

`Appearance` represents every field as an optional value. Null is unset;
`Color.Default`, `Attributes.None`, zero `Thickness`, and a concrete border Rune
are explicit overlays. `Resolver.ToTerminal` converts the final optional fields
to the complete semantic style stored in terminal cells.

## Visual states

Standard states are normal, hovered, pressed, focused, checked, and disabled.
Resolution applies base values, then state overlays in deterministic precedence:
disabled, pressed, checked, focused, hovered, normal for conflicting properties.
Independent properties from combined states remain combined.

`Style.Set` accepts only `State.Normal` or one overlay flag. `Resolver.Resolve`
applies normal, hovered, focused, checked, pressed, then disabled, yielding the
conflict precedence disabled > pressed > checked > focused > hovered > normal.
Fields not set by a higher state remain supplied by lower states.

Appearance never controls behavior: `IsEnabled` determines input acceptance; a
disabled-looking brush alone does not disable a control.

## Invalidation and tests

Color, attribute, border Rune, and border-color changes invalidate render.
Style-padding changes invalidate measure so consuming concrete controls can
remeasure. Tests cover direct versus inherited values, resource replacement,
combined states, dependency cleanup, disabled semantics, and exact cell styles.

`Control.Style` is nullable: null inherits the nearest ancestor resource. A
direct subscriber propagates changes only through descendants that still inherit
it and stops at another direct style. Replacement, detach, and disposal
unsubscribe explicitly; reattachment subscribes once. Resource replacement is
measure-conservative, while committed resource changes carry precise render or
measure impact.

Behavior state produces `IsHovered`, `IsFocused`, `IsPressed`, and disabled
overlays. Checked controls extend the protected state calculation with
`State.Checked`. Appearance never writes those behavior values.
