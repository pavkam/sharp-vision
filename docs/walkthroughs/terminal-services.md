# Use terminal services

Controls render cells; they never emit escape bytes. Application code reaches
the implemented output protocols through `Application.Terminal`, which orders
out-of-band writes between frames and falls back safely when a capability is
missing.

```csharp
protected override void OnStarted(Application application)
{
    if (application.Terminal.IsTitleSupported)
    {
        application.Terminal.SetTitle("SharpVision dashboard");
    }

    if (application.Terminal.Bell.IsSupported)
    {
        application.Terminal.Bell.Ring();
    }

    if (application.Terminal.Clipboard.IsSupported)
    {
        application.Terminal.Clipboard.Write("Report copied");
    }
}
```

| Service                                     | Protocol                                      | Unsupported behavior                                                           |
| ------------------------------------------- | --------------------------------------------- | ------------------------------------------------------------------------------ |
| `Terminal.Bell.Ring()`                      | Executable zero-parameter described `bel`     | No-op when unsupported                                                         |
| `Terminal.SetTitle(string)`                 | Proven built-in OSC 2 or described `TS`/`fsl` | No-op when unsupported                                                         |
| `Terminal.Clipboard.Write(...)`             | Kitty OSC 5522, else authoritative OSC 52     | No-op when neither is authoritative                                            |
| `Terminal.Clipboard.Request(...)`           | Kitty OSC 5522, else authoritative OSC 52     | No-op when neither is authoritative; replies use `KittyClipboardReplyReceived` |
| `Terminal.Clipboard.ClipboardPasteReceived` | Opt-in Kitty OSC 5522 paste events            | No event without an authoritative leaseable route                              |

Title support is proven in one of two ways: a library-owned built-in OSC 2
profile, or a complete, parameterless described `TS` prefix and `fsl` suffix.
The described programs must prove non-empty paired output; they are emitted
exactly as described and are never assumed to encode OSC 2. Bell support
likewise requires `bel` to prove non-empty zero-parameter output. Database OSC
52 support additionally requires an executable `Ms` program with exactly two
string parameters and non-empty representative output; when `Ms` is absent, has
the wrong arity, or produces no output, and Kitty clipboard support is not
authoritatively proven either, clipboard calls stay byte-quiet.

`Clipboard.IsSupported` reports authoritative evidence for either protocol:
Kitty OSC 5522 or OSC 52. `Write` and `Request` prefer Kitty when proven,
falling back to OSC 52 text, per the
[safe-degradation contract](../concepts/safe-degradation.md). Every completed
`Request`, and every `Write` served by Kitty OSC 5522, that actually reached the
terminal - success, terminal failure, or timeout - raises
`KittyClipboardReplyReceived` once. The one exception is cancellation: when a
still-pending `Write` or `Request` is superseded by a newer call on the same
selection, the pending one is silently abandoned instead - no event fires for
the stale one. A `Write` that fell back to OSC 52 raises nothing: that protocol
defines no acknowledgement for a write, so there is no outcome to report. Check
the Kitty clipboard capability before making a "copied" confirmation depend on
the event.

Clipboard reads may display a terminal-owned permission prompt. Their 30-second
default deadline is therefore separate from the shorter startup query timeout;
customize it with `ConsoleRunOptions.ClipboardOperationTimeout` or
`ConsoleApplicationBuilder.WithClipboardOperationTimeout(TimeSpan)`.

Terminal-initiated Kitty paste offers are a separate opt-in surface. Set
`ConsoleRunOptions.ClipboardPasteEvents` or call `UseClipboardPasteEvents()` on
the console builder. When authoritative mode 5522 support and the active route
permit it, the session leases the mode and `ClipboardPasteReceived` publishes an
owned MIME inventory, selection, and one-time password. Malformed, incomplete,
expired, or credential-inconsistent notifications are ignored rather than
exposed partially. An explicitly approved tmux route carries clipboard strings
one packet at a time; GNU screen and routes without clipboard approval leave the
service unsupported.

Do not concatenate escape sequences in control or application code. The typed
services validate their data, respect the active capability profile, and keep
writes out of synchronized frame bodies. The
[runtime routing contract](../protocols/runtime-routing.md#ordering-and-ownership)
defines ordering and ownership.

## Inspect support

`Application.TerminalProfile` is the complete immutable active description, and
`Application.Capabilities` is its semantic capability snapshot. Both are
detected and negotiated at startup unless the host supplies an explicit
override. Prefer a typed service's `IsSupported` member when one exists for the
operation, and use the
[protocol coverage matrix](../protocols/coverage-matrix.md#coverage) to
distinguish implemented, observable, fallback-only, and unsupported protocol
families.

A missing capability is expected environmental state, not a control-flow
exception. The
[safe-degradation contract](../concepts/safe-degradation.md#overview) owns
fallback and strict diagnostics, and the
[feature support map](../features/index.md#feature-support) links common
application needs to the exact proof.
