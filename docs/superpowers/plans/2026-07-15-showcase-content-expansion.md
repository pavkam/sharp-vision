# Showcase Content Expansion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn every SharpVision showcase page into a progressive, example-rich
user guide with multiple sections, live application-shaped specimens, concise C#
excerpts where useful, and substantially deeper Canvas coverage.

**Architecture:** Preserve the existing `Gallery → View pane → Doc` composition
and public-control-only rule. Extend `Doc` with section grouping and optional
source excerpts, then expand the 19 existing panes without adding production
APIs. Dedicated custom-drawing controls keep the Canvas page readable while
semantic screen and interaction tests prove each page through the public runtime
path.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly, Microsoft Testing Platform,
SharpVision semantic `Frame`/`Canvas`, and the existing fake-terminal
Application harness.

**Design source:**
`docs/superpowers/specs/2026-07-15-showcase-content-expansion-design.md`

---

## Baseline and target

The current catalog has 19 pages and 62 `Doc.Example` calls. The target is not a
mechanical example quota; it is complete user journeys. Every page must have at
least four visible section groups, at least one compact C# excerpt, a live
subject control, one application-shaped composition, and one relevant boundary
or state example. Canvas must cover both child layout and custom cell drawing in
separate named sections.

Existing examples remain when they are useful. Expansion should reorganize and
clarify them rather than duplicating them under slightly different headings.

## File map

### Shared showcase composition

- Modify `src/SharpVision.Showcase/Doc.cs`: section groups and optional source
  excerpts.
- Create `src/SharpVision.Showcase/ShowcaseCommand.cs`: ordinary reusable
  `ICommand` specimen for Button command/parameter and `CanExecute` behavior.

### Existing pages

- Modify every file under `src/SharpVision.Showcase/Panes/*Pane.cs` registered
  by `Gallery`: Button, Canvas, CheckBox, ComboBox, Dock, FigletText, Grid,
  List, Menu, Overlay, Popup, RadioButton, ScrollBar, Stack, Table, Text,
  TextInput, Window, and Theming.

### Canvas drawing specimens

- Keep and narrow `src/SharpVision.Showcase/Panes/CanvasSample.cs` to a focused
  line/box specimen, or rename it to `CanvasLineStylesSample.cs` if the rename
  can be performed without an intermediate duplicate type.
- Create `src/SharpVision.Showcase/Panes/CanvasShadeSample.cs`.
- Create `src/SharpVision.Showcase/Panes/CanvasUnicodeSample.cs`.
- Create `src/SharpVision.Showcase/Panes/CanvasChartSample.cs`.
- Create `src/SharpVision.Showcase/Panes/CanvasPointerSample.cs`.

Each named type lives in its exact matching file. Do not place helper enums,
records, or event argument types inside these controls.

### Tests

- Create `tests/SharpVision.Showcase.Tests/ControlTree.cs` for reusable public
  tree traversal in showcase tests.
- Create `tests/SharpVision.Showcase.Tests/ShowcaseContentTests.cs` for the
  per-page section and excerpt contract.
- Create `tests/SharpVision.Showcase.Tests/InputPaneTests.cs`.
- Create `tests/SharpVision.Showcase.Tests/SelectionPaneTests.cs`.
- Create `tests/SharpVision.Showcase.Tests/LayoutPaneTests.cs`.
- Create `tests/SharpVision.Showcase.Tests/DataPaneTests.cs`.
- Create `tests/SharpVision.Showcase.Tests/LayerPaneTests.cs`.
- Create `tests/SharpVision.Showcase.Tests/DisplayPaneTests.cs`.
- Create `tests/SharpVision.Showcase.Tests/CanvasPaneTests.cs`.
- Modify `tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs` only for
  assertions that genuinely span the whole gallery.
- Modify `tests/SharpVision.Showcase.Tests/GalleryInteractionTests.cs` only for
  end-to-end terminal-input paths shared across pages.

### Normative documentation

- Modify `docs/architecture/showcase.md`.
- Modify `docs/testing/showcase.md`.
- Add precise inline links to
  `docs/architecture/rendering-pipeline.md#rendering-pipeline-contract` and
  `docs/concepts/unicode-cell-geometry.md#unicode-cell-geometry-contract` where
  Canvas drawing and Unicode examples depend on those contracts.

## Global implementation rules

- Every new example uses public APIs available to an ordinary application.
- Every example description states what to try and what observable result to
  expect; it does not restate a property name as prose.
- Source excerpts are short enough to remain readable in the documentation
  column. Escape all dynamic text with `Text.Escape`.
