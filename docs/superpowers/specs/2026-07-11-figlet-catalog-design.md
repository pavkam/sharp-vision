# FIGlet Engine and Font Catalog Design

**Status:** Approved design

**Date:** 2026-07-11

## Purpose

SharpVision includes a bounded FIGfont engine and a compressed, reproducible
catalog of 400 audited FIGlet and TOIlet fonts. The implementation adapts the
useful ideas from Sharpie while conforming to SharpVision's validation,
ownership, file-layout, Unicode, and documentation rules.

## Engine

`FigletFont` parses FIGfont version 2 files from streams or UTF-8 memory. It
validates the signature, dimensions, hardblank, comment count, layout modes,
print direction, code-tagged glyphs, line termination, glyph row counts, and
finite resource limits before publishing a usable font.

Rendering supports left-to-right and right-to-left fonts, hardblanks, full
width, horizontal fitting, horizontal smushing, vertical fitting, and vertical
smushing. Input is processed as Unicode scalar values. Missing glyphs use the
font's fallback glyph when present and otherwise render a documented
replacement.

`FigletOptions` is an immutable value that controls direction and layout
overrides. `FigletLimits` bounds input bytes, glyph count, glyph height, row
width, comments, and rendered output. Public arguments are validated before
observable state changes.

## Catalog and packaging

The catalog is stored as one deterministic ZIP embedded in `SharpVision`.
Entries are sorted, use fixed timestamps, and are deflated without extracting
files to disk at runtime. The font source payload is approximately 4.48 MB and
the audited archive is approximately 1.39 MB.

`FigletCatalog` indexes entry metadata once, opens only the selected entry, and
uses bounded decompression. `Names` is deterministic and ordinally sorted.
Callers may load a named font or inspect its immutable `FigletFontInfo` audit
record.

The checked-in manifest records every entry's catalog name, source path,
SHA-256, source repository and commit, format, declared author/copyright text,
license classification, and attribution text. The audit command fails when an
archive entry is missing from the manifest, hashes differ, names collide, or a
license classification is absent.

The 400-font target is an acceptance requirement, not permission to redistribute
unknown work. A font whose redistribution terms cannot be established is a
release blocker and remains visible in the audit report until resolved; it is
never silently relabeled or omitted.

## UI integration

`FigletText` is a display control with `Content`, `Font`, and `Options`
properties. It measures and renders generated output through the semantic
terminal canvas, preserving clipping and style inheritance. The showcase font
page permits catalog selection and displays representative text beside its audit
metadata.

The existing `RichText` specification is implemented for showcase descriptions
using typed inline runs, explicit line breaks, and hyperlinks rather than raw
ANSI text.

## Correctness evidence

Parser tests cover valid headers, code-tagged Unicode glyphs, every layout mode,
right-to-left rendering, hardblanks, malformed terminators, truncation,
overflow, and configured limits. Catalog tests verify all 400 entries parse,
manifest hashes match, archive construction is reproducible, lookup is
case-sensitive and deterministic, and malformed names cannot escape the archive.

Reference tests compare representative and randomized output with the official
FIGlet implementation wherever the format and layout mode are supported by both
engines. Control and showcase tests compare exact cells after resize and
clipping.

## Sources

- [FIGlet and FIGfont specification](https://www.figlet.org/)
- [Official FIGlet repository](https://github.com/cmatsuoka/figlet)
- [Sharpie font implementation](https://github.com/pavkam/sharpie/tree/main/Sharpie/Font)
- [400-font source collection](https://github.com/xero/figlet-fonts)
