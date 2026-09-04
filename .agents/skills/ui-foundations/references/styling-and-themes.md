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
- [Style types](../../../../docs/concepts/themes.md#style-types)
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

Six well-known `ControlStyle` siblings — `ControlStyle`, `InputStyle`,
`ContainerStyle`, `WindowStyle`, `PopupStyle`, `TooltipStyle` — own the theme's
closed `styles.*` sections; every leaf control style declares no section of its
own and instead resolves through a declared one-hop fallback to one of the six,
via `StyleDefinitions.Control<TStyle, TFallback>`. A well-known type's section
key is derived from its type name, and that derivation never applies to a leaf.
See [Style types](../../../../docs/concepts/themes.md#style-types) and
[Where a section name comes from](../../../../docs/concepts/themes.md#where-a-section-name-comes-from)
for the full resolution mechanics.

## Workflow

1. Pick the well-known base type whose appearance the control should inherit,
   then declare a record deriving from it with only the extra members the
   control needs.
2. Resolve through `StyleDefinitions.Control<TStyle, TFallback>`, declaring the
   one-hop fallback. Keep complete immutable values separate from the partial
   per-state contributions the fallback's own theme section authors.
3. Return the earliest genuinely affected phase from `compare` — structural
   members are `Measure`, color-only changes are `Render`.
4. Test local value, theme value, state precedence, ambient inheritance,
   dispatcher affinity, reset, and exact invalidation.
5. Update every bundled theme and the schema together when changing a role
   section's authored members.
6. Verify representative states on a mounted surface, not just the resolved
   style.

## Current API model

A control opts into themed styling by declaring `IStyled<TStyle>` and forwarding
`Style`/`ActualStyle` itself over a private `StyleSlot<TStyle>` field returned
by `ControlBase.InitializeStyle<TStyle>(definition, changed)`, the same way
regardless of the control's actual base type. There is no virtual
`OnStyleChanged` to override — pass a private method as `InitializeStyle`'s
optional `changed` callback instead. See
[Styling](../../../../docs/concepts/styling.md#overview) and
[Visual states](../../../../docs/concepts/styling.md#visual-states) for the full
API surface, including `ActualStyle` and `ResolveAppearance`; see
[Intrinsic chrome](../../../../docs/concepts/intrinsic-chrome.md#overview) for
`EnableChromeAuthoring()` and raw `Border`/`Shadow` authoring, which live on
`ControlBase` itself, not on the styling API.

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
- **`Theme.Unthemed` is internal.** `DocumentStyle.Default` and
  `CodeViewStyle.Default` complete their static presets against a bare
  `new Theme()` rather than `Theme.Unthemed`. `Complete` never actually reads
  the theme it is given for these styles, so any valid instance resolves
  identically; do not "fix" this by switching either style to `Theme.Unthemed`.
- **Passive styles ignore interactive states.** Only `"input"` inherits
  `"control"`'s per-state deltas. Containers, windows, popups, and tooltips
  deliberately do not answer hover or focus — cascading into them tints every
  panel and window border. This is asserted; do not "fix" it.
- **Cascade deltas, not whole values.** Carrying a whole per-state value across
  replaces a style's own `Border.Sides`/`GlyphStyle` and moves measured widths
  by whole cells.
- **Measure and arrange must reserve the same affix columns.** `ArrangeChrome`
  deflates `ContentBounds` by `MeasureAffixes(StartAffix, EndAffix, gap)` before
  laying out a control's inner viewport, but `MeasureOverride` has to fold that
  identical reservation into its own returned `Size` on every measured path,
  including a `WordWrap`-style reflow branch that computes width independently
  of the plain path. Skip it on one path and an auto-sized control measures as
  if the affix were free, so arrange later deflates an already-too-narrow
  content box down toward zero, starving the viewport inside it.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ControlBaseTests" --minimum-expected-tests 1 --timeout 60s
```

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ControlStyleTests" --minimum-expected-tests 1 --timeout 60s
```