- Interactive examples expose output through a status label or event log.
- Examples use the active theme unless the point of the example is an explicit
  style override.
- New tests are watched failing for the expected missing heading, state, cell,
  or event before implementation.
- Tests assert semantic cells, state, events, focus, or final bounds. Snapshot
  appearance alone is insufficient.
- Each task ends with the focused tests and `git diff --check`. Commits are
  listed as implementation checkpoints; stage only the files named by that task.

---

### Task 1: Add section and source-excerpt composition

**Files:**

- Modify: `src/SharpVision.Showcase/Doc.cs`
- Test: `tests/SharpVision.Showcase.Tests/ShowcaseContentTests.cs`

- [ ] **Step 1: Write failing tests for section and excerpt composition**

Create `ShowcaseContentTests` with a focused `Doc` test that requires:

- a wrapped section heading and description;
- child examples retained in insertion order;
- a source excerpt labeled `C#`;
- literal `<`, `>`, backslash, and generic syntax rendered as text rather than
  interpreted as markup; and
- blank headings/descriptions/source rejected before any child is parented.

Use this public-observable shape:

```csharp
var specimen = new Button { Content = new Text("Run") };
using var section = Doc.Section(
    "Start here",
    "Activate the command and observe the result.",
    Doc.Example(
        "One command",
        "Enter, Space, or a click invokes it once.",
        specimen,
        "var values = new List<string>();\nvar button = new Button();"));

new Engine().Layout(section, new Size(48, 20));

ControlTree.Text(section).ShouldContain("Start here");
ControlTree.Text(section).ShouldContain("C#");
ControlTree.Text(section).ShouldContain("List<string>");
ControlTree.FindAll<Button>(section).ShouldContain(specimen);
```

- [ ] **Step 2: Run the focused test and confirm the missing API failure**

Run:

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*ShowcaseContentTests" --timeout 60s
```

Expected: compilation fails because `Doc.Section` and the four-argument
`Doc.Example` overload do not exist.

- [ ] **Step 3: Implement `Doc.Section` and optional source excerpts**

Preserve the existing three-argument calls while adding this surface:

```csharp
internal static Control Section(
    string heading,
    string description,
    params Control[] examples)

internal static Control Example(
    string heading,
    string description,
    Control specimen,
    string? source = null)
```

`Section` builds a vertical `Stack` with a bold wrapped heading, a dim wrapped
orientation paragraph, and the supplied examples. `Example` appends a framed
source block only when `source` is non-null. The source block uses
`Text.Escape(source)`, `Overflow.WrapAnywhere`, intrinsic border properties on a
plain `Dock`, and no fixed width. Validate the complete candidate inputs before
parenting any control.

- [ ] **Step 4: Run focused tests and formatting**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*ShowcaseContentTests" --timeout 60s
dotnet format SharpVision.slnx --verify-no-changes
git diff --check
```

Expected: the content tests pass and formatting reports no changes required.

- [ ] **Step 5: Commit the composition primitive**

```bash
git add src/SharpVision.Showcase/Doc.cs \
  tests/SharpVision.Showcase.Tests/ShowcaseContentTests.cs
git commit -m "feat(showcase): add progressive documentation sections"
```

---

### Task 2: Add the per-page content contract

**Files:**

- Create: `tests/SharpVision.Showcase.Tests/ControlTree.cs`
- Modify: `tests/SharpVision.Showcase.Tests/ShowcaseContentTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryTests.cs`

- [ ] **Step 1: Extract reusable control-tree traversal**

Create one internal static `ControlTree` type with generic `Find`, `FindAll`,
and visible marked-text aggregation. Move only duplicated traversal from
existing showcase tests; do not add reflection or reach through private fields.

```csharp
internal static IReadOnlyList<T> FindAll<T>(Control root) where T : Control
internal static T? Find<T>(Control root, Func<T, bool> predicate) where T : Control
internal static string Text(Control root)
```

- [ ] **Step 2: Write the failing page-catalog theory**

For each exact `Gallery.Pages` entry, require four or more expected section
headings from the design catalog, at least one `C#` source block, wrapped prose,
and the matching subject control. Store expectations as fields on
`ShowcaseContentTests`; do not declare a nested record or test-case type.

The first required headings are:

