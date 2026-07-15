# Showcase Rewrite Implementation Plan

<!-- markdownlint-disable MD013 -->

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax.

**Goal:** Rewrite `SharpVision.Showcase` as the exemplar of the new API — each
pane a standalone `View` with `Build()`, real control names (no aliases),
example-rich pages with inline descriptions — and delete the leftover
metadata/templating machinery.

**Architecture:** Incremental migration that keeps the build green after every
task. A catalog shim (`Func<Control>`) lets `ShowcasePane`-based and
`View`-based panes coexist while panes are converted one batch at a time; once
all panes are `View`, the old base, the metadata structs, and the 22 aliases are
deleted and `Stack` is re-sealed. Panes rely on the inherited theme (no
per-specimen styling).

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly. `View`/`Build()` composition
(from the library work already merged).

**Base commit:** `a006c9c`. Branch: `codex/runtime-protocol-router` (shared; a
concurrent effort has NOT touched the showcase — low collision risk).

## Global Constraints

- .NET 10 / C# 14; file-scoped namespaces; `var` for locals; `using` after
  `namespace`.
- **One public/named type per file, named after the type (incl. test helpers)**
  — enforced by `make lint` (`scripts/validate-csharp-types.mjs`). No nested
  named types.
- No primary constructors / positional records. XML docs on every
  public/internal type + member and every thrown exception.
- Panes must NOT set per-specimen `Style=`; rely on the inherited theme cascade
  (`application.Theme`, default `Themes.Dark`). Set explicit colors/attributes
  ONLY when a pane deliberately demonstrates them.
- Use REAL control names (`Button`, `Text`, `Stack`, `Dock`, `RichText`, `Run`,
  `Border`, …). No `ControlXxx` aliases in any new/edited file.
- Quality gate before every commit: `make format && make lint && make build`,
  plus the task's focused tests. `make test` must stay at/above its configured
  minimum discovered-test count (deleting tests is fine as long as the run still
  meets the minimum).
- KNOWN pre-existing flaky test: `Integration/ScrollingTests` errors ~1 run in 3
  (unrelated). If a full run shows exactly one error in an unchanged file,
  re-run once.

## Invariants the rewrite MUST preserve (the test contract)

**Inventory (exact names + order)** — `Gallery.Pages` and the sidebar, asserted
by `GalleryTests._controls`, `GalleryRenderingTests`, `CellGeometryTests`,
`TmuxSmokeTests`:
`Border, Button, Canvas, CheckBox, ComboBox, Dock, FigletText, Grid, List, Menu, Overlay, Popup, RadioButton, RichText, ScrollBar, ScrollView, Shadow, Stack, Table, Text, TextInput, Window, Theming`.

**Gallery surface** (keep exactly): `public ControlBorder→Border Sidebar`,
`public new Control Content => _main.Content!` (its `.Parent` MUST be a
`ScrollView`), `public string SelectedPage`, `internal int SelectedIndex`,
`internal IReadOnlyList<string> Pages`,
`internal IReadOnlyList<NavigationItem> Navigation`,
`internal void Select(int)`, `internal bool FocusSelected(FocusManager)`,
`internal static View CreatePage(int)` (retyped from `ShowcasePane`), keyboard
nav (Up/Down/Left/Right/Tab/Shift+Tab/Home/End/PageUp/PageDown), theme
Light/Dark buttons, `OnAttach` sets `Themes.Dark`, `OnStarted` focuses
selection.

**Per-page invariants** (each page's content tree must satisfy these — ported
from the existing pane into the new `View`):

