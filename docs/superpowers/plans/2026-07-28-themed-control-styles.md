# Themed Control Styles Implementation Plan

<!-- markdownlint-disable MD013 -->

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace independently mutable presentation properties with validated immutable control styles supplied automatically by Theme, while protecting raw chrome on specialized controls.

**Architecture:** Add complete per-control styles and partial Theme sets, then make the Theme loader publish complete defaults. Migrate each control to nullable local `Style` plus complete `ActualStyle`, make intrinsic chrome authoring protected, selectively republish raw chrome on structural hosts, and remove superseded alpha APIs without aliases.

**Tech Stack:** .NET 10, C# 14, System.Text.Json, xUnit v3, Shouldly, Microsoft Testing Platform, SharpVision `ComponentSurface`, PublicApiGenerator/Verify, Markdown/Prettier, tmux.

---

## File map

- Create `AppearanceProfileSet`, the six complete control styles, their partial `*StyleSet` values, and typed JSON definitions under the matching `src/SharpVision` namespaces.
- Modify `Theme`, `ThemeDefinition`, `ThemeStylesDefinition`, `Themes`, all bundled Theme JSON documents, and the existing Theme tests.
- Modify `Control`, `AppearanceResolver`, and `ControlAppearance` for style-owned profiles and protected chrome.
- Modify `Button`, `ScrollBar`, generated-scrollbar hosts, `CheckBox`, `RadioButton`, `Spinner`, and `ChaseIndicator`; delete superseded pattern/kind files.
- Modify Dock, Grid, Stack, Overlay, Window, and Popup to republish approved raw chrome.
- Add focused value, resolution, consumer, surface, and catalog tests.
- Update affected showcase panes, normative concept/control docs, and the reviewed public API snapshot.

## Task 1: Partial appearance profile foundation

**Files:**

- Create: `src/SharpVision/Styling/AppearanceProfileSet.cs`
- Create: `src/SharpVision/Styling/StyleResolution.cs`
- Modify: `src/SharpVision/Styling/ThemeProfileDefinition.cs`
- Test: `tests/SharpVision.Tests/Styling/AppearanceProfileSetTests.cs`

- [ ] **Step 1: Write failing composition tests**

Prove empty sets preserve a baseline, normal members overlay the complete normal appearance, and later state members overlay rather than replace inherited state members.

```csharp
[Fact]
public void Apply_WhenMembersArePartial_CompletesAgainstBaseline()
{
    var baseline = new ThemeProfile(
        AppearanceTestValues.Appearance(),
        pointerOver: new AppearanceSet(
            face: new FaceSet(foreground: ThemeColor.ActiveText)));
    var set = new AppearanceProfileSet(
        normal: new AppearanceSet(
            border: new BorderSet(glyphStyle: BorderGlyphStyle.Heavy)),
        pointerOver: new AppearanceSet(
            border: new BorderSet(foreground: ThemeColor.ActiveBorder)));

    var actual = StyleResolution.Apply(baseline, set);

    actual.Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
    actual.PointerOver.Face!.Value.Foreground.ShouldBe(ThemeColor.ActiveText);
    actual.PointerOver.Border!.Value.Foreground.ShouldBe(ThemeColor.ActiveBorder);
}
```

- [ ] **Step 2: Run the focused test and verify the compile failure**

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*AppearanceProfileSetTests" --timeout 60s
```

Expected: compilation fails because `AppearanceProfileSet` and `StyleResolution.Apply` do not exist.

- [ ] **Step 3: Implement the immutable partial profile and resolver**

Define nullable `AppearanceSet` members for Normal, PointerOver, FocusWithin, Focused, Current, Selected, Checked, Indeterminate, Pressed, and Disabled. Implement `Overlay` and `StyleResolution.Apply` with the existing `AppearanceSet.Overlay` and `ThemeAppearance.Apply` operations. Reuse the existing `ThemeProfileDefinition` as the JSON partial-profile shape; style definitions reference it rather than adding a duplicate DTO. Resolve its strings only after palette and semantic values are available.

```csharp
public readonly record struct AppearanceProfileSet
{
    public AppearanceProfileSet(
        AppearanceSet? normal = null,
        AppearanceSet? pointerOver = null,
        AppearanceSet? focusWithin = null,
        AppearanceSet? focused = null,
        AppearanceSet? current = null,
        AppearanceSet? selected = null,
        AppearanceSet? @checked = null,
        AppearanceSet? indeterminate = null,
        AppearanceSet? pressed = null,
        AppearanceSet? disabled = null)
    {
        Normal = normal;
        PointerOver = pointerOver;
        FocusWithin = focusWithin;
        Focused = focused;
        Current = current;
        Selected = selected;
        Checked = @checked;
        Indeterminate = indeterminate;
        Pressed = pressed;
        Disabled = disabled;
    }
}
```

- [ ] **Step 4: Run the focused tests and commit**

Run the Step 2 command. Expected: all tests pass.

```bash
git add src/SharpVision/Styling/AppearanceProfileSet.cs src/SharpVision/Styling/StyleResolution.cs src/SharpVision/Styling/ThemeProfileDefinition.cs tests/SharpVision.Tests/Styling/AppearanceProfileSetTests.cs
git commit -m "feat: add partial appearance profiles"
```

## Task 2: Button style value and invariants

**Files:**

- Create: `src/SharpVision/Controls/Input/ButtonStyle.cs`
- Create: `src/SharpVision/Controls/Input/ButtonStyleSet.cs`
- Create: `src/SharpVision/Styling/ButtonStyleDefinition.cs`
- Test: `tests/SharpVision.Tests/Controls/Input/ButtonStyleTests.cs`

- [ ] **Step 1: Write failing value and invariant tests**

Cover `default == Standard`, Filled padding, semantic equality, partial set application, and validation of combined states. A profile where hover enables a border and focus enables a shadow must fail because the combined state is invalid.

```csharp
var profile = new ThemeProfile(
    AppearanceTestValues.Appearance(
        border: AppearanceTestValues.Border(BorderSide.None),
        shadow: AppearanceTestValues.Shadow(visible: false)),
    pointerOver: new AppearanceSet(border: new BorderSet(sides: BorderSide.All)),
    focused: new AppearanceSet(shadow: new ShadowSet(isVisible: true)));

