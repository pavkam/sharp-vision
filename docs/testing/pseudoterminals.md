# Pseudoterminal testing

## Pseudoterminal testing

Unix tests spawn the showcase or a focused fixture under a pseudoterminal and
control window size, input bytes, output bytes, signals, closure, and timing.
Windows tests use the supported console/ConPTY facility in a Windows CI job.

## Scenarios

Verify startup queries, alternate screen/cursor/mode changes, resize delivery,
bracketed paste, focus, cell/pixel mouse where the host supports it, output
batching, shutdown restoration, child exit, transport disconnect, and signal/
cancellation paths. tmux and GNU screen smoke tests run only when installed and
must report an explicit skip reason otherwise.

Tests use deterministic deadlines and condition-based waits, never arbitrary
sleep as proof. Raw transcripts redact clipboard/credential payloads and are
attached on failure.

## Separation

Pseudoterminal tests prove OS integration and lifecycle, not every parser edge.
Unit and in-memory integration suites remain the exhaustive sources. Platform
tests cannot silently pass when no tests or required fixture executed.
