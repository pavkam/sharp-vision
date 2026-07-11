# Correctness model

## Correctness model

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
