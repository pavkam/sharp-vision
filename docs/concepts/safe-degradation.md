# Safe degradation and strict diagnostics

## Safe degradation contract

Unsupported or uncertain environmental features choose a deterministic lower
capability without throwing by default. Programmer contract violations still
throw before mutation.

| Feature             | Preferred                     | Safe fallback                            |
| ------------------- | ----------------------------- | ---------------------------------------- |
| RGB color           | 24-bit color                  | 256-color SGR, then basic/default colors |
| Styled underline    | Variant/color                 | Plain underline, then omission           |
| Synchronized output | Mode 2026 frame               | Same frame without atomic presentation   |
| Kitty keyboard      | Typed enhanced events         | Legacy VT/xterm key decoding             |
| Pixel mouse         | Pixel plus cell coordinates   | Cell SGR mouse, then keyboard only       |
| Kitty clipboard     | OSC 5522 MIME (not yet wired) | OSC 52 text, then unavailable result     |
| Graphics            | Kitty/sixel/iTerm2 extension  | Text or cell-based representation        |

Fallback never changes logical control state or silently reports success for an
operation that did not occur.

`Capabilities.Feature` distinguishes supported, unsupported, and tentative
evidence plus its origin. `Runtime.Session` enables optional focus, paste,
mouse, and Kitty keyboard modes only for supported evidence; terminal-name and
environment hints alone remain tentative. Cleanup attempts every recorded mode
lease in reverse even when its enabling write may have failed partway.

## Strict mode

Strict mode promotes configured diagnostics—malformed input, unsupported
requested feature, inconsistent terminal reply, fallback use, or cleanup
failure—to exceptions at safe boundaries. It does not change valid wire bytes,
parser grammar, timeouts, or capability detection.

## Test obligations

Each capability-dependent feature tests preferred, every fallback step, strict
promotion, diagnostics, caller override, misleading environment hints, missing
queries, and logical equivalence where presentation fidelity changes.
