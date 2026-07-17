# Framed Surfaces and Showcase Clarity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give `NavigationView`, `List`, and `Expander` square framed surfaces
by default, preserve transparent normal content with readable interaction
states, and remove accidental clipping and meaningless labels from the live
Showcase.

**Architecture:** Apply the frame through existing public `Control` properties
in each concrete constructor; do not add a style registry or wrapper control.
Keep normal item faces transparent and express hover through foreground/state
overlays while selection retains its opaque semantic colors. Treat Showcase
clarity as specimen geometry and copy work, backed by rendered-cell assertions
rather than a new documentation framework.

**Tech Stack:** .NET 10, C# 14, SharpVision retained controls and semantic cell
renderer, xUnit v3, Shouldly, Microsoft Testing Platform, Markdown documentation
gates.

---

## Tasks

### Task 1: Prove the framed-surface defaults

**Files:**

- Modify: `tests/SharpVision.Tests/Controls/ListTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/NavigationViewTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ExpanderTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ListSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/NavigationViewSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ExpanderSurfaceTests.cs`

- [ ] **Step 1: Add constructor-default tests**

Add one focused test per public control with this assertion shape:

```csharp
var control = new List();

control.BorderThickness.ShouldBe(new Thickness(1));
control.BorderGlyphs.ShouldBe(Glyphs.Light);
control.Background.ShouldBe(ThemeColor.From(ColorRole.Surface));
```

Use `NavigationView` and `Expander` in their respective fixtures. Keep each
named type in its existing file and use the established Arrange/Act/Assert
comments.

- [ ] **Step 2: Add mounted-cell proof**

Update or add focused surface tests that mount each control at a useful size and
assert:

```text
┌──────────┐
│ content  │
└──────────┘
```

Assert the four corner graphemes, a body cell resolved to `ColorRole.Surface`,
and content bounds inset by one cell. Add a two-by-two or similarly tiny case
proving geometry saturates without negative bounds.

- [ ] **Step 3: Prove item transparency and state contrast**

For `List`, render idle, hovered, and selected rows. Assert the idle row's cell
background equals the owning List surface, hover keeps a transparent background
while using an accent foreground, and selection uses
`SelectionBackground`/`SelectionForeground`.

For `NavigationView`, assert an idle item reveals the owner surface, pointer
hover uses the current marker plus accent foreground without painting a row
background, and a selected item retains the selection colors.

- [ ] **Step 4: Run the focused tests and confirm RED**

Run:

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ListTests" "*ListSurfaceTests" "*NavigationViewTests" \
  "*NavigationViewSurfaceTests" "*ExpanderTests" "*ExpanderSurfaceTests" \
  --timeout 60s
```

Expected: the new default-property and square-frame assertions fail because all
three controls currently default to zero border and transparent body; the List
hover assertion fails because it currently paints `Surface`.

### Task 2: Implement the concrete defaults and transparent interaction policy

**Files:**

- Modify: `src/SharpVision/Controls/List.cs`
- Modify: `src/SharpVision/Controls/NavigationView.cs`
- Modify: `src/SharpVision/Controls/Expander.cs`
- Modify: `src/SharpVision/Styling/ControlAppearanceDefaults.cs`
- Modify: `tests/SharpVision.Tests/Controls/ListTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ListSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/NavigationViewTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/NavigationViewSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ExpanderTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ExpanderSurfaceTests.cs`

- [ ] **Step 1: Set the three constructor defaults**

In each concrete constructor, set the ordinary inherited properties:

```csharp
BorderThickness = new Thickness(1);
BorderGlyphs = Glyphs.Light;
Background = ColorRole.Surface;
```

Do not add a shared base class, wrapper control, or type-style cascade. Callers
can override or reset the existing properties normally.

- [ ] **Step 2: Keep idle and hover rows transparent**

Change the `ListItem` pointer-over policy in `ControlAppearanceDefaults` from an
opaque `Surface` background to a transparent accent-foreground overlay. Give
`NavigationViewItem` the same explicit pointer-over foreground before the
generic non-focusable-control guard. Leave the existing selected overlay
untouched.

- [ ] **Step 3: Preserve unrelated test geometry explicitly**

Where a focused test is proving selection, input, mutation, or scrolling against
legacy unframed coordinates, set `BorderThickness = default` in that test
fixture. Update tests that are explicitly proving public default rendering to
include the square frame instead of opting out.

- [ ] **Step 4: Run the focused tests and confirm GREEN**

Run the Task 1 command again. Expected: all filtered tests pass with zero
warnings and the new mounted-cell assertions prove the public behavior.

### Task 3: Align normative control documentation and Showcase defaults

**Files:**

- Modify: `docs/controls/collections/list.md`
- Modify: `docs/controls/menus/navigation-view.md`
- Modify: `docs/controls/layout/expander.md`
- Modify: `src/SharpVision.Showcase/Panes/ListPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/NavigationViewPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/ExpanderPane.cs`
- Modify: `tests/SharpVision.Showcase.Tests/ShowcaseContentTests.cs`

- [ ] **Step 1: Specify exact defaults and rendering ownership**

In each control contract, state the one-cell `Glyphs.Light` border,
`ColorRole.Surface` body, transparent normal content, caller override behavior,
and selected/hover exception. Keep the rule in the individual control contract
and link to shared chrome rather than duplicating the box-model algorithm.

- [ ] **Step 2: Make Showcase examples demonstrate defaults**

Remove repeated square-frame configuration from normal List, NavigationView, and
Expander specimens. Replace the Expander page's generic “Bordered expander”
section with an explicitly named override such as “Rounded chrome override”;
keep `BorderGlyphs = Glyphs.Rounded` only there and explain that the constructor
default is square.

- [ ] **Step 3: Update Showcase structure assertions**

Revise `ShowcaseContentTests` section expectations to match the retained
Expander override section and assert that representative List, NavigationView,
and Expander specimens expose the new public defaults without per-example setup.

- [ ] **Step 4: Run focused documentation-page tests**

Run:

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*ShowcaseContentTests" "*NavigationViewPaneTests" "*LayoutPaneTests" \
  --timeout 60s
```