_ = Should.Throw<ArgumentException>(() =>
    new ButtonStyle(new Thickness(1, 0), profile));
```

- [ ] **Step 2: Run the focused tests and verify the compile failure**

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ButtonStyleTests" --timeout 60s
```

Expected: compilation fails because the style types do not exist.

- [ ] **Step 3: Implement ButtonStyle and ButtonStyleSet**

Use a `readonly struct` with explicit semantic `IEquatable<ButtonStyle>` implementation; make default resolve to Standard. Store internal padding and an immutable `ThemeProfile`. Equality compares resolved public members so default and an equivalent constructed value compare equally. Validate every combination of supported visual-state flags before committing fields.

```csharp
public readonly struct ButtonStyle: IEquatable<ButtonStyle>
{
    private readonly ThemeProfile? _appearance;
    private readonly Thickness? _padding;

    public ButtonStyle(Thickness padding, ThemeProfile appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        ValidateAppearance(appearance);
        _padding = padding;
        _appearance = appearance;
    }

    public static ButtonStyle Standard => default;
    public static ButtonStyle Filled { get; } = CreateFilled();
    public Thickness Padding =>
        _padding ?? new Thickness(horizontal: 1, vertical: 0);
    public ThemeProfile Appearance => _appearance ?? StandardAppearance;

    public bool Equals(ButtonStyle other) =>
        Padding == other.Padding &&
        StyleResolution.Equals(Appearance, other.Appearance);
}
```

`ButtonStyleSet.Apply` overlays padding and appearance independently onto a complete baseline and constructs a validated result.

- [ ] **Step 4: Run the focused tests and commit**

Run the Step 2 command. Expected: all tests pass.

```bash
git add src/SharpVision/Controls/Input/ButtonStyle.cs src/SharpVision/Controls/Input/ButtonStyleSet.cs src/SharpVision/Styling/ButtonStyleDefinition.cs tests/SharpVision.Tests/Controls/Input/ButtonStyleTests.cs
git commit -m "feat: define button styles"
```

## Task 3: Remaining complete style values

**Files:**

- Create: `src/SharpVision/Controls/ScrollBarStyle.cs`, `ScrollBarStyleSet.cs`, `ScrollBarGlyphs.cs`
- Create: `src/SharpVision/Controls/Input/CheckBoxStyle.cs`, `CheckBoxStyleSet.cs`, `RadioButtonStyle.cs`, `RadioButtonStyleSet.cs`, `RadioButtonGlyphs.cs`, `RadioButtonMarkStyle.cs`
- Create: `src/SharpVision/Controls/Display/SpinnerStyle.cs`, `SpinnerStyleSet.cs`, `ChaseIndicatorStyle.cs`, `ChaseIndicatorStyleSet.cs`
- Create: `src/SharpVision/Styling/ScrollBarStyleDefinition.cs`, `CheckBoxStyleDefinition.cs`, `RadioButtonStyleDefinition.cs`, `SpinnerStyleDefinition.cs`, `ChaseIndicatorStyleDefinition.cs`
- Test: `tests/SharpVision.Tests/Controls/Layout/ScrollBarStyleTests.cs`
- Test: `tests/SharpVision.Tests/Controls/Input/CheckBoxStyleTests.cs`, `RadioButtonStyleTests.cs`
- Test: `tests/SharpVision.Tests/Controls/Display/SpinnerStyleTests.cs`, `ChaseIndicatorStyleTests.cs`

- [ ] **Step 1: Write failing defaults, copy-safety, and validation tests**

```csharp
ScrollBarStyle.Default.Chrome.ShouldBe(ScrollBarChrome.Full);
ScrollBarStyle.Default.Fill.ShouldBe(ScrollBarFill.Block);
CheckBoxStyle.Default.MarkWidth.ShouldBe(3);
RadioButtonStyle.Parentheses.UncheckedText.ShouldBe("( )");
RadioButtonStyle.Parentheses.CheckedText.ShouldBe("(•)");
SpinnerStyle.Braille.Frames.Length.ShouldBe(10);
ChaseIndicatorStyle.Circle.Active.ShouldBe(new Rune('●'));
```

