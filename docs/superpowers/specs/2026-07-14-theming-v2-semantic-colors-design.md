# Theming v2: semantic colors as first-class `Color` values

Date: 2026-07-14

## Problem

Theming currently has two parallel color mechanisms:

1. The **type-keyed style cascade** — `Theme` holds a `ControlStyle` per control
   type, resolved base→derived, that sets typed properties (`Background`,
   `BorderColor`, …). This is the primary, WPF/Avalonia-style mechanism.
2. A **separate semantic-color layer** — `Theme` also holds a
   `Dictionary<ColorRole, Color>` palette, queried out-of-band through
   `Control.TryGetThemeColor(ColorRole)`.

The second layer needs its own control-facing API (`TryGetThemeColor`) and led
to a bespoke `RoleSwatch` control just to display a role's color. The recipe
(`ThemeBuilder`) resolves each role to a concrete color at theme-build time and
bakes the concrete value into the base style, so the semantic link is lost the
moment the theme is built.

The two ideas collapse into one if a semantic color is simply a **kind of
`Color`** that the active theme resolves late — exactly the WinForms
`SystemColors` / Avalonia `{DynamicResource}` model. Then a semantic color flows
through the _same_ style cascade as any other color, needs no side API, and the
base style expresses intent (`Background ← the theme's background role`) instead
of a frozen literal.

## Goal

- A semantic color is a first-class `Color` value (`ThemeColors.Accent`), usable
  anywhere a `Color` is: in a theme's styles, in a per-instance local value, in
  custom control code.
- It resolves to a concrete color **late**, against the active theme, through
  the existing property-resolution path — no separate query API.
- The type-keyed style cascade stays the one mechanism; per-type styles and
  local values still override, so themeability is unchanged.
- Retire `Control.TryGetThemeColor` and the `RoleSwatch` special case.
- The base control style becomes **theme-independent** (a fixed property→role
  mapping); a theme differs from another only by its palette (and any per-type
  style overrides).

No change to the JSON theme file format, the loader, or the `ColorRole` set.
This is an internal-representation and resolution-timing change, plus a small
public ergonomic surface (`ThemeColors`).

## Design

Layers touched: `SharpVision.Terminal/Protocols` (the `Color` representation and
its encode guard), `SharpVision/Styling` (`ThemeColors`, `ThemeBuilder`,
resolver), `SharpVision/Controls` (central resolution point),
`SharpVision.Showcase` (delete `RoleSwatch`, adjust the Theming pane), and
docs/tests.

### 1. `Color` gains a deferred role kind — `SharpVision.Terminal`

`Color` already has one deferred kind: `ColorKind.Default` means "let the
terminal choose." Add a second: `ColorKind.Role` means "let the theme choose,"
carrying an opaque role id. The Terminal layer stays ignorant of what an id
_means_ — it only knows a role color must be resolved before it is encoded.

- `ColorKind` gains `Role`.
- `Color.Role(int id)` — factory validating `0 ≤ id ≤ 255`, storing the id in
  the `Red` byte (as `Indexed` does). Throws `ArgumentOutOfRangeException`
  otherwise.
- `Color.RoleId` — `int` accessor returning the id when `Kind == Role`; throws
  `InvalidOperationException` otherwise.
- Equality: two role colors are equal iff their ids match (falls out of the
  existing record-struct equality over `Kind`/`Red`).

