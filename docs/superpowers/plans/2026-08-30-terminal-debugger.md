# Terminal Debugger Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a polished `TerminalDebugger` example that reports detected terminal support, records richly formatted decoded input, and lets the user explicitly verify output features.

**Architecture:** A `TerminalDebuggerScreen` composes three retained panes around small example-local models. Capability rows are generated from the active immutable profile plus curated presentation metadata; input records are immutable owned snapshots kept in a bounded session log; explicit probes use only `Application.Terminal` and other public SharpVision surfaces.

**Tech Stack:** .NET 10, C# 14, SharpVision public UI and terminal APIs, xUnit v3/Shouldly only if a missing library seam requires production-library coverage.

**Spec:** `docs/superpowers/specs/2026-08-30-terminal-debugger-design.md`

## Global Constraints

- The example targets .NET 10 and references only `src/SharpVision/SharpVision.csproj`.
- The example diagnoses decoded public SharpVision behavior and never emits or sniffs raw terminal bytes.
- Side-effecting probes run only after explicit activation.
- The event log owns displayed payload data, is capped at 500 records, and is never persisted.
- Every named C# type has its own same-named file; no primary constructors or positional records are used.
- Every public and internal type/member has meaningful XML documentation.
- The example receives no dedicated test project or example-specific test files.
- Existing unrelated working-tree changes are preserved.

---

### Task 1: Project shell and responsive application frame

**Files:**
- Create: `examples/TerminalDebugger/TerminalDebugger.csproj`
- Create: `examples/TerminalDebugger/GlobalUsings.cs`
- Create: `examples/TerminalDebugger/Program.cs`
- Create: `examples/TerminalDebugger/TerminalDebuggerScreen.cs`
- Modify: `SharpVision.slnx`

**Interfaces:**
- Consumes: `ConsoleApplication.RunAsync(Screen, Action<ConsoleApplicationBuilder>)`, `CompositeControl.InitializeContent(ControlBase)`, `Application.Terminal`.
- Produces: `TerminalDebuggerScreen`, the retained root consumed by every later task; pane factory methods named `BuildCapabilitiesPane`, `BuildInputPane`, and `BuildTestsPane`.

- [ ] **Step 1: Create the executable project**

Use the same project properties as `examples/ProcessMonitor/ProcessMonitor.csproj`, set `AssemblyName` and `RootNamespace` to `TerminalDebugger`, and reference `../../src/SharpVision/SharpVision.csproj`.

- [ ] **Step 2: Add the host entry point**

```csharp
var status = await ConsoleApplication.RunAsync(
    new TerminalDebugger.TerminalDebuggerScreen(),
    static builder => builder.TreatControlCAsInput());

return status == ConsoleRunStatus.Failed ? 1 : 0;
```

- [ ] **Step 3: Build the retained frame**

Create a header, semantic summary strip, `TabControl` with Capabilities/Input events/Tests tabs, and a `StatusBar`. Register Ctrl+Q at the screen root. Use stretch alignment throughout and only standard SharpVision controls.

- [ ] **Step 4: Add the project to the solution**

Insert `<Project Path="examples/TerminalDebugger/TerminalDebugger.csproj"/>` beside the other examples in `SharpVision.slnx`.

- [ ] **Step 5: Verify the shell**

Run: `dotnet build examples/TerminalDebugger/TerminalDebugger.csproj --no-restore`

Expected: build succeeds with zero warnings and zero errors.

- [ ] **Step 6: Commit the shell**

```bash
git add SharpVision.slnx examples/TerminalDebugger
git commit -m "feat(examples): add terminal debugger shell"
```

### Task 2: Diagnostic state and rich formatting

**Files:**
- Create: `examples/TerminalDebugger/VerificationState.cs`
- Create: `examples/TerminalDebugger/CapabilityDescriptor.cs`
- Create: `examples/TerminalDebugger/CapabilityCatalog.cs`
- Create: `examples/TerminalDebugger/DiagnosticEventKind.cs`
- Create: `examples/TerminalDebugger/DiagnosticField.cs`
- Create: `examples/TerminalDebugger/DiagnosticEventRecord.cs`
- Create: `examples/TerminalDebugger/DiagnosticEventLog.cs`
- Create: `examples/TerminalDebugger/DiagnosticTextFormatter.cs`

