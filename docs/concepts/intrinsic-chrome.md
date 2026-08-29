# Intrinsic chrome

## Overview

Border and shadow are intrinsic `ControlBase` appearance, not wrapper controls.
Every control has one semantic `Border` and one semantic `Shadow`. Ordinary
chrome is owned by themes and control styles; the raw authoring properties are
public on `ControlBase` but gated behind an opt-in capability, so an unrelated
descendant cannot silently change layout just because the member exists. The
sealed control pipeline paints this shared chrome around `OnRenderContent`.

Reach for an ordinary `Container` only when the chrome needs its own layout,
styling, ownership, routed ancestry, focus, or lifetime node. There are no
border or shadow control types.

## Control API

| Member                                           | Visibility | Description                                                                        |
| ------------------------------------------------ | ---------- | ---------------------------------------------------------------------------------- |
| `EnableChromeAuthoring()`                        | Protected  | Opts a control into `Border`/`Shadow` authoring; called once from its constructor. |
| `Border`, `ResetBorder()`                        | Public     | Complete derived-control border authoring and reset; throws until enabled.         |
| `Shadow`, `ResetShadow()`                        | Public     | Complete derived-control shadow authoring and reset; throws until enabled.         |
| `SetAppearance(VisualState, AppearanceOverlay?)` | Protected  | Derived-control partial state contribution.                                        |
| `ActualBorder`, `ActualShadow`                   | Public     | Always-present current resolved values for inspection and composition.             |

`Border` and `Shadow` are declared once on `ControlBase` and are public on every
control, but reading or writing either throws `InvalidOperationException` until
the owning control calls `EnableChromeAuthoring()`. `Dock`, `Grid`, `Stack`,
`Overlay`, `GroupBox`, `Window`, `Popup`, `TabControl`, `NavigationView`,
`NavigationViewGroup`, and `NavigationViewSeparator` call it from their
constructors because caller-defined chrome is part of their public purpose;
every other control leaves it disabled, so accessing `Border` or `Shadow` on,
say, a `TabItem` or `Expander` throws instead of silently doing nothing.
Specialized controls such as Button and TextInput publish a complete Style
instead. Third-party controls may likewise call `EnableChromeAuthoring()` when
their layout contract supports raw chrome authoring.

After a control is attached, these assignments must happen on the owning
dispatcher. Changing which border sides are enabled affects measure, because
each enabled side reserves one cell. Border glyphs, colors, attributes, and
every shadow member affect rendering only, not the desired size.

## Border API

`Border` is a complete immutable value; `BorderOverlay` is the partial
member-wise contribution used by themes and states.

| Member       | `Border` type       | `BorderOverlay` type | Description                                                        |
| ------------ | ------------------- | -------------------- | ------------------------------------------------------------------ |
| `Sides`      | `BorderSide`        | `BorderSide?`        | IsEnabled one-cell physical edges; unknown flag bits are rejected. |
| `GlyphStyle` | `BorderGlyphStyle`  | `BorderGlyphStyle?`  | Eight validated single-cell runes for corners and edges.           |
| `Foreground` | `ControlColor`      | `ControlColor?`      | Paint color for flat borders; transparent is rejected.             |
| `Relief`     | `BorderRelief`      | `BorderRelief?`      | Semantic `Flat`, `Raised`, or `Sunken` depth treatment.            |
| `Background` | `ControlColor`      | `ControlColor?`      | Independent border-cell background channel.                        |
| `Attributes` | `ControlDecoration` | `ControlDecoration?` | Terminal attributes or semantic decoration.                        |

`BorderGlyphStyle` provides the `Light`, `Heavy`, `Paired`, `Rounded`, `Ascii`,
`Solid`, `HalfBlock`, `LightShade`, `MediumShade`, and `DarkShade` families. A
caller-created family validates every rune as printable and exactly one cell
wide. Partial edges reserve and draw only the physical sides they select.

`BorderRelief.Flat` paints every enabled edge with `Border.Foreground`. `Raised`
paints the top and left edges with the active Theme's `ReliefHighlight` color
and the right and bottom edges with `ReliefShade`; `Sunken` reverses that
mapping. Horizontal edges own corner glyphs, so the top color owns both top
corners and the bottom color owns both bottom corners. The same precedence
applies to partial and one-row frames. Undefined relief values are rejected by
complete borders, overlays, `with` expressions, and Theme JSON.