| Page        | Required headings before page-specific tests                                       |
| ----------- | ---------------------------------------------------------------------------------- |
| Button      | Start here; Commands; Window roles; Chrome and states                              |
| Canvas      | Canvas layout; Constraints; Drawing fundamentals; Useful custom drawing            |
| CheckBox    | Two-state choice; Three-state policy; Marks; Form recipe                           |
| ComboBox    | Start here; Commit versus dismiss; Long choices; Constrained placement             |
| Dock        | Application shell; Order and spacing; Sizing from the remainder; Constrained space |
| FigletText  | Live editor; Font comparison; Layout options; Large output                         |
| Grid        | Track fundamentals; Percentage and limits; Responsive form; Constrained space      |
| List        | Single selection; Selection modes; Templates; Long data                            |
| Menu        | Command menu; Menu bar; Popup composition; Selection and invocation                |
| Overlay     | Layering; Stable ties; Pointer transparency; Clipping                              |
| Popup       | Anchored menu; Placement; Fallback and clamp; Lifecycle                            |
| RadioButton | Named group; Arrow traversal; Unnamed scope; Events                                |
| ScrollBar   | Range anatomy; Input parity; Live range; Tiny rails                                |
| Stack       | Orientation; Mixed sizing; Visibility; Constrained space                           |
| Table       | Column sizing; Interactive cells; Dynamic rows; Boundary states                    |
| Text        | Safe content; Markup; Overflow; Unicode                                            |
| TextInput   | Editing and submission; Selection; Clipboard and history; Multiline                |
| Window      | Frame and title; Shadows; Default and cancel; Boundaries                           |
| Theming     | Application theme; Catalog; Visual states; Third-party controls                    |

- [ ] **Step 3: Run and confirm all 19 page rows fail for missing sections**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-method "*Content_WhenEveryPageBuilds*" --timeout 60s
```

Expected: failures list missing section headings on the current sparse pages.

- [ ] **Step 4: Keep the test failing while page tasks proceed**

This catalog test becomes green only after Task 9. During Tasks 3-9, run each
page’s filtered theory row or its domain test class rather than accepting a
false all-green claim.

- [ ] **Step 5: Commit the test harness with the first expanded page task**

Do not commit a knowingly red default test suite alone. Stage these test files
with Task 3 after the first catalog subset has its filter passing.

---

### Task 3: Expand Button, CheckBox, and RadioButton

**Files:**

- Create: `src/SharpVision.Showcase/ShowcaseCommand.cs`
- Modify: `src/SharpVision.Showcase/Panes/ButtonPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/CheckBoxPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/RadioButtonPane.cs`
- Create: `tests/SharpVision.Showcase.Tests/InputPaneTests.cs`
- Include the uncommitted test-harness files from Task 2.

- [ ] **Step 1: Write focused failing public-behavior tests**

Require Button command/parameter execution, live `CanExecute` disabling,
Programmatic `PerformClick`, real Window Enter/Escape fallback, and all three
shadow states. Require CheckBox two-/three-state cycles, custom `Marks`,
state-specific-before-general event output, programmatic normalization, and a
disabled form option. Require RadioButton named and unnamed scopes, empty
initial selection, arrow traversal with disabled skipping, regrouping, and
Unchecked → Checked → SelectionChanged output.

- [ ] **Step 2: Run the focused tests and confirm missing specimens**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*InputPaneTests" --timeout 60s
```

Expected: tests fail because the expanded sections and live controls are absent.

- [ ] **Step 3: Implement the Button sections**

Build these exact section groups with live status output:

- `Start here`: click/Enter/Space/pointer and activation cause.
- `Commands`: `ShowcaseCommand`, borrowed parameter, and a CheckBox that raises
  `CanExecuteChanged` to enable/disable execution.
- `Window roles`: a small Window containing default Apply and cancel Cancel
  buttons plus an action log.
- `Chrome and states`: composite, block-glyph, flat, alternate glyphs, padding,
  and disabled variants.
- `Programmatic use`: a separate trigger calls `PerformClick()` and proves the
  Programmatic cause.

`ShowcaseCommand` must be an ordinary internal `ICommand` implementation with
validated execute/can-execute delegates and a public-to-showcase method that
raises `CanExecuteChanged`; it lives in its own exact file.

- [ ] **Step 4: Implement the CheckBox sections**

Build `Two-state choice`, `Three-state policy`, `Marks`, `Events`, and
`Form recipe`. Use a visible event log for `Checked`/`Unchecked`/`Indeterminate`
followed by `StateChanged`, one `PerformToggle` trigger, a custom printable
one-cell `Marks` value, and a settings card with an unavailable retained state.

- [ ] **Step 5: Implement the RadioButton sections**

Build `Named group`, `Arrow traversal`, `Unnamed scope`, `No initial selection`,
`Programmatic regrouping`, and `Events`. Keep groups in separate owned
containers so unnamed scope is real, not simulated with unique names.

