# Styling and Themes

## Load this reference when

Changing Theme, ThemeRole, ThemeProfile, visual states, Face, Border, Shadow,
control Style or ActualStyle, ambient inheritance, theme JSON, or bundled
themes.

## Normative documentation

- [Styling](../../../../docs/concepts/styling.md#overview)
- [Visual states](../../../../docs/concepts/styling.md#visual-states)
- [Intrinsic chrome](../../../../docs/concepts/intrinsic-chrome.md#overview)
- [Themes](../../../../docs/concepts/themes.md#overview)
- [Theming controls](../../../../docs/concepts/theming-new-controls.md#overview)
- [Invalidation impact](../../../../docs/concepts/invalidation.md#choosing-an-impact)

## Code map

- Theme values, profiles, schemas, catalog: `src/SharpVision/Styling/`
- Control style ownership: `src/SharpVision/Controls/`
- Bundled theme resources: `src/SharpVision/Styling/Themes/`
- Tests: styling/theme/control appearance tests under `tests/SharpVision.Tests/`
- Showcase: `examples/Showcase/Panes/StylingPane.cs`

## Workflow

1. Choose an existing semantic role before adding theme vocabulary.
2. Separate complete immutable values from partial theme/state contribution
   sets.
3. Test local value, theme value, state precedence, ambient inheritance,
   dispatcher affinity, reset, and exact invalidation.
4. Update every bundled theme and schema together when adding a required role.
5. Verify representative states on a mounted surface.

## Current API model

- Specialized controls expose nullable local `Style` and always-present
  `ActualStyle`; `ButtonStyle.Filled` is the filled button presentation.
- Raw `Border` and `Shadow` authoring is protected on `Control`.
- Dock, Grid, Stack, Overlay, Window, and Popup intentionally republish complete
  chrome authoring.
- Use `BorderGlyphStyle` and `ThemeColor` roles such as `ControlBorder` and
  `ControlShadow`; retired `Glyphs` and `ButtonKind` APIs do not exist.

## Focused verification

```bash
dotnet test --project tests/SharpVision.Tests \
  --filter-class "*ControlCompositeAppearanceTests" \
  --minimum-expected-tests 1 --timeout 60s
```
