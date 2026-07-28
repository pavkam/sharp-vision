# Intrinsic chrome

## Intrinsic-chrome contract

Border and shadow are intrinsic `Control` appearance, not wrapper controls.
Every control has one semantic `Border` and one semantic `Shadow`. Themes and
control styles own ordinary chrome; raw authoring is protected so an unrelated
descendant cannot silently change layout. The sealed control pipeline paints
shared chrome around `OnRenderContent`.

Use an ordinary `Container` only when chrome needs its own layout, styling,
ownership, routed ancestry, focus, or lifetime node. There are no border or
shadow control types.

## Control API

| Member                                       | Visibility | Contract                                                               |
| -------------------------------------------- | ---------- | ---------------------------------------------------------------------- |
| `Border`, `ResetBorder()`                    | Protected  | Complete derived-control border authoring and reset.                   |
| `Shadow`, `ResetShadow()`                    | Protected  | Complete derived-control shadow authoring and reset.                   |
| `SetAppearance(VisualState, AppearanceSet?)` | Protected  | Derived-control partial state contribution.                            |
| `ActualBorder`, `ActualShadow`               | Public     | Always-present current resolved values for inspection and composition. |

`Dock`, `Grid`, `Stack`, `Overlay`, `Window`, and `Popup` intentionally
republish complete Border/Shadow authoring because their public purpose includes
caller-defined chrome. Specialized controls such as Button publish a complete
Style instead. Third-party controls may likewise republish only the chrome their
layout contract supports.

Assignments are dispatcher-affine after attachment. Border-side changes affect
measure because each enabled side reserves one cell. Border glyphs, colors,
attributes, and every shadow member affect rendering but not desired size.

## Border API

`Border` is a complete immutable value; `BorderSet` is a partial member-wise
contribution used by themes and states.

| Member       | `Border` type      | `BorderSet` type    | Contract                                                         |
| ------------ | ------------------ | ------------------- | ---------------------------------------------------------------- |
| `Sides`      | `BorderSide`       | `BorderSide?`       | Enabled one-cell physical edges; unknown flag bits are rejected. |
| `GlyphStyle` | `BorderGlyphStyle` | `BorderGlyphStyle?` | Eight validated single-cell runes for corners and edges.         |
| `Foreground` | `ColorValue`       | `ColorValue?`       | Paint color; transparent is rejected.                            |
| `Background` | `ColorValue`       | `ColorValue?`       | Independent border-cell background channel.                      |
| `Attributes` | `AttributeValue`   | `AttributeValue?`   | Terminal attributes or semantic decoration.                      |

`BorderGlyphStyle` provides `Light`, `Heavy`, `Paired`, `Rounded`, `Ascii`,
`Solid`, `HalfBlock`, `LightShade`, `MediumShade`, and `DarkShade` families.
Caller-created families validate every rune as printable and exactly one cell
wide. Partial edges reserve and draw only their selected physical sides.

## Shadow API

`Shadow` is complete; `ShadowSet` overlays only supplied members.

| Member       | `Shadow` type    | `ShadowSet` type  | Contract                                                                           |
| ------------ | ---------------- | ----------------- | ---------------------------------------------------------------------------------- |
| `IsVisible`  | `bool`           | `bool?`           | Enables intrinsic shadow rendering.                                                |
| `Mode`       | `ShadowMode`     | `ShadowMode?`     | Selects destination composition; undefined values are rejected.                    |
| `Offset`     | `Point`          | `Point?`          | Signed X columns and Y depth units; fractional mode interprets Y in half rows.     |
| `Glyph`      | `Rune`           | `Rune?`           | Single-cell replacement used by `BlockGlyph`; zero selects the code-owned default. |
| `Foreground` | `ColorValue`     | `ColorValue?`     | Non-transparent shadow foreground.                                                 |
| `Background` | `ColorValue`     | `ColorValue?`     | Shadow background.                                                                 |
| `Attributes` | `AttributeValue` | `AttributeValue?` | Shadow attributes or semantic decoration.                                          |

| Mode              | Cell behavior                                                      | Geometry                                 |
| ----------------- | ------------------------------------------------------------------ | ---------------------------------------- |
| `Composite`       | Preserves destination graphemes and replaces their resolved style. | X and Y offsets are whole cells.         |
| `BlockGlyph`      | Replaces footprint cells with `Glyph`.                             | X and Y offsets are whole cells.         |
| `FractionalBlock` | Uses code-owned `▄`, `▀`, and `█` cells; `Glyph` is ignored.       | X is whole columns; Y is half-row steps. |

Shadow overflow is clipped by the owning presentation boundary and never
replaces cells belonging to the control's own frame. Unsupported or clipped
footprints degrade to the visible intersection without emitting terminal bytes
from control code.

## Resolution and rendering

Appearance resolves in the order defined by [Styling](styling.md#visual-states).
For chrome specifically:

1. Resolve the semantic role's complete normal border and shadow.
2. Overlay active theme-state `BorderSet` and `ShadowSet` contributions.
3. Apply the complete local control Style and any derived-control chrome.
4. Overlay protected derived-control state contributions.
5. Compute border inset and shadow visual overflow.
6. Paint shadow and body, call `OnRenderContent`, render normal children, then
   overlay the border.

Border background is independent from face background. Changing a hovered face
does not recolor border-cell backgrounds unless the corresponding `BorderSet`
also supplies `Background`.

## Example

```csharp
var card = new Stack
{
    Border = new Border(
        BorderSide.All,
        BorderGlyphStyle.Rounded,
        ThemeColor.ControlBorder,
        Color.Transparent,
        ThemeDecoration.Border),
    Shadow = new Shadow(
        isVisible: true,
        ShadowMode.Composite,
        new Point(1, 1),
        default,
        ThemeColor.ControlShadow,
        Color.Transparent,
        ThemeDecoration.Shadow)
};
```

On an approved chrome host, assigning either complete value makes it local and
authoritative. Call `ResetBorder()` or `ResetShadow()` to resume semantic-theme
ownership.

## Test obligations

| Layer       | Required evidence                                                                                                 |
| ----------- | ----------------------------------------------------------------------------------------------------------------- |
| Unit        | Constructor validation, complete/partial overlay order, reset behavior, invalidation impact, and resolved values. |
| Surface     | Exact border edges, corners, body backdrop, clipping, shadow modes, signed offsets, and Unicode cell ownership.   |
| Integration | Theme publication and visual-state changes through a mounted control without private render calls.                |

- Cover no-border/no-shadow, border-only, shadow-only, and combined chrome.
- Cover every individual border side and tiny rectangles where corners compete.
- Cover positive and negative shadow offsets at every clipping edge.
- Cover complete local values winning over themes and partial state sets winning
  member by member.
- Cover wide and combining destination graphemes under composite shadows.