- [ ] **Step 6: Verify and commit**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*InputPaneTests" --timeout 60s
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-method "*Content_WhenEveryPageBuilds*" --timeout 60s
git diff --check
```

Expected: InputPane tests pass; the catalog theory now passes Button, CheckBox,
and RadioButton rows and continues to report only unexpanded pages.

```bash
git add src/SharpVision.Showcase/ShowcaseCommand.cs \
  src/SharpVision.Showcase/Panes/ButtonPane.cs \
  src/SharpVision.Showcase/Panes/CheckBoxPane.cs \
  src/SharpVision.Showcase/Panes/RadioButtonPane.cs \
  tests/SharpVision.Showcase.Tests/ControlTree.cs \
  tests/SharpVision.Showcase.Tests/ShowcaseContentTests.cs \
  tests/SharpVision.Showcase.Tests/GalleryTests.cs \
  tests/SharpVision.Showcase.Tests/InputPaneTests.cs
git commit -m "feat(showcase): deepen command and choice examples"
```

---

### Task 4: Expand ComboBox, List, and TextInput

**Files:**

- Modify: `src/SharpVision.Showcase/Panes/ComboBoxPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/ListPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/TextInputPane.cs`
- Create: `tests/SharpVision.Showcase.Tests/SelectionPaneTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryInteractionTests.cs`

- [ ] **Step 1: Write failing selection/editing tests**

Drive ComboBox commit, Escape dismissal, clear-to-`-1`, long-list scrolling, and
edge fallback placement. Drive List None/Single/Multiple modes, Control toggle,
Shift range, custom variable-height templates, Home/End/Page scrolling, item
replacement, and unavailable-item skipping. Drive TextInput submission,
selection/caret reporting, undo/redo, read-only/password/max-length policy,
cancellable changes, multiline offsets, and one complete ZWJ deletion.

- [ ] **Step 2: Confirm the focused tests fail**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*SelectionPaneTests" --timeout 60s
```

Expected: failures identify absent specimens and status output.

- [ ] **Step 3: Implement ComboBox sections**

Build `Start here`, `Commit versus dismiss`, `Long choices`, `No selection`,
`Constrained placement`, and `Unavailable state`. The constrained stage must be
large enough to show the popup above the field when below does not fit, and its
status must report the committed item rather than only the highlighted row.

- [ ] **Step 4: Implement List sections**

Build `Single selection`, `Selection modes`, `Templates`, `Long data`,
`Snapshot replacement`, and `Unavailable items`. Use ordinary detached controls
from `ItemTemplate`; never reuse a template child. Report `ActiveIndex`, sorted
selected indexes/items, invocation cause, and `VerticalOffset` where relevant.

- [ ] **Step 5: Implement TextInput sections**

Build `Editing and submission`, `Selection`, `Clipboard and history`,
`Policies`, `Events`, `Multiline`, and `Unicode boundary`. Do not display
password source text in any status, diagnostic, or source excerpt. Clipboard
guidance must use the public TextInput/terminal services path and must not claim
host clipboard availability when the capability is absent.

- [ ] **Step 6: Add one end-to-end terminal path per page family**

In `GalleryInteractionTests`, keep the existing TextInput wheel proof and add:

- ComboBox open → arrow → Enter commit through decoded terminal input;
- List Control/Shift selection through routed keys; and
- TextInput select/undo or submission through decoded input.

- [ ] **Step 7: Verify and commit**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*SelectionPaneTests|*GalleryInteractionTests" --timeout 90s
git diff --check
```

Expected: all selection/editing tests pass with no runtime failure.

```bash
git add src/SharpVision.Showcase/Panes/ComboBoxPane.cs \
  src/SharpVision.Showcase/Panes/ListPane.cs \
  src/SharpVision.Showcase/Panes/TextInputPane.cs \
  tests/SharpVision.Showcase.Tests/SelectionPaneTests.cs \
  tests/SharpVision.Showcase.Tests/GalleryInteractionTests.cs