**Interfaces:**
- Produces: `CapabilityCatalog.All`, an exhaustive `IReadOnlyList<CapabilityDescriptor>` keyed by `TerminalProtocol`; `DiagnosticEventLog.Add`, `Clear`, `IsPaused`, and `Records`; `DiagnosticTextFormatter.EscapeText(ReadOnlySpan<char>)` and `FormatBytes(ReadOnlySpan<byte>)`.
- Consumes: `TerminalCapabilities.Features`, `Feature.State`, `Feature.Origin`, and owned data copied from public event arguments.

- [ ] **Step 1: Define verification and descriptor values**

Use a `VerificationState` enum with `NotRun`, `Observed`, `Passed`, and `Failed`. Make `CapabilityDescriptor` an immutable `readonly record struct` with explicit constructor and `TerminalProtocol Protocol`, `string Group`, `string Label`, and `string Explanation` properties.

- [ ] **Step 2: Make the catalog exhaustive**

Create one descriptor for every current `TerminalProtocol` member. At startup compare `TerminalCapabilities.Features.Select(static value => value.Protocol)` with catalog keys and fail fast with a descriptive `InvalidOperationException` if either side differs, so new protocols cannot vanish from the UI.

- [ ] **Step 3: Define owned event records**

`DiagnosticEventRecord` is an immutable sealed class with explicit constructor taking sequence, timestamp, `DiagnosticEventKind`, summary, explanation, and `IReadOnlyList<DiagnosticField>`. Copy fields into a read-only owned array after validating every element.

- [ ] **Step 4: Implement the bounded log**

`DiagnosticEventLog.Add` assigns a monotonically increasing sequence and evicts the oldest record after 500 entries. If `IsPaused` is true, it performs no mutation. `Clear` removes records but does not reuse sequence numbers.

- [ ] **Step 5: Implement unambiguous formatters**

Escape NUL, BEL, ESC, CR, LF, TAB, backslash, C0/C1 controls, and non-printing scalars with colored SharpVision markup and a plain-language annotation. Format arbitrary bytes as bounded uppercase hexadecimal plus UTF-8 interpretation when valid.

- [ ] **Step 6: Verify model integration**

Run: `dotnet build examples/TerminalDebugger/TerminalDebugger.csproj --no-restore`

Expected: build succeeds, analyzers accept every value type and XML member, and catalog initialization has no missing protocol.

- [ ] **Step 7: Commit diagnostic state**

```bash
git add examples/TerminalDebugger
git commit -m "feat(examples): model terminal diagnostics"
```

### Task 3: Capability dashboard

**Files:**
- Create: `examples/TerminalDebugger/CapabilityStatus.cs`
- Create: `examples/TerminalDebugger/CapabilityDashboard.cs`
- Modify: `examples/TerminalDebugger/TerminalDebuggerScreen.cs`

**Interfaces:**
- Consumes: `CapabilityCatalog.All`, `Application.Capabilities`, `Application.Terminal.Description`, `IBell.IsSupported`, `IClipboard.IsSupported`, `INotifications.IsSupported`, `ITerminalServices.IsTitleSupported`.
- Produces: `CapabilityDashboard.SetVerification(TerminalProtocol, VerificationState, string)` and summary counts exposed through `CapabilityDashboard.SummaryChanged`.

- [ ] **Step 1: Define row state**

`CapabilityStatus` owns descriptor, detected `Feature`, verification state, and verification detail. Validate non-null text and defined enums before assignment.

- [ ] **Step 2: Render environment identity**

Show description name/platform/origin, Unicode version, ambiguous-width policy, color depth and origin, title/bell/clipboard/notification service support, and terminal cell dimensions. Use semantic labels plus text, never color alone.

