---
name: terminal-systems
description:
  Use when changing SharpVision ANSI, ECMA-48, CSI, OSC, DCS, keyboard, mouse,
  paste, focus, clipboard, capabilities, discovery, terminal identity, terminfo,
  termcap, multiplexers, backend selection, Kitty, iTerm2, sixel, or raw
  terminal protocol bytes.
---

# Terminal Systems

## Overview

Keep terminal behavior sourced, typed, bounded, byte-exact, and recoverable.
Normative documents own the protocol and discovery contracts; code and tests
prove their declared support.

## Workflow

1. Classify the change with the routing table and read only the matching
   references.
2. Read every normative section linked by those references and the nearest code
   and tests before deciding behavior.
3. Write a focused failing exact-byte, typed-event, discovery, or fallback test.
4. Implement through typed boundaries with explicit limits, ownership, timeout,
   malformed-input, and unsupported-environment behavior.
5. Update the owning protocol or architecture contract and the
   [coverage matrix](../../../docs/protocols/coverage-matrix.md#coverage) when
   observable support changes.
6. Run the focused commands from the selected references, then the repository
   gates.

## Reference routing

<!-- markdownlint-disable MD013 -->

| Task signal                                                                       | Read                                                              | Normative starting point                                                          |
| --------------------------------------------------------------------------------- | ----------------------------------------------------------------- | --------------------------------------------------------------------------------- |
| CSI, OSC, DCS, DEC, xterm, tmux, screen, Kitty, iTerm2, sixel wire grammar        | [protocols.md](references/protocols.md)                           | [Protocol index](../../../docs/protocols/index.md#protocol-families)              |
| Keyboard, mouse, paste, focus, incremental decoding                               | [input.md](references/input.md)                                   | [Terminal protocol testing](../../../docs/testing/terminal-protocols.md#overview) |
| Capabilities, identity, queries, environment evidence, terminfo, backend fallback | [discovery-and-backends.md](references/discovery-and-backends.md) | [Discovery pipeline](../../../docs/architecture/discovery-pipeline.md#overview)   |
| Kitty, sixel, or iTerm2 image encoding and authorization                          | [graphics-protocols.md](references/graphics-protocols.md)         | [Terminal backends](../../../docs/architecture/terminal-backends.md#overview)     |
| Any terminal-system verification                                                  | [testing.md](references/testing.md)                               | [Correctness model](../../../docs/testing/correctness-model.md#overview)          |

For an active graphics-query task, begin with `discovery-and-backends.md`. Add
`protocols.md` only when parsing or wire grammar changes, and add
`graphics-protocols.md` only when backend authorization, encoding, or cleanup
changes.

<!-- markdownlint-enable MD013 -->

## Boundaries

- Use `rendering-and-text` for cells, frames, damage, image placement, and final
  screen equivalence.
- Use `runtime-and-hosting` when changing Session, transport lifetime, host
  leases, event-loop ordering, or terminal restoration.
- Loading a second skill is required only when its implementation changes.

## Invariants

- Parse arbitrary transport fragmentation and multiple frames per read.
- Bound parameters, metadata, payloads, buffers, transactions, and query time.
- Preserve protocol evidence provenance; do not infer identity from an unrelated
  capability.
- Recover the outer parser after malformed, oversized, unknown, or interrupted
  input.
- Keep controls protocol-free and redact clipboard or untrusted payloads from
  diagnostics.
- Use primary standards or terminal-author documentation and record the
  supported version or access date.

## Common mistakes

- Treating terminal identity, graphics backend selection, and cell fallback as
  one policy.
- Claiming support from environment detection without typed behavior.
- Testing a complete buffer without fragmentation and recovery.
- Copying escape strings instead of modeling grammar and validation.
