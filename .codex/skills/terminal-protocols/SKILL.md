---
name: terminal-protocols
description: Use when changing SharpVision ANSI, ECMA-48, CSI, OSC, DCS, DEC, xterm, Kitty, iTerm2, sixel, tmux, GNU screen, keyboard, mouse, paste, focus, clipboard, or terminal capability protocol behavior.
---

# Terminal Protocols

## Overview

Keep protocol behavior sourced, typed, bounded, byte-exact, and recoverable.
The normative docs own wire semantics; code and tests prove the declared
coverage state.

## Workflow

1. Read `docs/protocols/coverage-matrix.md`, the protocol file linked from its
   entry, `docs/architecture/capabilities.md`, and
   `docs/testing/terminal-protocols.md`.
2. Check the protocol file's primary source and version. Refresh the source
   before changing behavior when the extension can evolve.
3. Write exact-byte or typed-event tests first. For streaming input, repeat a
   representative sequence across every possible read split.
4. Implement typed commands/events over `Span<T>`, `ReadOnlySpan<T>`,
   `IBufferWriter<byte>`, or owned memory. Keep raw extensions at the boundary.
5. Bound numeric parameters, metadata, payloads, buffering, query timeouts, and
   concurrent transactions. Recover the outer parser after invalid input.
6. Update the protocol spec and coverage matrix in the same change. Never mark
   a feature implemented until typed behavior and tests exist.

## Invariants

- Parse arbitrary transport fragmentation and multiple frames per read.
- Encode culture-independent ASCII grammar and UTF-8 payloads deterministically.
- Preserve unknown valid sequences as diagnostics; never silently reinterpret
  malformed data as another command.
- Degrade unsupported environmental features safely. Strict mode may promote
  diagnostics but must not alter valid encoding.
- Redact clipboard contents, credentials, and untrusted payloads from logs.
- Keep controls protocol-free; terminal bytes belong in
  `SharpVision.Terminal`.
- Treat SharpVision as the terminal application side. Do not invent emulator
  permission policy inside the client library.
- Keep one named type per file, including generated files, name the file exactly
  after the type, and never declare nested named types.
- Make immutable value types readonly. Leave a struct mutable only when its role
  intrinsically advances or accumulates state, and keep that mutability narrow.

## Example review

For a Kitty clipboard change, require OSC 52 and OSC 5522 to remain distinct,
verify `ST` termination and Base64 chunk rules, test capability detection and
transaction ordering, then prove the real writer-to-parser path. A helper-only
test is insufficient.

## Verification

```bash
dotnet test --project tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj --filter-class "*Protocol*Tests" --timeout 60s
make lint
make build
make test
```

## Common mistakes

- Copying escape strings instead of modeling grammar and validation.
- Testing a complete buffer but not fragmented input or recovery.
- Claiming support from environment detection without a typed implementation.
- Updating code while leaving the coverage matrix or security limits stale.
