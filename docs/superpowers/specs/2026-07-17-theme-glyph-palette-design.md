# Theme glyph palette design

## Purpose

Move every framework-selected control glyph into the active theme. Progress
fills, disclosure and drop-down indicators, borders, shadows, selection marks,
scrollbars, separators, tabs, navigation markers, window chrome, and truncation
must no longer be selected by literal Unicode characters inside control
renderers.

The finished design preserves explicit per-control customization. A control with
no local glyph value follows `Application.Theme`; a control with a local value
keeps it across live theme changes. User text and low-level terminal drawing
primitives remain outside theme policy.

## Current-state finding

Themes currently contain semantic colors only. Controls use three inconsistent
glyph models:

- `Glyphs`, `Marks`, shadow, and scrollbar properties expose some local
  customization;
- several controls select Unicode drawing characters directly during render;
- terminal-safe ASCII repair characters are also selected inside render code.

This makes a live color theme only a partial visual theme. It also makes related
controls disagree about ownership: an expander owns its arrows, a scrollbar
partly owns its glyphs, and a border accepts a caller-selected family.

The worktree already contains unrelated changes to `Expander`, showcase panes,
and surface tests. Implementation must preserve those changes and reconcile
overlapping edits instead of replacing them.

## Alternatives

### Typed semantic glyph palette

Add one complete immutable `ThemeGlyphs` value to `Theme`, composed from
purpose-specific immutable groups. Theme files may override any semantic glyph
or group, and loading fills omitted values from the SharpVision baseline glyph
resource. Controls resolve a local value first and the active palette second.

This is the selected approach. It keeps control classes independent of theme
file structure, gives progress and border families coherent validation, and
preserves semantic intent at the public API.

### Flat glyph-role dictionary

Map a `GlyphRole` enum directly to `Rune`. This makes parsing simple, but border
segments and progress fractions produce a long, fragile role list. Callers could
assemble incomplete families whose ordering cannot be validated as a unit.

### Type-keyed control recipes

Store a glyph recipe for each control type in the theme. This is flexible but
turns themes into a type-style registry, couples the styling layer to the
control catalog, and contradicts the existing rule that themes do not supply
type-keyed appearance recipes.

## Ownership boundary

The theme owns every glyph selected by production code under
`src/SharpVision/Controls`. The audit includes:

- border families, block shadows, and terminal-safe repair glyphs;
- progress empty, full, indeterminate, horizontal fractional, and vertical
  fractional cells;
- collapsed, expanded, and drop-down indicators;
- checkbox, radio, menu-check, and menu-radio marks;
- navigation current, idle, group-disclosure, and separator glyphs;
- scrollbar decrement, increment, track, and thumb glyphs for both orientations
  and both line and block treatments;
- horizontal and vertical separators;
- tab dividers and selected-tab underline;
- window close chrome; and
- text truncation ellipsis.

Spaces and punctuation used only to lay out a composed label are not theme
glyphs. Caller-provided text is never interpreted as a glyph role. Unicode
measurement, canvas line primitives, protocol encoders, FIGlet fonts, and other
lower-level terminal facilities keep their own domain-specific data.

Public `Glyphs` presets remain available as caller conveniences, but controls do
not select a preset implicitly. A preset affects rendering only when a theme or
caller explicitly chooses it.

## Public model

`Theme` gains a `Glyphs` property returning a complete immutable `ThemeGlyphs`.
The value is frozen with the color palette and participates in theme identity.
`ThemeGlyphs` groups related contracts rather than exposing a type-keyed
dictionary:

- `Chrome` contains border, shadow, repair, and window-close values;
- `Progress` contains empty, full, indeterminate, and fractional sequences;
- `Disclosure` contains collapsed, expanded, and drop-down indicators;
- `Selection` contains checkbox, radio, and menu marks;
- `Navigation` contains navigation item, group, and separator values;
- `ScrollBars` contains orientation- and fill-specific button, track, and thumb
  values;