Also reject empty Spinner frames, invalid glyph widths, transparent paint foregrounds, and undefined enums. Mutate caller-owned frame input after construction and prove the style retained a copy.

- [ ] **Step 2: Run all new style tests and verify compile failures**

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ScrollBarStyleTests" --filter-class "*CheckBoxStyleTests" --filter-class "*RadioButtonStyleTests" --filter-class "*SpinnerStyleTests" --filter-class "*ChaseIndicatorStyleTests" --timeout 60s
```

Expected: compilation fails because the style values do not exist.

- [ ] **Step 3: Implement complete styles and partial sets**

`ScrollBarStyle` stores chrome, fill, complete glyphs, three `ColorValue` parts, and appearance. `CheckBoxStyle` stores `CheckBoxMarkStyle`, `CheckBoxGlyphs`, and appearance. `RadioButtonStyle` stores `RadioButtonMarkStyle`, `RadioButtonGlyphs`, and appearance; parentheses are generated from validated inner runes. The mark enums are members of the complete styles, never independent control properties. `SpinnerStyle` copies frames into `ImmutableArray<Rune>`. `ChaseIndicatorStyle` stores validated active/inactive runes and appearance. Every matching set has nullable members and returns a complete validated style from `Apply`.

Publish named presets for all retained choices: ScrollBar FullBlock/FullLine/ThinBlock/ThinLine; CheckBox Brackets/Tick/Square; RadioButton Parentheses/Glyph; Spinner Braille/DenseBraille/Ascii; and ChaseIndicator Circle/Diamond/Square/Up/Down/Left/Right.

```csharp
public ScrollBarStyle(
    ScrollBarChrome chrome,
    ScrollBarFill fill,
    ScrollBarGlyphs glyphs,
    ColorValue trackColor,
    ColorValue thumbColor,
    ColorValue buttonColor,
    ThemeProfile appearance)
{
    EnumValidation.ValidateDefined(chrome);
    EnumValidation.ValidateDefined(fill);
    ColorValue.ValidatePaint(trackColor, nameof(trackColor));
    ColorValue.ValidatePaint(thumbColor, nameof(thumbColor));
    ColorValue.ValidatePaint(buttonColor, nameof(buttonColor));
    ArgumentNullException.ThrowIfNull(appearance);
    // Assign only after every validation succeeds.
}
```

- [ ] **Step 4: Run all new style tests and commit**

Run the Step 2 command. Expected: all tests pass. Stage only files listed in this task.

```bash
git add src/SharpVision/Controls/ScrollBarStyle.cs src/SharpVision/Controls/ScrollBarStyleSet.cs src/SharpVision/Controls/ScrollBarGlyphs.cs src/SharpVision/Controls/Input/CheckBoxStyle.cs src/SharpVision/Controls/Input/CheckBoxStyleSet.cs src/SharpVision/Controls/Input/RadioButtonStyle.cs src/SharpVision/Controls/Input/RadioButtonStyleSet.cs src/SharpVision/Controls/Input/RadioButtonGlyphs.cs src/SharpVision/Controls/Input/RadioButtonMarkStyle.cs src/SharpVision/Controls/Display/SpinnerStyle.cs src/SharpVision/Controls/Display/SpinnerStyleSet.cs src/SharpVision/Controls/Display/ChaseIndicatorStyle.cs src/SharpVision/Controls/Display/ChaseIndicatorStyleSet.cs src/SharpVision/Styling tests/SharpVision.Tests/Controls
git commit -m "feat: define themed control styles"
```

## Task 4: Theme schema, completion, and catalog

**Files:**

- Modify: `src/SharpVision/Styling/Theme.cs`, `ThemeDefinition.cs`, `ThemeStylesDefinition.cs`, `Themes.cs`
- Modify: `src/SharpVision/Styling/Themes/*.theme.json`
- Modify: `tests/SharpVision.Tests/Styling/ThemeJson.cs`, `CuratedThemesTests.cs`
- Create: `tests/SharpVision.Tests/Styling/ThemeControlStyleTests.cs`

- [ ] **Step 1: Write failing Theme style tests**

Cover complete typed properties, missing external blocks using fallbacks, single-member overrides preserving other members, embedded themes declaring defaults, Button role extraction, and a path-labelled invalid Button combination.

```csharp
var theme = Themes.Parse(
    ThemeJson.Create(controlStyles: "\"scrollBar\":{\"fill\":\"line\"}"),
    "partial-style");

theme.ScrollBar.Fill.ShouldBe(ScrollBarFill.Line);
theme.ScrollBar.Chrome.ShouldBe(ScrollBarStyle.Default.Chrome);
```

- [ ] **Step 2: Run focused Theme tests and verify failures**

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ThemeControlStyleTests" --filter-class "*CuratedThemesTests" --timeout 60s
```

Expected: compilation fails because Theme does not expose the typed styles.

- [ ] **Step 3: Extend Theme and typed JSON definitions**

Add get-only Button, ScrollBar, CheckBox, RadioButton, Spinner, and ChaseIndicator style properties. Keep Control, Input, Container, Window, and Popup semantic profiles. `GetProfile(ThemeRole.Button)` returns `Button.Appearance`. Resolve JSON semantic names, apply partial sets to fallbacks, validate complete styles, and freeze.

```csharp
public ButtonStyle Button { get; private set; }
public ScrollBarStyle ScrollBar { get; private set; }
public CheckBoxStyle CheckBox { get; private set; }
public RadioButtonStyle RadioButton { get; private set; }
public SpinnerStyle Spinner { get; private set; }
public ChaseIndicatorStyle ChaseIndicator { get; private set; }
```

- [ ] **Step 4: Update every bundled Theme document**

Add explicit intended defaults under `styles`: full/block ScrollBar, bracket CheckBox, parenthesized RadioButton, Braille Spinner, and circle ChaseIndicator. Move the existing Button profile inside the Button style and add its padding. Do not rewrite palettes.

- [ ] **Step 5: Run Theme tests and commit**

Run the Step 2 command, then:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ThemesLoadingTests" --filter-class "*ThemesSemanticSchemaTests" --timeout 60s
```

Expected: all pass.

```bash
git add src/SharpVision/Styling tests/SharpVision.Tests/Styling
git commit -m "feat: load typed control styles from themes"
```

## Task 5: Styled-control resolver seam

**Files:**

- Modify: `src/SharpVision/Controls/Control.cs`
- Modify: `src/SharpVision/Runtime/Application.cs`
- Modify: `src/SharpVision/Styling/AppearanceResolver.cs`
- Modify: `src/SharpVision/Styling/ControlAppearance.cs`
- Create: `tests/SharpVision.Tests/Controls/StyledProbe.cs`
- Modify: `tests/SharpVision.Tests/Controls/ControlCompositeAppearanceTests.cs`, `ControlBorderReservationTests.cs`

- [ ] **Step 1: Write failing profile-source and invalidation tests**

Create a derived probe whose protected profile source is switchable. Prove it feeds ActualFace/ActualBorder/ActualShadow, color-only change requests Render, border-side change requests Measure, semantic equality is a no-op, attached mutation is dispatcher-affine, and property observers see committed Style/Actual/appearance values.

```csharp
public sealed class StyledProbe: Control
{
    public ThemeProfile Profile { get; set; } =
        new(Theme.CreateDefaultAppearance());

    protected override ThemeProfile AppearanceProfile => Profile;
}
```

- [ ] **Step 2: Run focused tests and verify the compile failure**

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ControlCompositeAppearanceTests" --filter-class "*ControlBorderReservationTests" --timeout 60s
```

Expected: compilation fails because `AppearanceProfile` does not exist.

- [ ] **Step 3: Add profile resolution and style invalidation**

Move role lookup behind a protected virtual profile source. Add `SetControlStyle<TStyle>`: verify access, compare nullable local values, capture the prior resolved style, commit the field, resolve the current style, calculate exact impact through a supplied comparer, clear caches, invalidate, and then publish Style/ActualStyle/Actual appearance notifications.

```csharp
protected virtual ThemeProfile AppearanceProfile =>
    (Theme ?? Themes.Dark).GetProfile(ThemeRole);
```

Add a virtual Theme-impact calculation to Control. `CommitTheme` calculates the profile/style impact before replacing `InheritedTheme`; each migrated control overrides it to compare the old/new complete Theme styles. Remove Application's unconditional root Measure invalidation after `PropagateTheme`; child impacts bubble normally and `ProcessInvalidation` drains the strongest requested phase.

- [ ] **Step 4: Run focused tests and commit**

Run the Step 2 command. Expected: all pass.

```bash
git add src/SharpVision/Controls/Control.cs src/SharpVision/Runtime/Application.cs src/SharpVision/Styling/AppearanceResolver.cs src/SharpVision/Styling/ControlAppearance.cs tests/SharpVision.Tests/Controls/StyledProbe.cs tests/SharpVision.Tests/Controls/ControlCompositeAppearanceTests.cs tests/SharpVision.Tests/Controls/ControlBorderReservationTests.cs
git commit -m "refactor: resolve control appearance from styles"
```

## Task 6: Migrate Button to Style

**Files:**

- Modify: `src/SharpVision/Controls/Input/Button.cs`
- Delete: `src/SharpVision/Controls/Input/ButtonKind.cs`
- Modify: `tests/SharpVision.Tests/Controls/Input/ButtonTests.cs`, `ButtonSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Dialogs/DialogButtonAppearanceTests.cs`

- [ ] **Step 1: Add failing local/Theme precedence tests**

Assert Style starts null, ActualStyle uses Theme.Button after mount, Filled wins across Theme replacement, null resumes Theme ownership, structural changes request Measure, color-only changes request Render, and no state combines border with visible shadow.

```csharp
button.Style.ShouldBeNull();
button.ActualStyle.ShouldBe(button.Theme!.Button);

await surface.UpdateAsync(() => button.Style = ButtonStyle.Filled, "fill button");
button.ActualBorder.Sides.ShouldBe(BorderSide.None);
button.ActualShadow.IsVisible.ShouldBeTrue();
```

- [ ] **Step 2: Run focused Button tests and verify failures**

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ButtonTests" --filter-class "*ButtonSurfaceTests" --filter-class "*DialogButtonAppearanceTests" --timeout 60s
```

Expected: compile failures for new Style members and old ButtonKind assertions.

- [ ] **Step 3: Implement Button style resolution**

Remove kind constructors and constructor-authored Padding/Border/Shadow. Store only the nullable local style and use ActualStyle for internal padding and appearance. Do not write inherited Padding, because that would invent a local developer value.

```csharp
private ButtonStyle? _style;

public ButtonStyle? Style
{
    get => _style;
    set => SetControlStyle(ref _style, value, nameof(Style));
}

public ButtonStyle ActualStyle => Style ?? Theme?.Button ?? ButtonStyle.Standard;

protected override ThemeProfile AppearanceProfile => ActualStyle.Appearance;
```

- [ ] **Step 4: Migrate and run focused tests**

Replace kind construction with object-initializer style only where Filled is intentional. Remove dialog style assignments so dialog buttons prove Theme defaults. Run Step 2; expected: all pass.

- [ ] **Step 5: Commit Button migration**

```bash
git add src/SharpVision/Controls/Input tests/SharpVision.Tests/Controls/Input tests/SharpVision.Tests/Dialogs/DialogButtonAppearanceTests.cs
git commit -m "refactor: apply button styles from themes"
```

## Task 7: Migrate standalone and generated ScrollBars

**Files:**

- Modify: `src/SharpVision/Controls/ScrollBar.cs`, `Container.cs`, `ContainerScrollController.cs`
- Modify: `src/SharpVision/Controls/Collections/ListView.cs`
- Modify: `src/SharpVision/Controls/Input/TextInput.cs`, `ComboBox.cs`
- Modify: `src/SharpVision/Controls/Layout/Table.cs`, `TablePresenter.cs`
- Retain: `src/SharpVision/Layout/ScrollBarChrome.cs`, `ScrollBarFill.cs` as members of `ScrollBarStyle`, not Control properties
- Modify: `tests/SharpVision.Tests/Controls/Layout/ScrollBarTests.cs`, `ScrollBarSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ContainerScrollTests.cs`, `ContainerScrollGeometryTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/Collections/ListViewTests.cs`, `ListViewSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/Input/TextInputTests.cs`, `TextInputSurfaceTests.cs`, `ComboBoxTests.cs`, `ComboBoxSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/Layout/TableTests.cs`, `TableSurfaceTests.cs`

- [ ] **Step 1: Write failing standalone and generated-bar tests**

Cover Theme fallback, local override/reset, generated creation after host override, Theme swap without reconstruction, and bar removal/recreation without copying Theme values locally.

```csharp
container.ScrollBarStyle.ShouldBeNull();
container.ActualScrollBarStyle.ShouldBe(container.Theme!.ScrollBar);
horizontal.Style.ShouldBeNull();
horizontal.ActualStyle.ShouldBe(container.Theme.ScrollBar);
```

Also prove Thin measures one cell, Full reserves buttons when possible, and Fill changes Render only.

- [ ] **Step 2: Run focused scrolling tests and verify failures**

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ScrollBarTests" --filter-class "*ContainerScrollTests" --filter-class "*ListViewTests" --filter-class "*TextInputTests" --filter-class "*ComboBoxTests" --filter-class "*TableTests" --timeout 60s
```

Expected: compile failures for new style members and old independent properties.

- [ ] **Step 3: Migrate ScrollBar itself**

Add nullable Style, complete ActualStyle, and AppearanceProfile. Replace all measure/render/glyph reads with ActualStyle. Keep Orientation, range, viewport, and change values ordinary. Remove independent colors, glyphs, reset, chrome, and fill properties.

- [ ] **Step 4: Migrate generated-bar hosts**

Expose nullable `ScrollBarStyle` and complete `ActualScrollBarStyle`. A generated bar receives only the host's explicit nullable override; a Theme-owned host leaves the generated bar Style null.

```csharp
public ScrollBarStyle? ScrollBarStyle { get; set; }

public ScrollBarStyle ActualScrollBarStyle =>
    ScrollBarStyle ??
    Theme?.ScrollBar ??
    global::SharpVision.Controls.ScrollBarStyle.Default;
```

Use resolved structural style during host measure and propagate explicit override changes to existing bars.

- [ ] **Step 5: Run unit and surface tests**

Run Step 2 plus `*ScrollBarSurfaceTests`, `*ListViewSurfaceTests`, `*TextInputSurfaceTests`, `*ComboBoxSurfaceTests`, and `*TableSurfaceTests`. Expected: all pass.

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ScrollBarSurfaceTests" --filter-class "*ListViewSurfaceTests" --filter-class "*TextInputSurfaceTests" --filter-class "*ComboBoxSurfaceTests" --filter-class "*TableSurfaceTests" --timeout 60s
```

- [ ] **Step 6: Commit ScrollBar migration**

Stage only listed files, inspect `git diff --cached --name-only`, then commit.

```bash
git add src/SharpVision/Controls/ScrollBar.cs src/SharpVision/Controls/Container.cs src/SharpVision/Controls/ContainerScrollController.cs src/SharpVision/Controls/Collections/ListView.cs src/SharpVision/Controls/Input/TextInput.cs src/SharpVision/Controls/Input/ComboBox.cs src/SharpVision/Controls/Layout/Table.cs src/SharpVision/Controls/Layout/TablePresenter.cs src/SharpVision/Layout/ScrollBarChrome.cs src/SharpVision/Layout/ScrollBarFill.cs tests/SharpVision.Tests/Controls
git commit -m "refactor: apply scrollbar styles from themes"
```

## Task 8: Migrate CheckBox and RadioButton

**Files:**

- Modify: `src/SharpVision/Controls/Input/CheckBox.cs`, `RadioButton.cs`
- Retain: `src/SharpVision/Controls/Input/CheckBoxMarkStyle.cs` as a member of `CheckBoxStyle`, not a Control property
- Modify: `tests/SharpVision.Tests/Controls/Input/CheckBoxTests.cs`, `CheckBoxSurfaceTests.cs`, `RadioButtonTests.cs`, `RadioButtonSurfaceTests.cs`

- [ ] **Step 1: Write failing style and exact parentheses tests**

Cover null local style, Theme fallback, explicit precedence/reset, mark-width Measure invalidation, exact output, and stable content alignment.

```csharp
surface.ShouldRender("""
                     ( ) One
                     (•) Two
                     """);
```

Under wide ambiguous-width policy, assert the inner checked mark repairs to one cell without moving parentheses or content.

- [ ] **Step 2: Run focused tests and verify failures**

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*CheckBoxTests" --filter-class "*CheckBoxSurfaceTests" --filter-class "*RadioButtonTests" --filter-class "*RadioButtonSurfaceTests" --timeout 60s
```

Expected: compile failures for new styles and render failures for parentheses.

- [ ] **Step 3: Implement style-backed marks**

Add nullable Style, complete ActualStyle, and AppearanceProfile to both controls. Compute measure and marks only from ActualStyle; never copy mark members into local fields. Delete independent mark/glyph/reset APIs while preserving CheckBox event order and RadioButton grouping/navigation.

- [ ] **Step 4: Run focused tests and commit**

Run Step 2. Expected: behavior, exact cells, fallback, and alignment all pass.

```bash
git add src/SharpVision/Controls/Input tests/SharpVision.Tests/Controls/Input
git commit -m "refactor: theme checkbox and radio styles"
```

## Task 9: Migrate Spinner and ChaseIndicator

**Files:**

- Modify: `src/SharpVision/Controls/Display/Spinner.cs`, `ChaseIndicator.cs`
- Delete: `src/SharpVision/Controls/Display/SpinnerPattern.cs`, `ChasePattern.cs`
- Modify: `tests/SharpVision.Tests/Controls/Display/SpinnerTests.cs`, `SpinnerSurfaceTests.cs`, `ChaseIndicatorTests.cs`, `ChaseIndicatorSurfaceTests.cs`

- [ ] **Step 1: Write failing Theme/local style tests**

Cover Theme fallback, local precedence/reset, phase reset after style change, style change while playing, copied frames, and preservation of timer/movement/trail behavior.

```csharp
spinner.Style.ShouldBeNull();
spinner.ActualStyle.ShouldBe(spinner.Theme!.Spinner);

await surface.UpdateAsync(
    () => spinner.Style = SpinnerStyle.Ascii,
    "switch spinner style");

surface.ShouldRender("|");
```

- [ ] **Step 2: Run focused display tests and verify failures**

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*SpinnerTests" --filter-class "*SpinnerSurfaceTests" --filter-class "*ChaseIndicatorTests" --filter-class "*ChaseIndicatorSurfaceTests" --timeout 60s
```

Expected: compile failures for new styles and old Pattern usages.

- [ ] **Step 3: Implement style-backed presentation**

Add nullable Style, complete ActualStyle, and AppearanceProfile. Spinner reads frames from ActualStyle; ChaseIndicator reads active/inactive glyphs. An effective style change resets only presentation phase/history that depends on the sequence. Preserve Interval, IsPlaying, Movement, Orientation, Length, Spacing, trail, fade, and timer ownership.

- [ ] **Step 4: Run focused tests and commit**

Run Step 2. Expected: existing timing/movement and new style tests pass.

```bash
git add src/SharpVision/Controls/Display tests/SharpVision.Tests/Controls/Display
git commit -m "refactor: theme indicator styles"
```

## Task 10: Protect Control chrome and republish approved hosts

**Files:**

- Modify: `src/SharpVision/Controls/Control.cs`
- Modify: `src/SharpVision/Controls/Layout/Dock.cs`, `Grid.cs`, `Stack.cs`
- Modify: `src/SharpVision/Controls/Overlay.cs`
- Modify: `src/SharpVision/Windows/Window.cs`, `src/SharpVision/Popups/Popup.cs`
- Create: `tests/SharpVision.Tests/Compatibility/ChromeAccessibilityTests.cs`
- Modify: `tests/SharpVision.Tests/Compatibility/PackageConsumers/FloatingSurfaceConsumer/ConsumerSurface.cs`
- Create: `tests/SharpVision.Tests/Controls/ChromeProbe.cs`
- Modify: `tests/SharpVision.Tests/Styling/ControlChromeTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/IntrinsicBorderTests.cs`, `IntrinsicBorderSurfaceTests.cs`, `ComponentGeometrySurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Layout/BoxModelSurfaceTests.cs`

- [ ] **Step 1: Write failing accessibility and third-party tests**

Use reflection to assert specialized controls expose no public Border, Shadow, reset, or SetAppearance. Assert every approved host exposes raw Border/Shadow/reset. Extend the package consumer with a derived control that uses protected authoring and publishes its own validated style.

```csharp
[Theory]
[InlineData(typeof(Button))]
[InlineData(typeof(TextInput))]
[InlineData(typeof(ListView))]
public void Chrome_WhenControlIsSpecialized_IsNotPublic(Type type)
{
    type.GetProperty("Border", BindingFlags.Instance | BindingFlags.Public)
        .ShouldBeNull();
    type.GetProperty("Shadow", BindingFlags.Instance | BindingFlags.Public)
        .ShouldBeNull();
}
```

- [ ] **Step 2: Run compatibility tests and verify failure**

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ChromeAccessibilityTests" --filter-class "*PackedPackageConsumerTests" --timeout 60s
```

Expected: reflection finds inherited public chrome on specialized controls.

- [ ] **Step 3: Protect the base authoring surface**

Change Control.Border, ResetBorder, Shadow, ResetShadow, and SetAppearance to protected. Leave Face/ResetFace and ActualBorder/ActualShadow public. Update XML docs to distinguish derived authoring from consumer inspection.

- [ ] **Step 4: Republish approved hosts**

Dock, Grid, Stack, Overlay, Window, and Popup declare explicit public wrappers. Use `new` to keep intentional hiding warning-free.

```csharp
public new Border Border
{
    get => base.Border;
    set => base.Border = value;
}

public new void ResetBorder() => base.ResetBorder();
```

Repeat for Shadow. Do not republish SetAppearance.

- [ ] **Step 5: Migrate internal tests and showcase helpers**

Base chrome tests use a dedicated derived `ChromeProbe` with test-only wrappers. Showcase chrome examples use an approved host. Never expose raw chrome on a specialized production control merely to retain a test.

- [ ] **Step 6: Run focused tests and commit**

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*ChromeAccessibilityTests" --filter-class "*ControlChromeTests" --filter-class "*IntrinsicBorderTests" --filter-class "*BoxModelSurfaceTests" --filter-class "*WindowSurfaceTests" --filter-class "*PopupSurfaceTests" --timeout 60s
```

Expected: all pass and package-consumer compilation succeeds. Stage only intended files; preserve the existing FilePicker edit if present.

```bash
git add src/SharpVision/Controls/Control.cs src/SharpVision/Controls/Layout/Dock.cs src/SharpVision/Controls/Layout/Grid.cs src/SharpVision/Controls/Layout/Stack.cs src/SharpVision/Controls/Overlay.cs src/SharpVision/Windows/Window.cs src/SharpVision/Popups/Popup.cs tests/SharpVision.Tests/Compatibility tests/SharpVision.Tests/Controls tests/SharpVision.Tests/Layout
git commit -m "refactor: protect specialized control chrome"
```

## Task 11: Showcase and normative documentation migration

**Files:**

- Modify: `src/SharpVision.Showcase/Panes/ButtonPane.cs`, `ScrollBarPane.cs`, `CheckBoxPane.cs`, `RadioButtonPane.cs`, `SpinnerPane.cs`, `ChaseIndicatorPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/ListViewPane.cs`, `TextInputPane.cs`, `ComboBoxPane.cs`, `TablePane.cs`, `WindowPane.cs`, `PopupPane.cs`, `ShadowPane.cs`, `BorderPane.cs`
- Modify: `src/SharpVision.Showcase/Controls/DocCard.cs`, `DocExample.cs`, `DocPage.cs`, `src/SharpVision.Showcase/Doc.cs`
- Modify: `docs/concepts/styling.md`, `themes.md`, `intrinsic-chrome.md`, `theming-new-controls.md`, `custom-components.md`, `box-model.md`, `scrolling.md`
- Modify: `docs/controls/control.md`, `input/button.md`, `input/check-box.md`, `input/radio-button.md`, `input/combo-box.md`, `input/text-input.md`
- Modify: `docs/controls/layout/scroll-bar.md`, `docs/controls/collections/list-view.md`, `docs/controls/display/spinner.md`, `docs/controls/display/chase-indicator.md`, `docs/controls/windows/window.md`, `docs/controls/popups/popup.md`
- Modify: `docs/architecture/showcase.md`

- [ ] **Step 1: Migrate showcase construction**

Ordinary controls receive no local Style. Pages that explicitly demonstrate styles use complete presets. Remove raw Border/Shadow assignments from specialized controls; dialog buttons remain Theme-owned.

```csharp
var filled = new Button("Filled") { Style = ButtonStyle.Filled };
var parenthesized = new RadioButton("Parentheses")
{
    Style = RadioButtonStyle.Parentheses
};
var thin = new ScrollBar { Style = ScrollBarStyle.ThinLine };
```

Audit compile references before editing and after migration:

```bash
rg -n 'ButtonKind|CheckBoxMarkStyle|ScrollBarChrome|ScrollBarFill|SpinnerPattern|ChasePattern|MarkGlyphs|UncheckedGlyph|CheckedGlyph|TrackColor|ThumbColor|ButtonColor|SetAppearance' src/SharpVision.Showcase docs
```

Expected after migration: no obsolete control-property references; `CheckBoxMarkStyle`, `ScrollBarChrome`, and `ScrollBarFill` may appear only as members documented inside complete style values.

- [ ] **Step 2: Update normative docs**

`styling.md` owns complete Style vs partial StyleSet, precedence, invalidation, and protected chrome. `themes.md` owns JSON completion. `intrinsic-chrome.md` and `control.md` document protected raw authoring and public Actual values. Each affected control page documents Style, ActualStyle, defaults, validation, removed properties, and proof.

- [ ] **Step 3: Format and validate docs**

```bash
npx prettier --write docs/concepts/styling.md docs/concepts/themes.md docs/concepts/intrinsic-chrome.md docs/concepts/theming-new-controls.md docs/concepts/custom-components.md docs/concepts/box-model.md docs/controls/control.md docs/controls/input/button.md docs/controls/input/check-box.md docs/controls/input/radio-button.md docs/controls/layout/scroll-bar.md docs/controls/display/spinner.md docs/controls/display/chase-indicator.md docs/architecture/showcase.md
npx markdownlint-cli2 "docs/**/*.md"
npm run lint:links
```

Expected: formatting is stable, Markdown has zero errors, and links resolve.

- [ ] **Step 4: Build showcase and commit**

```bash
dotnet build src/SharpVision.Showcase/SharpVision.Showcase.csproj --no-restore
```

Expected: zero warnings and errors.

```bash
git add src/SharpVision.Showcase docs
git commit -m "docs: document themed control styles"
```

## Task 12: Public API review and full verification

**Files:**

- Modify: `tests/SharpVision.Compatibility.Tests/Snapshots/0.5.0-alpha.1/SharpVision.verified.txt`

- [ ] **Step 1: Run the compatibility snapshot test**

```bash
dotnet test --project tests/SharpVision.Compatibility.Tests --filter-class "*PublicApiCompatibilityTests" --timeout 60s
```

Expected: `SharpVision.received.txt` contains only the intentional removal of old style/chrome APIs, addition of complete/partial style values, Theme properties, protected base chrome, and approved host wrappers.

- [ ] **Step 2: Review and promote the API snapshot**

Reject unexpected namespace, accessibility, constructor, or terminal-project changes. Apply the reviewed received changes to `SharpVision.verified.txt`, then remove the received artifact.

- [ ] **Step 3: Run focused suites**

```bash
dotnet test --project tests/SharpVision.Tests --filter-namespace "SharpVision.Tests.Styling" --timeout 60s
dotnet test --project tests/SharpVision.Tests --filter-namespace "SharpVision.Tests.Controls.Input" --timeout 60s
dotnet test --project tests/SharpVision.Tests --filter-namespace "SharpVision.Tests.Controls.Display" --timeout 60s
dotnet test --project tests/SharpVision.Tests --filter-class "*ScrollBar*" --timeout 60s
```

Expected: all pass and no filter discovers zero tests.

- [ ] **Step 4: Run repository gates**

```bash
make format
make lint
make build
make test
```

Expected: zero warnings, zero errors, minimum discovered tests satisfied, and no Markdown/link failures.

- [ ] **Step 5: Validate the live showcase in tmux**

```bash
tmux new-session -d -s sharpvision-style-validation -x 120 -y 40 \
  'dotnet run --project src/SharpVision.Showcase'
sleep 3
tmux capture-pane -t sharpvision-style-validation -p
```

Navigate to Button, ScrollBar, CheckBox, RadioButton, Spinner, and ChaseIndicator; switch between a dark and light Theme and capture styled output. Verify ordinary controls follow Theme, explicit presets retain structure, Buttons never combine border/shadow, generated bars match standalone defaults, RadioButton parentheses occupy three cells, and state feedback remains immediate. Then run:

```bash
tmux kill-session -t sharpvision-style-validation
```

- [ ] **Step 6: Review final diff and commit API approval**

Run `git diff --check` and inspect `git status --short`. Confirm unrelated work is neither staged nor overwritten.

```bash
git add tests/SharpVision.Compatibility.Tests/Snapshots/0.5.0-alpha.1/SharpVision.verified.txt
git commit -m "test: approve themed control style api"
```

The work is complete only with current focused, full-gate, and tmux evidence and a clean feature worktree.