| Page       | Must contain / assert                                                                                                                                                                                                                                                                                                                                                     |
| ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| (every)    | a `RichText`; the page name as text (≥80×24); a control whose `GetType().Name == pageName`; the literal `"Overview"`                                                                                                                                                                                                                                                      |
| Button     | `Text` starting `"Activation log: Keyboard"` after Enter on focused enabled `Button`; specimens with `ShadowMode.Composite`, `!HasShadow`, `ShadowMode.BlockGlyph && ShadowGlyph==Rune('░')`; hover Foreground `Indexed(14)`, focused Attributes `Underline`, focused Background `Indexed(0)`, focused-not-hovered Foreground `Indexed(15)`                               |
| Canvas     | text "Fixed placement","Percentage placement","Edge constraints","Layering and clipping","fixed 2,1","50%,50%","Right 2 / Bottom 1"; a `Border` whose child is `Text{"Right 2 / Bottom 1"}` fully on-screen at 120×80                                                                                                                                                     |
| Window     | text "Apply"/"Cancel"; ≥4 `Window`s; one `Glyphs.Paired && TitlePlacement==Center`; a `Window` titled `"Project settings"` height>10 with exactly 2 action `Button`s centered ±1 above the bottom border                                                                                                                                                                  |
| List       | enabled `List` `Background==Color.Indexed(0)`; cell at `(x, y+1)` `Background==Color.Indexed(4)`; set `SelectedIndex=2` → `Text` starting `"Selected item: Gamma"` (items include "Gamma" at index 2)                                                                                                                                                                     |
| RichText   | `Run`s covering Bold, Dim, Italic, Underline, Blink, Reverse, Strike, Hidden, RapidBlink, Overline; a curly-underline run `UnderlineColor==Color.Indexed(11)`; a combined `Bold\|Underline\|Italic`; a `Button` content `"Append a Run"` `HorizontalAlignment.Left`, `Bounds.Width==DesiredSize.Width`, `<40`; activating it → `Text` starting `"Activity log: Keyboard"` |
| Shadow     | text "Composite stage","Block glyph stage","░"                                                                                                                                                                                                                                                                                                                            |
| Text       | text "Cell geometry specimen","orphan","Cells: unavailable","Uneven pixel pointer grid" (backed by `PointerProbe`); this is page index 19 (19 Downs from Border)                                                                                                                                                                                                          |
| TextInput  | a `TextInput` `AcceptsReturn && Height==Length.Cells(3)` (wheel scrolls editor not page); a single-line editor accepts typed text                                                                                                                                                                                                                                         |
| FigletText | `TextInput` text `"SharpVision"`; `FigletText` content `"SharpVision"`; `ComboBox` with selected item `"Standard"`                                                                                                                                                                                                                                                        |
| ScrollBar  | dragging the horizontal `ScrollBar` thumb to max → `Text` exactly `"Thumb value: 100"`                                                                                                                                                                                                                                                                                    |
| Theming    | page tree contains a `ShowcasePanel` (satisfies the `GetType().Name=="Theming"` special case)                                                                                                                                                                                                                                                                             |

**Deletions:** `InteractionDescription.cs`, `PropertyDescription.cs`,
`Panes/ShowcasePane.cs`, tests `ShowcasePaneTests.cs`,
`PropertyDescriptionTests.cs`, and
`GalleryTests.Properties_WhenCatalogLoads_...`.

---

### Task 1: Catalog shim + remove metadata-coupled tests

Let `View`-based and `ShowcasePane`-based panes coexist during migration, and
drop the tests coupled to the doomed structs/base.

**Files:**

- Modify: `src/SharpVision.Showcase/Gallery.cs` (`Catalog`, `CreatePage`,
  `Select`)
- Modify: `tests/SharpVision.Showcase.Tests/GalleryTests.cs`
- Delete: `tests/SharpVision.Showcase.Tests/PropertyDescriptionTests.cs`,
  `tests/SharpVision.Showcase.Tests/ShowcasePaneTests.cs`

**Interfaces produced:**
`Gallery.Catalog : (string Name, Func<Control> Create)[]`;
`internal static Control Gallery.CreatePage(int index)`.

- [ ] **Step 1: Retype the catalog to the common base**

