# Themes

## Overview

A SharpVision theme is one bounded UTF-8 JSON document. It defines global
semantic colors, terminal attributes, and five high-level appearance profiles.
Controls supply their own mechanical defaults and select one of those profiles;
the document contains no control instances or application-defined selector
names.

```mermaid
flowchart LR
    JSON["bounded JSON"] --> Values["colors and attributes"]
    JSON --> Profiles["semantic profiles"]
    Values --> Profiles
    Profiles --> Theme["frozen Theme"]
    Theme --> Controls["controls select a ThemeRole"]
```

The root accepts only these fields:

| Field                                  | Type     | Description                                                 |
| -------------------------------------- | -------- | ----------------------------------------------------------- |
| `name`, `slug`, `colorScheme`, `order` | metadata | Embedded catalog identity and ordering.                     |
| `author`, `license`, `source`          | metadata | Attribution and provenance.                                 |
| `palette`                              | object   | At most 256 case-sensitive names mapped to RGB literals.    |
| `colors`                               | object   | One concrete value for every known `ThemeColor`.            |
| `attributes`                           | object   | One concrete value for every known `ThemeDecoration`.       |
| `styles`                               | object   | The five fixed semantic appearance profiles.                |
| `status`                               | object   | Optional compatibility aliases for the six status meanings. |

Unknown and duplicate fields are rejected. Embedded themes require complete
metadata. External documents default missing identity metadata to `Custom`,
`custom`, dark, and order zero.

## Global values

`colors` requires these 29 properties:

```text
window, windowText, surface, surfaceText, control, controlText,
controlBorder, controlShadow, activeControl, activeText, activeBorder,
focusedControl, focusedText, focusedBorder, pressedControl, pressedText,
pressedBorder, selectedControl, selectedText, disabledControl,
disabledText, disabledBorder, accent, muted, hotkey, error, warning,
success, info
```

Each value is `#RGB`, `#RRGGBB`, or an exact palette name. Palette entries are
RGB literals and cannot reference each other. Theme loading resolves these
values to concrete `Color` instances. `Theme.ResolveColor(ThemeColor)` is the
typed public lookup.

`attributes` requires these nine properties:

```text
normalText, activeText, focusedText, pressedText, selectedText,
disabledText, border, shadow, hotkey
```

Each accepts one attribute name or an array of names. Empty arrays mean no
attributes. `Theme.ResolveAttributes(ThemeDecoration)` is the typed public
lookup.

The optional `status` object accepts `error`, `warning`, `success`, `info`,
`muted`, and `hotkey`. Missing entries use the equivalent global color. The
`Theme.Error`, `Warning`, `Success`, `Info`, `Muted`, and `Hotkey` properties
remain convenience accessors.

## Semantic profiles

`styles` is strongly typed. Five properties are semantic appearance profiles:

| JSON property | `ThemeRole` | Intended use                              |
| ------------- | ----------- | ----------------------------------------- |
| `control`     | `Control`   | Passive controls and the common fallback. |
| `input`       | `Input`     | Editable or selectable input chrome.      |
| `container`   | `Container` | Framed grouping surfaces.                 |
| `window`      | `Window`    | Top-level window surfaces.                |
| `popup`       | `Popup`     | Transient popup surfaces.                 |

Every profile has a complete `normal` appearance after loading and may supply
partial contributions for `pointerOver`, `focusWithin`, `focused`, `current`,
`selected`, `checked`, `indeterminate`, `pressed`, and `disabled`. Missing role
normal values and most missing state contributions inherit from `control`;
missing members inherit from the earlier complete appearance. `Container` and
`Window` start `pointerOver` empty instead of inheriting generic hover. `Window`
also starts `focused` empty and supplies an `activeBorder`-only `focusWithin`
default, so descendant or direct focus activates the frame without changing the
window face.

The bundled themes keep `container` and `window` visually unchanged during
`pointerOver`. The bundled `input` profile uses `surface` for its normal face
and `activeControl` during hover, while `button` also opts into the filled hover
background. External themes may still author an explicit passive-role hover
contribution when that feedback is intentional.

An appearance has optional `face`, `border`, and `shadow` objects. Their members
match `FaceSet`, `BorderSet`, and `ShadowSet`: face colors and decorations;
border sides, glyph style, colors, and attributes; and shadow visibility, mode,
offset, glyph, colors, and attributes. A normal profile is completed with the
role's library defaults. State contributions remain partial.

Colors and attributes inside profiles may be literal values or the JSON name of
a global semantic value. Border glyph styles and shadow geometry are allowed
because they define the high-level role's chrome, not a specific control type.
Individual controls may still set complete local styles that take precedence.

Control-specific structure — paddings, glyph families, frame sequences, and part
colors — is code-owned and is not part of the theme document. Each styled
control completes its typed `Style` value from library structural defaults plus
the appropriate semantic profile above, so a theme influences those controls
only through its five profiles. A complete local `Style` assignment overrides
both. The five profile properties are the only members `styles` accepts;
control-named sections are rejected as unknown fields.

