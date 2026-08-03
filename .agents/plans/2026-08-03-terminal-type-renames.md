# Terminal Type Renames Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the remaining ambiguous Terminal type names with
purpose-revealing names, publish the break as `0.8.0-alpha.1`, and retain only
that version's API snapshots.

**Architecture:** This is a declaration-and-reference migration with no runtime
behavior change. Each type stays in its owning namespace and moves to a matching
file; active consumers use the real compound name directly, obsolete aliases
disappear, and the compatibility gate freezes only the current alpha surface.

**Tech Stack:** .NET 10, C# 14, xUnit v3, PublicApiGenerator, Verify, Markdown
tooling, Make.

---

### Task 1: Rename Terminal declarations

**Files:**

- Rename: the 28 declaration files listed in
  `.agents/designs/2026-08-03-terminal-type-renames.md`
- Modify: the renamed files and their same-namespace references under
  `src/SharpVision.Terminal/`
- Test: `tests/SharpVision.Terminal.Tests/`

- [ ] **Step 1: Establish the green pre-refactor baseline**

Run:

```bash
make test
```

Expected: 5,241 tests discovered, 5,236 passed, 5 platform tests skipped, zero
failures, and 35 documentation-script tests passed.

- [ ] **Step 2: Rename each declaration file and declared type**

Use these exact declaration replacements and rename every file to match its
replacement type:

```text
Protocols.Writer -> ProtocolWriter
Iterm.Writer -> ItermWriter
Sixel.Writer -> SixelWriter
Kitty.Graphics.Writer -> KittyGraphicsWriter
Kitty.Clipboard.Writer -> KittyClipboardWriter
Rendering.Encoder -> FrameEncoder
Sixel.Encoder -> SixelEncoder
Graphics.Format -> ImageFormat
Kitty.Graphics.Format -> KittyGraphicsFormat
Graphics.Limits -> ImageLimits
Geometry.Metrics -> CellMetrics
Rendering.Metrics -> RenderMetrics
Runtime.Options -> TerminalOptions
Input.Options -> InputOptions
Rendering.Palette -> TerminalPalette
Sixel.Palette -> SixelPalette
Multiplexing.Policy -> MultiplexingPolicy
Unicode.Policy -> UnicodePolicy
Multiplexing.Operation -> MultiplexingOperation
Terminfo.Operation -> TerminfoOperation
Kitty.Clipboard.Operation -> KittyClipboardOperation
Kitty.Graphics.Response -> KittyGraphicsResponse
Xterm.Response -> XtermCapabilitiesResponse
Input.Text -> TerminalText
Capabilities.Capabilities -> TerminalCapabilities
Rendering.Attributes -> TerminalAttributes
Rendering.Canvas -> TerminalCanvas
Kitty.Graphics.Medium -> KittyGraphicsMedium
```

Constructors, static return types, `Deconstruct` signatures, XML examples, and
self-references must use the replacement name.

- [ ] **Step 3: Run the build and observe the expected RED state**

Run:

```bash
dotnet build src/SharpVision.Terminal/SharpVision.Terminal.csproj --no-restore
```

Expected: compilation fails only because consumers still reference retired type
names. A failure caused by an incorrectly renamed member, namespace, or
constructor must be corrected before continuing.

### Task 2: Migrate production consumers and remove aliases

**Files:**

- Modify: `src/SharpVision.Terminal/**/*.cs`
- Modify: `src/SharpVision/**/*.cs`
- Modify: `examples/**/*.cs`
- Modify: project `GlobalUsings.cs` files

- [ ] **Step 1: Replace qualified and unqualified production references**

Apply the Task 1 mapping semantically, using namespace ownership and compiler
errors rather than unrestricted replacements of generic words such as `Text`,
`Options`, or `Writer`.

Examples of required direct references:

```csharp
using SharpVision.Terminal.Input;

TerminalText text;
InputOptions inputOptions;
TerminalOptions terminalOptions;
TerminalCanvas canvas;
TerminalAttributes attributes;
TerminalCapabilities capabilities;
```

- [ ] **Step 2: Delete aliases made redundant by the real names**

Delete aliases such as:

```csharp
using ProtocolWriter = Protocols.ProtocolWriter;
using TerminalText = Terminal.Input.TerminalText;
using TerminalOptions = Terminal.Runtime.TerminalOptions;
using TerminalCanvas = Terminal.Rendering.TerminalCanvas;
using TerminalAttributes = Terminal.Rendering.TerminalAttributes;
using TerminalCapabilities = Terminal.Capabilities.TerminalCapabilities;
```

Keep aliases that genuinely disambiguate UI types or imports and are unrelated
to the renamed Terminal declarations.

- [ ] **Step 3: Restore a green production build**

Run:

```bash
dotnet build SharpVision.slnx --no-restore
```

Expected: build succeeds with zero warnings and zero errors except for the
intentionally stale API snapshot test output, which is addressed in Task 4.

### Task 3: Migrate tests, examples, and documentation

**Files:**