## Shadow API

`Shadow` is a complete value; `ShadowOverlay` overlays only the members it
supplies.

| Member       | `Shadow` type       | `ShadowOverlay` type | Description                                                                        |
| ------------ | ------------------- | -------------------- | ---------------------------------------------------------------------------------- |
| `IsVisible`  | `bool`              | `bool?`              | Enables intrinsic shadow rendering.                                                |
| `Mode`       | `ShadowMode`        | `ShadowMode?`        | Selects destination composition; undefined values are rejected.                    |
| `Offset`     | `Point`             | `Point?`             | Signed X columns and Y depth units; fractional mode interprets Y in half rows.     |
| `Glyph`      | `Rune`              | `Rune?`              | Single-cell replacement used by `BlockGlyph`; zero selects the code-owned default. |
| `Foreground` | `ControlColor`      | `ControlColor?`      | Non-transparent shadow foreground.                                                 |
| `Background` | `ControlColor`      | `ControlColor?`      | Shadow background.                                                                 |
| `Attributes` | `ControlDecoration` | `ControlDecoration?` | Shadow attributes or semantic decoration.                                          |

| Mode              | Cell behavior                                                      | Geometry                                 |
| ----------------- | ------------------------------------------------------------------ | ---------------------------------------- |
| `Composite`       | Preserves destination graphemes and replaces their resolved style. | X and Y offsets are whole cells.         |
| `BlockGlyph`      | Replaces footprint cells with `Glyph`.                             | X and Y offsets are whole cells.         |
| `FractionalBlock` | Uses code-owned `▄`, `▀`, and `█` cells; `Glyph` is ignored.       | X is whole columns; Y is half-row steps. |

Shadow overflow is clipped by the owning presentation boundary and never
replaces cells that belong to the control's own frame. When a footprint is
unsupported or clipped, the shadow degrades to the visible intersection; control
code never emits terminal bytes.

## Resolution and rendering

Appearance resolves in the order defined by [Styling](styling.md#visual-states).
For chrome specifically:

1. Resolve the semantic style's complete normal border and shadow.
2. Overlay active theme-state `BorderOverlay` and `ShadowOverlay` contributions.
3. Apply the complete local control Style and any derived-control chrome.
4. Overlay protected derived-control state contributions.
5. Resolve semantic relief to the active Theme's highlight and shade colors.
6. Compute border inset and shadow visual overflow.
7. Paint shadow and body, call `OnRenderContent`, render normal children, then
   overlay the border.

The border background is independent from the face background. Changing a
hovered face does not recolor border-cell backgrounds unless the corresponding
`BorderOverlay` also supplies `Background`.

## Example

```csharp
var card = new Stack
{
    Border = new Border(
        BorderSide.All,
        BorderGlyphStyle.Rounded,
        SemanticColor.ControlBorder,
        BorderRelief.Sunken,
        Color.Transparent,
        SemanticDecoration.Border),
    Shadow = new Shadow(
        isVisible: true,
        ShadowMode.Composite,
        new Point(1, 1),
        default,
        SemanticColor.ControlShadow,
        Color.Transparent,
        SemanticDecoration.Shadow)
};
```

On a control with chrome authoring enabled, assigning either complete value
makes it local and authoritative. Call `ResetBorder()` or `ResetShadow()` to
return ownership to the semantic theme.

## Expected behavior

| Layer       | Observable evidence                                                                                               |
| ----------- | ----------------------------------------------------------------------------------------------------------------- |
| Unit        | Constructor validation, complete/partial overlay order, reset behavior, invalidation impact, and resolved values. |
| Surface     | Exact border edges, corners, body backdrop, clipping, shadow modes, signed offsets, and Unicode cell ownership.   |
| Integration | Theme publication and visual-state changes through a mounted control without private render calls.                |

- Chrome behaves consistently with no border and no shadow, border only, shadow
  only, and both combined.
- Each individual border side works on its own, and tiny rectangles where
  corners compete resolve without overlap errors.
- Positive and negative shadow offsets clip correctly at every edge.
- Complete local values win over themes, and partial state sets win member by
  member.
- Composite shadows handle wide and combining destination graphemes without
  breaking cell ownership.
