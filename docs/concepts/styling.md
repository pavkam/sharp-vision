# Appearance

## Styling contract

SharpVision controls use direct CLR properties and immutable global themes.
There is no selector cascade, control-type map, or mutable style registry.

`Color` is the concrete RGB/special-color value. `Color.Default` means the
terminal default and `Color.Transparent` preserves a destination background.
`ColorValue` is the discriminated value accepted by appearance composites: it
contains either a concrete `Color` or a library-defined `ThemeColor`.
`AttributeValue` likewise contains either concrete `TerminalAttributes` or a
`ThemeDecoration`. Both provide implicit conversions from either branch.

Appearance is grouped by responsibility:

| Complete value    | Partial set     | Members                                                              |
| ----------------- | --------------- | -------------------------------------------------------------------- |
| `Face`            | `FaceSet`       | Foreground, background, attributes, underline, underline color.      |
| `Border`          | `BorderSet`     | Sides, glyph style, foreground, background, attributes.              |
| `Shadow`          | `ShadowSet`     | Visibility, mode, offset, glyph, foreground, background, attributes. |
| `ThemeAppearance` | `AppearanceSet` | Face, border, and shadow as one bulk appearance.                     |

Complete values always contain every member and are suitable for developer
assignment. Set values contain nullable members and overlay only those supplied;
themes and visual-state customization use them to avoid replacing unrelated
parts of a composite.

Controls with library-owned mechanics expose a complete local style value:

The control property is always nullable `Style`; it accepts a complete value.
`ActualStyle` is always present and combines local mechanics with the active
semantic profile, or uses the code-owned mechanical fallback. Assigning null
restores semantic Theme ownership; a complete local Style still wins.

## Visual states

Each control selects one of the five global `ThemeRole` values. The base
`Control` role is the fallback. Built-in controls choose the appropriate role,
and a third-party control may override the protected `ThemeRole` property.

The resolver applies appearance in this order, with later supplied members
winning:

1. The semantic role's complete normal theme appearance.
2. Theme state contributions in the fixed state order.
3. The complete local control Style's appearance, when one is assigned.
4. The developer's complete local `Face` and any protected derived-control
   border, shadow, or state contributions.

Therefore a developer assignment always wins over the Theme. `ResetFace()`
removes the public local face. Protected chrome reset and state-appearance seams
serve control authors; ordinary applications select a complete control Style.

`ActualFace`, `ActualBorder`, and `ActualShadow` expose the fully resolved
values for the control's current state. They are always available, even when the
developer has assigned no local value. This is the supported inspection surface
for third-party rendering and composition.

Active states apply in this order:

```text
PointerOver -> FocusWithin -> Focused -> Current -> Selected -> Checked
-> Indeterminate -> Pressed -> Disabled
```

Only the direct focus owner contributes `Focused`; ancestors contribute
`FocusWithin`. Physical pointer ancestry remains observable, but passive
controls do not opt into subtree state invalidation.

## Ambient face inheritance

Descendants may inherit a parent's resolved normal text face. Border and shadow
never inherit. A complete local face is authoritative and is not overwritten by
ambient inheritance. An opaque face establishes a natural boundary; set
`AppearanceBoundary` when a transparent composition owner must also stop
inheritance.

Transparent is valid for background composition. Foreground and underline paint
channels reject it. `Color.Default` remains an opaque terminal-default color; it
is not transparency.

## Shared chrome

Border, shadow, and body fill are intrinsic control chrome. The sealed render
pipeline draws shadow and body before content, then overlays the border. Custom
controls implement `OnRenderContent` and do not invoke a chrome helper. The
[intrinsic-chrome contract](intrinsic-chrome.md#intrinsic-chrome-contract) owns
the exact public values, modes, geometry, clipping, and test matrix.

Border background is an independent channel. A hover or focus face-background
change does not repaint border-cell backgrounds unless that state's `BorderSet`
explicitly changes `Background`. This keeps button and input frames visually
stable while still allowing a theme to animate any border member by state. The
semantic `Container` and `Window` profiles do not inherit generic hover
contributions, so passive surfaces remain visually unchanged while pointer
ancestry stays observable. Windows map application-owned `IsActive` onto the
`FocusWithin` appearance contribution to switch only their border to
`ThemeColor.ActiveBorder`; keyboard focus remains an independent fact. Button
and input profiles opt into a filled hover face, and a custom theme may
explicitly add hover styling to any passive role.

`ShadowMode.Composite` restyles destination cells, `BlockGlyph` writes one
configured rune, and `FractionalBlock` uses code-owned half-block glyphs.
Offsets use columns; fractional Y offsets use half rows. Shadows contribute
visual overflow but do not change desired layout size.

Changing border sides can change reserved layout space and causes measure
invalidation. Color, attributes, and glyph-family state changes require only
render invalidation. Exact resolved appearances are cached per control state;
theme publication, local assignments, state changes, and ambient-boundary
changes clear the affected cache entries. A theme replacement compares only the
currently rendered state; inactive state changes are resolved and laid out when
that state next becomes active.

Global Theme profiles define high-level role chrome. Complete typed Theme styles
also own control-specific padding, glyph families, part colors, and appearance.
Ordinary controls receive these automatically; application code needs no Theme
plumbing. Local complete Style values remain authoritative.

The [Control contract](../controls/control.md#intrinsic-appearance) defines the
public properties, layout effects, and rendering order. The
[theme contract](themes.md#theme-file-contract) defines the JSON schema and
global values. The exact border and shadow value surfaces live in
[Intrinsic chrome](intrinsic-chrome.md#intrinsic-chrome-contract).

## Test obligations

| Layer       | Required evidence                                                                                                     |
| ----------- | --------------------------------------------------------------------------------------------------------------------- |
| Unit        | Complete/partial composition order, state precedence, validation, cache invalidation, reset, and ambient inheritance. |
| Surface     | Face, border, shadow, transparency, focus ancestry, combined states, and Unicode-safe chrome.                         |
| Integration | Theme replacement through the retained tree without reconstructing controls.                                          |

- Cover every `ThemeRole` and active `VisualState` combination.
- Cover local complete values followed by local member-wise state overrides.
- Cover transparent boundaries and exact border-background independence.
