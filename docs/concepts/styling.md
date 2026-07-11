# Styling and visual states

## Styling contract

Styles are mutable resources with change notification; controls are ordinary
mutable objects and no virtual-tree reconciliation is involved. A style change
invalidates only dependent controls and only the required phase.

## Values and scope

Styles can provide foreground/background, text attributes, border glyphs/colors,
padding, and control-specific appearance. Inheritable values flow through
documented resource scopes. Direct control values override scoped values. Unset
and explicit default are distinct so inheritance can be restored.

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

Color/attribute changes invalidate render. Border thickness, padding, font-cell
metrics, or other size-affecting values invalidate measure. Tests cover direct
versus inherited values, resource replacement, combined states, dependency
cleanup, disabled semantics, and exact cell styles.

`Control.Style` is nullable: null inherits the nearest ancestor resource. A
direct subscriber propagates changes only through descendants that still inherit
it and stops at another direct style. Replacement, detach, and disposal
unsubscribe explicitly; reattachment subscribes once. Resource replacement is
measure-conservative, while committed resource changes carry precise render or
measure impact.

Behavior state produces `IsHovered`, `IsFocused`, `IsPressed`, and disabled
overlays. Checked controls extend the protected state calculation with
`State.Checked`. Appearance never writes those behavior values.
