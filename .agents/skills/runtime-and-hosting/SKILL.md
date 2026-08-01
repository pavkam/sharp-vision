---
name: runtime-and-hosting
description:
  Use when changing SharpVision Dispatcher execution, timers, idle, Application,
  ConsoleApplication, ConsoleHost, Session, transport, runtime event ordering,
  resize, terminal services, protocol routing, Ctrl+C handling, shutdown,
  cancellation, raw or VT modes, host leases, or platform restoration.
---

# Runtime and Hosting

## Overview

Keep application work ordered on one dispatcher while terminal resources remain
explicitly owned and reliably restored. Startup, steady-state iteration, and
shutdown are one lifecycle contract, not independent conveniences.

## Workflow

1. Route the task to the smallest matching runtime references.
2. Read their normative sections and trace ownership from public host entry
   point through Application, Session, transport, platform lease, and cleanup.
3. State dispatcher affinity, event order, cancellation, exception precedence,
   and reverse-disposal order.
4. Add a focused failing ordering or lifetime test with deterministic clocks,
   transports, terminals, and restore probes.
5. Implement without callbacks under locks or background mutation of UI state.
6. Reconcile lifecycle docs and run focused verification before repository
   gates.

## Reference routing

<!-- markdownlint-disable MD013 -->

| Task signal                                                                     | Read                                                      | Normative starting point                                                            |
| ------------------------------------------------------------------------------- | --------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| Dispatcher queue, affinity, timers, idle, reentrancy                            | [dispatcher.md](references/dispatcher.md)                 | [Threading](../../../docs/concepts/threading.md#overview)                           |
| ConsoleApplication, builder, options, ConsoleHost, preflight, Ctrl+C            | [hosting.md](references/hosting.md)                       | [Hosting](../../../docs/concepts/hosting.md#overview)                               |
| Iteration order, input, timers, layout, render, resize, shutdown                | [event-loop.md](references/event-loop.md)                 | [Runtime event loop](../../../docs/architecture/runtime-event-loop.md#overview)     |
| Bell, title, clipboard, graphics, out-of-band writes, routing                   | [terminal-services.md](references/terminal-services.md)   | [Terminal integration](../../../docs/architecture/terminal-integration.md#overview) |
| Session, transport, Unix/Windows modes, leases, cleanup, exception preservation | [platform-lifecycle.md](references/platform-lifecycle.md) | [Runtime shutdown](../../../docs/architecture/runtime-event-loop.md#shutdown)       |
| Any runtime or hosting verification                                             | [testing.md](references/testing.md)                       | [Pseudoterminal testing](../../../docs/testing/pseudoterminals.md#overview)         |

<!-- markdownlint-enable MD013 -->

## Boundaries

- Use `terminal-systems` for protocol grammar, discovery evidence, and backend
  selection policy.
- Use `rendering-and-text` for frame semantics and renderer internals.
- Use `ui-foundations` for layout, input routing, focus, and modality policy.
- Use `ui-components` for concrete control and surface behavior.

## Invariants

- One dispatcher orders mutation, input delivery, timers, layout, render, and
  user callbacks.
- Resize commits the newest size and completes layout before the resize event
  and render.
- Idle fires only after ready work drains and before waiting; queued idle work
  cannot cause busy spinning.
- Every acquired terminal resource has one owner and reverse-order cleanup.
- Cleanup failures never hide the original exception.
- Cancellation and disposal are race-safe, idempotent, and exception-complete.
- Controls reach terminal features through `Application.Terminal`, never raw
  protocol bytes.

## Common mistakes

- Calling portable hosts by manually wiring Session and transport.
- Mixing public lifecycle callbacks with renderer/session/platform cleanup
  order.
- Restoring cooked mode before dependent VT cleanup is complete.
- Running callbacks under locks or mutating the tree from transport threads.
