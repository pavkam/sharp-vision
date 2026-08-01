# Rendering equivalence testing

## Overview

Given a committed frame A and a target frame B, the production `Encoder` emits
an incremental update from A to B. `VirtualScreen`, an independent test terminal
model, applies a full render of A followed by that update, while a second model
applies a clean full render of B. Both models must equal B — and each other — in
grapheme text, lead/continuation ownership, style, hyperlink, cursor position,
and cursor visibility.

The model parses the emitted ECMA-48 bytes and implements only terminal
semantics, including colon-form underline variants, underline color, overline,
slow and rapid blink, and resets; it never calls `Damage` or `Encoder`.
Exact-byte tests remain separate so that two implementations cannot agree on the
same unnecessary or malformed sequence.

Description-driven cases compile deliberately non-canonical but semantically
equivalent `cup`, rendition, indexed/direct/default color, cursor-visibility,
and cursor-shape programs, and the same independent model proves the final
cells, style, cursor position, and visibility. Targeted cases cover `am`/`xenl`
final-column repair, wide owners, explicit spaces without `bce`, and safe
trailing `el` with `bce`. Renderer tests additionally prove that uppercase
terminfo variables persist across committed frames, reset across profiles, and
roll back after late expansion or transport failure.

## Damage proof

`Damage.Enumerate` is tested for no-op, sparse and adjacent runs, style-only
changes, deletion, narrow-to-wide, wide-to-narrow, and changed wide graphemes.
Every run is half-open, row-major, and expanded to complete ownership in both
frames. Dimension changes and explicit invalidation return every target row.

Canvas primitive tests also paint a non-default surface, then draw lines, single
glyphs, and transparent style overlays over it. Foreground and attributes may
change, but the destination background must remain identical — this catches
isolated table dividers, borders, shadows, and indicator glyphs that
accidentally fall back to the terminal default.

Foreground-transformation tests require row-major selector callbacks at absolute
lead-cell coordinates, exactly once per complete stored owner. They cover stored
spaces versus untouched blank cells, preservation of every non-foreground
semantic field, clipped wide owners receiving no callback, and equal lead and
continuation styles. Callback-failure tests prove the exception identity is
unchanged, a valid transformed prefix exists, the failing owner is untouched,
and wide-cell ownership links stay intact.

Write-scoped foreground tests prepaint a rich underlay inside the effect region
and require that only callback-mutated owners change. They cover identical
overwrites, written spaces, untouched stored owners and blanks, mixed narrow and
wide owners, a provenance-eligible wide owner transformed across a
requested-region boundary, full-owner exclusion when the effective canvas clip
cuts that owner, row-major selector order, nested inner and outer regions,
null-callback validation before any drawing or disposed-frame access, drawing
failure with no selector pass, selector failure with deterministic partial
progress, and selector-side writes to later or current owners being excluded by
the closed draw-revision window. A semantic no-op overwrite followed by an
identical selected foreground must produce no damage span, which proves mutation
revisions never enter semantic frame comparison. Normal capture also preserves
untouched cell revisions, preventing an accidental full-frame metadata rebase on
the rendering hot path.

Cell hashes may reject unequal graphemes quickly, but hash equality never proves
semantic equality: the complete UTF-8 bytes and renderer metadata are compared,
which keeps collision behavior correct.

## Semantic graphics proof

Placement tests cover stable immutable image identity, positive contained pixel
sources, canvas and frame clipping, contain/cover/stretch preservation, stable
paint order, finite capacity, and clone/copy/clear/disposal ownership. Weak
image references prove that clear and disposal release pooled references without
needing a production inspection API. Ordered damage independently changes
whenever image identity, source rectangle, destination movement or resize,
fitting mode, or z-order changes.

Placement provenance tests paint later cells through direct canvas writes,
ordinary windows, and the elevated popup layer. They require the intersected
placement to become ineffective while public equality and hashing stay
unchanged. Kitty tests require an exact removal for an occluded retained
placement. Shared non-retained tests require visible-to-occluded and
occluded-to-visible transitions to trigger complete cell repair.

A consumer-facing compatibility test constructs nonempty placements and observes
`default(Placement)` and `Placement.Empty` as equal valid sentinels with a null
image, identity zero, empty rectangles, and contain mode. Public boundary tests
reject null images, empty or out-of-image sources, invalid destinations and
modes, and disposed frame or canvas access. Fully clipped and empty canvas
destinations remain no-ops, and empty sentinels never enter active frame spans.

Renderer and backend tests record one shared output batch and require upload
before cell replacement, replacement placement next, and stale removal last.
Preparation and encoding failures must be byte-quiet. Write and flush failures
must leave the prior semantic front uncommitted, invalidate backend state, and
make the next operation a complete repair. A renderer without a backend emits
exactly the same cell bytes as the corresponding text-only frame and still
commits byte-quiet placement-only changes. Size and ambiguous-width transitions
with otherwise equal placements must upload and place the complete graphics
again, then commit an exact front whose next render is unchanged. Backend
cleanup that throws must preserve the original exception while releasing local
front, batch, and writer-gate ownership and leaving disposal idempotent.
Backend-specific cases additionally cancel an in-flight output batch, fail both
synchronized frame and cleanup writes or flushes, switch profiles, explicitly
invalidate, and contend render against disposal during preparation and transport
output. They assert full-repair flags, commit/invalidate counts, cleanup
diagnostics, and the upload-before-placement-before-removal ordering.

Real Kitty backend tests additionally require every successful prepare to
commit, including unchanged and cell-only frames. A partially written new image
or placement reserves its IDs, the next frame emits exact hard-image or
uncovered soft-placement tombstones before any reuse, and repeated failure
cannot release those IDs early. Immediate shutdown after a failed batch deletes
committed and uncertain image state, suppresses redundant soft deletes already
covered by hard deletes, flushes once, and releases IDs without allocating
during the cleanup commit.

## Randomized transitions

Fixed seed `0xD1FF` generates 128 frame pairs containing ASCII, CJK, combining
clusters, emoji ZWJ sequences, spaces, RGB colors, attributes, hyperlinks, typed
underline variants and colors, overline, slow and rapid blink, edge policies,
and cursor states. Every pair runs through the full/incremental oracle. A
failure reports the seed and case before it becomes a named regression.

## Required evidence

| Layer          | Required observation                                                          |
| -------------- | ----------------------------------------------------------------------------- |
| Semantic frame | Grapheme ownership, style, hyperlink, cursor, image placement, and damage.    |
| Equivalence    | Incremental transition and clean full render end in identical terminal state. |
| Transport      | Ordered bytes, commit after write/flush, and complete next-frame repair.      |
| Graphics       | Upload, placement, removal, revocation, tombstones, and cleanup order.        |
