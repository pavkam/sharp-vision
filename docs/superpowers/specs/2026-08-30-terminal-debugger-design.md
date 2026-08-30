# Terminal Debugger design

## Purpose

`TerminalDebugger` is a runnable SharpVision example for inspecting the terminal environment in which it runs. It gives developers one responsive dashboard that distinguishes what SharpVision detected from what the user has actually observed working. It also exposes the decoded input events delivered to a SharpVision application, with enough structured detail to diagnose keyboard, pointer, focus, paste, clipboard, resize, and rendering problems.

The example diagnoses public SharpVision behavior. It does not bypass the runtime to sniff transport bytes, promise support that SharpVision does not expose, or persist potentially sensitive input without an explicit future feature.

## Success criteria

- The dashboard identifies the active terminal description, color depth, Unicode policy, and every optional protocol in `TerminalCapabilities.Features`.
- Each feature presents detected support and evidence origin separately from live verification state.
- Passive features become verified only after the corresponding decoded event is observed.
- Side-effecting output checks run only after an explicit user action.
- The event inspector displays structured, readable details, including escaped text and explained protocol values, while remaining bounded in memory.
- The layout remains usable in compact and large terminals.
- The example uses only public SharpVision APIs and owns no library behavior.
- Library behavior used by the example remains covered by the owning library test projects; the example receives no dedicated test project.

## Information architecture

The application is a retained `Screen` with a header, a summary strip, a primary tabbed workspace, and a status bar.

The header shows the application name, terminal-description name and origin, current cell dimensions, and current focus state. The summary strip counts detected, unsupported, unknown, verified, and failed checks.

The workspace has three views:

1. **Capabilities** groups terminal profile data into environment, input, output, clipboard, graphics, and rendition sections. Each row shows the feature name, detected support, evidence origin, verification state, and a short explanation.
2. **Input events** contains a bounded newest-first event list and a structured detail pane for the selected event. It records timestamp, event family, decoded values, and a human-readable explanation. Controls allow pausing capture, clearing the session, and revealing or collapsing long payloads.
3. **Tests** contains explicit probes and visual specimens. Actions cover bell, window title, desktop notification, clipboard write/read round-trip, rendition styles, color ramps, Unicode width samples, and supported graphics paths. The app never triggers these actions on startup.

## Capability and verification model

Detected support comes from the immutable active `TerminalCapabilities` profile and terminal-service support flags. The UI preserves all three support states and the evidence origin. It never converts `Unknown` into `Unsupported`.

Verification uses four states:

- `NotRun`: no observation can yet support a claim.
- `Observed`: a passive input event or deterministic reply was received.
- `Passed`: the user confirmed a visual or audible result, or a round-trip produced the expected value.
- `Failed`: a requested check timed out, returned a mismatch, or the user marked its visible result incorrect.

Rows may also explain that a feature is not independently verifiable from inside the terminal. For example, synchronized output can be exercised by rendering but cannot be proven solely by the application that emitted it.

Capability presentation is data-driven from `TerminalCapabilities.Features`, with a curated metadata map for grouping, labels, descriptions, passive event relationships, and available probes. A new enum value therefore requires an intentional metadata entry and remains visible during development rather than being silently omitted.

## Input capture

The screen registers handled-events-too routed handlers at its root for key, text, pointer, paste, and terminal-focus events. Resize state is observed through the application lifecycle surface already exposed publicly. Capture records what SharpVision decoded, not raw escape bytes.

Each record is immutable and owns any payload it displays. Records include:

- monotonic sequence number and local timestamp;
- family and routed-event phase where available;
- key code, rune/text, modifiers, press/repeat/release kind, and handled state;
- pointer action, button, modifiers, cell coordinates, pixel coordinates, wheel or motion data, and handled state;
- focus gained/lost state;
- paste byte and rune counts plus an escaped payload;
- clipboard reply selection, protocol result, MIME information, byte count, and escaped content when exposed by the public API;
- old and new terminal dimensions for resize observations.

The log retains at most 500 records. A paused capture ignores new records rather than accumulating a hidden queue. Long payloads are stored only up to the applicable runtime transfer bound and rendered through an escaped, wrapping detail view. Control characters, whitespace, Unicode scalars, and invalid data are represented unambiguously and accompanied by plain-language labels.

## Explicit tests

All output or environment-changing tests require activation from the Tests view.

- **Bell:** invokes `Application.Terminal.Bell.Ring()` and asks the user to confirm whether an alert was perceived.
- **Title:** saves the debugger title used by the application, emits a temporary diagnostic title, and restores the debugger title when the confirmation closes or the application exits.
- **Notification:** sends a clearly identified test notification and asks for confirmation.
- **Clipboard:** warns that the clipboard will be modified, writes a unique short marker, requests it back, compares the returned value, and reports the exact protocol/result exposed by the service. It does not restore prior clipboard content unless that content was successfully read first.
- **Rendition and color:** shows stable specimens for basic colors, indexed colors where detected, true color where detected, underline styles, underline color, and overline. The user marks the specimen correct or incorrect.
- **Unicode geometry:** shows combining marks, variation selectors, ZWJ emoji, ambiguous-width characters, and wide characters against cell guides. The user marks the specimen correct or incorrect.
- **Graphics:** offers only the graphics mechanisms represented by the active public SharpVision surface. Each specimen reports the selected backend and any observable placement result, then asks the user to confirm the rendered result.

Unsupported checks remain visible but disabled with an explanation. Unknown checks may be attempted only when the public API safely permits it; otherwise they remain informational.

## Responsive layout

At normal widths, capability and event views use a list on the left and a detail or action pane on the right. At compact widths they collapse into a vertical layout and reduce nonessential columns while retaining full details for the selected row. The header shortens labels before hiding diagnostic state.

Color is semantic and never the only status signal. Every status includes text or a glyph. Focus order follows visible reading order, and all actions have keyboard access. The dashboard uses the existing theme system and standard controls rather than custom terminal byte output.

## Error handling and lifecycle

Probe failures are represented as diagnostic results and do not terminate the application. Time-bound clipboard operations end as failed or inconclusive with an explanation. Actions reject duplicate concurrent probes. Event handlers and timers are detached or disposed with the screen. Terminal title restoration and any pending probe cleanup run during disposal without hiding an earlier failure.

The application treats Ctrl+C as input and provides an explicit Ctrl+Q exit path. It does not write captured events to stdout while the terminal UI is active.

## Project integration

The new executable lives at `examples/TerminalDebugger/TerminalDebugger.csproj`, references `src/SharpVision/SharpVision.csproj`, targets .NET 10, and is non-packable. Each named type has its own file. The project is added to `SharpVision.slnx`, the repository architecture documentation, and the root examples index. Its README explains the detected-versus-verified distinction, shortcuts, side effects, and how to run it.

No example-specific test project is added. New or missing library behavior discovered during implementation must be tested in the owning library test project before the example consumes it.

## Verification

Development begins with compile-time and behavioral coverage for any library seam that the example proves is missing. The example is then manually exercised in at least one terminal for keyboard, pointer, paste, focus, resize, clipboard, and visual specimens. Repository completion requires `make format`, `make lint`, `make build`, and `make test`, with zero warnings and errors and all configured quality thresholds satisfied.
