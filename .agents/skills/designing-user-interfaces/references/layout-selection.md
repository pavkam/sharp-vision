# Layout Selection

Choose a panel from the relationship among children, not from the screenshot.

## Decision table

| Relationship                                         | Use       | Typical cases                                         | Avoid                                  |
| ---------------------------------------------------- | --------- | ----------------------------------------------------- | -------------------------------------- |
| Regions consume outer edges and one center fills     | `Dock`    | Menu/header, sidebar, inspector, status bar, editor   | Nested fixed coordinates               |
| Children share row and column edges                  | `Grid`    | Forms, property sheets, aligned commands, split views | Separate Stack rows with copied widths |
| Children form one ordered run                        | `Stack`   | Toolbar actions, navigation list, vertical sections   | Cross-row alignment requirements       |
| Children share the same rectangle at different depth | `Overlay` | Content plus badge, scrim, floating status            | Canvas offsets for pure layering       |
| Children have authored or draggable positions        | `Canvas`  | Movable Windows, diagrams, coordinate plots           | Forms, shells, responsive columns      |

## Dock

Use `Dock` for the application skeleton. Add edge consumers in outside-to-inside
order and add the fill child last. `LastChildFills = true` gives the remaining
rectangle to the final visible child. Collapsed regions consume no edge or gap,
so optional sidebars naturally return space to the center.

Use fixed cells for genuinely fixed chrome such as a one-row menu or status
area. Give resizable content regions a percentage, Star-capable nested layout,
or the final fill rectangle.

## Grid

Use one Grid whenever multiple rows must share edges. A form normally uses:

- Auto label column;
- Star field column with a usable minimum;
- Auto action columns;
- Auto rows for editors, messages, and actions;
- row and column spacing instead of per-child spacer controls.

Use spans for validation under a field, a title across columns, or a footer that
shares the leading region. Do not nest a horizontal Stack merely to simulate
columns already represented by the Grid.

## Stack

Use `Stack` when only order and consistent gaps matter. It is excellent for a
vertical group of sections or one toolbar run. Star children can absorb the
remaining stack axis, but Stack does not align unrelated rows into columns.

If you start copying widths between children or inserting invisible spacers, the
relationship is probably a Grid.

## Overlay

Use `Overlay` when children occupy the same content box and z-order matters. Set
`Overlay.ZIndex` only to express real depth. Keep pointer-transparent visual
layers `IsHitTestVisible = false` so decoration does not steal input.

Popup elevation is automatic; do not add arbitrary high z-index values to make a
Popup visible.

## Canvas

Use Canvas for explicit positions. Set `Left`, `Top`, `Right`, or `Bottom` in
cells or percentages and let normal child width/height resolve the slot. A
Canvas-hosted Window is clamped into the content bounds after resize.

Canvas is not a shortcut around responsive layout. If controls should align,
share remaining space, or reflow, choose Grid, Dock, or Stack.

## Nesting

Nest panels only when relationships genuinely change. A common composition is:

```text
Dock application shell
├── top Menu
├── bottom StatusBar
└── fill Grid workspace
    ├── Auto navigation column
    └── Star editor column
```

Each nesting level should answer a distinct geometry question.
