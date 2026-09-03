// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies CheckBox states and activation through a mounted terminal surface.</summary>
public sealed class CheckBoxSurfaceTests
{
    /// <summary>Verifies the unchecked bracket mark and wide label retain exact cell ownership.</summary>
    [Fact]
    public async Task Render_WhenUncheckedUnicodeContentIsMounted_ShowsExactNormalCellsAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "界" };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(6, 1),
            TestContext.Current.CancellationToken);

        // Assert
        checkBox.IsChecked.ShouldBe(false);
        surface.ShouldHaveState(checkBox, VisualState.Normal);
        surface.ShouldRender("[ ] 界");
        surface.Cell(default).Style.Foreground.ShouldBe(ReferenceColors.Get(15));
        var wide = surface.Cell(new Point(4, 0));
        wide.Text.ShouldBe("界");
        wide.Width.ShouldBe(2);
        surface.Cell(new Point(5, 0)).Continuation.ShouldBeTrue();
    }

    /// <summary>Verifies a theme document authoring the root-level "glyphs" field reaches a
    /// mounted CheckBox's rendered mark - the ascii family's bracket layout and '.'/'X'/'-' trio,
    /// not the code-owned defaults (see themes.md#glyph-families).</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsAnAsciiGlyphFamily_DrawsItsCheckBoxMarkAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Go", ThreeState = true, IsChecked = null };
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("[─] Go");

        // Act
        await surface.UpdateAsync(
            () => surface.Application.Theme = ThemeCatalog.Parse(ThemeJson.Create(glyphs: "ascii")),
            "author an ascii glyph family");

        // Assert the indeterminate mark switches to the ascii family's own glyph trio.
        surface.ShouldRender("[-] Go");

        // Act and assert the checked and unchecked marks too.
        await surface.UpdateAsync(() => checkBox.IsChecked = true, "check");
        surface.ShouldRender("[X] Go");
        await surface.UpdateAsync(() => checkBox.IsChecked = false, "uncheck");
        surface.ShouldRender("[.] Go");
    }

    /// <summary>Verifies hover, held press, release, focus, and pointer activation compose correctly.</summary>
    [Fact]
    public async Task Pointer_WhenCheckBoxIsClicked_ComposesStatesAndTogglesWithPointerCauseAsync()
    {
        // Arrange
        ActivationCause? cause = null;
        var checkBox = new CheckBox { Text = "Choice" };
        checkBox.Checked += (_, eventArgs) => cause = eventArgs.Cause;
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        var hoveredForeground = TerminalPalette.Project(ThemeColorHelper.HoveredForeground(ThemeCatalog.Dark), ColorDepth.Basic16);
        var focusedForeground = TerminalPalette.Project(
            ThemeColorHelper.FocusedForeground(ThemeCatalog.Dark),
            ColorDepth.Basic16);

        // Act and assert hover
        await surface.Pointer.MoveToAsync(checkBox);
        surface.ShouldHaveState(checkBox, VisualState.IsPointerOver);
        surface.Cell(default).Style.Foreground.ShouldBe(hoveredForeground);
        surface.Cell(default).Style.Background.ShouldBe(ReferenceColors.Get(0));

        // Act and assert held press
        await surface.Pointer.PressAsync();
        checkBox.IsChecked.ShouldBe(false);
        surface.ShouldHaveState(checkBox, VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);
        surface.ShouldRender("[ ] Choice");

        // Act and assert release
        await surface.Pointer.ReleaseAsync();
        checkBox.IsChecked.ShouldBe(true);
        cause.ShouldBe(ActivationCause.Pointer);
        surface.ShouldHaveState(checkBox, VisualState.IsPointerOver | VisualState.Focused);
        surface.ShouldRender("[✓] Choice");
        // Focus follows PointerOver in the visual-state order, so the released caption carries
        // the focused cue while the pointer remains over the control.
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(focusedForeground);
        surface.Cell(new Point(4, 0)).Style.Background.ShouldBe(ReferenceColors.Get(0));

        // Act unavailable while another activation is held
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(checkBox);
        await surface.UpdateAsync(() => checkBox.IsEnabled = false, "disable held CheckBox");

        // Assert cleanup without another toggle
        checkBox.IsChecked.ShouldBe(true);
        checkBox.IsPressed.ShouldBeFalse();
        checkBox.IsFocused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveState(checkBox, VisualState.Disabled);
    }

    /// <summary>Verifies a press-only terminal's Space toggles immediately. Legacy input never
    /// delivers a key release, so the old arm-on-press/complete-on-release Space latched forever
    /// and never activated outside the Kitty keyboard protocol; the press itself completes when
    /// releases are not expected.</summary>
    [Fact]
    public async Task Keyboard_WhenLegacySpaceHasNoRelease_TogglesOnThePressAsync()
    {
        // Arrange
        List<ActivationCause> causes = [];
        var checkBox = new CheckBox { Text = "Option" };
        checkBox.Checked += (_, eventArgs) => causes.Add(eventArgs.Cause);
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act: a bare UTF-8 space - the only thing a press-only terminal ever sends.
        await surface.Keyboard.TypeAsync(" ");

        // Assert
        checkBox.IsChecked.ShouldBe(true);
        causes.ShouldBe([ActivationCause.Keyboard]);
        surface.ShouldRender("[✓] Option");
    }

    /// <summary>Verifies complete Space actions reach checked and indeterminate states with keyboard cause.</summary>
    [Fact]
    public async Task Keyboard_WhenThreeStateCheckBoxCompletesSpace_CyclesThroughIntendedStatesAsync()
    {
        // Arrange
        List<ActivationCause> causes = [];
        var checkBox = new CheckBox { Text = "Option", ThreeState = true };
        checkBox.StateChanged += (_, eventArgs) => causes.Add(eventArgs.Cause);
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        var focusedForeground = ThemeColorHelper.FocusedForeground(ThemeCatalog.Dark);

        // Act and assert focus
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveState(checkBox, VisualState.Focused);
        surface.ShouldRender("[ ] Option");

        await surface.Keyboard.PressAsync(Code.Right);
        checkBox.IsChecked.ShouldBe(false);

        // Act
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

        // Assert
        checkBox.IsChecked.ShouldBeNull();
        causes.ShouldBe([ActivationCause.Keyboard, ActivationCause.Keyboard]);
        surface.ShouldHaveState(checkBox, VisualState.Focused);
        surface.ShouldRender("[─] Option");
        surface.Cell(default).Style.Foreground.ShouldBe(focusedForeground);
        surface.Cell(default).Style.Background.ShouldBe(ReferenceColors.Get(0));

        // The transparent caption inherits the ambient focused face, matching the mark's cue.
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(focusedForeground, ColorDepth.Basic16));
        surface.Cell(new Point(4, 0)).Style.Background.ShouldBe(ReferenceColors.Get(0));
    }

    /// <summary>Verifies disabled checked state refuses keyboard and pointer activation.</summary>
    [Fact]
    public async Task Input_WhenCheckedCheckBoxIsDisabled_PreservesValueAndMutedAppearanceAsync()
    {
        // Arrange
        var changes = 0;
        var checkBox = new CheckBox { Text = "Disabled", IsChecked = true };
        checkBox.StateChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(12, 1),
            TestContext.Current.CancellationToken);

        var selectedContent = surface.Cell(new Point(4, 0)).Style.Foreground;
        selectedContent.IsRgb.ShouldBeTrue();
        selectedContent.ShouldBe(ReferenceColors.Get(15));

        // Act
        await surface.UpdateAsync(() => checkBox.IsEnabled = false, "disable checked CheckBox");
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Pointer.ClickAsync(checkBox);

        // Assert
        checkBox.IsChecked.ShouldBe(true);
        checkBox.IsFocused.ShouldBeFalse();
        changes.ShouldBe(0);
        surface.ShouldHaveState(checkBox, VisualState.Disabled);
        surface.ShouldRender("[✓] Disabled");
        var expectedDisabledFg = TerminalPalette.Project(ThemeColorHelper.DisabledForeground(ThemeCatalog.Dark), ColorDepth.Basic16);
        var foreground = surface.Cell(default).Style.Foreground;
        foreground.IsRgb.ShouldBeTrue();
        foreground.ShouldBe(expectedDisabledFg);
        var contentForeground = surface.Cell(new Point(4, 0)).Style.Foreground;
        contentForeground.IsRgb.ShouldBeTrue();
        contentForeground.ShouldBe(expectedDisabledFg);

        // Act and assert restored availability
        await surface.UpdateAsync(() => checkBox.IsEnabled = true, "re-enable checked CheckBox");
        surface.Cell(default).Style.Foreground.ShouldBe(ReferenceColors.Get(15));
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(15));

        // Assert normal interaction resumes
        surface.ShouldHaveState(checkBox, VisualState.Normal);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(checkBox);
        await surface.Pointer.ClickAsync(checkBox);
        checkBox.IsChecked.ShouldBe(false);
        changes.ShouldBe(1);
    }

    /// <summary>Verifies a CheckBox inherits disabled state from an ancestor and keeps stable
    /// geometry across a genuine resize while disabled, matching an independently-mounted enabled
    /// instance arranged at the same size.</summary>
    [Fact]
    public async Task Input_WhenAncestorDisablesCheckBoxAndResized_InheritsStateAndPreservesGeometryAsync()
    {
        // Arrange a CheckBox disabled only through its ancestor
        var checkBox = new CheckBox
        {
            Text = "Choice",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { checkBox }
        };
        await using var surface = await ComponentSurface.MountAsync(
            overlay,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        // Act disable the ancestor, not the CheckBox itself
        await surface.UpdateAsync(() => overlay.IsEnabled = false, "disable CheckBox's ancestor");

        // Assert the disabled state is inherited
        checkBox.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(checkBox, VisualState.Disabled);

        // Act resize to a genuinely different size while disabled
        await surface.ResizeAsync(new Size(20, 3));

        // Assert geometry matches an independently-mounted enabled instance at the same size
        var reference = new CheckBox
        {
            Text = "Choice",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var referenceSurface = await ComponentSurface.MountAsync(
            reference,
            new Size(20, 3),
            TestContext.Current.CancellationToken);

        checkBox.Bounds.ShouldBe(reference.Bounds);
        checkBox.DesiredSize.ShouldBe(reference.DesiredSize);
    }

    /// <summary>Verifies a CheckBox keeps its desired mark height and centers it in an oversized
    /// horizontal row by default.</summary>
    [Fact]
    public async Task Render_WhenCheckBoxSharesOversizedHorizontalRow_CentersDesiredMarkByDefaultAsync()
    {
        // Arrange
        var parentBackground = ReferenceColors.Get(1);
        var checkBoxBackground = ReferenceColors.Get(4);
        var checkBox = new CheckBox
        {
            Text = "Go",
            Width = Length.Cells(12),
            Face = AppearanceTestValues.Face(background: checkBoxBackground)
        };
        var row = new Stack
        {
            Orientation = Orientation.Horizontal,
            Face = AppearanceTestValues.Face(background: parentBackground),
            Children = { checkBox }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            row,
            new Size(12, 5),
            TestContext.Current.CancellationToken);

        // Assert
        checkBox.Bounds.ShouldBe(new Rect(0, 2, 12, 1));
        surface.Cell(new Point(0, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("[");
        surface.Cell(new Point(4, 2)).Text.ShouldBe("G");
        surface.Cell(new Point(0, 4)).Text.ShouldBe(" ");
        surface.Cell(new Point(0, 0)).Style.Background.ShouldBe(parentBackground);
        surface.Cell(new Point(0, 2)).Style.Background.ShouldBe(checkBoxBackground);
        surface.Cell(new Point(0, 4)).Style.Background.ShouldBe(parentBackground);
    }

    /// <summary>Verifies a CheckBox can explicitly stretch its mark face across an oversized horizontal row.</summary>
    [Fact]
    public async Task Render_WhenCheckBoxSharesOversizedHorizontalRowAndStretchIsSelected_FillsRowAsync()
    {
        // Arrange
        var parentBackground = ReferenceColors.Get(1);
        var checkBoxBackground = ReferenceColors.Get(4);
        var checkBox = new CheckBox
        {
            Text = "Go",
            Width = Length.Cells(12),
            VerticalAlignment = VerticalAlignment.Stretch,
            Face = AppearanceTestValues.Face(background: checkBoxBackground)
        };
        var row = new Stack
        {
            Orientation = Orientation.Horizontal,
            Face = AppearanceTestValues.Face(background: parentBackground),
            Children = { checkBox }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            row,
            new Size(12, 5),
            TestContext.Current.CancellationToken);

        // Assert
        checkBox.Bounds.ShouldBe(new Rect(0, 0, 12, 5));
        surface.Cell(new Point(0, 0)).Text.ShouldBe("[");
        surface.Cell(new Point(0, 4)).Text.ShouldBe(" ");
        surface.Cell(new Point(0, 0)).Style.Background.ShouldBe(checkBoxBackground);
        surface.Cell(new Point(0, 4)).Style.Background.ShouldBe(checkBoxBackground);
    }

    /// <summary>Verifies tiny bounds clip the mark without emitting content outside the control.</summary>
    [Fact]
    public async Task Render_WhenCheckBoxIsTwoCellsWide_ClipsMarkAndContentAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Hidden" };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(2, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("[ ");

        // Act and assert expanded surface
        await surface.ResizeAsync(new Size(10, 1));
        surface.ShouldRender("[ ] Hidden");
    }

    /// <summary>Verifies the selection mark keeps the only available cell when either or both
    /// affixes cannot fit beside it, at both supported caption edges.</summary>
    /// <param name="placement">The caption edge that owns the mark.</param>
    /// <param name="hasStart">Whether a leading affix competes for the cell.</param>
    /// <param name="hasEnd">Whether a trailing affix competes for the cell.</param>
    [Theory]
    [InlineData(SelectionMarkPlacement.Leading, true, false)]
    [InlineData(SelectionMarkPlacement.Leading, false, true)]
    [InlineData(SelectionMarkPlacement.Leading, true, true)]
    [InlineData(SelectionMarkPlacement.Trailing, true, false)]
    [InlineData(SelectionMarkPlacement.Trailing, false, true)]
    [InlineData(SelectionMarkPlacement.Trailing, true, true)]
    public async Task Render_WhenOneCellCannotHoldAffixes_PreservesCheckMarkAsync(
        SelectionMarkPlacement placement,
        bool hasStart,
        bool hasEnd)
    {
        // Arrange
        var checkBox = new CheckBox
        {
            IsChecked = true,
            StartAffix = hasStart ? new Affix(">") : null,
            EndAffix = hasEnd ? new Affix("<") : null,
            Style = CheckBoxStyle.Square with { MarkPlacement = placement }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(1, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("☑");
    }

    /// <summary>Verifies constrained overflow drops the end affix before the start affix, while
    /// preserving the one-cell mark and its gap.</summary>
    [Fact]
    public async Task Render_WhenOnlyStartAffixAndMarkFit_DropsEndAffixFirstAsync()
    {
        // Arrange
        var checkBox = new CheckBox
        {
            IsChecked = true,
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<"),
            Style = CheckBoxStyle.Square
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(3, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("> ☑");
    }

    /// <summary>Verifies both affixes reserve their own cell column, the start affix drawn before
    /// the mark and the end affix drawn after the caption, matching the documented layout.</summary>
    [Fact]
    public async Task Render_WhenCheckBoxHasBothAffixes_PinsThemBesideTheMarkAndCaptionAsync()
    {
        // Arrange
        var checkBox = new CheckBox
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            Text = "Go",
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("> [ ] Go <");
    }

    /// <summary>Verifies a trailing mark stays inside the affixes and honors the authored
    /// mark-to-caption gap exactly.</summary>
    [Fact]
    public async Task Render_WhenMarkIsTrailingWithCustomGap_PlacesCaptionBeforeMarkAsync()
    {
        // Arrange
        var checkBox = new CheckBox
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(11),
            Height = Length.Cells(1),
            Text = "Go",
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<"),
            Style = CheckBoxStyle.Brackets with
            {
                MarkGap = 2,
                MarkPlacement = SelectionMarkPlacement.Trailing
            }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(11, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("> Go  [ ] <");
    }

    /// <summary>Verifies a same-width content swap invalidates rendering only, and the mounted
    /// surface reflects the new glyph without a remeasure - the exact grading an animated affix
    /// (a spinner swapping frames) depends on.</summary>
    [Fact]
    public async Task StartAffix_WhenContentChangesAtTheSameResolvedWidth_UpdatesRenderOnlyAsync()
    {
        // Arrange
        var checkBox = new CheckBox
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            Text = "Go",
            StartAffix = new Affix("|")
        };
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        surface.Cell(default).Text.ShouldBe("|");
        var impact = Invalidation.None;

        // Act
        await surface.UpdateAsync(
            () =>
            {
                checkBox.Clear(Invalidation.All);
                checkBox.StartAffix = new Affix("/");
                impact = checkBox.Pending;
            },
            "swap start affix content at the same resolved width");

        // Assert
        impact.ShouldBe(Invalidation.Render);
        surface.Cell(default).Text.ShouldBe("/");
    }

    /// <summary>Verifies Padding shifts the mark and caption together against the deflated
    /// content box, matching the box-model contract - only the whole-Bounds body fill is
    /// allowed to paint across the raw border box, everything else must respect padding
    /// deflation.</summary>
    [Fact]
    public async Task Render_WhenPaddingIsSet_ShiftsMarkByPaddingLeftAsync()
    {
        // Arrange
        var checkBox = new CheckBox("x") { Padding = new Thickness(2, 0, 0, 0) };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            checkBox,
            new Size(8, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("  [ ] x");
    }
}