Expected: all selected tests pass.

### Task 4: Replace clipped and cryptic Showcase specimens

**Files:**

- Modify: `src/SharpVision.Showcase/Panes/DockPane.cs`
- Inspect: `src/SharpVision.Showcase/Panes/`
- Modify: `src/SharpVision.Showcase/Panes/StackPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/CanvasPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/GridPane.cs`
- Modify: `tests/SharpVision.Showcase.Tests/LayoutPaneTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/CanvasPaneTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs`
- Modify: `docs/architecture/showcase.md`

- [ ] **Step 1: Write a failing Dock rendered-label test**

Render `DockPane` wide enough to show its application-shell specimen and assert
that the final cell screen contains these complete semantic labels:

```csharp
screen.Text.ShouldContain("Explorer");
screen.Text.ShouldContain("Application header");
screen.Text.ShouldContain("Inspector");
screen.Text.ShouldContain("Status bar");
screen.Text.ShouldContain("Editor workspace");
screen.Text.ShouldNotContain("Exp\n");
screen.Text.ShouldNotContain("Insp\n");
```

Also assert each direct label's measured width fits its committed content
bounds. Run `*LayoutPaneTests` and confirm the new test fails on the current
seven/eight-cell side regions and two-row top/bottom regions.

- [ ] **Step 2: Repair the Dock application shell**

Increase the shell specimen's width and height, give top and bottom regions an
interior row, allocate side widths that fit `Explorer` and `Inspector`, and
rename `Header`, `Status`, and `Main` to the semantic labels above. Update the
example heading and description to use those same terms.

- [ ] **Step 3: Audit every live Showcase pane at wide layout**

Render every catalog page at 140 by 80 cells. Inspect fixed-size cards, headers,
buttons, table columns, and layout-region labels. For each accidental
truncation, either enlarge the specimen, remove decorative padding that consumes
the teaching area, or replace shorthand with a concise semantic label. Preserve
deliberate clipping only in examples whose heading/description explicitly
teaches clipping or constrained overflow.

- [ ] **Step 4: Add regression assertions for repaired Dock, Stack, Canvas, and
      Grid specimens**

Place behavior-specific assertions in the nearest pane fixture. Do not create a
brittle global snapshot. Assert the complete final-cell label and, where
geometry caused the defect, that the label's desired width fits its final
content bounds.

- [ ] **Step 5: Strengthen the Showcase contract**

Add one paragraph to `docs/architecture/showcase.md` requiring self-explanatory
visible specimen labels at the supported wide layout and requiring deliberate
clipping examples to identify the clipping behavior in adjacent prose.

- [ ] **Step 6: Run the Showcase proof**

Run:

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj --timeout 60s
```

Expected: all Showcase tests pass, including all-page rendering and Unicode
continuation validation.

### Task 5: Verify the repository gates

**Files:**

- Verify all intentional changes only; do not stage unrelated existing worktree
  edits.

- [ ] **Step 1: Check formatting and diff hygiene**

Run:

```bash
git diff --check
make format
```

Expected: no whitespace errors and no formatter changes left unaccounted for.

- [ ] **Step 2: Run lint and build**

Run:

```bash
make lint
make build
```

Expected: zero Markdown/link failures, zero compiler warnings, and zero build
errors.

- [ ] **Step 3: Run the full test gate**

Run:

```bash
make test
```

Expected: every configured test project passes at or above its minimum discovery
count.

- [ ] **Step 4: Review the final diff**

Confirm that only the approved defaults, interaction styling, control contracts,
Showcase specimens, and their tests changed. Preserve all unrelated user
modifications in the dirty worktree and do not commit overlapping user-owned
files without an explicit clean staging boundary.