In `Gallery.cs`, change
`private static readonly (string Name, Func<ShowcasePane> Create)[] Catalog` to
`Func<Control>`; change `internal static ShowcasePane CreatePage(int index)` to
`internal static Control CreatePage(int index)`. `Select` assigns
`_main.Content = Catalog[index].Create();` — `Content` already accepts
`Control`, so no other change. Leave the `static () => new XxxShowcasePane()`
entries as-is for now.

- [ ] **Step 2: Update GalleryTests to the `Control` type and drop the
      Properties test**

In `GalleryTests.cs`: change `CreatePage_WhenEveryPageBuildsTwice_...` to
`using Control first = Gallery.CreatePage(index);` / `Control second = ...`
(keep the fresh-instance, `Parent==null`, and `ContainsType(tree, pageName)`
assertions). DELETE the test
`Properties_WhenCatalogLoads_DescribeMeaningfulControlAttributes` entirely (it
reads `pane.Properties`).

- [ ] **Step 3: Delete the metadata-coupled test files**

```bash
git rm tests/SharpVision.Showcase.Tests/PropertyDescriptionTests.cs tests/SharpVision.Showcase.Tests/ShowcasePaneTests.cs
```

- [ ] **Step 4: Verify + commit**

Run:
`dotnet build src/SharpVision.Showcase/SharpVision.Showcase.csproj -clp:ErrorsOnly -nologo`
→ succeeds. Run:
`dotnet test --project tests/SharpVision.Showcase.Tests --filter-class "*GalleryTests" --timeout 180s`
→ passes.

```bash
git add src/SharpVision.Showcase/Gallery.cs tests/SharpVision.Showcase.Tests/GalleryTests.cs
git commit -m "refactor(showcase): catalog shim (Func<Control>); drop metadata-coupled tests"
```

---

### Task 2: The lean doc helper (`Doc`)

Replace the reliance on `ShowcasePane`'s chrome with small, optional, composable
helpers panes call. This is the "description above each example block" pattern
the redesign wants.

**Files:**

- Create: `src/SharpVision.Showcase/Panes/Doc.cs`
- Test: none (exercised via panes; covered by rendering tests)

**Interfaces produced (used by every pane):**

- `static Stack Doc.Page(string name, string overview, params Control[] sections)`
  — builds the page root: a heading (`name` bold + `"Overview"` label +
  `overview` summary, word-wrapped `RichText`) followed by the sections,
  `Padding(1)`, `Spacing 1`.
- `static Control Doc.Example(string heading, string description, Control specimen)`
  — a `Stack` of a word-wrapped `RichText` (heading bold, newline, `description`
  dim) above `specimen`.
- `static Border Doc.Card(Control child)` — rounded bordered card.
- `static Stack Doc.Row(params Control[] children)` /
  `Doc.Column(params Control[] children)`.

- [ ] **Step 1: Create `Doc.cs`** (complete code)

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Terminal.Rendering;
using SharpVision.Text;

/// <summary>Small composable helpers for building example-rich showcase pages.</summary>
internal static class Doc
{
    /// <summary>Builds a page root: a heading with an Overview summary, then the given sections.</summary>
    /// <param name="name">The exact control/page name shown as the heading.</param>
    /// <param name="overview">The one- or two-sentence overview shown under the heading.</param>
    /// <param name="sections">The example/section controls, in display order.</param>
    /// <returns>A vertically stacked page root.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="overview"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="sections"/> is null.</exception>
    internal static Stack Page(string name, string overview, params Control[] sections)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(overview);
        ArgumentNullException.ThrowIfNull(sections);

        RichText heading = new() { Wrapping = Wrapping.Word };
        heading.Inlines.Add(new Run(name) { Attributes = Attributes.Bold });
        heading.Inlines.Add(new LineBreak());
        heading.Inlines.Add(new Run("Overview") { Attributes = Attributes.Bold });
        heading.Inlines.Add(new LineBreak());
        heading.Inlines.Add(new Run(overview));

