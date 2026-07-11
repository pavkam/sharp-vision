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

## Visual states

Standard states are normal, hovered, pressed, focused, checked, and disabled.
Resolution applies base values, then state overlays in deterministic precedence:
disabled, pressed, checked, focused, hovered, normal for conflicting properties.
Independent properties from combined states remain combined.

Appearance never controls behavior: `IsEnabled` determines input acceptance; a
disabled-looking brush alone does not disable a control.

## Invalidation and tests

Color/attribute changes invalidate render. Border thickness, padding, font-cell
metrics, or other size-affecting values invalidate measure. Tests cover direct
versus inherited values, resource replacement, combined states, dependency
cleanup, disabled semantics, and exact cell styles.
