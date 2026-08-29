# Safe degradation and strict diagnostics

## Overview

When an environmental feature is unsupported or uncertain, SharpVision picks a
deterministic lower capability instead of throwing by default. Programmer
contract violations still throw before mutation.

| Feature             | Preferred                    | Safe fallback                            |
| ------------------- | ---------------------------- | ---------------------------------------- |
| RGB color           | 24-bit color                 | 256-color SGR, then basic/default colors |
| Styled underline    | Variant/color                | Plain underline, then omission           |
| Synchronized output | Mode 2026 frame              | Same frame without atomic presentation   |
| Kitty keyboard      | Typed enhanced events        | Legacy VT/xterm key decoding             |
| Pixel mouse         | Pixel plus cell coordinates  | Cell SGR mouse, then keyboard only       |
| Kitty clipboard     | OSC 5522 MIME                | OSC 52 text, then unavailable result     |
| Graphics            | Kitty/sixel/iTerm2 extension | Text or cell-based representation        |

Falling back never changes logical control state and never reports success for
an operation that did not actually occur.

The
[Kitty clipboard contract](../protocols/kitty-clipboard.md#supported-features)
owns its application-integration status and its exact fallback boundary.

`Capabilities.Feature` distinguishes supported, unsupported, and tentative
evidence, and records where that evidence came from. `Runtime.Session` enables
the optional focus, paste, mouse, and Kitty keyboard modes only when the
evidence says supported; terminal-name and environment hints on their own stay
tentative. Cleanup attempts every recorded mode lease in reverse order, even
when the enabling write may have failed partway through.

## Strict mode

Strict mode promotes configured diagnostics - malformed input, an unsupported
requested feature, an inconsistent terminal reply, fallback use, or a cleanup
failure - to exceptions at safe boundaries. It does not change valid wire bytes,
parser grammar, timeouts, or capability detection.

`DiagnosticPromotion` is a flags policy with one value for each family and
`None` as its default. Set `ConsoleRunOptions.DiagnosticPromotions`, call the
builder's `PromoteDiagnostics(...)`, or set
`TerminalOptions.DiagnosticPromotions` directly. Passing no argument to the
builder method selects `All`; selecting individual flags keeps unrelated
diagnostics lenient.

Promotion throws `TerminalDiagnosticException`, whose `Promotion` property
identifies exactly one family without retaining terminal payload bytes. Protocol
diagnostics are raised through `Application.Diagnostic` before promotion;
graphics degradation is raised through `Application.GraphicsDiagnostic` first.
Description diagnostics promote after profile resolution, requested optional
modes promote before their lease writes, render fallback promotes after the
frame commits, and cleanup promotes only after the bounded cleanup walk. When a
primary failure already exists, cleanup promotion is secondary and both are
preserved.

`EncodeResult.UsedFallback` identifies color or decoration projection during
encoding. `RenderMetrics.UsedFallback` additionally includes unavailable
synchronized output and graphics placement fallback, so a completed
`FrameRendered` event records the fidelity decision before strict promotion.

## Expected behavior

For each capability-dependent feature, the preferred path, every fallback step,
strict promotion, diagnostics, caller overrides, misleading environment hints,
and missing queries behave as described, and where presentation fidelity changes
the logical result stays equivalent.
