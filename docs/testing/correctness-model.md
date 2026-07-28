# Correctness model

## Correctness-model contract

Tests use xUnit v3, Shouldly, explicit Arrange/Act/Assert, and names of the form
`MethodName_WhenThis_ThatIsExpected`. Every new behavior starts with a focused
test observed failing for the intended reason.

## Proof levels

1. Pure unit tests prove validation, state transitions, algorithms, and exact
   values.
2. Boundary/exhaustive tests prove fragmentation, geometry edges, and invalid
   recovery.
3. Seeded randomized tests compare independent models and invariants.
4. Integration tests exercise real layers from input bytes to output bytes.
5. Platform tests exercise pseudoterminal/console behavior.
6. Performance tests protect allocation, throughput, bytes, and retained memory.

Higher levels do not excuse missing focused tests; focused mocks do not replace
integration.

The track allocator uses seed `0x4A70` for 20,000 bounded definition sets. Each
case is resolved twice and proves stable output, exact allocation when an
uncapped star can absorb the remainder, non-negative extents, and feasible
min/max compliance. A separate warmed caller-owned-span loop proves the core
allocator performs no managed allocation.

The mutable UI infrastructure uses seed `0x51A47001` for 2,000 attach/detach,
length, edge, visibility, enabled, resize, focus, capture, pointer, layout, and
render operations. Every case reports seed, case, operation, and size while
checking one-parent ownership, dispatcher consistency, containment, valid
manager targets, and complete wide-cell lead/continuation ownership.

## Test doubles

Use Moq only for a genuine external interaction whose call contract is the
observable behavior. Prefer deterministic fakes for transport, terminal,
dispatcher, clock, waiter, frame sink, and capability queries. Do not mock the
parser/control/layout/render pipeline.

## Required observations

Assert exact bytes, typed events, cell contents/styles, bounds, cursor/mode
state, focus/capture, event order, exceptions, diagnostics/redaction, cleanup,
and allocation as applicable. A snapshot supplements these assertions and is
never the only oracle.

## Discovery gate

Root test commands use `--minimum-expected-tests`; a zero-discovery exit cannot
be green. Tests do not retry flakes. A flaky failure is diagnosed and fixed or
the gate remains red.

## Public API compatibility

`SharpVision.Compatibility.Tests` generates the complete public surfaces of
`SharpVision.Terminal` and `SharpVision` with PublicApiGenerator and compares
them with Verify snapshots. Each assembly has one approved baseline under a
directory named after the shared `OverallVersion`. A changed public signature
therefore fails the current-version baseline, while a version change produces a
new missing baseline instead of overwriting the historical surface.

An intentional public API change requires both actions:

1. Update `OverallVersion` in `Directory.Build.props`.
2. Run the compatibility tests, inspect both `.received.txt` files, and approve
   them as `.verified.txt` files in the new version directory.

Commit the version and both approved snapshots together. Preserve older version
directories as API history. Never accept a received file without reviewing its
signature changes; generated snapshots are approval artifacts, not disposable
test output.

## Required evidence

| Claim                | Minimum proof                                                        |
| -------------------- | -------------------------------------------------------------------- |
| Pure value behavior  | Focused deterministic unit test over the public value.               |
| Stateful UI behavior | Unit state proof plus mounted surface observation.                   |
| Protocol behavior    | Exact bytes, fragmentation, malformed recovery, and typed routing.   |
| Rendering behavior   | Semantic cells plus incremental/full equivalence and terminal bytes. |
| Public API change    | Reviewed versioned snapshots for both production assemblies.         |

No snapshot, mock interaction, green build, or smoke test substitutes for a
missing proof layer.