When several state flags are active, contributions apply in this order:

```text
PointerOver -> FocusWithin -> Focused -> Current -> Selected -> Checked
-> Indeterminate -> Pressed -> Disabled
```

The [appearance contract](styling.md) defines local precedence, ambient face
inheritance, and resolved values.

## Example

```json
{
  "name": "Example",
  "slug": "example",
  "colorScheme": "dark",
  "order": 10,
  "author": "Example author",
  "license": "MIT",
  "source": "https://example.invalid/theme",
  "palette": { "page": "#101218", "ink": "#e7e9ee" },
  "colors": {
    "window": "page",
    "windowText": "ink",
    "surface": "#181b24",
    "surfaceText": "ink",
    "control": "#202431",
    "controlText": "ink",
    "controlBorder": "#596170",
    "controlShadow": "#050608",
    "activeControl": "#283044",
    "activeText": "ink",
    "activeBorder": "#72a7ff",
    "focusedControl": "#22304a",
    "focusedText": "ink",
    "focusedBorder": "#72a7ff",
    "pressedControl": "#182030",
    "pressedText": "ink",
    "pressedBorder": "#72a7ff",
    "selectedControl": "#315b99",
    "selectedText": "ink",
    "disabledControl": "#202431",
    "disabledText": "#808080",
    "disabledBorder": "#464b57",
    "accent": "#72a7ff",
    "muted": "#808080",
    "hotkey": "#ffcc66",
    "error": "#ff5c57",
    "warning": "#f3f99d",
    "success": "#5af78e",
    "info": "#72a7ff"
  },
  "attributes": {
    "normalText": [],
    "activeText": [],
    "focusedText": "bold",
    "pressedText": [],
    "selectedText": [],
    "disabledText": "dim",
    "border": [],
    "shadow": "dim",
    "hotkey": "underline"
  },
  "styles": {
    "control": {
      "normal": {
        "face": {
          "foreground": "controlText",
          "background": "control",
          "attributes": "normalText"
        },
        "border": {
          "sides": "none",
          "glyphStyle": "rounded",
          "foreground": "controlBorder",
          "background": "control",
          "attributes": "border"
        },
        "shadow": {
          "visible": false,
          "mode": "composite",
          "offset": { "x": 0, "y": 0 },
          "glyph": "▓",
          "foreground": "controlShadow",
          "background": "transparent",
          "attributes": "shadow"
        }
      },
      "pointerOver": {
        "face": { "foreground": "activeText" },
        "border": { "foreground": "activeBorder" }
      }
    },
    "input": {
      "normal": {
        "face": { "background": "surface" },
        "border": { "sides": "all", "glyphStyle": "heavy" }
      },
      "pointerOver": { "face": { "background": "activeControl" } }
    },
    "container": {
      "normal": { "border": { "sides": "all", "glyphStyle": "light" } }
    },
    "window": {
      "normal": { "border": { "sides": "all", "glyphStyle": "paired" } }
    },
    "popup": {
      "normal": { "border": { "sides": "all", "glyphStyle": "rounded" } }
    }
  }
}
```

## Loading and publication

`Themes.Load(slug)` loads an embedded theme, `Themes.Parse(json)` parses text,
`Themes.Load(stream)` reads a caller-owned stream without closing it, and
`Themes.LoadFile(path)` reads a file. `Themes.Entries` and `Themes.Slugs` expose
the ordered embedded catalog. Embedded themes are parsed lazily and cached;
external loads return a new frozen instance.

Input is limited to 64 KiB, JSON depth eight, 256 palette entries, six status
entries, and 2,048 characters per metadata string. Comments, trailing commas,
malformed UTF-8, invalid element kinds, unknown names, malformed colors,
conflicting attributes, and invalid composite members fail with a
source-labelled `InvalidDataException`.

Assigning `Application.Theme` publishes the frozen theme through the retained
control tree on the dispatcher. Controls are not reconstructed. The resolver
caches each semantic role and exact visual-state combination; theme replacement,
local appearance changes, and relevant state changes invalidate those entries.

## Expected behavior

| Layer      | Required evidence                                                                                         |
| ---------- | --------------------------------------------------------------------------------------------------------- |
| Parser     | Size/depth bounds, malformed UTF-8/JSON, unknown names, invalid composites, and source-labelled failures. |
| Resolution | Global values, semantic inheritance, state overlays, local precedence, and frozen publication.            |
| Catalog    | Every embedded theme parses, exposes required roles, and retains stable metadata and slug order.          |
| Surface    | Theme swaps update mounted controls, chrome, text, and visual states without reconstruction.              |

- Test stream ownership and file/embedded loading separately.
- Test exact role fallback from `control` and every bundled role override.
- Test dispatcher-affine publication and phase-appropriate invalidation.
