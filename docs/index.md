# SharpVision specifications

SharpVision documentation is normative. A behavior is complete only when its
specification, implementation, tests, XML documentation, and showcase example
agree.

The [protocol index](protocols/index.md#protocol-families) defines terminal wire
behavior and the [coverage matrix](protocols/coverage-matrix.md#coverage) states
what the current code actually proves. The
[runtime routing contract](protocols/runtime-routing.md#runtime-routing-contract)
defines how parsed input, terminal replies, and bounded extension strings reach
their owners. The [architecture map](architecture/index.md#architecture-map)
defines ownership and runtime flow, while the
[concept map](concepts/index.md#concept-map) defines shared UI and terminal
behavior. The [control catalog](controls/index.md#control-catalog) defines
public widgets, and the [test map](testing/index.md#test-map) defines acceptable
correctness evidence. The
[single-content authoring contract](controls/content-control.md#contentcontrol-contract)
defines the public zero-or-one ownership role. The
[retained-component authoring contract](controls/composite-control.md#compositecontrol-contract)
defines constructor-time private composition without public child leakage. The
[project structure contract](architecture/project-structure.md#project-structure-contract)
defines the product boundary while the detailed specifications are built out.
The
[rendering pipeline contract](architecture/rendering-pipeline.md#rendering-pipeline-contract)
defines Unicode drawing and visual overflow, while the
[FigletText contract](controls/display/figlet-text.md#figlettext-contract)
defines large-text rendering and the audited compressed font library. The
[showcase contract](architecture/showcase.md#showcase-contract) defines the
runnable documentation gallery, executable interaction evidence, and
external-resource boundary.

## Documentation contract

- Protocol documents define grammar, limits, detection, fallback, security, and
  test obligations.
- Architecture documents define ownership and cross-layer flow.
- Concept documents define shared UI and terminal behavior.
- Control documents define one public control API each.
- Testing documents define acceptable correctness evidence.

All cross-document references must target the section that owns the rule.
