# Spacing and Sizing

## Box model

SharpVision uses `margin → border → padding → content`.

| Tool            | Meaning                                               | Use                                          |
| --------------- | ----------------------------------------------------- | -------------------------------------------- |
| `Margin`        | Space outside a control's border box                  | Separate one control from neighboring layout |
| Panel `Spacing` | Uniform gaps between participating children or tracks | Establish rhythm across a group              |
| `Border`        | Selected edges inside the border box                  | Communicate a real boundary                  |
| `Padding`       | Space between border and content                      | Keep content from touching chrome            |

Prefer panel spacing for uniform groups. Use margin for an exceptional outer
separation. Avoid stacking spacing, margins, borders, and padding at every
level; terminal cells are scarce and nested insets compound quickly.

## Length choice

| Length       | Choose when                                    | Typical use                                               |
| ------------ | ---------------------------------------------- | --------------------------------------------------------- |
| `Auto`       | Content determines the intrinsic extent        | Labels, compact buttons, status text, dialog rows         |
| `Cells(n)`   | The extent is semantically fixed               | One-row bars, known icon columns, minimum tool regions    |
| `Percent(p)` | A surface should track its containing viewport | Dialog or pane width, bounded by Min/Max                  |
| `Star(w)`    | A track should absorb remaining space          | Editor, text field, main content, flexible status message |

For a responsive surface, combine percentage or Star sizing with cell limits:

```csharp
Width = Length.Percent(75),
MinWidth = 48,
MaxWidth = 72
```

The percentage responds; the limits preserve usability and prevent an
overstretched dialog.

## Alignment

Automatic controls are content-sized unless stretched. Use alignment to place a
control inside space already allocated by its parent; do not use alignment to
repair the wrong track structure.

- Stretch flexible fields and content panes.
- Center text inside buttons, not the whole form row.
- Align related actions to one shared trailing Grid column.
- Keep labels consistent, usually vertically centered beside single-line input.
- Let long supporting text wrap in a finite Star column and Auto row.

## Responsive priorities

Decide shrinking behavior before choosing numbers:

1. Preserve essential commands and the active field.
2. Let flexible content shrink to a documented minimum.
3. Wrap or clip supporting text according to its meaning.
4. Collapse optional regions when the interaction model supports it.
5. Allow a bounded surface to scroll only when reflow cannot preserve access.

Fixed widths are appropriate for semantic constraints, not visual alignment. If
two fields must have equal width, place them in the same Grid column.

## Compact terminal rhythm

Start with one-cell gaps and one-cell content padding. Increase only when the
surface has enough room and the grouping benefits. Blank rows can clarify
sections, but repeated empty spacer controls usually indicate missing Grid rows
or panel spacing.