git commit -m "feat(showcase): add selection and editing recipes"
```

---

### Task 5: Expand Stack, Dock, and Grid

**Files:**

- Modify: `src/SharpVision.Showcase/Panes/StackPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/DockPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/GridPane.cs`
- Create: `tests/SharpVision.Showcase.Tests/LayoutPaneTests.cs`

- [ ] **Step 1: Write failing layout and screen tests**

Assert final child bounds for vertical/horizontal Stack, mixed lengths, margins
versus spacing, reverse focus order, hidden/collapsed differences, and tiny
shrink. Assert Dock application-shell sides, insertion-order spacing,
percentage-of-remainder sizing, collapse/reclaim, and saturated tiny bounds.
Assert Grid fixed/auto/percent/star/min/max tracks, implicit tracks, spans,
wrapped form remeasure, spacing saturation, and non-negative tiny bounds.

- [ ] **Step 2: Confirm focused failures**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*LayoutPaneTests" --timeout 60s
```

- [ ] **Step 3: Implement Stack sections**

Build `Orientation`, `Mixed sizing`, `Spacing and margins`, `Reverse`,
`Visibility`, `Constrained space`, and `Action-bar recipe`. The action bar uses
a proportional spacer and remains usable when the page narrows.

- [ ] **Step 4: Implement Dock sections**

Build `Application shell`, `Order and spacing`, `Sizing from the remainder`,
`Collapse and fill`, and `Constrained space`. Use recognizable header/sidebar/
status/inspector/main labels and a real toggle that collapses the sidebar.

- [ ] **Step 5: Implement Grid sections**

Build `Track fundamentals`, `Percentage and limits`, `Spans`, `Implicit grid`,
`Responsive form`, and `Constrained space`. The form contains labels,
TextInputs, wrapped validation text, and actions so finite-width remeasure is
visible.

- [ ] **Step 6: Verify and commit**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*LayoutPaneTests" --timeout 60s
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*GalleryRenderingTests" --timeout 60s
git diff --check
```

```bash
git add src/SharpVision.Showcase/Panes/StackPane.cs \
  src/SharpVision.Showcase/Panes/DockPane.cs \
  src/SharpVision.Showcase/Panes/GridPane.cs \
  tests/SharpVision.Showcase.Tests/LayoutPaneTests.cs
git commit -m "feat(showcase): add responsive layout recipes"
```

---

### Task 6: Expand Menu, ScrollBar, and Table

**Files:**

- Modify: `src/SharpVision.Showcase/Panes/MenuPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/ScrollBarPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/TablePane.cs`
- Create: `tests/SharpVision.Showcase.Tests/DataPaneTests.cs`

- [ ] **Step 1: Write failing data/navigation tests**

Require Menu separator/check/radio transitions, horizontal traversal, Popup
composition, distinct selection/invocation status, spacing, and unavailable-item
skipping. Require ScrollBar anatomy labels, typed input causes, range/viewport
updates, full/thin/custom chrome, one-/two-/three-cell fallback, and endpoint
wheel bubbling. Require Table all four column modes, header/grid variants,
interactive focusable cells, add/remove row ownership, Unicode/wrapping, a
header-only state, and safe tiny rendering.

- [ ] **Step 2: Confirm focused failures**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*DataPaneTests" --timeout 60s
```

- [ ] **Step 3: Implement Menu sections**

Build `Command menu`, `Menu bar`, `Popup composition`,
`Selection and invocation`, and `Spacing and unavailable items`. Keep Popup
non-modal and restore focus using its public lifecycle.

- [ ] **Step 4: Implement ScrollBar sections**

Build `Range anatomy`, `Input parity`, `Chrome`, `Live range`, `Tiny rails`, and
`Nested behavior`. Status text reports `PreviousValue`, `Value`, and typed
`Cause`; live controls mutate `Maximum` and `ViewportSize` only within validated
ranges.

- [ ] **Step 5: Implement Table sections**

Build `Column sizing`, `Header and grid chrome`, `Interactive cells`,
`Dynamic rows`, `Responsive text`, and `Boundary states`. Row replacement must
use new detached controls and visibly update the row count.

- [ ] **Step 6: Verify and commit**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*DataPaneTests|*GalleryInteractionTests" --timeout 90s
git diff --check
```

```bash
git add src/SharpVision.Showcase/Panes/MenuPane.cs \
  src/SharpVision.Showcase/Panes/ScrollBarPane.cs \
  src/SharpVision.Showcase/Panes/TablePane.cs \
  tests/SharpVision.Showcase.Tests/DataPaneTests.cs
