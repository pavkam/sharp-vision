# Chrome and Depth

## Visual hierarchy

Use chrome to explain structure:

- background separates a surface;
- border defines a boundary;
- title names a bounded region;
- shadow indicates elevation;
- accent indicates priority, selection, focus, or state.

When everything is emphasized, nothing is. Prefer one dominant surface, quiet
supporting regions, and transient elevation only where it communicates layering.

## Intrinsic chrome

Every `Control` owns `Border`, `BorderGlyphs`, `HasShadow`, shadow mode/offset,
background, and related colours. There are no Border or Shadow wrapper controls.
Use a container only when it contributes distinct layout, styling, ownership,
routed ancestry, or lifetime.

Borders consume layout cells; shadows do not. A shadow expands visual drawing
outside the arranged border box and must not be used to calculate alignment.

Give a component one default boundary signal: border, contrasting background, or
shadow. Layout containers, disclosure rows, and selection marks need none;
navigation/list/status owners use one continuous background; buttons and edit
fields use a light border; range controls use their track; titled groups use
their border; all Windows use a paired border by default; and detached windows
may opt into shadow depth.

## Borders

Choose glyphs by hierarchy and environment:

| Glyph family     | Character | Good use                                |
| ---------------- | --------- | --------------------------------------- |
| `Glyphs.Light`   | Quiet     | Fields, subtle groups, secondary panels |
| `Glyphs.Rounded` | Friendly  | Dialogs, cards, approachable grouping   |
| `Glyphs.Paired`  | Strong    | Primary Window frame                    |
| `Glyphs.Heavy`   | Emphatic  | Rare high-priority region               |
| `Glyphs.Ascii`   | Portable  | Restricted terminal environments        |

One enclosing border is usually enough. Avoid bordering each nested layout
panel; reserve inner borders for independently meaningful groups.

## Shadows

Use shadows for elevated surfaces such as Windows, Popups, and a primary action
when the theme calls for it. Prefer `Composite` for subtle theme-resolved
restyling and block glyph mode for a deliberate retro style. The resolved shadow
must remain distinguishable from the application background; black on black is
empty geometry, not depth.

Use `new Button(ButtonKind.Filled)` for a compact borderless primary action with
two horizontal padding cells, centered vertical alignment, and a code-owned
`▄`/`▀`/`█` fractional shadow. Its `(1,1)` offset means one column right and one
half row down. The centered default preserves the intrinsic one-row face beside
taller controls; set Stretch explicitly only when the filled face should grow.
Do not recreate that silhouette with adjacent Text controls or literal showcase
colors.

Buttons are flat by default. Opt a genuinely primary action into depth only when
the surrounding hierarchy benefits:

```csharp
var save = new Button
{
    Content = new Text("Save") { TextAlignment = Alignment.Center },
    HasShadow = true,
    ShadowOffset = new Point(1, 1),
    ShadowAttributes = Attributes.Dim,
    IsDefault = true
};
```

Do not place shadows on every button, field, and group. Shadows consume visual
space even though they do not reserve layout space, so dense shadowing creates
collisions and noise.

## Color and semantic colors

Prefer semantic `Color` values over literal colors:

- `Background` for the application plane;
- `Surface` for ordinary panels and controls;
- `WindowBackground` for Window bodies;
- `Border` and `Shadow` for chrome;
- `Accent` for active priority;
- `Muted` for secondary information;
- `Error`, `Warning`, `Success`, and `Info` for status meaning.

Semantic roles survive theme changes. Literal colours are appropriate only when
the product meaning depends on an exact colour and contrast remains verified.

A null background is transparent and preserves existing cells; `Color.Default`
is an opaque terminal default, not transparency.
