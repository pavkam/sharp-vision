# Correctness model

## Overview

Tests are written with xUnit v3 and Shouldly, follow an explicit
Arrange/Act/Assert layout, and use names of the form
`MethodName_WhenThis_ThatIsExpected`. Every new behavior starts with a focused
test that has been observed failing for the intended reason.

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
`SharpVision.Terminal` and `SharpVision` with PublicApiGenerator and compares
them against Verify snapshots. Each assembly has one approved baseline stored in
a directory named after the shared `OverallVersion`. A changed public signature
therefore fails against the current version's baseline, while a version change
produces a new missing baseline instead of overwriting the historical surface.

An intentional public API change requires both actions:

1. Update `OverallVersion` in `Directory.Build.props`.
2. Run the compatibility tests, inspect both `.received.txt` files, and approve
   them as `.verified.txt` files in the new version directory.

Commit the version bump and both approved snapshots together, and keep the older
version directories as API history. Never accept a received file without
reviewing its signature changes: generated snapshots are approval artifacts, not
disposable test output.

## Shape and reflection

`SharpVision.Compatibility.Tests` is the shape oracle for both public surfaces;
see [Public API compatibility](#public-api-compatibility) above. A hand-written
test asserting that a member exists, is absent, or has a given accessibility
duplicates that snapshot while covering less: the snapshot compares the complete
surface on every run, not one asserted member.

Reflecting into production state — `BindingFlags`, `GetField`, `GetMethod`,
`GetProperty`, `Activator`, or walking a private call graph — has the same
defect from the other direction. It reaches state a consumer cannot reach, so it
proves nothing about the public contract, and it turns a rename error from a
compile-time broken reference into a run-time `null` reflection result or a
silently stale assertion.

When a test genuinely needs state a type does not expose, add a documented
`internal` member instead. Both test assemblies are already friend assemblies,
so the member is directly readable without reflection. An `internal` seam is not
production surface: `PublicApiGenerator` excludes it, it appears in neither
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
| Public API change    | Reviewed versioned snapshots for both production assemblies.         |

No snapshot, mocked interaction, green build, or smoke test substitutes for a
missing proof layer.
