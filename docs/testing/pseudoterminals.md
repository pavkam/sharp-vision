# Pseudoterminal testing

## Pseudoterminal testing

Unix tests open an owned raw master/slave pseudoterminal pair and control window
size, input bytes, output bytes, signals, closure, and timing. The current
fixture drives `StreamTransport`, `UnixResizeSource`, and `Runtime.Session`
directly. Windows tests use the supported console/ConPTY facility in a Windows
CI job.

## Scenarios

Verify startup queries, alternate screen/cursor/mode changes, resize delivery,
bracketed paste, focus, cell/pixel mouse where the host supports it, output
batching, shutdown restoration, child exit, transport disconnect, and signal/
cancellation paths. Multiplexer framing and capability narrowing are verified
separately through exact-byte and deterministic capability tests.

Capability-negotiation proof requires the exact bounded query batch, ordinary
input delivery while replies are pending, one finite shared deadline, profile
publication before the first resize, capability-gated mode activation, and
reverse cleanup after closure or cancellation.

Tests use deterministic deadlines and condition-based waits, never arbitrary
sleep as proof. Raw transcripts redact clipboard/credential payloads and are
attached on failure.

The macOS fixture initializes cell/pixel dimensions through fixed-signature
`openpty`, changes cells through the platform utility, then sends a real
SIGWINCH. Linux uses the corresponding PTY and ioctl path. Unsupported platforms
use xUnit's runtime skip with an explicit reason.

## Separation

Pseudoterminal tests prove OS integration and lifecycle, not every parser edge.
Unit and in-memory integration suites remain the exhaustive sources. Platform
tests cannot silently pass when no tests or required fixture executed.
