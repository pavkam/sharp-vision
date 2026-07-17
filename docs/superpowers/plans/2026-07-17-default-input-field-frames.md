# Default Input Field Frames Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give `TextInput` and the closed `ComboBox` field a discoverable
one-cell light border by default while retaining explicit borderless overrides.

**Architecture:** Set the existing intrinsic chrome properties in each concrete
constructor. Keep `Control`, content primitives, layout containers, appearance
state resolution, and the independently framed ComboBox popup unchanged.

**Tech Stack:** .NET 10, C# 14, SharpVision retained controls and cell renderer,
xUnit v3, Shouldly, Microsoft Testing Platform, Markdown documentation gates.

---

## Tasks

### Task 1: Prove the constructor and rendering defaults

**Files:**

- Modify: `tests/SharpVision.Tests/Controls/TextInputTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ComboBoxTests.cs`

- [ ] **Step 1: Add failing constructor assertions**

Extend the existing default/property tests with:

```csharp
control.BorderThickness.ShouldBe(new Thickness(1));
control.BorderGlyphs.ShouldBe(Glyphs.Light);
```

Add an equivalent `Properties_WhenConstructed_UsesLightFieldBorder` test for
`ComboBox`.

- [ ] **Step 2: Add exact default-frame cell proof**

Render each control at a three-cell height and assert `┌`, `┐`, `└`, and `┘` at
the corners. Assert TextInput text/caret and the ComboBox selected label and
drop-down glyph occupy the one-cell-inset content row.

- [ ] **Step 3: Run the focused tests and confirm RED**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*TextInputTests" "*ComboBoxTests" --timeout 60s
```

Expected: the new constructor assertions fail because both controls currently
default to zero border.

### Task 2: Implement the concrete defaults and preserve legacy fixtures

**Files:**

- Modify: `src/SharpVision/Controls/TextInput.cs`
- Modify: `src/SharpVision/Controls/ComboBox.cs`
- Modify: affected focused tests that deliberately require a one-row border box

- [ ] **Step 1: Set the minimal constructor defaults**

Add to both concrete constructors:

```csharp
BorderThickness = new Thickness(1);
BorderGlyphs = Glyphs.Light;
```

- [ ] **Step 2: Preserve tests whose subject is not default chrome**

For fixtures that explicitly set `Height = Length.Cells(1)` to test input,
selection, or composition at legacy coordinates, add
`BorderThickness = default`. Do not opt out any default-rendering proof or
Showcase specimen.

- [ ] **Step 3: Run focused tests and confirm GREEN**

Run the Task 1 command. Expected: all filtered tests pass with zero warnings.

### Task 3: Align normative docs and runnable Showcase evidence

**Files:**

- Modify: `docs/controls/input/text-input.md`
- Modify: `docs/controls/input/combo-box.md`
- Modify: `src/SharpVision.Showcase/Panes/ComboBoxPane.cs`
- Modify: `tests/SharpVision.Showcase.Tests/SelectionPaneTests.cs`

- [ ] **Step 1: Specify the exact field defaults**

State in both control contracts that the field defaults to
`BorderThickness = new Thickness(1)` and `BorderGlyphs = Glyphs.Light`, uses the
base content-box inset, and opts out through `BorderThickness = default`.

- [ ] **Step 2: Make the ComboBox comparison intentional**

Keep the normal specimen on constructor defaults. Change the second specimen
from an explicitly bordered field to an explicitly borderless field, and update
its heading and explanation accordingly.

- [ ] **Step 3: Update Showcase assertions**

Change `ComboBox_WhenPageBuilds_ShowsDefaultAndBorderedFields` to assert one
light default field and one explicit borderless field. Add a TextInput page
assertion proving representative editors use the light default frame.

- [ ] **Step 4: Run focused Showcase tests**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*SelectionPaneTests" --timeout 60s
```

Expected: all selected tests pass.

### Task 4: Verify the repository

- [ ] **Step 1: Run focused control and Showcase tests again**
- [ ] **Step 2: Run `make format`**
- [ ] **Step 3: Run `make lint`**
- [ ] **Step 4: Run `make build`**
- [ ] **Step 5: Run `make test`**
- [ ] **Step 6: Inspect the final diff and stage only intentional files**
