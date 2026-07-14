# Themeable palettes: JSON theme files and curated defaults

Date: 2026-07-14

## Problem

SharpVision ships two hardcoded themes (`Themes.White`, `Themes.Dark`) built in
C# from palette indices. There is no way to define a theme without writing code,
no curated set of recognizable themes, and nothing loadable at runtime. The
popular editor themes users expect (Tokyo Night, Catppuccin, Gruvbox, Dracula,
Nord, Monokai, Solarized, One Dark) are all authored as **palettes** — a named
set of hex colors plus an assignment of those colors to semantic slots — which
maps directly onto SharpVision's existing `ColorRole` layer.

## Goal

- A theme is defined by a small JSON file: a palette plus a role map. Authoring
  a new theme requires no C# changes.
- Themes ship embedded in `SharpVision` and are discoverable and loadable by
  name at runtime; user-supplied theme files load from any file or stream.
- A curated set of ~10 well-known themes ships as embedded defaults.
- The built-in Light/Dark themes become JSON themes too, so _every_ theme is
  defined the same way.

This layers on the existing `ColorRole` + `ControlStyle` model. It does not
introduce per-control stylesheets in files, a new value-parser surface, YAML, or
any new runtime dependency (`System.Text.Json` only, matching `FigletCatalog`).

## Design

Scope: `src/SharpVision.Terminal/Protocols/` (hex parsing),
`src/SharpVision/Styling/` (roles, loader, catalog, JSON theme resources),
`src/SharpVision.Showcase/` (picker wiring), and affected docs and tests.

### 1. Hex color parsing — `Color`

Editor palettes are hex, so `Color` gains culture-independent parsing producing
`ColorKind.Rgb`:

- `public static Color FromHex(string value)` — accepts `#rgb` and `#rrggbb`,
  case-insensitive, leading `#` optional, no surrounding whitespace. `#rgb`
  expands each nibble (`#f80` → `#ff8800`).
- `public static bool TryFromHex(string value, out Color color)` — non-throwing
  form; returns `false` and `Color.Default` on any rejection.

`FromHex` throws `ArgumentNullException` for null and `FormatException` for any
malformed input (wrong length, non-hex digit, `#rrggbbaa` — terminals have no
alpha). Parsing uses `byte.Parse`/manual nibble math with
`CultureInfo.InvariantCulture` and no allocation of intermediate strings in the
common path.

### 2. Semantic roles — `ColorRole` (7 → 12)

Rename the existing `Selection` member to `SelectionBackground` (its documented
meaning is already "the background color of a selected item"), so it pairs
symmetrically with a new `SelectionForeground`. This matches VS Code's
`selection.background`/`.foreground` naming. `Selection` has a single existing
reference (`Themes.cs`, rewritten by §7), so the rename is contained.

Then add five members:

- `SelectionForeground` — text color on a selected item (today only the
  background is themed).
- `Error`, `Warning`, `Success`, `Info` — status colors.

Every new role has a fallback derivation (§4.2) so a theme is valid with only
`Background` and `Foreground` defined. `Themes.cs` role assignment and the base
control-style recipe are updated to set the new roles.

The internal type that turns resolved roles into a frozen `Theme` (the "recipe")
is named `ThemeBuilder`.

### 3. Theme file format