git commit -m "feat(showcase): deepen navigation and data examples"
```

---

### Task 7: Expand Overlay, Popup, and Window

**Files:**

- Modify: `src/SharpVision.Showcase/Panes/OverlayPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/PopupPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/WindowPane.cs`
- Create: `tests/SharpVision.Showcase.Tests/LayerPaneTests.cs`

- [ ] **Step 1: Write failing layering/lifecycle tests**

Require Overlay stable equal-z order, live z changes, pointer-transparent
decoration, alignment/percentage sizing, clipping, and focus-order independence.
Require Popup preferred/fallback/clamped bounds, `Closing` before collapse,
`Closed` after collapse, Escape focus restoration, styled surfaces, and resize
repositioning. Require Window glyph/title/shadow variants, real default/cancel
fallback, surface styles, Canvas/Overlay composition, long-title clipping, and
tiny-bound safety.

- [ ] **Step 2: Confirm focused failures**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*LayerPaneTests" --timeout 60s
```

- [ ] **Step 3: Implement Overlay sections**

Build `Layering`, `Stable ties`, `Pointer transparency`, `Alignment and sizing`,
`Clipping`, and `Notification composition`. The transparent layer must render
while a lower Button receives the actual click.

- [ ] **Step 4: Implement Popup sections**

Build `Anchored menu`, `Placement`, `Fallback and clamp`, `Lifecycle`,
`Surface style`, and `Resize`. Every open/close action reports public lifecycle
state and focus owner; do not simulate placement with manually positioned
children.

- [ ] **Step 5: Implement Window sections**

Build `Frame and title`, `Shadows`, `Default and cancel`, `Surface style`,
`Composition`, and `Boundaries`. The default/cancel specimen must receive real
routed Enter/Escape keys inside the Window.

- [ ] **Step 6: Verify and commit**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*LayerPaneTests" --timeout 60s
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*GalleryRenderingTests" --timeout 60s
git diff --check
```

```bash
git add src/SharpVision.Showcase/Panes/OverlayPane.cs \
  src/SharpVision.Showcase/Panes/PopupPane.cs \
  src/SharpVision.Showcase/Panes/WindowPane.cs \
  tests/SharpVision.Showcase.Tests/LayerPaneTests.cs
git commit -m "feat(showcase): add layering and window lifecycle recipes"
```

---

### Task 8: Expand Text, FigletText, and Theming

**Files:**

- Modify: `src/SharpVision.Showcase/Panes/TextPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/FigletTextPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/ThemingPane.cs`
- Create: `tests/SharpVision.Showcase.Tests/DisplayPaneTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/ThemeGalleryTests.cs`

- [ ] **Step 1: Write failing display/theme tests**

Require Text escaping and malformed recovery, all overflow modes over identical
Unicode input, alignment and `Lines` metrics after resize, semantic colors,
ambiguous-width comparison, tabs/line endings, and live mutation. Require
FigletText editor, three-font comparison, layout option comparison, inherited
versus explicit style, scrolling of large output, and Unicode fallback. Require
Theming catalog metadata, live semantic roles, type/local precedence, visual
state matrix, intrinsic border/shadow chrome, and custom `StyleProperty` use.

- [ ] **Step 2: Confirm focused failures**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*DisplayPaneTests|*ThemeGalleryTests" --timeout 60s
```

- [ ] **Step 3: Reorganize and extend Text**

Build `Safe content`, `Markup`, `Overflow`, `Alignment and lines`, `Unicode`,
`Tabs and logical lines`, and `Live mutation`. Split the current attribute wall
into readable specimens while retaining coverage for every supported tag, typed
underline, underline color, semantic role, and OSC 8 link.

- [ ] **Step 4: Expand FigletText**

Build `Live editor`, `Font comparison`, `Layout options`, `Style`,
`Large output`, and `Fallback`. Use audited embedded fonts loaded lazily from
`FigletCatalog.Default`; do not expand the archive wholesale.

- [ ] **Step 5: Expand Theming**

Build `Application theme`, `Catalog`, `Type and local styles`, `Visual states`,
`Shared chrome`, and `Third-party controls`. Read theme catalog metadata through
the public catalog surface and keep the sidebar picker as the application-wide
mutation path.

- [ ] **Step 6: Verify and commit**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*DisplayPaneTests|*ThemeGalleryTests|*ThemeSwatchLiveThemeTests" \
  --timeout 90s
git diff --check
```

```bash
git add src/SharpVision.Showcase/Panes/TextPane.cs \
  src/SharpVision.Showcase/Panes/FigletTextPane.cs \
  src/SharpVision.Showcase/Panes/ThemingPane.cs \
  tests/SharpVision.Showcase.Tests/DisplayPaneTests.cs \
  tests/SharpVision.Showcase.Tests/ThemeGalleryTests.cs