- [ ] **Step 3: Render all optional protocols**

Group rows under Input, Output, Clipboard, Graphics, and Rendition. Each row shows Supported/Unsupported/Unknown, evidence origin, Not run/Observed/Passed/Failed, and the descriptor explanation. Selection updates a wrapping detail pane.

- [ ] **Step 4: Wire summary counts**

Raise `SummaryChanged` after initialization and every verification transition. Update the screen summary strip without rebuilding the retained tree.

- [ ] **Step 5: Verify compact and normal layouts**

Run the example at approximately 80x24 and 140x40. Confirm that compact mode stacks list and detail content, labels remain legible, selection remains reachable, and no row relies only on color.

- [ ] **Step 6: Commit the dashboard**

```bash
git add examples/TerminalDebugger
git commit -m "feat(examples): show terminal capability evidence"
```

### Task 4: Decoded input event inspector

**Files:**
- Create: `examples/TerminalDebugger/InputEventInspector.cs`
- Create: `examples/TerminalDebugger/InputEventRecorder.cs`
- Modify: `examples/TerminalDebugger/TerminalDebuggerScreen.cs`

**Interfaces:**
- Consumes: `Events.Key`, `Events.Text`, `Events.Pointer`, `Events.Paste`, `Events.TerminalFocusChanged`, `IClipboard.ClipboardPasteReceived`, `IClipboard.KittyClipboardReplyReceived`, and `DiagnosticEventLog`.
- Produces: `InputEventRecorder.Attach(Application, ControlBase)`, `InputEventRecorder.Dispose()`, and `InputEventInspector.Refresh()`.

- [ ] **Step 1: Attach routed input handlers**

Register handlers on the screen with `handledEventsToo: true`. Record route phase, handled state, key action/code/modifiers/text, pointer action/button/cell/pixel/local coordinates/click count, text input, bracketed-paste byte/rune counts and escaped content, and terminal focus state.

- [ ] **Step 2: Attach clipboard event handlers**

Record Kitty paste selection, MIME inventory, and password bytes in explained hexadecimal. For clipboard replies, copy all text/MIME data needed for display and dispose owned Kitty results after copying.

- [ ] **Step 3: Correlate passive verification**

Mark focus, bracketed paste, cell mouse, pixel mouse, Kitty keyboard, and key-release capabilities `Observed` only after their corresponding decoded event arrives. Do not infer one protocol from an unrelated event.

- [ ] **Step 4: Build the inspector UI**

Use a newest-first selectable list and a structured detail pane with timestamp, kind, summary, explanation, and individually colored field names/values. Add Pause/Resume, Clear, and Expand payload controls. Refresh only affected retained controls after new records.

- [ ] **Step 5: Exercise representative input**

Run the example and verify ordinary key press, modified key, key repeat/release where available, printable Unicode text, mouse move/press/release/wheel, bracketed paste containing CR/LF/TAB/ESC, focus out/in, and terminal resize. Confirm the log stops at 500 and pause records nothing.

- [ ] **Step 6: Commit input inspection**

```bash
git add examples/TerminalDebugger
git commit -m "feat(examples): inspect decoded terminal input"
```

### Task 5: Explicit output probes and visual specimens

**Files:**
- Create: `examples/TerminalDebugger/ProbeStatus.cs`
- Create: `examples/TerminalDebugger/TerminalProbe.cs`
- Create: `examples/TerminalDebugger/TerminalProbePanel.cs`
- Create: `examples/TerminalDebugger/VisualSpecimenPanel.cs`
- Create: `examples/TerminalDebugger/ClipboardRoundTripProbe.cs`
- Modify: `examples/TerminalDebugger/TerminalDebuggerScreen.cs`

**Interfaces:**
- Consumes: `Application.Terminal.Bell`, `SetTitle`, `Notifications`, `Clipboard`; capability dashboard verification mutation; clipboard completion events.
- Produces: explicit actions for bell, title, notification, clipboard round-trip, rendition/color specimens, Unicode geometry, and every public graphics surface safely available to the example.