One JSON object per theme, self-describing. Roles reference a palette key **or**
an inline `#hex` string. Example (Tokyo Night — exact values taken from the
project's official source at implementation time):

```jsonc
{
  "name": "Tokyo Night",
  "slug": "tokyo-night", // stable unique catalog key
  "colorScheme": "dark", // "dark" | "light"
  "order": 10, // deterministic catalog sort key
  "author": "Folke Lemaitre",
  "license": "MIT",
  "source": "https://github.com/folke/tokyonight.nvim",
  "palette": {
    // arbitrary named hex colors
    "bg": "#1a1b26",
    "fg": "#c0caf5",
    "comment": "#565f89",
    "blue": "#7aa2f7",
    "red": "#f7768e",
    "green": "#9ece6a",
    "yellow": "#e0af68",
    "magenta": "#bb9af7",
    "selection": "#283457",
  },
  "roles": {
    // semantic slot -> palette key or #hex
    "background": "bg",
    "foreground": "fg",
    "surface": "#16161e",
    "border": "comment",
    "muted": "comment",
    "accent": "blue",
    "selectionBackground": "selection",
    "selectionForeground": "fg",
    "error": "red",
    "warning": "yellow",
    "success": "green",
    "info": "blue",
  },
}
```

Field rules:

- `slug` — non-empty, unique within the catalog, `[a-z0-9-]`, used as the
  resource key and load key.
- `colorScheme` — exactly `"dark"` or `"light"` (CSS `color-scheme` naming).
- `order` — non-negative integer; ties broken by ordinal `slug`.
- `name`, `author`, `license`, `source` — non-empty display/attribution
  metadata.
- `palette` — object of name → color-value string (§3.1); may be empty if roles
  use only inline values.
- `roles` — object of role name (camelCase matching `ColorRole` members) →
  color-value string (§3.1) or palette key. Unknown role names are rejected.

`palette` is authoring sugar; it is resolved into roles at load and **not
retained** on the produced `Theme` in v1.

#### 3.1 Color-value grammar

A color value is one of:

- `#rgb` / `#rrggbb` — a hex RGB color parsed via `Color.FromHex` (§1).
- `idx:N` — a 256-color palette index, `0 ≤ N ≤ 255`, producing
  `Color.Indexed(N)`. This lets a theme use terminal-palette-relative colors
  that adapt to the user's terminal color scheme.

In a `palette` value only the two forms above are legal. In a `roles` value a
string is first tested against these forms (a leading `#` or `idx:` prefix), and
otherwise treated as a palette key. Palette keys are `[a-z0-9-]` and never
contain `#` or `:`, so the three cases are unambiguous. Parsing lives in an
internal `ThemeColorValue` helper in the styling layer, not on `Color` (which
stays pure hex).

### 4. Loading pipeline

`JSON → ThemeDefinition → recipe → frozen Theme`.

#### 4.1 Deserialize

An internal `ThemeDefinition` DTO deserialized with `System.Text.Json`
(`JsonSerializerOptions` with case-insensitive names off; explicit camelCase
contract). Malformed JSON surfaces as `InvalidDataException` wrapping the parse
error and naming the theme source.

#### 4.2 Resolve roles

Each `roles` value is resolved per the §3.1 grammar: `#hex` via `Color.FromHex`,
`idx:N` via `Color.Indexed`, otherwise a `palette` key lookup (whose value is
itself `#hex` or `idx:N`). A bad hex value, out-of-range index, or unknown
palette key throws `InvalidDataException` naming the theme slug and the role.

Missing roles fill by derivation. To keep the `Border`/`Muted` cross-reference
non-circular, derivation runs in a **fixed order**, each step consulting only
roles resolved by an earlier step:

1. `Background` — required; load throws `InvalidDataException` if unresolved.
2. `Foreground` — required; load throws if unresolved.
3. `Accent` ← `Foreground`.
4. `Muted` ← explicit `Border` if present, else `Foreground`.
5. `Border` ← `Muted` (resolved in step 4).
6. `Surface` ← `Background`.
7. `SelectionBackground` ← `Accent`.
8. `SelectionForeground` ← `Foreground`.
9. `Error`, `Warning`, `Success`, `Info` ← `Accent`.

Worked cases: both `Border` and `Muted` absent → both become `Foreground`;
`Border` present only → `Muted` takes `Border`; `Muted` present only → `Border`
takes `Muted`. Every chain terminates at a required role.

#### 4.3 Recipe

A single built-in recipe generalizes today's `Themes.CreateWhite/CreateDark`.
Given the 12 resolved role colors it produces a fresh `Theme`, sets every role
via `SetColor`, and sets one base `ControlStyle<Control>`:

| Property                   | State    | Source role            |
| -------------------------- | -------- | ---------------------- |
| `ForegroundProperty`       | Normal   | `Foreground`           |
| `BackgroundProperty`       | Normal   | `Background`           |
| `BorderColorProperty`      | Normal   | `Border`               |
| `ForegroundProperty`       | Hovered  | `Accent`               |
| `AttributesProperty`       | Focused  | `Underline` (constant) |
| `ForegroundProperty`       | Checked  | `SelectionForeground`  |
| `BackgroundProperty`       | Checked  | `SelectionBackground`  |
| `ForegroundProperty`       | Selected | `SelectionForeground`  |
| `BackgroundProperty`       | Selected | `SelectionBackground`  |
| `ForegroundProperty`       | Disabled | `Muted`                |
| `ShadowForegroundProperty` | Normal   | `Border`               |

The theme is then `Freeze()`d and returned. v1 ships exactly this recipe; a
pluggable recipe delegate is a future extension and is not built now.

### 5. Embedded resources and `ThemeCatalog`

- Theme files live at `src/SharpVision/Styling/Themes/<slug>.theme.json`,
  embedded through `<EmbeddedResource>` in `SharpVision.csproj` with logical
  names `SharpVision.Styling.Themes.<slug>.theme.json` (same mechanism as the
  FIGlet archive).
- `ThemeCatalog.Default` is a process-wide singleton mirroring `FigletCatalog`.
  On construction it enumerates embedded theme resources
  (`Assembly.GetManifestResourceNames()` filtered by prefix/suffix), parses each
  file's metadata, validates unique slugs, and orders entries by
  `(order, slug)`. There is **no separate manifest file** — each theme file is
  its own source of truth, avoiding drift.
- Public surface:
  - `IReadOnlyList<ThemeCatalogEntry> Entries` — ordered metadata (`Name`,
    `Slug`, `ColorScheme`, `Author`, `License`, `Source`).
  - `IReadOnlyList<string> Slugs` — ordered slugs.
  - `Theme Load(string slug)` — builds the theme, freezes, and caches the
    result; repeated loads return the same frozen instance.
    `KeyNotFoundException` for an unknown slug.
- `ColorScheme` is a small enum (`Dark`, `Light`).

Curated default set (~10, per approval): Tokyo Night (Night/Storm/Day),
Catppuccin (Mocha/Latte), Gruvbox (Dark/Light), Dracula, Nord, Monokai,
Solarized (Dark/Light), One Dark. Exact palette values are taken from each
project's official repository during implementation; `author`, `license`, and
`source` are recorded in every file. A catalog test asserts the expected slug
count so accidental resource drift fails the build.

### 6. Runtime loading of external themes

A public `ThemeFile` loader exposes the same pipeline for user-supplied files:

- `static Theme Parse(string json)` — from JSON text.
- `static Theme Load(Stream stream)` — from a stream (caller owns the stream).
- `static Theme LoadFile(string path)` — from a file path.

All three run §4 validation and return a frozen `Theme`. `ArgumentNullException`
for null arguments; `InvalidDataException` for malformed or invalid content;
`FileNotFoundException`/`IOException` propagate from `LoadFile`.

### 7. Built-in Light/Dark unified as JSON

`Themes.White` and `Themes.Dark` keep their public API but become
catalog-backed. Two files ship — `default-light.theme.json` and
`default-dark.theme.json` — reproducing the current palettes **exactly** using
`idx:N` values (`background`/`foreground`/`border`/`accent`/`surface`/`muted`/
`selectionBackground`/`selectionForeground` map to the same indices the
hardcoded themes use today). Indexed values keep the default themes adapting to the user's
terminal palette, unlike the absolute-RGB editor themes. `Themes.White`/`Dark`
become lazy properties delegating to `ThemeCatalog.Default.Load(...)`. The
hardcoded palette code in `Themes.cs` is deleted. Because the recipe (§4.3)
reproduces the existing base control-style table, existing built-in-theme tests
(`StandardThemeTests`, `ColorRoleTests`) keep asserting the same indexed values.
This makes every theme, including the built-ins, defined in JSON.

### 8. Showcase integration

- `Gallery` replaces its hardcoded theme array with `ThemeCatalog.Default`. The
  sidebar `ComboBox` items come from catalog entry display names (dark group
  first, then light, using each entry's `ColorScheme`, preserving catalog order
  within each). `SelectionChanged` sets
  `Application.Theme = ThemeCatalog.Default.Load(slug)`. Default selection stays
  the dark built-in to match `OnAttach`.
- `ThemingShowcasePane` documents the theme-file format and renders role
  swatches for the active theme (a small stack of labeled color cells) so the
  palette is visible.

### 9. Tests

Per AGENTS.md (xUnit v3, Shouldly, Arrange/Act/Assert, failing-first):

- `Color`: `FromHex`/`TryFromHex` for `#rgb`, `#rrggbb`, missing `#`, mixed
  case, and each rejection (null, empty, wrong length, non-hex digit, 8-digit
  alpha); RGB component round-trip.
- Color-value grammar: `#hex`, `idx:N` (incl. `idx:0`/`idx:255`), and
  palette-key resolution; rejects `idx:256`, `idx:-1`, and empty.
- Loader: role resolution via palette key, inline hex, and inline `idx:N`; every
  fallback in the §4.2 table; each failure mode (missing `background`/
  `foreground`, bad hex, out-of-range index, unknown palette key, unknown role
  name, duplicate slug, malformed JSON).
- Recipe: produced `Theme` is frozen, exposes all 12 roles, and resolves the
  base control-style table for representative states.
- `ThemeCatalog`: every embedded theme loads and freezes; slug count matches the
  curated set; ordering is deterministic; `Load` caches (same instance).
- `ThemeFile`: `Parse`/`Load`/`LoadFile` round-trip a known file to an
  equivalent theme; invalid inputs throw the documented exceptions.
- Showcase: a screen test switches themes through the `ComboBox` and asserts
  `Application.Theme` is the loaded theme; the built-in Light/Dark equivalence
  test is updated to the catalog-backed values.

### 10. Documentation

- New concept spec under `docs/concepts/` describing the theme-file format
  (fields, roles, fallbacks, the recipe, authoring a theme, attribution).
- Update the `ColorRole` reference and the Theming control page for the new
  roles and the picker.
- Update `docs/architecture/showcase.md` for the catalog-backed picker.

## Out of scope (YAGNI)

- Per-control or per-state overrides in theme files; a full stylesheet format.
- YAML support (would add a runtime dependency).
- Pluggable recipes.
- Retaining the named palette on the runtime `Theme`.
- Live file-watching / hot reload of theme files.

Each is a natural later extension the format leaves room for.
