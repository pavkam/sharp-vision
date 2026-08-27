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

Every themeable value is an immutable record deriving from `ControlStyle`. A
theme's `styles` object is closed to exactly six top-level sections, one per
well-known base type — `ControlStyle` and its `InputStyle`, `ContainerStyle`,
`WindowStyle`, `PopupStyle`, `TooltipStyle` siblings. Nothing else is a
theme-visible key: not a leaf control's own name, not a namespaced vendor
section.

- `Theme.GetStyleSet<TStyle>(codeOwnedDefault)` — resolves one of the six
  well-known types against its own `styles.*` section.
- `StyleDefinitions.Control<TStyle, TFallback>(fallbackTo, complete, compare)` —
  every leaf control style's one factory, with one declared one-hop fallback.
  `fallbackTo` resolves one of the six well-known types' own per-state set (via
  `Theme.GetStyleSet`) or one of `Theme`'s four derived interaction sets;
  `complete` folds that fallback's contribution into this type's own shape. A
  leaf declares no `styles.*` section of its own at all — its only sources of
  appearance are this completion logic, the fallback's resolved states, and a
  locally assigned `Style`. `complete` itself is shaped
  `(TFallback, VisualState, Theme) -> TStyle`; the `Theme` parameter is how
  completion reaches theme-level values beyond the fallback's own resolved
  appearance — the glyph-aware styles (CheckBox, RadioButton, ScrollBar,
  Spinner, ProgressBar, ChaseIndicator) read `theme.Glyphs` from it to complete
  their own structural members.
- `StyleDefinitions.Part<TStyle>(fallback, compare)` — a secondary style that
  does not own its control's appearance states.

A well-known type's key is **derived from its type name** by the internal
`StyleKey`: drop a trailing `Style`, lower-case the first character — so
`ControlStyle` owns `"control"`, `WindowStyle` owns `"window"`. That derivation
only ever resolves for the six well-known roots; a leaf style's own derived key
(`ButtonStyle` would derive `"button"`) is never looked up against a theme
document, since `styles` admits only the six names regardless of what a leaf
type's key would compute to. There is no registry, no vendor namespace, and no
explicit-key overload for a leaf or third-party section.

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

- Specialized controls expose nullable local `Style` and always-present
  `ActualStyle`; `ButtonStyle.Filled` is the filled button presentation.
- A control gets a typed style by declaring `IStyled<TStyle>` (a plain marker
  interface, no default members) and forwarding `Style`/`ActualStyle` itself
  over a private `StyleSlot<TStyle>` field returned by
  `ControlBase.InitializeStyle<TStyle>(definition, changed)`. This works the
  same regardless of the control's actual base (`ControlBase`, `InputBase`,
  `CompositeControlBase`, `FloatingSurfaceBase`, or otherwise) - there is no
  generic `Control<TStyle>`/`Pressable<TStyle>`/`CompositeControl<TStyle>`/
  `FloatingSurface<TStyle>` layer to derive from. There is also no virtual
  `OnStyleChanged` to override; pass a private method as `InitializeStyle`'s
  optional `changed` callback instead.
- Raw `Border` and `Shadow` authoring is public on `ControlBase` but throws
  `InvalidOperationException` until a control calls the protected
  `EnableChromeAuthoring()`.
- Dock, Grid, Stack, Overlay, GroupBox, Window, Popup, TabControl,
  NavigationView, NavigationViewGroup, and NavigationViewSeparator call it from
  their own constructors. Do not add a `public new` hiding shim over `Border`
  or `Shadow` on individual controls - call `EnableChromeAuthoring()` instead.
- Use `BorderGlyphStyle` and `SemanticColor` tokens such as `ControlBorder` and
  `ControlShadow`; retired `Glyphs` and `ButtonKind` APIs do not exist.
- `ActualFace`/`ActualBorder`/`ActualShadow` are the **resolved** render-ready
  appearance and carry concrete colors. A style's own `Face` still carries
  semantic tokens. Never assert one against the other — compare each in its own
  representation, or resolve explicitly with `theme.ResolveColor(...)`.
- `ResolveAppearance(theme, visualState)` (public) previews the same resolved
  appearance for an explicit Theme and state without attachment, cache writes,
  or events. It models whole-tree inheritance — what `PropagateTheme` and a
  mounted Application publish — not the single-control internal `SetTheme`,
  which themes one control and reaches descendants only ambiently (pinned by
  ControlBaseTests.Appearance).

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
- **`Theme.Unthemed` is internal.** `SharpVision.Document` and
  `SharpVision.SyntaxHighlighting` do hold an `InternalsVisibleTo` grant onto
  `SharpVision`, but their style types stay independent of that assembly-
  boundary detail: `DocumentStyle.Default` and `CodeViewStyle.Default`
  complete their static presets against a bare `new Theme()` rather than
  `Theme.Unthemed`. `Complete` never actually reads the theme it is given for
  these styles, so any valid instance resolves identically; do not "fix" this
  by switching either style to `Theme.Unthemed`.
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