**Encode guard (the invariant).** A role color must never be encoded. Enforce at
the lowest choke point, `CellStyle` construction: the `CellStyle` constructor
throws `ArgumentException` if any of its color arguments has `Kind == Role` ("a
role color must be resolved against a theme before it can be rendered"). Because
every rendered style becomes a `CellStyle` before reaching `Sgr`, this makes an
unresolved role color fail fast in a test rather than emit garbage. `Sgr`'s
existing `default:` throw on unknown `ColorKind` remains as a second backstop.

### 2. `ThemeColors` — the friendly semantic-color surface — `SharpVision.Styling`

`ColorRole` (the enum) stays: it is the id backing, the JSON `roles` keys, and
the palette-map key. It is no longer a control-facing query vocabulary.

Add `public static class ThemeColors` exposing one `Color` property per role,
each returning `Color.Role((int)ColorRole.X)`:

```csharp
public static Color Foreground { get; } = Color.Role((int)ColorRole.Foreground);
public static Color Background { get; } = Color.Role((int)ColorRole.Background);
public static Color Surface { get; }
public static Color Border { get; }
public static Color Accent { get; }
public static Color Muted { get; }
public static Color SelectionBackground { get; }
public static Color SelectionForeground { get; }
public static Color Error { get; }
public static Color Warning { get; }
public static Color Success { get; }
public static Color Info { get; }
```

Usage anywhere a `Color` is expected:

```csharp
style.Set(Control.BackgroundProperty, State.Normal, ThemeColors.Background);
var swatch = new Dock
{
    Background = ThemeColors.Accent,
    FillMode = FillMode.Opaque,
};
```

(Naming: `ThemeColors` — "colors the active theme supplies." Alternatives
considered: `SystemColors` (collides with the WinForms/`System.Drawing`
connotation of OS colors), `StandardColors`. Open to change at spec review; the
design does not depend on the name.)

### 3. Central late resolution — `SharpVision/Controls` + `SharpVision/Styling`

A role color collapses to a concrete color at exactly one place: **property
resolution**. Every consumer (appearance composition, border, shadow, the public
`Foreground`/`Background` getters, the design-time resolver) then sees a
concrete color, so no composition site needs role-awareness.

- `Control.ResolveProperty<T>` (in `Control.ThemeValues.cs`): after
  `ThemeResolver.Resolve` produces the value, if it is a `Color` with
  `Kind == Role`, replace it with the theme's concrete color for that role via
  the control's `ThemeContext`:

  ```csharp
  T value = ThemeResolver.Resolve(this, property, visualState);
  if (value is Color { Kind: ColorKind.Role } role)
  {
      Color concrete = ThemeContext is { } ctx && ctx.TryGetColor((ColorRole)role.RoleId, out Color c)
          ? c
          : Color.Default; // theme always defines all roles (loader fallbacks); Default is a safe last resort
      value = (T)(object)concrete; // T is Color or Color?; both round-trip through the boxed Color
  }
  ```

  This matches `Color?` and `Color` (a `Color?` with a value matches
  `is Color role`). Resolution is cached in the existing
  `_resolvedPropertyCache`, which `SetThemeContext` already clears on every
  theme swap, so a role color re-resolves against the new theme automatically.

- `ThemeResolver.Resolve(Theme, Type, StyleProperty<T>, State)` (the design-time
  overload, which has no live control): apply the same collapse using the passed
  `Theme.TryGetColor`, so design-time/tooling queries also return concrete
  colors.

- Factor the collapse into one internal helper (e.g.
  `SemanticColor.Resolve(Color, lookup)` in `Styling`) shared by both call
  sites.

The single write-time surprise: setting a role color as a **local** value
(`control.Background = ThemeColors.Accent`) is stored as-is and resolved on read
— so a plain control with a role-color background is a live swatch that tracks
theme swaps, with no custom control.

### 4. Recipe: base style expresses roles — `SharpVision/Styling`

`ThemeBuilder.Build` changes so the base `ControlStyle<Control>` holds
role-color _values_ instead of resolved concretes. The mapping is identical to
the current recipe, but every color is a `ThemeColors.X` reference:

| Property                   | State    | Value                             |
| -------------------------- | -------- | --------------------------------- |
| `ForegroundProperty`       | Normal   | `ThemeColors.Foreground`          |
| `BackgroundProperty`       | Normal   | `ThemeColors.Background`          |
| `BorderColorProperty`      | Normal   | `ThemeColors.Border`              |
| `ForegroundProperty`       | Hovered  | `ThemeColors.Accent`              |
| `AttributesProperty`       | Focused  | `Underline` (unchanged)           |
| `ForegroundProperty`       | Checked  | `ThemeColors.SelectionForeground` |
| `BackgroundProperty`       | Checked  | `ThemeColors.SelectionBackground` |
| `ForegroundProperty`       | Selected | `ThemeColors.SelectionForeground` |
| `BackgroundProperty`       | Selected | `ThemeColors.SelectionBackground` |
| `ForegroundProperty`       | Disabled | `ThemeColors.Muted`               |
| `ShadowForegroundProperty` | Normal   | `ThemeColors.Border`              |

Because none of these values depend on the specific theme, this base style is
now **theme-independent** — every theme's base style is byte-identical.
`ThemeBuilder` still writes the palette (`SetColor(role, concreteColor)` for all
12 roles) from the resolved role map; that palette is what the late resolution
reads. The theme is then frozen as before.

Consequence: `Themes.White`/`Dark` and every catalog theme still resolve to the
same concrete appearance as today (the palette values are unchanged; only _when_
they are applied moved from build-time to render-time).

### 5. Retire the old semantic-color API — `SharpVision/Controls` + Showcase

- Delete `Control.TryGetThemeColor` (superseded by role-colors flowing through
  style properties). `ThemeContext.TryGetColor` stays (internal; the resolver
  uses it).
- Delete `RoleSwatch`. The Theming pane's role-swatch section becomes a column
  of plain `Dock` fill controls with `Background = ThemeColors.<role>`, which
  resolve live through §3 — same visible behavior, no bespoke control.

### 6. Public API surface after v2

- New: `ThemeColors` (12 `Color` properties); `Color.Role(int)`, `Color.RoleId`,
  `ColorKind.Role`.
- Removed: `Control.TryGetThemeColor`; `RoleSwatch` (internal showcase type).
- Unchanged: `ColorRole`, `Theme.SetColor`/`TryGetColor`, `ThemeCatalog`,
  `ThemeFile`, the JSON format, `Themes.White`/`Dark`, all style properties.

## Tests

- `Color`: `Role(id)` valid/invalid ids; `RoleId` accessor + throw off-role;
  role-color equality by id; `Kind == Role`.
- `CellStyle`: constructing with a role-kind foreground/background/underline
  color throws `ArgumentException`.
- `ThemeColors`: each property is a role color whose `RoleId` maps back to the
  matching `ColorRole`.
- Resolution: a control whose resolved `Foreground` is `ThemeColors.Foreground`
  resolves (via `GetValue`/appearance) to the active theme's `Foreground`
  palette color; after `Application.Theme` swaps to a theme with a different
  value, it re-resolves to the new color (proves late binding). A role color set
  as a local value resolves the same way and updates on swap.
- `ThemeResolver` design-time overload returns concrete colors for role-valued
  styles.
- Regression: existing `StandardThemeTests` / `ColorRoleTests` still assert the
  same concrete indexed values (they resolve through §3, so they pass
  unchanged); `ThemeBuilderTests` updated to assert the base style now stores
  role colors and that `ThemeBuilder` output resolves to the seeded palette.
- Showcase: the Theming page renders the active theme's role colors and updates
  on theme swap (replaces the `RoleSwatch` test), driving through the real
  `Application`.

## Migration & compatibility

- No JSON theme file changes; `ThemeCatalog`/`ThemeFile` untouched.
- Built-in and curated themes render identically (same palette values, resolved
  later).
- Breaking API: `Control.TryGetThemeColor` is removed. Its only caller was
  `RoleSwatch` (also removed). No other production consumer exists.
- `control.Foreground`/`Background` getters continue to return concrete `Color?`
  (resolution happens inside property resolution), so external readers are
  unaffected.

## Out of scope (YAGNI)

- Per-role "on-color" pairing (Material-style `OnAccent`).
- Exposing the palette as a public runtime map beyond the existing
  `Theme.TryGetColor`.
- Blending/opacity or derived shades of a role color.
- Renaming the `ColorRole` members or the JSON keys.
