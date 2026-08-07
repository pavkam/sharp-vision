# Styling and Themes

## Load this reference when

Changing Theme, a control's style type, AppearanceStates, visual states, Face,
Border, Shadow, control Style or ActualStyle, ambient inheritance, theme JSON,
or bundled themes.

## Normative documentation

- [Styling](../../../../docs/concepts/styling.md#overview)
- [Visual states](../../../../docs/concepts/styling.md#visual-states)
- [Intrinsic chrome](../../../../docs/concepts/intrinsic-chrome.md#overview)
- [Themes](../../../../docs/concepts/themes.md#overview)
- [Theming controls](../../../../docs/concepts/theming-new-controls.md#overview)
- [Invalidation impact](../../../../docs/concepts/invalidation.md#choosing-an-impact)

## Code map

- Theme values, style types, schemas, catalog: `src/SharpVision/Styling/`
- Control style ownership: `src/SharpVision/Controls/`
- Bundled theme resources: `src/SharpVision/Styling/Themes/`
- Tests: styling/theme/control appearance tests under `tests/SharpVision.Tests/`
- Showcase: `examples/Showcase/Panes/StylingPane.cs`

## Naming scheme

Three layers, three words. The prefix says which layer a type belongs to; the
suffix says what kind of thing it is. Follow it for anything new — the
vocabulary was collapsed onto this deliberately, and a type that does not fit
one of these buckets probably belongs in a bucket that already exists.

| Word         | Means                                                     |
| ------------ | --------------------------------------------------------- |
| `Theme`      | The document and the catalog — authored, loaded.          |
| `Style`      | Typed authored intent for a control type; carries tokens. |
| `Appearance` | The concrete visual result; resolves to literals.         |
| `Semantic*`  | A token enum a theme maps to concrete values.             |
| `Control*`   | A value as authored on a control.                         |
| `*Overlay`   | A partial delta — only the members it names win.          |
| `*States`    | The per-visual-state collection.                          |

Retired as type vocabulary, and not to be reintroduced: **`Role`** anywhere,
**`Profile`**, **`Definition`** for a JSON DTO (it now means only a
`StyleDefinition`'s resolution policy), and **`Set`** meaning partial.

## The model

Every themeable value is an immutable record deriving from `ControlStyle`. There
is no fixed, closed set of them and no enum naming one — a control declares its
own style type, and that type declares how it resolves.

- `StyleDefinitions.Control<TStyle>(codeOwnedDefault, compare)` — a
  self-contained root. Only the six well-known base types use this:
  `ControlStyle` and its `InputStyle`, `ContainerStyle`, `WindowStyle`,
  `PopupStyle`, `TooltipStyle` siblings.
- `StyleDefinitions.Control<TStyle, TFallback>(fallbackTo, complete, compare)` —
  every leaf control style, with one declared one-hop fallback.
- `StyleDefinitions.Part<TStyle>(fallback, compare)` — a secondary style that
  does not own its control's appearance states.

A style's `styles.*` theme key is **derived from its type name** by `StyleKey`:
drop a trailing `Style`, drop a leading `Theme`, lower-case the first character.
`ButtonStyle` owns `"button"`, `ScrollBarStyle` owns `"scrollBar"`,
`ControlStyle` owns `"control"`. Never hand-write a library style's key, and
never maintain a separate list of section names — the registry that validates
theme documents is built by reflecting over the style types themselves. The
explicit-key overloads exist only for third-party sections, which must be
`vendor.control` namespaced (a dot cannot appear in a type name), and for test
probes using synthetic `test.*` keys.

## Workflow

1. Pick the well-known base type whose appearance the control should inherit,
   then declare a record deriving from it with only the extra members the
   control needs.
2. Resolve through `StyleDefinitions.Control<TStyle, TFallback>` with the
   derived key. Keep complete immutable values separate from the partial
   per-state contributions a theme authors.
3. Return the earliest genuinely affected phase from `compare` — structural
   members are `Measure`, color-only changes are `Render`.
4. Test local value, theme value, state precedence, ambient inheritance,
   dispatcher affinity, reset, and exact invalidation.
5. Update every bundled theme and the schema together when adding a section a
   theme is expected to author.
6. Verify representative states on a mounted surface, not just the resolved
   style.

## Current API model

- Specialized controls expose nullable local `Style` and always-present
  `ActualStyle`; `ButtonStyle.Filled` is the filled button presentation.
- Raw `Border` and `Shadow` authoring is protected on `Control`.
- Dock, Grid, Stack, and Overlay widen it via `ChromeAuthoringContainer`; Window
  and Popup via `ChromeAuthoringFloatingSurface`. Do not re-publish those
  members on individual controls.
- Use `BorderGlyphStyle` and `SemanticColor` tokens such as `ControlBorder` and
  `ControlShadow`; retired `Glyphs` and `ButtonKind` APIs do not exist.
- `ActualFace`/`ActualBorder`/`ActualShadow` are the **resolved** render-ready
  appearance and carry concrete colors. A style's own `Face` still carries
  semantic tokens. Never assert one against the other — compare each in its own
  representation, or resolve explicitly with `theme.ResolveColor(...)`.

## Traps that have already caused regressions

- **Two different `Default`s.** A glyph family's `Default` (e.g.
  `CheckBoxGlyphs.Default`, the one-cell Square family) is not the style's
  `Default` preset (`CheckBoxStyle.Default`, the three-cell Brackets
  presentation). Substituting one for the other renders a box inside brackets.
  If a preset is written `Complete(...) with { Glyphs = ... }`, that `with` is a
  sign `Complete` is producing the wrong family.
- **Collection members break record equality.** `ImmutableArray<T>` compares by
  wrapped-array reference, so a style holding one needs hand-written
  `Equals`/`GetHashCode` over content. The symptom is an assertion failing while
  printing byte-identical expected and actual values.
- **Static initializer order.** A `static` member that reads other statics of
  the same class must be declared _after_ them; initializers run in textual
  order and a too-early declaration silently captures zeroed structs.
- **Passive styles ignore interactive states.** Only `"input"` inherits
  `"control"`'s per-state deltas. Containers, windows, popups, and tooltips
  deliberately do not answer hover or focus — cascading into them tints every
  panel and window border. This is asserted; do not "fix" it.
- **Cascade deltas, not whole values.** Carrying a whole per-state value across
  replaces a style's own `Border.Sides`/`GlyphStyle` and moves measured widths
  by whole cells.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ControlCompositeAppearanceTests" --minimum-expected-tests 1 --timeout 60s
```

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ControlStyleTests" --minimum-expected-tests 1 --timeout 60s
```
