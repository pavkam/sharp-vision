# Correctness model

## Overview

Tests are written with xUnit v3 and Shouldly, follow an explicit
Arrange/Act/Assert layout, and use names of the form
`MethodName_WhenThis_ThatIsExpected`. Every new behavior starts with a focused
test that has been observed failing for the intended reason.

Test classes are named `<Type>Tests` for the type they exercise, declared under
`src/` or `examples/`. The `SurfaceTests`, `PerformanceTests`, `ConsumerTests`,
and `CompatibilityTests` suffixes identify an evidence tier — mounted-terminal
rendered-cell proof, an allocation-perf gate, a packed-package consumer proof,
and a public-contract/ABI freeze, respectively — not a different subject; a
class covering multiple subjects at once belongs on the suite-level allow-list
defined inside
[`scripts/validate-test-class-naming.mjs`](../../scripts/validate-test-class-naming.mjs)
instead of inventing a new suffix. Classes that predate the rule are tracked in
a baseline that may only shrink, so a rename never has to happen all at once,
but a new test class must comply immediately. Some of these grandfathered
classes still split one type's coverage across several `<Type><Aspect>Tests`
classes, as named in [controls integration](controls-integration.md#overview);
that split is baseline-tracked pending per-area consolidation, not a pattern to
copy. `npm run lint:test-names` enforces the rule and reports both new
violations and baseline entries that no longer reproduce.

## Proof levels

1. Pure unit tests prove validation, state transitions, algorithms, and exact
   values.
2. Boundary and exhaustive tests prove fragmentation handling, geometry edges,
   and recovery from invalid input.
3. Seeded randomized tests compare the implementation against independent models
   and invariants.
4. Integration tests exercise the real layers, from input bytes to output bytes.
5. Platform tests exercise pseudoterminal and console behavior.
6. Performance tests protect allocation, throughput, output bytes, and retained
   memory.

A higher level never excuses a missing focused test, and a focused test built on
mocks never replaces integration coverage.

The track allocator uses seed `0x4A70` to generate 20,000 bounded definition
sets. Each case is resolved twice to prove the output is stable, that an
uncapped star track absorbs the remainder exactly, that no extent goes negative,
and that feasible min/max limits are honored. A separate warmed loop over a
caller-owned span proves the core allocator performs no managed allocation.

The mutable UI infrastructure uses seed `0x51A47001` to generate 2,000
operations spanning attach/detach, length, edge, visibility, enabled state,
resize, focus, capture, pointer, layout, and render. Every case reports its
seed, case number, operation, and size while checking that each control has
exactly one parent, dispatcher use stays consistent, children stay contained,
manager targets remain valid, and every wide cell keeps complete
lead/continuation ownership.

## Test doubles

Use Moq only when a genuine external interaction's call contract is itself the
observable behavior. Everywhere else, prefer deterministic fakes — for the
transport, terminal, dispatcher, clock, waiter, frame sink, and capability
queries. Never mock the parser, control, layout, or render pipeline.

## Required observations

Assert the outcomes a consumer can observe: exact bytes, typed events, cell
contents and styles, bounds, cursor and mode state, focus and capture, event
order, exceptions, diagnostics and redaction, cleanup, and allocation —
whichever apply. A snapshot may supplement these assertions, but it is never the
only oracle.

## Discovery gate

Root test commands pass `--minimum-expected-tests`, so a run that discovers zero
tests exits red rather than green. Tests never retry flakes: a flaky failure is
diagnosed and fixed, or the gate stays red.

## Public API compatibility

`SharpVision.Compatibility.Tests` generates the complete public surfaces of
`SharpVision.Terminal`, `SharpVision`, and `SharpVision.FigletFonts` with
PublicApiGenerator, strips every attribute-application line from the generated
text (attributes are metadata annotations, not binary-breaking surface, so
adding, removing, or editing one is never itself a reason to fail this gate),
and compares what remains against one Verify snapshot per assembly in
`Snapshots/*.verified.txt`. There is no per-version subfolder: the snapshot
always reflects the current, approved shape of the public API, not a frozen
baseline tied to a released version number.

A changed public signature fails this gate. That failure is the maintainer's own
signal to decide, from the diff, whether the change is significant enough to
warrant bumping that assembly's own version property
(`SharpVisionTerminalVersion`, `SharpVisionVersion`, or
`SharpVisionFigletFontsVersion`) in `Directory.Build.props` - the gate does not
make that decision automatically, and accepting a new baseline does not by
itself require a version bump. Each of the three libraries versions and
publishes independently, so a signature change in one assembly never requires
bumping the other two.

Reviewing and accepting a changed baseline requires:

1. Run the compatibility tests and inspect all three `.received.txt` files.
2. Confirm every difference is an intended addition, removal, signature, or
   visibility change - not an accidental one.
3. Overwrite the paired `.verified.txt` file with the `.received.txt` content to
   accept it.
4. If the change warrants one, update that assembly's own version property in
   the same change.

Never accept a received file without reviewing its signature changes: generated
snapshots are approval artifacts, not disposable test output.

## Shape and reflection

`SharpVision.Compatibility.Tests` is the shape oracle for all three public
surfaces; see [Public API compatibility](#public-api-compatibility) above. A
hand-written test asserting that a member exists, is absent, or has a given
accessibility duplicates that snapshot while covering less: the snapshot
compares the complete surface on every run, not one asserted member.

Reflecting into production state — `BindingFlags`, `GetField`, `GetMethod`,
`GetProperty`, `Activator`, or walking a private call graph — has the same
defect from the other direction. It reaches state a consumer cannot reach, so it
proves nothing about the public contract, and it turns a rename error from a
compile-time broken reference into a run-time `null` reflection result or a
silently stale assertion.

When a test genuinely needs state a type does not expose, add a documented
`internal` member instead. Both test assemblies are already friend assemblies,
so the member is directly readable without reflection. An `internal` seam is not
production surface: `PublicApiGenerator` excludes it, it appears in no
`.verified.txt` snapshot, and no consumer can observe it. Document on the member
which invariant it exists to prove — `KeySequenceMatcher.RetainsStorage`,
`Frame.CurrentMutationRevision`, and `Session.Backend` are established uses of
this pattern.

A shape assertion is allowed only inside a test that exercises the behavior the
shape protects, and only for a fact the snapshot cannot express, such as
`internal` or `private protected` accessibility on a seam like the ones above. A
standalone `Type_WhenInspected_*` test that only walks members is not allowed.

## Required evidence

| Claim                | Minimum proof                                                        |
| -------------------- | -------------------------------------------------------------------- |
| Pure value behavior  | Focused deterministic unit test over the public value.               |
| Stateful UI behavior | Unit state proof plus mounted surface observation.                   |
| Protocol behavior    | Exact bytes, fragmentation, malformed recovery, and typed routing.   |
| Rendering behavior   | Semantic cells plus incremental/full equivalence and terminal bytes. |
| Public API change    | Reviewed versioned snapshots for all three production assemblies.    |

No snapshot, mocked interaction, green build, or smoke test substitutes for a
missing proof layer.