- [ ] **Step 1: Define probe lifecycle**

Use `ProbeStatus` values `Ready`, `Running`, `Passed`, `Failed`, and `Inconclusive`. `TerminalProbe` exposes validated label/description/support and explicit `Run` plus user-confirmation transitions. Reject a second run while `Running`.

- [ ] **Step 2: Add manual bell/title/notification probes**

Each action invokes exactly one public terminal service call and opens Pass/Fail controls. Title testing emits `SharpVision Terminal Debugger - title test` and restores `SharpVision Terminal Debugger` after confirmation and during disposal.

- [ ] **Step 3: Add clipboard round-trip**

Warn before mutation. Generate a unique marker, call `Write`, request the same selection after an acknowledged Kitty write or immediately for OSC 52, compare the owned reply, display protocol/failure/diagnostic/payload details, and time out to `Inconclusive`. Restore prior text only when a successful pre-read captured it.

- [ ] **Step 4: Add stable visual specimens**

Render labeled basic/indexed/true-color ramps, styled underlines, underline color, overline, combining marks, variation selectors, ZWJ emoji, ambiguous-width characters, and wide-character cell guides. Give each group Pass/Fail controls that update the corresponding capability verification without claiming automatic proof.

- [ ] **Step 5: Add supported graphics specimens**

Inspect the current public graphics APIs before implementation. Add a specimen only for mechanisms reachable without internal access; otherwise show detected graphics capabilities with an honest explanation that this example cannot independently exercise them through the current public surface.

- [ ] **Step 6: Verify startup is byte-quiet for probes**

Run the example without activating Tests. Confirm it does not ring, change the title beyond its normal application title, notify, touch clipboard content, or emit a graphics payload. Then activate every supported probe and confirm pass/fail/inconclusive transitions.

- [ ] **Step 7: Commit probes**

```bash
git add examples/TerminalDebugger
git commit -m "feat(examples): add explicit terminal probes"
```

### Task 6: Documentation, integration, and full verification

**Files:**
- Create: `examples/TerminalDebugger/README.md`
- Modify: `README.md`
- Modify: `docs/architecture/index.md`
- Modify: `docs/architecture/project-structure.md`
- Modify: `docs/walkthroughs/index.md`

**Interfaces:**
- Consumes: the completed example behavior and shortcuts.
- Produces: discoverable run instructions, safety notes, detected-versus-verified semantics, and architecture dependency documentation.

- [ ] **Step 1: Document operation and safety**

Explain how to run the project, each tab, status meanings, keyboard shortcuts, the 500-record bound, full payload visibility, clipboard mutation warning, and why detected support can differ from verified behavior.

- [ ] **Step 2: Update repository discovery pages**

Add Terminal Debugger to the root example list, architecture diagrams, project-structure prose, and walkthrough index. Keep dependency arrows from the example to `SharpVision` only.

- [ ] **Step 3: Run documentation checks**

Run:

```bash
npm run lint:doc-content
npm run lint:markdown
npm run lint:links
npm run test:docs
```

Expected: every command succeeds with no content, Markdown, link, or documentation-contract failures.

- [ ] **Step 4: Run repository gates**

Run:

```bash
make format
make lint
make build
make test
```

Expected: all commands succeed; builds contain zero warnings and errors; discovered tests meet the configured minimum.

- [ ] **Step 5: Perform final manual matrix**

Run Terminal Debugger in at least one available terminal and record observed results for keyboard, pointer, paste, focus, resize, bell, title, notification, clipboard, color, rendition, Unicode, and graphics. Any unavailable terminal-dependent feature remains Unknown/Not run or Inconclusive, never falsely Passed.

- [ ] **Step 6: Commit integration**

```bash
git add README.md docs/architecture/index.md docs/architecture/project-structure.md docs/walkthroughs/index.md examples/TerminalDebugger/README.md
git commit -m "docs: add terminal debugger example"
```
