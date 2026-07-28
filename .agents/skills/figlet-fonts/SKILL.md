---
name: figlet-fonts
description:
  Use when changing SharpVision FIGfont parsing, FIGlet fitting or smushing,
  FigletFont, FigletCatalog, FigletText, embedded .flf/.tlf resources, font
  provenance, the 400-font ZIP, or its audit and packaging scripts.
---

# FIGlet Fonts

## Overview

Preserve byte provenance, bounded parsing, reference-compatible rendering, and
lazy compressed catalog access. The upstream collection contains malformed and
legacy fonts; compatibility fixes must remain deterministic and tested rather
than broadening the parser without evidence.

## Workflow

1. Read `docs/controls/display/figlet-text.md`, `extern/figlet/README.md`, and
   the nearest tests under `tests/SharpVision.Tests/Fonts/`.
2. Write a focused failing test before changing the parser, renderer, catalog,
   control, manifest, or archive.
3. Keep parser input, glyph count, height, row width, comments, nested archive,
   and rendered output bounded by `FigletLimits`.
4. Compare renderer changes with the official `figlet` executable using the
   exact font bytes and exact whitespace, including trailing spaces.
5. Run every catalog entry through `FigletCatalog.Load`; a representative font
   is not proof that the 400-font collection remains usable.
6. Rebuild the manifest and ZIP only from the pinned upstream commit. Build the
   ZIP twice and require byte-identical output.
7. Update the manifest, provenance README, API docs, tests, and showcase page in
   the same change.

## Compatibility invariants

- Preserve original archive bytes and verify SHA-256 before parsing.
- Keep catalog names case-sensitive and ordinally sorted. Resolve basename
  collisions deterministically without dropping either entry.
- Unwrap only a bounded single-entry nested ZIP; never extract catalog data to
  disk at runtime.
- Accept strict UTF-8 first and use lossless Latin-1 only for undeclared legacy
  byte fonts.
- Normalize CRLF and CR explicitly. Do not use Unicode-wide line-ending
  replacement because legacy byte values may map to NEL.
- Consume negative code tags as extension records without publishing them as
  Unicode scalars.
- Treat the header maximum row width as advisory while enforcing the explicit
  configured limit. Some upstream fonts exceed their declared value.
- Permit per-glyph and malformed per-row endmarks by removing each row's final
  marker; retain final-row repeated-marker handling.
- Safely degrade invalid optional direction, full-layout, and baseline metadata
  while rejecting invalid required structure and finite limits.
- Process input as Unicode scalar values and retain the question-mark fallback.
- Keep hardblanks until composition completes, then replace them with spaces.

## License and provenance invariants

The upstream repository has no collection-wide license. The checked-in audit
currently classifies most entries as `attribution-only` or
`upstream-unverified`; those values are release blockers, not permission grants.
Never relabel, omit, or strip notices to make the audit green.

Use:

```bash
node scripts/audit-figlet-fonts.mjs --source "$FIGLET_FONT_SOURCE" \
  --commit 417429ef36ab039cbf192a4424c60aa23fc32de8 \
  --output src/SharpVision/Fonts/Resources/fonts.manifest.json --check
node scripts/package-figlet-fonts.mjs --source "$FIGLET_FONT_SOURCE" \
  --output /tmp/figlet-fonts.zip
```

## Code invariants

- Keep one named type per exact-name file.
- Prefer readonly structs for small immutable options and metadata; use classes
  for fonts, catalog state, and owned glyph data.
- Never use primary or positional constructors. Validate explicitly before
  assignment and document exceptions.
- Use purposeful regions in substantial files and `Debug.Assert` for internal
  impossible states.
- Keep FIGlet parsing and composition out of terminal escape-sequence code.
  `FigletText` renders only through the semantic Canvas.

## Verification

```bash
node --test scripts/audit-figlet-fonts.test.mjs
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*Figlet*Tests" --timeout 60s
make lint
make build
make test
```

## Common mistakes

- Assuming GitHub availability grants redistribution permission.
- Decoding every font as UTF-8 with replacement characters.
- Applying Unicode-wide newline normalization to legacy bytes.
- Testing only `Standard.flf`.
- Extracting the complete archive for one lookup.
- Comparing FIGlet output after trimming significant whitespace.
