# Pseudoterminal testing

## Overview

Unix tests open a raw master/slave pseudoterminal pair that they own outright,
which gives them control over window size, input bytes, output bytes, signals,
closure, and timing. The current fixture drives `StreamTransport`,
`UnixResizeSource`, and `Runtime.Session` directly. Windows tests use the
supported console/ConPTY facility in a Windows CI job.

## Scenarios

The tests verify startup queries, alternate-screen/cursor/mode changes, resize
delivery, bracketed paste, focus reporting, cell and pixel mouse where the
host supports it, output batching, shutdown restoration, child exit, transport
disconnect, and the signal and cancellation paths. Multiplexer framing and
capability narrowing are verified separately, through exact-byte and
deterministic capability tests.

Capability-negotiation proof requires the exact bounded query batch, ordinary
input delivery while replies are still pending, one finite shared deadline,
profile publication before the first resize, capability-gated mode activation,
and reverse-order cleanup after closure or cancellation.

Tests use deterministic deadlines and condition-based waits; an arbitrary
sleep is never accepted as proof. Raw transcripts redact clipboard and
credential payloads and are attached to failures.

The macOS fixture initializes cell and pixel dimensions through the
fixed-signature `openpty` call, changes the cell size through the platform
utility, and then sends a real SIGWINCH. Linux uses the corresponding PTY and
ioctl path. On unsupported platforms the tests use xUnit's runtime skip with
an explicit reason.

## Separation

Pseudoterminal tests prove OS integration and lifecycle behavior; they are not
the place to prove every parser edge. The unit and in-memory integration
suites remain the exhaustive sources. Platform tests cannot silently pass when
no test — or no required fixture — actually executed.

## Required evidence

| Boundary      | Required observation                                                                 |
| ------------- | ------------------------------------------------------------------------------------ |
| Platform mode | Raw/VT entry, saved state, and restoration after success, cancellation, and failure. |
| Transport     | Fragmented bytes, EOF/disconnect, serialization, and flush ordering.                 |
| Resize        | Real signal/ioctl delivery with cells and pixels where supported.                    |
| Session       | Query/mode ordering, input dispatch, output, and reverse cleanup.                    |

Transcripts redact sensitive payloads, and deterministic condition waits
replace arbitrary sleeps.
