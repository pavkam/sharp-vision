# SharpVision specifications

SharpVision documentation is normative. A behavior is complete only when its
specification, implementation, tests, XML documentation, and showcase example
agree.

The [protocol index](protocols/index.md#protocol-families) defines terminal wire
behavior and the [coverage matrix](protocols/coverage-matrix.md#coverage) states
what the current code actually proves. The
[architecture map](architecture/index.md#architecture-map) defines ownership and
runtime flow, while the [concept map](concepts/index.md#concept-map) defines
shared UI and terminal behavior. The
[control catalog](controls/index.md#control-catalog) defines public widgets, and
the [test map](testing/index.md#test-map) defines acceptable correctness
evidence. The
[approved foundation design](superpowers/specs/2026-07-11-sharpvision-foundation-design.md#1-purpose)
defines the product boundary while the detailed specifications are built out.
The
[Canvas, border, and shadow design](superpowers/specs/2026-07-11-canvas-borders-shadows-design.md#purpose)
defines Unicode drawing and visual overflow, while the
[FIGlet engine and catalog design](superpowers/specs/2026-07-11-figlet-catalog-design.md#purpose)
defines large-text rendering and the audited compressed font library.

## Documentation contract

- Protocol documents define grammar, limits, detection, fallback, security, and
  test obligations.
- Architecture documents define ownership and cross-layer flow.
- Concept documents define shared UI and terminal behavior.
- Control documents define one public control API each.
- Testing documents define acceptable correctness evidence.

All cross-document references must target the section that owns the rule.