git commit -m "feat(showcase): expand text font and theme guidance"
```

---

### Task 9: Give Canvas dedicated layout and drawing galleries

**Files:**

- Modify: `src/SharpVision.Showcase/Panes/CanvasPane.cs`
- Modify or replace: `src/SharpVision.Showcase/Panes/CanvasSample.cs`
- Create: `src/SharpVision.Showcase/Panes/CanvasShadeSample.cs`
- Create: `src/SharpVision.Showcase/Panes/CanvasUnicodeSample.cs`
- Create: `src/SharpVision.Showcase/Panes/CanvasChartSample.cs`
- Create: `src/SharpVision.Showcase/Panes/CanvasPointerSample.cs`
- Create: `tests/SharpVision.Showcase.Tests/CanvasPaneTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/CellGeometryTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryInteractionTests.cs`

- [ ] **Step 1: Write failing layout-Canvas tests**

Assert fixed, percentage, right/bottom, opposing-edge automatic stretch,
explicit-size left/top precedence, intrinsic fixed-child union, negative origin,
clipping, stable insertion order, and pointer-transparent top-child input. Run
each representative layout at two sizes and assert final public bounds.

- [ ] **Step 2: Write failing `TerminalCanvas` screen tests**

Require exact semantic cells for:

- every supported line/box style represented by the sample;
- Light/Medium/Dark shade and quadrant combinations;
- opaque fill and explicit clear regions;
- combining text, CJK, emoji ZWJ, and clip-edge wide-cell repair;
- a deterministic chart whose bars and labels resize without drawing outside
  bounds; and
- a pointer marker whose status reports routed cell and optional exact pixel
  coordinates.

Apply each custom control to a complete `Frame`, validate continuation
ownership, and compare its final screen with the expected semantic model. Do not
assert raw struct bytes.

- [ ] **Step 3: Confirm focused failures**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*CanvasPaneTests|*CellGeometryTests" --timeout 60s
```

- [ ] **Step 4: Rebuild Canvas page information architecture**

Add an opening wrapped note:

> `Canvas` positions child controls. Custom controls receive `TerminalCanvas` in
> `OnRender` to draw semantic cells. Neither API emits terminal escape
> sequences.

Build these layout sections:

- `Canvas layout`: fixed, percentage, and trailing-edge placement.
- `Constraints`: opposing-edge automatic stretch and explicit-size precedence.
- `Intrinsic and constrained size`: finite union, negative placement, and tiny
  clipping.
- `Layering and input`: insertion-order overlap and pointer transparency with a
  visible hit log.

Build these drawing sections:

- `Drawing fundamentals`: lines, boxes, fill, clear, and styles.
- `Shade and quadrants`: separated from topology so each glyph family is
  legible.
- `Unicode drawing`: complete grapheme/cell ownership at clip edges.
- `Useful custom drawing`: responsive deterministic chart/dashboard.
- `Pointer-aware drawing`: routed pointer marker and coordinate readout.

At least three drawing examples include compact
`OnRender(TerminalCanvas canvas)` excerpts using only the methods the specimen
actually calls.

- [ ] **Step 5: Implement focused custom-drawing controls**

Each sample owns one responsibility and uses `ContentBounds` or its committed
`Bounds` consistently. Guard tiny sizes before offset arithmetic. Use
`TerminalStyle`/`CellStyle` values, `Rune`, and semantic drawing primitives;
never build ANSI strings or allocate one child per cell. The chart dataset is a
static deterministic integer array so tests can model its output exactly.

- [ ] **Step 6: Add end-to-end pointer and resize proof**

Through `Application` and `FakeTerminal`, select Canvas, target the pointer
sample with SGR cell input and one pixel-aware resize, and assert the visible
marker/readout and valid final frame. Preserve the existing tmux hover/click
coverage.

- [ ] **Step 7: Verify Canvas and complete the catalog contract**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*CanvasPaneTests|*CellGeometryTests|*GalleryInteractionTests" \
  --timeout 90s
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*ShowcaseContentTests" --timeout 60s
git diff --check
```

Expected: Canvas tests pass, all 19 content-catalog rows pass, and every custom
drawing frame has valid wide-cell continuation ownership.

- [ ] **Step 8: Commit Canvas expansion**

```bash
git add src/SharpVision.Showcase/Panes/CanvasPane.cs \
  src/SharpVision.Showcase/Panes/CanvasSample.cs \
  src/SharpVision.Showcase/Panes/CanvasShadeSample.cs \
  src/SharpVision.Showcase/Panes/CanvasUnicodeSample.cs \
  src/SharpVision.Showcase/Panes/CanvasChartSample.cs \
  src/SharpVision.Showcase/Panes/CanvasPointerSample.cs \
  tests/SharpVision.Showcase.Tests/CanvasPaneTests.cs \
  tests/SharpVision.Showcase.Tests/CellGeometryTests.cs \
  tests/SharpVision.Showcase.Tests/GalleryInteractionTests.cs