        Stack page = new() { Padding = new Thickness(1), Spacing = 1 };
        page.Children.Add(heading);

        foreach (Control section in sections)
        {
            page.Children.Add(section);
        }

        return page;
    }

    /// <summary>Builds one example block: a bold heading and dim description above a live specimen.</summary>
    /// <param name="heading">The example heading.</param>
    /// <param name="description">The prose describing what the specimen demonstrates.</param>
    /// <param name="specimen">The live control specimen.</param>
    /// <returns>A vertically stacked example block.</returns>
    /// <exception cref="ArgumentException"><paramref name="heading"/> or <paramref name="description"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="specimen"/> is null.</exception>
    internal static Control Example(string heading, string description, Control specimen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(specimen);

        RichText text = new() { Wrapping = Wrapping.Word };
        text.Inlines.Add(new Run(heading) { Attributes = Attributes.Bold });
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(new Run(description) { Attributes = Attributes.Dim });

        Stack block = new() { Spacing = 1 };
        block.Children.Add(text);
        block.Children.Add(specimen);
        return block;
    }

    /// <summary>Wraps a specimen in a rounded bordered card.</summary>
    /// <param name="child">The specimen to frame.</param>
    /// <returns>A bordered card.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="child"/> is null.</exception>
    internal static Border Card(Control child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return new Border
        {
            Child = child,
            BorderThickness = new Thickness(1),
            Glyphs = Glyphs.Rounded,
            Padding = new Thickness(1, 0),
        };
    }

    /// <summary>Stacks children horizontally with standard spacing.</summary>
    /// <param name="children">The children in order.</param>
    /// <returns>A horizontal stack.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="children"/> is null.</exception>
    internal static Stack Row(params Control[] children)
    {
        ArgumentNullException.ThrowIfNull(children);
        Stack row = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (Control child in children)
        {
            row.Children.Add(child);
        }

        return row;
    }

    /// <summary>Stacks children vertically with standard spacing.</summary>
    /// <param name="children">The children in order.</param>
    /// <returns>A vertical stack.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="children"/> is null.</exception>
    internal static Stack Column(params Control[] children)
    {
        ArgumentNullException.ThrowIfNull(children);
        Stack column = new() { Spacing = 1 };
        foreach (Control child in children)
        {
            column.Children.Add(child);
        }

        return column;
    }
}
```

- [ ] **Step 2: Verify + commit**

Run:
`dotnet build src/SharpVision.Showcase/SharpVision.Showcase.csproj -clp:ErrorsOnly -nologo`
→ succeeds.

```bash
git add src/SharpVision.Showcase/Panes/Doc.cs
git commit -m "feat(showcase): add lean Doc helper for example-rich pages"
```

---

### Task 3: Exemplar pane — `ButtonPane : View` (the pattern for all panes)

Convert the first pane fully; this is the template later panes follow. Rename
`ButtonShowcasePane` → `ButtonPane`.

**Files:**

- Create: `src/SharpVision.Showcase/Panes/ButtonPane.cs`
- Delete: `src/SharpVision.Showcase/Panes/ButtonShowcasePane.cs`
- Modify: `src/SharpVision.Showcase/Gallery.cs` (catalog entry),
  `tests/SharpVision.Showcase.Tests/GalleryInteractionTests.cs` (2
  `new ButtonShowcasePane()` → `new ButtonPane()`)

**Interfaces produced:** `internal sealed class ButtonPane : View`,
`internal const string ButtonPane.Title = "Button"`.

- [ ] **Step 1: Write `ButtonPane.cs`** (complete code)

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Documents the Button control with live, themed activation specimens.</summary>
internal sealed class ButtonPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Button";

    /// <inheritdoc/>
    protected override Control Build()
    {
        Text status = new("Activation log: waiting");
        Button primary = new() { Content = new Text("Click or press Enter") };
        primary.Click += (_, eventArgs) => status.Content = $"Activation log: {eventArgs.Cause}";

        Button dialogDefault = new() { Content = new Text("OK"), IsDefault = true };
        Button dialogCancel = new() { Content = new Text("Cancel"), IsCancel = true };

        Button composite = new() { Content = new Text("Composite shadow") };
        Button blockShadow = new()
        {
            Content = new Text("Block glyph shadow"),
            ShadowMode = ShadowMode.BlockGlyph,
            ShadowGlyph = new Rune('░'),
        };
        Button flat = new() { Content = new Text("Flat, no shadow"), HasShadow = false };
        Button disabled = new() { Content = new Text("Disabled"), IsEnabled = false };

        return Doc.Page(
            Title,
            "Activates one semantic action through keyboard, pointer, programmatic, or command paths.",
            Doc.Example(
                "Primary action",
                "A raised, bordered surface responds to hover, focus, press, Enter, Space, and a primary pointer click. Activation reports its cause below.",
                Doc.Column(primary, status)),
            Doc.Example(
                "Dialog command roles",
                "IsDefault activates on the owning window's Enter fallback; IsCancel on Escape.",
                Doc.Row(dialogDefault, dialogCancel)),
            Doc.Example(
                "Shadow styles",
                "Buttons carry a composite shadow by default, a Turbo Vision block-glyph shadow, or none.",
                Doc.Row(composite, blockShadow, flat)),
            Doc.Example(
                "Disabled",
                "A disabled button is skipped by focus and ignores activation.",
                disabled));
    }
}
```

