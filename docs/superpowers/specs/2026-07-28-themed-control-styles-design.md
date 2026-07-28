# Themed control styles and protected chrome

## Status

Approved design for replacing independently mutable control presentation
properties with validated, theme-owned style values. Application-level active
Window ownership is intentionally a separate design because it changes input,
focus, z-order, and surface lifecycle rather than control-style composition.

## Goals

- Give every built-in visual style a complete immutable value.
- Make the active Theme supply each control's default style automatically.
- Let one explicit developer style replace the complete themed style.
- Prevent ordinary consumers from creating unsupported border, shadow, and
  visual-state combinations on specialized controls.
- Preserve public read-only access to the fully resolved border and shadow.
- Keep behavioral policy independent from visual style.
- Remove superseded alpha APIs rather than retaining compatibility aliases.

## Non-goals

- This change does not add selectors, a mutable style registry, or CLR-type
  lookup.
- It does not move orientation, selection, movement, scrolling policy,
  placement, timing, or other behavior into Theme.
- It does not define active-Window ownership or modeless z-order promotion.
- It does not make every code-owned glyph in the library theme-configurable.
  A glyph becomes part of a style only when it is one member of the control's
  coupled presentation recipe.

## Style model

Every migrated control exposes one nullable local `Style` and one complete
resolved `ActualStyle`:

```csharp
public ButtonStyle? Style { get; set; }

public ButtonStyle ActualStyle { get; }
```

`null` means that the Theme owns the style. The resolution order is:

```text
explicit control Style -> active Theme style -> library fallback style
```

The library fallback keeps a detached control measurable and renderable before
it inherits a Theme. Attaching the control replaces that fallback with the
active Theme style unless the developer assigned `Style`.

An explicit style replaces the complete themed style; styles are not merged
member by member at the control. Concrete `Color` and
`TerminalAttributes` members remain literal across Theme replacement, while
`ThemeColor` and `ThemeDecoration` members resolve through the newly active
Theme. Assigning `null` returns the control to Theme ownership.

Every complete style is an immutable value with an explicit validating
constructor and static built-in presets. Every style has a matching immutable
`*StyleSet` whose nullable members represent a partial Theme contribution. A
Theme loader overlays its partial set on the library fallback and publishes
only the resulting complete style. Controls never consume a partial set.

Style equality is semantic. Assigning an equal local style or assigning `null`
when the style is already Theme-owned is a no-op and raises no notification or
invalidation.

## Style inventory

### ButtonStyle

`ButtonStyle` owns the Button's internal padding and complete normal/state
appearance profile. It supplies at least `Standard` and `Filled` presets and
replaces `ButtonKind` plus the kind-selecting constructors.

The constructor validates every reachable combination of the profile's visual
states. A resolved Button state cannot have both a visible shadow and any
enabled border side. This guarantees that changing hover, focus, pressed, or
disabled state cannot create a layout-invalid hybrid after the normal style was
accepted.

External alignment, explicit width/height, command behavior, default/cancel
semantics, and text alignment remain ordinary Button properties. A style does
not silently overwrite caller-owned outer layout policy.

### ScrollBarStyle

`ScrollBarStyle` owns:

- compact/full chrome;
- block/line fill treatment;
- the complete directional-button, track, and thumb glyph family;
- track, thumb, and button colors; and
- the complete normal/state appearance profile.

It replaces `ScrollBar.Chrome`, `ScrollBar.Fill`, the four glyph properties,
the three part-color properties, and `ResetGlyphs()`. The existing
`ScrollBarChrome` and `ScrollBarFill` values may remain implementation details
inside `ScrollBarStyle`; they are no longer independently mutable control
properties.

Hosts with generated scrollbars expose one nullable `ScrollBarStyle` override
instead of separate chrome and fill properties. This applies to `Container`,
`ListView`, `TextInput`, `ComboBox`, and `Table`. A generated bar resolves the
host override first and the Theme's `ScrollBar` style otherwise. Creating or
recreating generated bars must not copy the Theme style into a local override.