- `Separators` contains horizontal, vertical, tab-divider, and tab-underline
  values; and
- `Text` contains the truncation ellipsis.

Each group is an immutable value with an explicit validating constructor. Every
named public or internal type lives in its own same-named file. The existing
`Glyphs` and `Marks` value types are reused where their contracts match rather
than duplicated.

Themes created programmatically can replace a complete group before `Freeze`.
Mutation after freezing throws exactly as color mutation does. The immutable
published palette cannot expose mutable arrays; fractional progress sequences
use copied immutable storage.

## Local overrides and resolution

The resolution order is:

```text
explicit control value -> active Theme.Glyphs value -> baseline glyph value
```

The baseline applies when a control is detached or its application has no theme.
A loaded `Theme` is complete, so the third step is normally needed only for
detached controls and defensive repair.

Existing glyph properties retain their public types and behavior. Their backing
state records whether the caller explicitly assigned a value; constructor
defaults do not count as local values. A same-value assignment still establishes
a local override. Each affected control exposes a focused reset operation that
clears its local glyph overrides and returns it to theme resolution without
changing unrelated appearance properties.

Controls that currently have no glyph customization receive focused properties
for their semantic glyphs or glyph group. New local properties validate before
changing state and invalidate rendering only. All accepted values occupy one
cell, so neither local replacement nor live theme replacement invalidates
measure or arrange.

Theme propagation already invalidates rendering when `Application.Theme`
changes. Render code resolves the active glyph immediately before drawing, just
as semantic colors resolve immediately before rendering. Controls never cache
resolved theme glyphs across frames.

## Theme file contract

Theme schema version 1 gains an optional `glyphs` object. Existing version 1
files with no glyph data remain valid. The object is semantic and grouped in the
same shape as the public palette; for example:

```json
{
  "version": 1,
  "roles": {
    "background": "idx:0",
    "foreground": "idx:15"
  },
  "glyphs": {
    "disclosure": {
      "collapsed": ">",
      "expanded": "v",
      "dropDown": "v"
    },
    "progress": {
      "empty": ".",
      "full": "#",
      "indeterminate": "=",
      "horizontalFractions": [" ", ".", ":", "-", "=", "+", "*", "%", "#"],
      "verticalFractions": [" ", ".", ":", "-", "=", "+", "*", "%", "#"]
    }
  }
}
```

Omitted groups and members inherit from a versioned embedded baseline glyph
resource. The baseline is parsed by the same strict loader as user data, which
keeps the default glyph source out of control renderers. A loaded `Theme`
retains only the resolved typed palette, not parser dictionaries.

The built-in dark and light themes include explicit glyph sections. At least one
shipped showcase-selectable theme uses a visibly contrasting but terminal- safe
set so live switching demonstrates that glyph theming is functional, not merely
representable. Curated color themes may inherit the baseline when their source
project has no meaningful glyph language.

Unknown root groups, unknown group members, duplicate names, wrong JSON kinds,
and excessive collection sizes are rejected. Existing document byte, decoded
text, key, and nesting limits continue to apply; limits are adjusted only by the
exact bounded capacity required for the typed glyph groups.

## Validation and terminal safety

Every scalar glyph must be one valid Unicode scalar, printable, non-control, and
exactly one cell under SharpVision's default width policy. A malformed
surrogate, empty string, multi-scalar string, combining mark, newline, zero-cell
value, or wide value is rejected before the theme or control changes state.

Progress fraction arrays contain exactly nine entries in ascending fill order:
empty plus seven partial levels plus full. The first and last entries agree with
the group's `Empty` and `Full` values. Constructors copy their input before
publishing it.

Primary drawing glyphs that can become unsuitable under a runtime ambiguous-
width policy carry a theme-owned one-cell repair value in the same semantic
group. Rendering selects that repair value without throwing, drawing half a wide
cell, or changing layout. Control renderers do not supply literal fallback
characters.