- [ ] **Step 2: Delete the old pane and rewire the catalog + tests**

```bash
git rm src/SharpVision.Showcase/Panes/ButtonShowcasePane.cs
```

In `Gallery.cs` change the Button catalog entry to
`(ButtonPane.Title, static () => new ButtonPane())`. In
`GalleryInteractionTests.cs` (2 sites, ~lines 341, 375) change
`new ButtonShowcasePane()` → `new ButtonPane()`.

- [ ] **Step 3: Verify + commit**

Run:
`dotnet build src/SharpVision.Showcase/SharpVision.Showcase.csproj -clp:ErrorsOnly -nologo`
→ succeeds. Run:
`dotnet test --project tests/SharpVision.Showcase.Tests --filter-class "*GalleryInteractionTests" --timeout 240s`
→ the Button interaction tests pass (activation-log, hover/focus colors, shadow
modes). Run:
`dotnet test --project tests/SharpVision.Showcase.Tests --filter-class "*GalleryRenderingTests" --timeout 240s`
→ Button specimen assertions pass.

```bash
git add src/SharpVision.Showcase/Panes/ButtonPane.cs src/SharpVision.Showcase/Gallery.cs tests/SharpVision.Showcase.Tests/GalleryInteractionTests.cs
git commit -m "refactor(showcase): ButtonPane : View (example-rich, real names)"
```

---

### Tasks 4–9: Convert the remaining 22 panes to `View`, in batches

Each task converts a batch of panes using the same transformation as Task 3:
`XxxShowcasePane : ShowcasePane` → `XxxPane : View` in a new file `XxxPane.cs`;
write an example-rich `Build()` returning
`Doc.Page(name, overview, ...Doc.Example(...))`; use REAL control names (no
aliases); rely on the theme (no `Style=`); PRESERVE every invariant for that
page from the contract table above (port the specimens from the existing
`XxxShowcasePane` — read it first). Delete the old file, update the
`Gallery.Catalog` entry to `(XxxPane.Title, static () => new XxxPane())`. Keep
`internal const string Title` on each pane matching its inventory name exactly.

For each pane the executing subagent MUST: (1) read the existing
`Panes/XxxShowcasePane.cs` to recover its specimen content, (2) restructure into
`Doc.Page`/`Doc.Example` blocks with richer inline descriptions and MORE
examples than a bare port where natural, (3) preserve the contract-table
invariants for that page verbatim (exact strings, colors, control types), (4)
verify the page's focused tests, (5) keep the build green (catalog shim allows
mixed panes).