### CheckBoxStyle

`CheckBoxStyle` owns mark form, unchecked/checked/indeterminate glyphs, and the
complete normal/state appearance profile. It replaces `MarkStyle`,
`MarkGlyphs`, and `ResetMarkGlyphs()`. Its presets retain bracket, tick, and
square presentations. Mark widths are part of the style contract: brackets
reserve three cells and the other built-in forms reserve one.

### RadioButtonStyle

`RadioButtonStyle` owns mark form, unchecked/checked glyphs, and the complete
normal/state appearance profile. Its built-in presentations include:

- `Parentheses`, which renders `( )` and `(•)` and reserves three cells; and
- `Glyph`, which renders validated one-cell unchecked and checked marks.

It replaces the independent glyph properties and `ResetGlyphs()`. Grouping,
selection, arrow navigation, and activation remain behavioral properties.

### SpinnerStyle

`SpinnerStyle` owns an immutable, non-empty sequence of validated one-cell
frames and the complete appearance profile. Static presets replace the current
pattern enum at the control surface. Interval and playback remain behavioral
properties.

### ChaseIndicatorStyle

`ChaseIndicatorStyle` owns the validated active/inactive glyph presentation and
complete appearance profile. Static presets replace the current pattern enum at
the control surface. Movement, orientation, length, spacing, trail behavior,
timing, and playback remain behavioral properties.

## Theme contract

`Theme` publishes complete strongly typed properties named `Button`,
`ScrollBar`, `CheckBox`, `RadioButton`, `Spinner`, and `ChaseIndicator`. The
fixed semantic `Control`, `Input`, `Container`, `Window`, and `Popup` profiles
remain available for controls that do not have a specialized style.

The Button style owns the profile used by `ThemeRole.Button`. A third-party
command control can therefore continue selecting that role without writing
Theme plumbing or depending on `Button` itself.

The JSON `styles` object gains matching fixed properties. Each accepts the
partial shape represented by its `*StyleSet`, including partial appearance
contributions for normal and visual states. Unknown fields remain invalid.
Theme loading performs these steps:

1. Deserialize the bounded document into typed definition objects.
2. Resolve palette, semantic colors, and semantic attributes.
3. Overlay each supplied control style set on its library fallback.
4. Complete inherited appearance profiles.
5. Validate the resulting complete control style and cross-member invariants.
6. Freeze and publish the Theme.

All bundled Theme documents explicitly define the default style choices they
intend. External documents may omit a complete control-style block or any
member within it; omitted values use the library fallback. This permits a Theme
to change one member without manually reproducing an entire style.

An invalid JSON style fails Theme loading with a source- and property-path
labelled `InvalidDataException`. Programmatic style construction validates
before assigning any member and throws the documented argument exception.

## Protected chrome boundary

`Control` continues to own intrinsic chrome composition, caching, layout, and
rendering, but the following authoring members become protected:

- `Border` and `ResetBorder()`;
- `Shadow` and `ResetShadow()`; and
- `SetAppearance(VisualState, AppearanceSet?)`.

This includes both normal chrome and state-specific chrome; leaving
`SetAppearance` public would bypass the style invariants. `Face` remains public
because changing it does not change box geometry. `ActualFace`, `ActualBorder`,
and `ActualShadow` remain public read-only resolved values for inspection and
third-party rendering.

Derived controls, including third-party controls, can use the protected
authoring members to implement their own validated style contract. They do not
need access to internal resolvers.

Only these general-purpose structural hosts republish public `Border`,
`Shadow`, `ResetBorder()`, and `ResetShadow()` members:

- `Dock`;
- `Grid`;
- `Stack`;
- `Overlay`;
- `Window`; and
- `Popup`.

These controls intentionally allow caller-authored chrome. Specialized
controls—including Button, input controls, collections, menus, and GroupBox—do
not republish it. The public wrappers delegate to the protected Control
implementation and preserve validation, notification, and invalidation.

## Control resolution and rendering

