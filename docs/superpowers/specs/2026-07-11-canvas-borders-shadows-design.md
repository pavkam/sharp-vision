# Canvas, Borders, and Shadows Design

**Status:** Approved design

**Date:** 2026-07-11

## Purpose

SharpVision provides Unicode drawing primitives on the low-level semantic
`SharpVision.Terminal.Rendering.Canvas`. Higher-level controls consume those
primitives without emitting terminal escape sequences or duplicating cell
ownership rules.

## Canvas primitives

The canvas exposes validated operations for drawing a Rune, filling a region,
drawing horizontal and vertical lines, drawing boxes, merging quadrant blocks,
and applying a style without replacing the existing grapheme. Every operation
uses absolute cell coordinates and honors the canvas clip.

Line drawing uses immutable `LineStyle` values composed from `LineWeight`,
`LinePattern`, and rounded-corner intent. Light, heavy, double, rounded, and
dashed Unicode forms are supported. ASCII is an explicit fallback style. When
two line segments share a cell, their topology is merged. Resolution is
deterministic and independent of draw order. If Unicode has no exact glyph for a
mixed topology, the resolver selects the least destructive solid glyph using the
documented weight precedence `double`, `heavy`, then `light`.

Quadrant drawing merges the four quarter-cell bits into the corresponding Block
Elements Rune. Shade fills support light, medium, dark, and solid block forms.
All produced Runes are printable one-cell values under SharpVision's Unicode
width policy.

Style application preserves the complete stored grapheme. If a target touches
one cell of a wide grapheme, the complete owner is restyled when it is inside
the effective clip; otherwise the operation skips that owner. A drawing
primitive never leaves lead and continuation cells with inconsistent styles.

## Border presets

`Glyphs` retains its validated custom constructor and adds named presets for
light, heavy, double, rounded, ASCII, solid block, and light, medium, and dark
shade borders. `Glyphs.Default` remains the light preset for compatibility.

`Border` keeps independent physical edge thicknesses and continues to support
partial borders. It uses canvas Rune/fill operations so clipping and Unicode
validation have one implementation.

## Shadows

`Shadow` is a traditional capacity-one decorator. Its child participates in
normal measure, arrange, input, and focus behavior. The shadow is visual
overflow: it does not reserve layout space and is never hit-testable.

`ShadowMode.Composite` preserves glyphs in the shadow footprint and replaces
their semantic style. `ShadowMode.BlockGlyph` draws a validated one-cell Rune,
with block and shade Runes available naturally through the property. Both modes
default to the Turbo Vision offset of two columns and one row.

The shadow footprint is the shifted child rectangle minus the unshifted child
rectangle. Positive and negative offsets are supported. Rendering is clipped by
ancestors and the frame, participates in normal stable z-order, and is
recomputed from current bounds after every arrange or terminal resize.

Controls render their own visual overflow through a `VisualBounds` contract,
while descendants remain clipped according to the existing `ClipsChildren`
policy. This permits shadows without weakening input containment or ordinary
child clipping.

## Correctness evidence

Tests exhaust every uniform line topology, verify commutative merges, check safe
mixed-weight degradation, cover quadrant combinations, prove clipping and
wide-grapheme repair, and compare exact semantic cells. Shadow tests cover both
modes, positive and negative offsets, overlap, z-order, ancestor clipping, tiny
bounds, wide graphemes, and resize-driven rearrangement.

The showcase contains one page that displays every border family, drawing
primitive, and both shadow modes against patterned content so compositing is
visible.

## Sources

- [Unicode Box Drawing](https://www.unicode.org/charts/PDF/U2500.pdf)
- [Unicode Block Elements](https://www.unicode.org/charts/PDF/U2580.pdf)
- [Turbo Vision default shadow geometry](https://github.com/magiblot/tvision/blob/57b6f56b38e0ee75240a80a10ee0e11470c24693/source/tvision/tview.cpp#L35)
- [Turbo Vision shadow compositing](https://github.com/magiblot/tvision/blob/57b6f56b38e0ee75240a80a10ee0e11470c24693/source/tvision/tvwrite.cpp#L62-L84)
