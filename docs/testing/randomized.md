# Randomized testing

## Randomized testing

Randomized/property-style tests use explicit reproducible seeds and independent
oracles. Failures print the seed and serialized minimal case; shrinking may be
custom but must preserve the violated invariant. Every discovered failure is
added as a named permanent regression.

## Domains

- Protocols: bytes, fragmentation partitions, malformed strings, transaction
  order, and recovery.
- Unicode: valid/invalid UTF-16, grapheme sequences, width policies, clipping,
  and wide-cell ownership.
- Frames: sizes, semantic cells, style transitions, damage density, and
  full-versus-incremental equivalence.
- Layout: tree shapes, lengths, constraints, margins/padding, spans, visibility,
  resize, and containment/rounding invariants.
- Input: tree mutations, route handling, capture/focus changes, and event order.

## Guardrails

Generators enforce only input-domain preconditions, not the property being
tested. Production functions never create expected values. Seeded quick cases
run in pull requests; larger fixed-time corpora run in extended/release gates.
No random test uses time-based seeding without printing and persisting it.

Phase 4 adds `0x51A47001` as the hostile mutable-tree seed. Its permanent corpus
mixes zero through 240×80 viewports with layout, visibility, focus, capture, and
render mutations; every failure includes the exact case and operation.

Grid layout uses seed `0x051A475A` for 10,000 independently reconstructed pairs.
Cases mix one through five tracks per axis, every length kind, min/max limits,
spans, saturated spacing, collapsed children, and zero/tiny final sizes. The
suite requires pairwise deterministic geometry, containment, non-negative
extents, stable ordered edges, and exact final-axis consumption through an
uncapped proportional track. Failures report the corpus seed, case index,
derived case seed, and viewport.
