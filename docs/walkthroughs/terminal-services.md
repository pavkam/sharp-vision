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

| Service                           | Protocol                                      | Unsupported behavior                                   |
| --------------------------------- | --------------------------------------------- | ------------------------------------------------------ |
| `Terminal.Bell.Ring()`            | Executable zero-parameter described `bel`     | No-op when unsupported                                 |
| `Terminal.SetTitle(string)`       | Proven built-in OSC 2 or described `TS`/`fsl` | No-op when unsupported                                 |
| `Terminal.Clipboard.Write(...)`   | Authoritative OSC 52                          | No-op when unsupported                                 |
| `Terminal.Clipboard.Request(...)` | Authoritative OSC 52                          | No-op when unsupported; replies use `ResponseReceived` |

Title support is proven in one of two ways: a library-owned built-in OSC 2
profile, or a complete, parameterless described `TS` prefix and `fsl` suffix.
The described programs must prove non-empty paired output; they are emitted
exactly as described and are never assumed to encode OSC 2. Bell support
likewise requires `bel` to prove non-empty zero-parameter output. Database OSC
52 support additionally requires an executable `Ms` program with exactly two
string parameters and non-empty representative output; when `Ms` is absent, has
the wrong arity, or produces no output, clipboard calls stay byte-quiet.

`Clipboard.IsSupported` reports authoritative OSC 52 only. Kitty OSC 5522 is
implemented at the protocol layer but is not reachable through this facade,
because nothing routes its inbound replies yet. Reporting it here would claim
support for an operation that cannot complete, which the
[safe-degradation contract](../concepts/safe-degradation.md) forbids, so on a
Kitty-only profile the facade stays byte-quiet instead.

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