- Modify: `tests/SharpVision.Terminal.Tests/**/*.cs`
- Modify: `tests/SharpVision.Tests/**/*.cs`
- Modify: `tests/SharpVision.Terminal.Probe/**/*.cs`
- Modify: `examples/**/*.cs`
- Modify: `docs/**/*.md`
- Modify: `src/SharpVision.Terminal/README.md`
- Modify: `AGENTS.md` only if examples still name a retired type

- [ ] **Step 1: Migrate test and example references**

Use the replacement types directly. Remove self-referential aliases such as
`InputDecoder = ...InputDecoder` only when the owning namespace is imported;
otherwise add the narrow namespace import rather than retaining an alias whose
left and right names are identical.

- [ ] **Step 2: Migrate reader-facing documentation and code samples**

Replace only references to the retired Terminal declarations. Do not rename
ordinary English nouns, UI `Text`, local `Canvas` concepts, or unrelated APIs.

- [ ] **Step 3: Run focused tests**

Run:

```bash
dotnet test --project tests/SharpVision.Terminal.Tests \
  --filter-namespace "SharpVision.Terminal.Tests*" \
  --minimum-expected-tests 1 --timeout 120s

dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests*" \
  --minimum-expected-tests 1 --timeout 180s
```

Expected: both suites pass with only the five documented platform skips in the
Terminal suite.

- [ ] **Step 4: Commit the coherent type migration**

```bash
git add src tests/SharpVision.Terminal.Tests tests/SharpVision.Tests \
  tests/SharpVision.Terminal.Probe examples docs AGENTS.md
git commit -m "refactor: clarify terminal type names"
```

### Task 4: Publish the alpha surface as 0.8

**Files:**

- Modify: `Directory.Build.props`
- Delete: `tests/SharpVision.Compatibility.Tests/Snapshots/0.2.0-alpha.1/`
- Delete: `tests/SharpVision.Compatibility.Tests/Snapshots/0.5.0-alpha.1/`
- Delete: every `tests/SharpVision.Compatibility.Tests/Snapshots/0.6.0-alpha.*/`
  directory
- Delete: `tests/SharpVision.Compatibility.Tests/Snapshots/0.7.0-alpha.1/`
- Create:
  `tests/SharpVision.Compatibility.Tests/Snapshots/0.8.0-alpha.1/SharpVision.Terminal.verified.txt`
- Create:
  `tests/SharpVision.Compatibility.Tests/Snapshots/0.8.0-alpha.1/SharpVision.verified.txt`

- [ ] **Step 1: Bump the version**

Change:

```xml
<OverallVersion>0.7.0-alpha.1</OverallVersion>
```

to:

```xml
<OverallVersion>0.8.0-alpha.1</OverallVersion>
```

- [ ] **Step 2: Delete every historical snapshot directory**

Resolve and review the exact directories under
`tests/SharpVision.Compatibility.Tests/Snapshots/`, then remove every directory
except the new `0.8.0-alpha.1` directory. Do not touch files outside that
snapshot root.

- [ ] **Step 3: Generate the expected API snapshots**

Run:

```bash
DiffEngine_Disabled=true dotnet test \
  --project tests/SharpVision.Compatibility.Tests \
  --minimum-expected-tests 1 --timeout 60s
```

Expected: the test fails because the two `0.8.0-alpha.1` baselines are missing
and writes `.received.txt` files.

- [ ] **Step 4: Review and approve both snapshots**

Confirm the Terminal snapshot contains the replacement type names and no retired
declarations. Confirm the SharpVision snapshot changes only where its public
surface references renamed Terminal types. Rename the reviewed `.received.txt`
files to `.verified.txt`.

- [ ] **Step 5: Re-run the compatibility gate**

Run the Task 4 Step 3 command again.

Expected: the compatibility test passes with exactly two verified snapshot files
under `0.8.0-alpha.1` and no historical version directory.

- [ ] **Step 6: Commit version and snapshot retention changes**

```bash
git add Directory.Build.props tests/SharpVision.Compatibility.Tests/Snapshots
git commit -m "chore: publish 0.8 alpha API surface"
```

### Task 5: Prove the complete migration

**Files:**

- Verify: all changed files

- [ ] **Step 1: Search for retired declaration paths and redundant aliases**

Search active source, tests, examples, and normative docs for every old fully
qualified name from Task 1. Inspect each remaining generic token match and
accept it only when it names an unrelated concept or appears in the
design/history record.

- [ ] **Step 2: Check file and diff integrity**

Run:

```bash
git diff --check HEAD~2
git status --short
```

Expected: no whitespace errors; only intentional uncommitted verification
artifacts, if any, are present.

- [ ] **Step 3: Run every repository gate**

Run:

```bash
make format
make lint
make build
make test
```

Expected: every command exits zero, builds report zero warnings and errors,
tests meet the configured discovery minimum, and Markdown/link validation
passes.

- [ ] **Step 4: Inspect the final commit range**

Run:

```bash
git status --short
git log --oneline --decorate -4
git diff --stat HEAD~2..HEAD
```

Expected: a clean worktree and two implementation commits following the design
and plan commits.
