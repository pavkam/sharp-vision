# Use terminal services

Controls render cells; they never emit escape bytes. Application code reaches
implemented output protocols through `Application.Terminal`, which orders
out-of-band writes between frames and applies safe fallback.

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

| Service                           | Protocol                                      | Unsupported behavior                                   |
| --------------------------------- | --------------------------------------------- | ------------------------------------------------------ |
| `Terminal.Bell.Ring()`            | Executable zero-parameter described `bel`     | No-op when unsupported                                 |
| `Terminal.SetTitle(string)`       | Proven built-in OSC 2 or described `TS`/`fsl` | No-op when unsupported                                 |
| `Terminal.Clipboard.Write(...)`   | Authoritative OSC 52                          | No-op when unsupported                                 |
| `Terminal.Clipboard.Request(...)` | Authoritative OSC 52                          | No-op when unsupported; replies use `ResponseReceived` |

Title support is proven either by a library-owned built-in OSC 2 profile or by a
complete, parameterless described `TS` prefix and `fsl` suffix. The described
programs must prove non-empty paired output, are emitted exactly, and are not
assumed to encode OSC 2. Bell support likewise requires `bel` to prove non-empty
zero-parameter output. Database OSC 52 support additionally requires an
executable `Ms` program with exactly two string parameters and non-empty
representative output; an absent, wrong-arity, or outputless `Ms` leaves
clipboard calls byte-quiet.

`Clipboard.IsSupported` reports authoritative OSC 52 only. Kitty OSC 5522 is
implemented at the protocol layer but is not reachable through this facade,
because nothing routes its inbound replies yet. Reporting it here would claim
support for an operation that cannot complete, which the
[safe-degradation contract](../concepts/safe-degradation.md) forbids, so the
facade stays byte-quiet on a Kitty-only profile instead.

Do not concatenate escape sequences in control or application code. Typed
services validate data, respect the active capability profile, and keep writes
out of synchronized frame bodies. The
[runtime routing contract](../protocols/runtime-routing.md#ordering-and-ownership)
defines ordering and ownership.

## Inspect support

`Application.TerminalProfile` is the complete immutable active description;
`Application.Capabilities` is its semantic capability snapshot. They are
detected and negotiated at startup unless the host supplies an explicit
override. Use the typed service's `IsSupported` member for an operation when
available; use the
[protocol coverage matrix](../protocols/coverage-matrix.md#coverage) to
distinguish implemented, observable, fallback-only, and unsupported protocol
families.

Capability absence is expected environmental state, not a control-flow
exception. The
[safe-degradation contract](../concepts/safe-degradation.md#overview) owns
fallback and strict diagnostics. The
[feature support map](../features/index.md#feature-support) links common
application needs to the exact proof.