Some panes still need their specialized specimen controls/helpers that live
outside the deleted base: `Panes/CanvasSample.cs`, `Panes/ShowcasePanel.cs`,
`Panes/LabelPlacement.cs`, `Controls/PointerProbe.cs`,
`Controls/NavigationItem.cs` — these are NOT part of `ShowcasePane` and stay;
reuse them. If a pane used a `PaneSupport` helper (e.g. `CanvasSection`,
`ShadowStage`, `CanvasStage`, `AddGrid`, `AddAttributeLine`), either call the
surviving helper or inline an equivalent with `Doc.*`.

Batches (group by test weight; heavy pages get their own attention):

- [ ] **Task 4 (light):** Border, CheckBox, ComboBox, Dock — straightforward
      specimens; each page must contain its named control and "Overview". Commit
      `refactor(showcase): convert Border/CheckBox/ComboBox/Dock panes to View`.
- [ ] **Task 5 (light):** Grid, Menu, Overlay, Popup, RadioButton — preserve
      named controls; Popup/Overlay/Menu keep their interaction specimens.
      Commit.
- [ ] **Task 6 (medium):** Stack, Table, ScrollView, ScrollBar — ScrollBar MUST
      keep the draggable horizontal thumb + live label producing `Text` exactly
      `"Thumb value: 100"` at max. Commit.
