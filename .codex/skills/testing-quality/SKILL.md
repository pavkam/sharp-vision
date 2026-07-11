---
name: testing-quality
description: Use when designing, generating, reviewing, diagnosing, or running SharpVision unit, randomized, parser-fragmentation, Unicode, rendering, control, integration, pseudoterminal, allocation, performance, snapshot, or showcase tests.
---

# Testing Quality

## Overview

Prove observable terminal and UI behavior through independent oracles and real
cross-layer paths. A mock-heavy green suite is not evidence of correctness.

## Workflow

1. Read the relevant product spec and the matching file under `docs/testing/`.
2. Write one focused failing test named
   `MethodName_WhenThis_ThatIsExpected`; confirm the failure is caused by the
   missing behavior.
3. Use xUnit v3 and Shouldly with explicit Arrange/Act/Assert blocks. Prefer
   real pure collaborators and deterministic fakes.
4. Implement minimally, rerun focused tests, then add boundary, recovery,
   randomized, and integration proof required by the domain spec.
5. Record random seeds and shrink failures. Promote every discovered failure to
   a permanent regression.
6. Run the full gates and update testing docs when the oracle, fixture, platform,
   or performance contract changes.

## Proof ladder

- Unit tests cover pure protocol, geometry, layout, state, and diff logic.
- Exhaustive fragmentation tests prove whole-buffer behavior equals every
  possible read partition.
- Randomized/property tests use independent invariants and reproducible seeds.
- Integration tests drive raw input bytes through parser, dispatcher, controls,
  layout, rendering, and final output bytes.
- Platform tests use Unix pseudoterminals and Windows console facilities where
  the environment supports them.
- Allocation and throughput checks cover hot loops with versioned budgets.
- Snapshots supplement semantic assertions; they are never the only oracle.

## Invariants

- Test public behavior: exact bytes, cells, styles, events, ordering, exceptions,
  cursor state, focus, and terminal restoration.
- Use Moq only for genuine interaction boundaries. Prefer fakes for transports,
  clocks, dispatchers, terminals, and frame sinks.
- Never add production shortcuts solely for tests or assert private call graphs.
- Cover invalid arguments, malformed input, zero/tiny sizes, cancellation,
  resize, disabled state, and cleanup.
- Test discovery is part of the gate; use `--minimum-expected-tests` so zero
  discovered tests cannot pass.
- Keep one named type per file, including generated files, tests, and test
  helpers; name the file exactly after the type, and never declare nested named
  types.
- Make immutable value types readonly in production and test code. Retain
  mutable structs only for intrinsically stateful cursors, accumulators, or
  interop buffers.
- Prefer readonly structs for small immutable wrappers with valid defaults and
  cheap copies; preserve classes when tests rely on real identity, ownership,
  lifetime, or polymorphic behavior.
- Never use primary or positional constructors in production or test code.
  Define constructors explicitly and test every argument-validation boundary.
- Use named regions when a substantial test fixture has distinct setup, model,
  interaction, and assertion responsibilities; do not region tiny fixtures.

## Example review

For clipboard support, require exact encoder bytes, all parser split points,
malformed recovery, transaction ordering, randomized binary round trips, and a
writer-to-transport-to-parser integration test. A helper call assertion does not
prove the feature.

## Verification

```bash
dotnet test --solution SharpVision.slnx --configuration Release --no-build --minimum-expected-tests 3 --timeout 60s
npm run test:docs
make lint
make build
make test
```

## Common mistakes

- Writing tests after implementation and never observing a meaningful failure.
- Mocking the parser/control/render pipeline instead of exercising it.
- Using snapshots without semantic or model-based assertions.
- Hiding flakes with retries or accepting randomized failures without seeds.
