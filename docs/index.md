# SharpVision specifications

SharpVision documentation is normative. A behavior is complete only when its
specification, implementation, tests, XML documentation, and showcase example
agree.

The [protocol index](protocols/index.md#protocol-families) defines terminal wire
behavior and the [coverage matrix](protocols/coverage-matrix.md#coverage) states
what the current code actually proves. The
[approved foundation design](superpowers/specs/2026-07-11-sharpvision-foundation-design.md#1-purpose)
defines the product boundary while the detailed specifications are built out.

## Documentation contract

- Protocol documents define grammar, limits, detection, fallback, security, and
  test obligations.
- Architecture documents define ownership and cross-layer flow.
- Concept documents define shared UI and terminal behavior.
- Control documents define one public control API each.
- Testing documents define acceptable correctness evidence.

All cross-document references must target the section that owns the rule.