- [ ] **Task 7 (heavy — Canvas/Shadow):** Canvas (preserve
      "Fixed/Percentage/Edge/Layering" sections, "fixed 2,1", "50%,50%", "Right
      2 / Bottom 1" in a `Border`→`Text`; reuse `CanvasSample`) and Shadow
      ("Composite stage","Block glyph stage","░"). Commit.
- [ ] **Task 8 (heavy — text family):** Text (Cell geometry specimen: "Cell
      geometry specimen","orphan","Cells: unavailable","Uneven pixel pointer
      grid"; reuse `PointerProbe`), TextInput (multiline
      `AcceptsReturn && Height==Cells(3)` + single-line editor), FigletText
      (`TextInput`+`FigletText` both "SharpVision"; `ComboBox` selected
      "Standard" font picker), RichText (all attributes incl. curly underline
      `Indexed(11)` + combined `Bold|Underline|Italic`; "Append a Run" button,
      left-aligned, width<40, → `Text` "Activity log: Keyboard"). Commit.
- [ ] **Task 9 (heavy — List/Window/Theming):** List (enabled `List`
      `Background==Indexed(0)`, selected-cell `Indexed(4)`, item "Gamma" at
      index 2 → `"Selected item: Gamma"`), Window ("Apply"/"Cancel"; ≥4 Windows;
      one `Glyphs.Paired && Center`; "Project settings" dialog height>10 with 2
      centered action buttons), Theming (page contains a `ShowcasePanel`; reuse
      it). Commit.

Each task's verification: `dotnet build src/SharpVision.Showcase/...` green;
`dotnet test --project tests/SharpVision.Showcase.Tests --filter-class "*GalleryRenderingTests" --timeout 240s`
and `"*GalleryInteractionTests"` and `"*CellGeometryTests"` and
`"*ThemeGalleryTests"` as relevant to the batch → the converted pages'
assertions pass. Stage only the batch's pane files + `Gallery.cs`.

---

### Task 10: Delete the base, structs, and aliases; re-seal `Stack`

All panes are now `View`. Remove the leftovers and finish the naming cleanup.

**Files:**

- Delete: `src/SharpVision.Showcase/Panes/ShowcasePane.cs`,
  `src/SharpVision.Showcase/InteractionDescription.cs`,
  `src/SharpVision.Showcase/PropertyDescription.cs`
- Modify: `src/SharpVision.Showcase/Gallery.cs` (retype catalog to `Func<View>`,
  `CreatePage`→`View`; convert Gallery's own body to real control names),
  `src/SharpVision.Showcase/GlobalUsings.cs` (delete the 22 aliases), any
  remaining alias users
- Modify: `src/SharpVision/Controls/Stack.cs` (add `sealed`)
- Modify: `tests/SharpVision.Showcase.Tests/GalleryTests.cs` (`CreatePage` type
  → `View`)

- [ ] **Step 1: Delete the leftovers and retype the catalog**

```bash
git rm src/SharpVision.Showcase/Panes/ShowcasePane.cs src/SharpVision.Showcase/InteractionDescription.cs src/SharpVision.Showcase/PropertyDescription.cs
```

In `Gallery.cs` change `Catalog` to `(string Name, Func<View> Create)[]` and
`CreatePage`/`Select` to `View`. In `GalleryTests.cs` change
`Control first = Gallery.CreatePage(...)` → `View first = ...`.

- [ ] **Step 2: Delete the aliases and convert remaining alias users to real
      names**

Empty the alias block in `src/SharpVision.Showcase/GlobalUsings.cs` (keep
`System`, `System.Text`, `SharpVision.Controls`, `SharpVision.Layout`, and any
real global usings). Then build and fix every resulting error by replacing
`ControlXxx` with the real name (`ControlScrollView`→`ScrollView`, etc.) in the
offending files — primarily `Gallery.cs`, and `Panes/CanvasSample.cs`,
`Panes/ShowcasePanel.cs`, `Controls/NavigationItem.cs`,
`Controls/PointerProbe.cs` if they used aliases.

Run to confirm none remain:

```bash
grep -rn "Control\(Border\|Button\|Canvas\|CheckBox\|ComboBox\|Dock\|FigletText\|Grid\|List\|Menu\|Overlay\|Popup\|RadioButton\|RichText\|Run\|ScrollBar\|ScrollView\|Shadow\|Stack\|Table\|Text\|TextInput\|Window\)\b" src/SharpVision.Showcase --include=*.cs | grep -v /obj/
```

Expected: no output.

- [ ] **Step 3: Re-seal `Stack`**

In `src/SharpVision/Controls/Stack.cs`, change `public class Stack: Container` →
`public sealed class Stack: Container` (keep the `CA1711` suppression).

- [ ] **Step 4: Verify + commit**

Run: `make build` → 0 warnings/0 errors (whole solution: no pane subclasses
`Stack` now, so re-sealing compiles). Run:
`dotnet test --project tests/SharpVision.Showcase.Tests --timeout 300s` →
passes. Run: (guard)
`grep -rn "ShowcasePane\|InteractionDescription\|PropertyDescription" src/SharpVision.Showcase --include=*.cs | grep -v /obj/`
→ no output.

```bash
git add -A src/SharpVision.Showcase src/SharpVision/Controls/Stack.cs tests/SharpVision.Showcase.Tests/GalleryTests.cs
git commit -m "refactor(showcase): delete ShowcasePane base + metadata structs + aliases; seal Stack"
```

---

### Task 11: Test + doc reconciliation and final gate

Update the tests coupled to the OLD chrome and fix the pre-existing lint
violations; sync docs.

**Files:**

- Modify: `tests/SharpVision.Showcase.Tests/GalleryTests.cs`,
  `GalleryRenderingTests.cs`, `TmuxSmokeTests.cs` (only if a preserved string
  moved)
- Rename: `tests/SharpVision.Showcase.Tests/ShowcaseStartupOptionsTests.cs` →
  match its type `StartupOptionsTests` (or rename the type to
  `ShowcaseStartupOptionsTests`) to satisfy one-type-per-file
- Modify: `docs/architecture/showcase.md`

- [ ] **Step 1: Reconcile chrome-string assertions**

The rewrite keeps `"Overview"` and each control's name but DROPS the
`"Practical recipe"` narrative and the Property/Interaction tables. Update:

- `GalleryTests.CreatePage_WhenEachPageIsSelected_IncludesWrappedPracticalRecipe`
  → rename/retarget to assert each page's heading `RichText` contains a `Run`
  `"Overview"` and is `Wrapping.Word` (the new invariant). Keep
  `CreatePage_WhenEachPageIsSelected_ContainsRichTextDescription`.
- `GalleryRenderingTests` — remove `"Practical recipe"` from the text assertions
  in `Render_WhenViewportIsTypical` and `Render_WhenEveryPageUsesViewport_...`;
  keep `"Overview"`, `"SHARP VISION"`, `"Components"`, the page name,
  `HasNonDefaultColor`, ScrollView extent, `ValidateContinuations`, and all
  specimen assertions.
- `TmuxSmokeTests` waits for `"Overview"` (preserved) — no change unless the
  19-Downs→Text mapping shifted; confirm Text is still index 19.

- [ ] **Step 2: Fix the pre-existing one-type-per-file violation**

Rename `tests/SharpVision.Showcase.Tests/ShowcaseStartupOptionsTests.cs` so the
file name matches its single type (`StartupOptionsTests`), or rename the type to
`ShowcaseStartupOptionsTests`. (`ShowcasePaneTests.cs`, the other violation, was
deleted in Task 1.)

- [ ] **Step 3: Update `docs/architecture/showcase.md`**

Reflect the new structure: each pane is a `View` with `Build()` returning
`Doc.Page(...)` of `Doc.Example(...)` blocks; no `ShowcasePane` base, no
metadata structs, no aliases; panes rely on the theme. Keep the
responsive-behavior and capability sections accurate.

- [ ] **Step 4: FULL GATE + commit**

Run: `make format && make lint && make build && make test`. Expected: format
clean; **`lint` fully green** (the 2 pre-existing showcase violations are now
resolved — `ShowcasePaneTests` deleted, `ShowcaseStartupOptionsTests` renamed);
build 0/0; `make test` green (allow one re-run for the unrelated
`ScrollingTests` flake). Confirm `Gallery.Pages` still equals the 23-name
inventory in order.

```bash
git add -A tests/SharpVision.Showcase.Tests docs/architecture/showcase.md
git commit -m "test/docs(showcase): reconcile chrome assertions, fix one-type lint, sync docs"
```

---

## Self-Review

**Spec coverage** (design spec `2026-07-13-...` decisions 3,4,5 + this rewrite):

- Delete 22 aliases → Task 10 Step 2. ✓
- Each pane a `View` with `Build()` → Tasks 3–9. ✓
- Drop templated base + mandatory metadata → Tasks 1 (tests), 10 (base/structs).
  ✓
- Re-seal `Stack` → Task 10 Step 3. ✓
- Example-rich pages + inline descriptions, no
  `InteractionDescription`/`PropertyDescription` → `Doc` helper (Task 2) +
  per-pane `Doc.Example` blocks (Tasks 3–9) + deletions (Tasks 1, 10). ✓
- Keep the inventory, Gallery API, `Content.Parent==ScrollView`, per-page
  specimen invariants → contract table + per-task acceptance. ✓
- Green after every task via the `Func<Control>` shim → Task 1. ✓

**Placeholder scan:** Infra + exemplar tasks carry complete code; the batch
tasks (4–9) are guided transformations of existing panes with explicit per-page
invariants (the source panes supply the specimen code) — this is a migration,
not greenfield, so "read the existing pane and restructure preserving these
invariants" is the correct instruction, not a placeholder.

**Type consistency:** `Doc.Page`/`Doc.Example`/`Doc.Card`/`Doc.Row`/`Doc.Column`
signatures are used consistently in Task 3 and referenced by Tasks 4–9.
`Gallery.Catalog` evolves `Func<ShowcasePane>` → `Func<Control>` (Task 1) →
`Func<View>` (Task 10); `CreatePage` return type tracks it; `GalleryTests`
updated at both points. Pane class names `XxxPane` with
`internal const string Title` matching the inventory.