Theme file failures throw `InvalidDataException` and identify the exact path,
such as `glyphs.progress.horizontalFractions[4]`. Public constructor and setter
failures use the existing argument exception conventions and occur before any
observable state or invalidation changes.

## Rendering migration

Each affected renderer replaces literal selection with one small semantic
lookup. Rendering algorithms, geometry, event behavior, and visual-state
precedence do not otherwise change.

Composite text remains assembled without allocating in hot loops where the
existing renderer can draw a glyph and label separately. Progress rendering
indexes immutable fractional values rather than string arrays. Separators and
tracks draw validated `Rune` values through `DrawRune`; controls continue to
render semantic cells and never emit terminal bytes.

The migration removes implicit glyph assignments from control constructors.
Explicit assignments in showcase examples remain local overrides and must be
identified as such in their documentation.

## Documentation and showcase

The normative theme contract in `docs/concepts/themes.md` will define glyph
groups, JSON grammar, fallback, validation, precedence, live replacement, and
programmatic authoring. `docs/concepts/styling.md` will link glyph resolution to
render-time appearance resolution.

Every affected control specification will name its semantic theme defaults,
local override properties, reset behavior, validation, and render invalidation.
XML documentation will describe the same ownership and exceptions.

The theme showcase will display the same representative controls under a
Unicode-oriented palette and a contrasting terminal-safe palette. Existing
control pages continue to demonstrate local overrides where useful. Showcase
screen and interaction tests prove that theme switching updates existing
controls without reconstructing the tree.

## Test strategy

Implementation follows visible red-green cycles. Loader tests are written and
observed failing before parser or palette production changes. Each control
migration begins with an exact-cell test that supplies a distinctive theme glyph
and proves the current literal is still rendered.

The completed evidence includes:

- legacy version 1 files with no `glyphs` data;
- partial and complete glyph documents;
- frozen programmatic themes and immutable published sequences;
- unknown keys, duplicates, wrong JSON kinds, malformed Unicode, control,
  zero-cell, wide, and multi-scalar values;
- incorrect progress sequence length or endpoint disagreement;
- detached baseline rendering;
- exact themed cells for every audited control glyph family;
- same-value local assignments establishing overrides;
- local override precedence and focused reset behavior;
- live theme replacement repainting an existing mounted tree;
- ambiguous-width repair preserving one-cell output;
- tiny bounds and clipped rendering for migrated controls; and
- a mounted showcase screen containing the contrasting glyph palette.

Snapshots supplement semantic cell assertions and never replace them. The
unfriended consumer contract proves any new public palette/group constructors,
properties, setters, and reset operations from packed NuGet packages.

## Delivery order

1. Add failing theme palette, JSON compatibility, validation, and freeze tests.
2. Implement the typed immutable glyph model, baseline resource, loader merge,
   and public programmatic surface.
3. Add failing shared chrome tests, then migrate borders, shadows, repair, and
   window chrome.
4. Add failing control tests and migrate progress, disclosure, selection,
   navigation, scrollbars, separators, tabs, and truncation in focused batches.
5. Prove local override, reset, detached baseline, and live replacement paths.
6. Update built-in theme data, normative docs, XML documentation, and affected
   control specifications.
7. Update the theme showcase and representative screen tests.
8. Run focused suites, packed consumer tests, and then `make format`,
   `make lint`, `make build`, and `make test`.

## Completion criteria

The work is complete only when:

- every glyph selected by `src/SharpVision/Controls` resolves from a local
  override or `Theme.Glyphs`;
- no control renderer selects a literal special drawing glyph or repair glyph;
- old theme files remain loadable and malformed glyph data fails with bounded,
  path-specific diagnostics;
- every loaded theme exposes a complete immutable palette;
- local overrides and reset behavior are deterministic across live theme
  replacement;
- exact-cell tests cover every migrated glyph family and runtime repair;
- theme, styling, control, XML, and showcase documentation agree;
- unrelated worktree changes remain intact; and
- all repository quality gates pass with zero warnings and zero errors.