A migrated control reads `ActualStyle` during measure, arrangement, and
rendering. Theme application must not assign `Padding`, `Border`, `Shadow`, or
other public properties because doing so would incorrectly create local
developer overrides.

The appearance resolver obtains its complete profile from the resolved style
for a specialized control and from `ThemeRole` for other controls. It then
resolves the exact active visual-state combination and converts semantic color
and attribute references to concrete terminal values. Existing
`ActualBorder`/`ActualShadow` caches remain the inspection and rendering source.

Style and Theme changes compare the prior and current resolved structural
members:

- padding, border sides, mark width, scrollbar chrome, and any other desired
  size contribution request measure invalidation;
- colors, attributes, glyph substitutions with unchanged width, fill
  treatment, shadow presentation, and state-only appearance request render
  invalidation; and
- unchanged resolved styles request no work.

All style mutation is dispatcher-affine after attachment. Setters validate
before committing local state. Property-change observers see the new `Style`,
`ActualStyle`, `ActualBorder`, and `ActualShadow` values before callbacks run.

## API migration

This is an intentional alpha API break. Remove rather than obsolete or alias:

- `ButtonKind` and kind-selecting Button constructors;
- independent scrollbar chrome, fill, color, and glyph properties;
- `CheckBoxMarkStyle` as a standalone control property plus independent mark
  glyphs;
- independent RadioButton glyph properties;
- Spinner and ChaseIndicator pattern properties superseded by styles; and
- public raw chrome/state authoring inherited by specialized controls.

Tests, docs, and showcase examples migrate to object-initializer style
assignment where a non-themed presentation is intentional. Dialog buttons and
ordinary controls do not assign styles merely to reconstruct the Theme default.

## Testing

### Public and consumer contract

- Compile ordinary consumer code that assigns `Style` and reads
  `ActualStyle`, `ActualBorder`, and `ActualShadow` without internals access.
- Prove Button and other specialized controls expose no public Border, Shadow,
  reset, or `SetAppearance` authoring surface.
- Prove Dock, Grid, Stack, Overlay, Window, and Popup expose the approved raw
  chrome surface.
- Prove a third-party derived control can implement and publish a validated
  style using the protected members.

### Unit behavior

- Cover complete/partial style composition and every omitted Theme member.
- Cover local-style precedence, clearing to Theme ownership, detached fallback,
  Theme replacement, semantic Theme references, literal values, equality
  no-ops, property notifications, and exact invalidation impact.
- Cover every style constructor's enum, glyph, collection, color, and
  cross-member validation before mutation.
- Exhaust the Button profile's reachable state combinations and reject every
  border-plus-visible-shadow result.
- Cover generated scrollbar creation, removal, recreation, and host override
  changes without accidental local Theme copies.

### Surface behavior

- Render every built-in style in normal, hovered, focused, pressed, selected or
  checked where applicable, and disabled states.
- Prove standard Buttons remain bordered and shadowless while filled Buttons
  remain borderless and shadowed in every combined state.
- Prove RadioButton parentheses render exactly `( )` and `(•)`, reserve three
  cells, preserve label alignment, and degrade the inner mark safely under the
  active Unicode policy.
- Prove themed scrollbars update existing standalone and generated instances
  without reconstruction and retain local style overrides.
- Prove Theme swaps change semantic colors while concrete local values remain
  literal.

### Catalog, documentation, and showcase

- Parse every bundled Theme into all complete typed styles and retain catalog
  caching and metadata behavior.
- Update the Theme, styling, intrinsic chrome, custom-control, box-model, and
  affected control contracts so one normative section owns each rule.
- Update XML documentation for every changed public and protected member.
- Update showcase pages to demonstrate Theme defaults and intentional style
  overrides without raw specialized-control chrome repair.
- Validate the real showcase in tmux after automated gates pass.

## Delivery gates

Implementation is complete only after focused unit, consumer, and surface tests
pass, every bundled Theme loads, documentation and showcase examples agree, and
the repository passes `make format`, `make lint`, `make build`, and `make test`
with no warnings or errors.
