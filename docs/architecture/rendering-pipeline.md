# Rendering pipeline

## Rendering pipeline contract

Controls produce semantic cells; the terminal layer owns byte emission.

```mermaid
flowchart LR
    Tree["Arranged control tree"] --> Canvas["Clipped cell canvas"]
    Canvas --> Back["Back frame"]
    Front["Committed front frame"] --> Damage["Semantic damage scan"]
    Back --> Damage
    Damage --> Runs["Merged grapheme-safe runs"]
    Runs --> Encoder["Cursor and style encoder"]
    Encoder --> Transport["Bounded asynchronous transport"]
    Transport --> Commit["Commit or invalidate"]
    Commit --> Front
```

## Cell and frame rules

Cell equality includes grapheme identity, width/continuation ownership, colors,
attributes, hyperlinks, and renderer-visible metadata. Damage expands to every
cell owned by an affected grapheme, then merges adjacent ranges.

`Frame` owns a pooled row-major cell array and a finite pooled UTF-8 grapheme
arena. Public callers observe only `CellInfo` and copy a complete grapheme into
their own span; pooled memory never escapes. `Canvas.Draw` segments and measures
with the frame's explicit ambiguous-width policy, preflights the complete arena
cost, then mutates in a second pass. A failed capacity check therefore leaves
the frame unchanged.

Wide leads own exactly one continuation in the current implementation.
Overwriting or clearing either cell first repairs the complete previous owner.
`Edge.Clip`, `Edge.Wrap`, and `Edge.Replace` skip, move, or replace the whole
cluster at the right edge; none emits or stores half a glyph. Child canvases use
the geometric intersection of their requested clip, parent clip, and frame.

The encoder minimizes cursor moves and style transitions only after correctness
is known. When synchronized output is available, one complete frame is wrapped
according to the
[mode 2026 contract](../protocols/synchronized-output.md#synchronized-output-contract).

## Commit and invalidation

A complete successful write commits the back frame as the new front state. A
partial/interrupted write, resize, capability change, alternate-screen
transition, clear, or out-of-band output marks terminal state unknown and forces
the next frame to redraw completely.

## Correctness oracle

Tests apply incremental bytes for frame B to a virtual terminal initialized by
frame A and compare the final screen, cursor, style, hyperlink, and mode state
with a clean full render of B. Random frame pairs and targeted wide-cell
transitions use this same oracle.