git commit -m "feat(showcase): add comprehensive Canvas galleries"
```

If `CanvasSample.cs` is renamed, stage the actual rename instead of both paths.

---

### Task 10: Align showcase specifications and executable proof

**Files:**

- Modify: `docs/architecture/showcase.md`
- Modify: `docs/testing/showcase.md`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/TmuxSmokeTests.cs` only when an
  existing selector or visible label changed.

- [ ] **Step 1: Update the showcase architecture contract**

Specify the progressive section model, optional compact source excerpts,
application-shaped recipes, and the requirement that every material claim sits
next to a live public-API specimen. Replace the old “one or more examples”
language with the per-page content contract without copying every control’s
normative API.

Add the explicit distinction between layout `Canvas` and `TerminalCanvas`, with
inline links to the control contract, rendering pipeline, and Unicode geometry
sections where those rules matter.

- [ ] **Step 2: Update the showcase testing contract**

Document the section-heading catalog, source-excerpt escaping, representative
interaction paths, dedicated Canvas semantic-screen models, and the continued
30x8/80x24/140x40 page matrix. State that tmux is supplemental and screenshots
are not behavioral proof.

- [ ] **Step 3: Update broad rendering assertions**

Keep `GalleryRenderingTests` focused on inventory-wide properties: every page
renders at all three sizes, headings remain visible at normal sizes, the main
viewport scrolls, semantic colors are non-default, and continuations are valid.
Page-specific details remain in the new domain test classes.

- [ ] **Step 4: Run documentation and showcase gates**

```bash
npm run format
npm run lint:markdown
npm run lint:links
npm run test:docs
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --timeout 180s
git diff --check
```

Expected: no Markdown or link failures, all showcase tests pass, and no
whitespace errors remain.

- [ ] **Step 5: Commit documentation alignment**

```bash
git add docs/architecture/showcase.md docs/testing/showcase.md \
  tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs \
  tests/SharpVision.Showcase.Tests/TmuxSmokeTests.cs
git commit -m "docs(showcase): specify progressive example coverage"
```

Do not stage `TmuxSmokeTests.cs` when it did not need a selector update.

---

### Task 11: Run repository quality gates and audit the result

**Files:**

- No planned source changes. Fix only failures caused by this feature and stage
  those fixes with the task that owns them.

- [ ] **Step 1: Run the required repository gates**

```bash
make format
make lint
make build
make test
```

Expected: zero formatting changes after the format command, zero lint or link
failures, zero build warnings/errors, and all discovered tests at or above the
configured minimum.

- [ ] **Step 2: Run the completion audit against the design catalog**

For each of the 19 pages, inspect the current pane, its domain test, and its
rendered 80x24 screen and confirm:

- four or more useful section groups;
- at least one compact source excerpt;
- the exact application-shaped composition named in the design;
- the relevant interaction/state or layout proof;
- the relevant boundary/Unicode/responsive proof; and
- no private API, reflection, raw terminal bytes, or showcase-only production
  shortcut.

For Canvas, separately confirm all four layout groups and all five drawing
groups, plus exact cell, pointer, resize, and continuation evidence.

- [ ] **Step 3: Inspect the final diff and repository state**

```bash
git status --short
git diff --check
git diff --stat
git log --oneline -8
```

Expected: only intentional showcase, showcase-test, and showcase-documentation
files are changed or committed; unrelated user work remains untouched.

- [ ] **Step 4: Record final evidence**

Report the exact `make` gate outcomes, total discovered showcase tests, the
final page/section inventory, and the Canvas specimen inventory. Do not claim
completion from a narrow focused test.

## Self-review record

- **Spec coverage:** all 19 catalog pages have explicit extension content and a
  task; Canvas has separate layout and semantic drawing coverage.
- **Placeholders:** the plan contains no deferred implementation markers. Every
  task names files, required examples, observable tests, commands, and expected
  results.
- **Type consistency:** new named types are `ShowcaseCommand`, `ControlTree`,
  seven test classes, and focused Canvas sample controls, each assigned one
  exact file. The plan does not introduce `Border`, `Shadow`, or `ScrollView`
  types.
- **Architecture consistency:** panes remain mutable `View` objects built from
  public controls; custom drawing stays in `OnRender(TerminalCanvas)` and never
  emits terminal bytes.
