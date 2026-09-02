// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies RadioButton groups and appearance through a mounted terminal surface.</summary>
public sealed class RadioButtonSurfaceTests
{
    /// <summary>Verifies an unselected group renders exact marks and wide Unicode ownership.</summary>
    [Fact]
    public async Task Render_WhenRadioGroupStartsEmpty_ShowsExactUnselectedUnicodeRowsAsync()
    {
        // Arrange
        var first = Radio("One");
        var skipped = Radio("Skip", enabled: false);
        var third = Radio("界");
        var group = Group(first, skipped, third);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert
        first.IsChecked.ShouldBeFalse();
        skipped.IsChecked.ShouldBeFalse();
        third.IsChecked.ShouldBeFalse();
        surface.ShouldRender("""
                             ( ) One
                             ( ) Skip
                             ( ) 界
                             """);
        var wide = surface.Cell(new Point(4, 2));
        wide.Text.ShouldBe("界");
        wide.Width.ShouldBe(2);
        surface.Cell(new Point(5, 2)).Continuation.ShouldBeTrue();
        surface.Cell(new Point(0, 1)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(ThemeColorHelper.InactiveBorder(ThemeCatalog.Dark), ColorDepth.Basic16));
    }

    /// <summary>Verifies parenthesized marks show exact unchecked and checked terminal rows.</summary>
    [Fact]
    public async Task Render_WhenMarkStyleUsesParentheses_ShowsExactStateRowsAsync()
    {
        var uncheckedRadio = Radio("Off");
        uncheckedRadio.Style = RadioButtonStyle.Parentheses;
        var checkedRadio = Radio("On", isChecked: true);
        checkedRadio.Style = RadioButtonStyle.Parentheses;
        var group = Group(uncheckedRadio, checkedRadio);

        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(8, 2),
            TestContext.Current.CancellationToken);

        surface.ShouldRender("""
                             ( ) Off
                             (•) On
                             """);
    }

    /// <summary>Verifies a checked mark uses the theme's accent foreground - RadioButtonStyle's own
    /// code-owned Checked-state completion, the one style in the codebase whose <c>Complete</c>
    /// reads its state parameter. A leaf declares no theme section of its own to override this
    /// with a distinct color any more, so the render loop picking up the code-owned accent is the
    /// complete story now.</summary>
    [Fact]
    public async Task Render_WhenChecked_UsesTheThemesAccentForegroundAsync()
    {
        var radio = new RadioButton
        {
            IsChecked = true,
            Style = RadioButtonStyle.Parentheses
        };

        await using var surface = await ComponentSurface.MountAsync(
            radio,
            new Size(3, 1),
            TestContext.Current.CancellationToken);

        var expected = TerminalPalette.Project(ThemeColorHelper.Accent(ThemeCatalog.Dark), ColorDepth.Basic16);
        surface.ShouldRender("(•)");
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(expected);
        surface.Cell(new Point(1, 0)).Style.Foreground.ShouldBe(expected);
        surface.Cell(new Point(2, 0)).Style.Foreground.ShouldBe(expected);
    }

    /// <summary>Verifies Space selection and arrows skip disabled members and wrap.</summary>
    [Fact]
    public async Task Keyboard_WhenRadioGroupNavigates_SelectsEligibleMembersAndWrapsAsync()
    {
        // Arrange
        List<ActivationCause> causes = [];
        var first = Radio("One");
        var skipped = Radio("Skip", enabled: false);
        var third = Radio("界");
        first.SelectionChanged += (_, eventArgs) => causes.Add(eventArgs.Cause);
        third.SelectionChanged += (_, eventArgs) => causes.Add(eventArgs.Cause);
        var group = Group(first, skipped, third);
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Act and assert initial keyboard selection
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
        first.IsChecked.ShouldBeTrue();
        surface.ShouldHaveState(first, VisualState.Focused);

        // Act and assert disabled skipping
        await surface.Keyboard.PressAsync(Code.Down);
        first.IsChecked.ShouldBeFalse();
        third.IsChecked.ShouldBeTrue();
        third.IsFocused.ShouldBeTrue();
        surface.ShouldHaveState(third, VisualState.Focused);
        surface.ShouldRender("""
                             ( ) One
                             ( ) Skip
                             (•) 界
                             """);
        surface.Cell(new Point(0, 2)).Style.Foreground.ShouldBe(ReferenceColors.Get(14));

        // The transparent caption inherits the ambient checked face, which authors the accent.
        surface.Cell(new Point(4, 2)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(ThemeColorHelper.Accent(ThemeCatalog.Dark), ColorDepth.Basic16));

        // Act and assert wrapping
        await surface.Keyboard.PressAsync(Code.Down);
        first.IsChecked.ShouldBeTrue();
        first.IsFocused.ShouldBeTrue();
        third.IsChecked.ShouldBeFalse();
        causes.ShouldBe([
            ActivationCause.Keyboard,
            ActivationCause.Keyboard,
            ActivationCause.Keyboard
        ]);
    }

    /// <summary>Verifies primary-click selection is exclusive and reports pointer cause.</summary>
    [Fact]
    public async Task Pointer_WhenDifferentRadioIsClicked_MovesExclusiveSelectionAsync()
    {
        // Arrange
        ActivationCause? cause = null;
        var first = Radio("One", isChecked: true);
        var second = Radio("Two");
        second.SelectionChanged += (_, eventArgs) => cause = eventArgs.Cause;
        var group = Group(first, second);
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        var initialMark = surface.Cell(default).Style.Foreground;
        initialMark.IsRgb.ShouldBeTrue();
        initialMark.ShouldBe(ReferenceColors.Get(14));
        var initialContent = surface.Cell(new Point(4, 0)).Style.Foreground;
        initialContent.IsRgb.ShouldBeTrue();

        // The selected caption inherits the ambient checked face's accent foreground.
        initialContent.ShouldBe(TerminalPalette.Project(ThemeColorHelper.Accent(ThemeCatalog.Dark), ColorDepth.Basic16));

        // Act hover and held press
        await surface.Pointer.MoveToAsync(second);
        await surface.Pointer.PressAsync();

        // Assert held state before semantic activation
        second.IsChecked.ShouldBeFalse();
        surface.ShouldHaveState(second, VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);
        surface.ShouldHaveCapture(second);

        // Act release
        await surface.Pointer.ReleaseAsync();

        // Assert
        first.IsChecked.ShouldBeFalse();
        second.IsChecked.ShouldBeTrue();
        cause.ShouldBe(ActivationCause.Pointer);
        surface.ShouldHaveState(second, VisualState.IsPointerOver | VisualState.Focused);
        surface.ShouldRender("""
                             ( ) One
                             (•) Two
                             """);

        // Act unavailable while held again
        await surface.Pointer.PressAsync();
        await surface.UpdateAsync(() => second.IsEnabled = false, "disable held RadioButton");

        // Assert cleanup preserves the completed selection
        second.IsChecked.ShouldBeTrue();
        second.IsPressed.ShouldBeFalse();
        second.IsFocused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveState(second, VisualState.Disabled);
    }

    /// <summary>Verifies a retained selected value remains visible but wholly muted while disabled.</summary>
    [Fact]
    public async Task Render_WhenSelectedRadioIsDisabled_MutesMarkAndContentAsync()
    {
        // Arrange
        var radio = Radio("Locked");

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            radio,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => radio.IsChecked = true, "select RadioButton programmatically");
        surface.ShouldRender("(•) Locked");
        var selectedMark = surface.Cell(default).Style.Foreground;
        selectedMark.IsRgb.ShouldBeTrue();
        selectedMark.ShouldBe(ReferenceColors.Get(14));
        var selectedContent = surface.Cell(new Point(4, 0)).Style.Foreground;
        selectedContent.IsRgb.ShouldBeTrue();

        // The selected caption inherits the ambient checked face's accent foreground.
        selectedContent.ShouldBe(TerminalPalette.Project(ThemeColorHelper.Accent(ThemeCatalog.Dark), ColorDepth.Basic16));

        // Act
        await surface.UpdateAsync(() => radio.IsEnabled = false, "disable selected RadioButton");

        // Assert
        radio.IsChecked.ShouldBeTrue();
        surface.ShouldHaveState(radio, VisualState.Disabled);
        surface.ShouldRender("(•) Locked");
        var expectedDisabledFg = TerminalPalette.Project(ThemeColorHelper.DisabledForeground(ThemeCatalog.Dark), ColorDepth.Basic16);
        var mark = surface.Cell(default).Style.Foreground;
        mark.IsRgb.ShouldBeTrue();
        mark.ShouldBe(expectedDisabledFg);
        var content = surface.Cell(new Point(4, 0)).Style.Foreground;
        content.IsRgb.ShouldBeTrue();
        content.ShouldBe(expectedDisabledFg);

        // Act and assert restored availability, including the caption's inherited checked accent
        await surface.UpdateAsync(() => radio.IsEnabled = true, "re-enable selected RadioButton");
        surface.Cell(default).Style.Foreground.ShouldBe(ReferenceColors.Get(14));
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(ThemeColorHelper.Accent(ThemeCatalog.Dark), ColorDepth.Basic16));

        // Assert normal interaction resumes
        surface.ShouldHaveState(radio, VisualState.Normal);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveState(radio, VisualState.Focused);
    }

    /// <summary>Verifies a RadioButton inherits disabled state from an ancestor and keeps stable
    /// geometry across a genuine resize while disabled, matching an independently-mounted enabled
    /// instance arranged at the same size.</summary>
    [Fact]
    public async Task Input_WhenAncestorDisablesRadioButtonAndResized_InheritsStateAndPreservesGeometryAsync()
    {
        // Arrange a RadioButton disabled only through its ancestor
        var radio = Radio("Locked");
        radio.HorizontalAlignment = HorizontalAlignment.Stretch;
        radio.VerticalAlignment = VerticalAlignment.Stretch;
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { radio }
        };
        await using var surface = await ComponentSurface.MountAsync(
            overlay,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        // Act disable the ancestor, not the RadioButton itself
        await surface.UpdateAsync(() => overlay.IsEnabled = false, "disable RadioButton's ancestor");

        // Assert the disabled state is inherited
        radio.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(radio, VisualState.Disabled);

        // Act resize to a genuinely different size while disabled
        await surface.ResizeAsync(new Size(20, 3));

        // Assert geometry matches an independently-mounted enabled instance at the same size
        var reference = Radio("Locked");
        reference.HorizontalAlignment = HorizontalAlignment.Stretch;
        reference.VerticalAlignment = VerticalAlignment.Stretch;
        await using var referenceSurface = await ComponentSurface.MountAsync(
            reference,
            new Size(20, 3),
            TestContext.Current.CancellationToken);

        radio.Bounds.ShouldBe(reference.Bounds);
        radio.DesiredSize.ShouldBe(reference.DesiredSize);
    }

    /// <summary>Verifies a RadioButton keeps its desired mark height and centers it in an oversized
    /// horizontal row by default.</summary>
    [Fact]
    public async Task Render_WhenRadioButtonSharesOversizedHorizontalRow_CentersDesiredMarkByDefaultAsync()
    {
        // Arrange
        var radio = new RadioButton { Text = "Go", Width = Length.Cells(12) };
        var row = new Stack
        {
            Orientation = Orientation.Horizontal,
            Children = { radio }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            row,
            new Size(12, 5),
            TestContext.Current.CancellationToken);

        // Assert
        radio.Bounds.ShouldBe(new Rect(0, 2, 12, 1));
        surface.Cell(new Point(0, 0)).Text.ShouldBe(" ");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("(");
        surface.Cell(new Point(4, 2)).Text.ShouldBe("G");
        surface.Cell(new Point(0, 4)).Text.ShouldBe(" ");
    }

    /// <summary>Verifies a RadioButton can explicitly stretch its mark face across an oversized horizontal row.</summary>
    [Fact]
    public async Task Render_WhenRadioButtonSharesOversizedHorizontalRowAndStretchIsSelected_FillsRowAsync()
    {
        // Arrange
        var radio = new RadioButton
        {
            Text = "Go",
            Width = Length.Cells(12),
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var row = new Stack
        {
            Orientation = Orientation.Horizontal,
            Children = { radio }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            row,
            new Size(12, 5),
            TestContext.Current.CancellationToken);

        // Assert
        radio.Bounds.ShouldBe(new Rect(0, 0, 12, 5));
        surface.Cell(new Point(0, 0)).Text.ShouldBe("(");
        surface.Cell(new Point(0, 4)).Text.ShouldBe(" ");
    }

    /// <summary>Verifies both affixes reserve their own cell column, the start affix drawn before
    /// the mark and the end affix drawn after the caption, matching the documented layout.</summary>
    [Fact]
    public async Task Render_WhenRadioButtonHasBothAffixes_PinsThemBesideTheMarkAndCaptionAsync()
    {
        // Arrange
        var radio = new RadioButton
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
            radio,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("> ( ) Go <");
    }

    /// <summary>Verifies a same-width content swap invalidates rendering only, and the mounted
    /// surface reflects the new glyph without a remeasure - the exact grading an animated affix
    /// (a spinner swapping frames) depends on.</summary>
    [Fact]
    public async Task StartAffix_WhenContentChangesAtTheSameResolvedWidth_UpdatesRenderOnlyAsync()
    {
        // Arrange
        var radio = new RadioButton
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Length.Cells(10),
            Height = Length.Cells(1),
            Text = "Go",
            StartAffix = new Affix("|")
        };
        await using var surface = await ComponentSurface.MountAsync(
            radio,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        surface.Cell(default).Text.ShouldBe("|");
        var impact = Invalidation.None;

        // Act
        await surface.UpdateAsync(
            () =>
            {
                radio.Clear(Invalidation.All);
                radio.StartAffix = new Affix("/");
                impact = radio.Pending;
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
        var radio = new RadioButton("x") { Padding = new Thickness(2, 0, 0, 0) };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            radio,
            new Size(8, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("  ( ) x");
    }

    private static RadioButton Radio(
        string content,
        bool enabled = true,
        bool isChecked = false) => new()
        {
            Text = content,
            GroupName = "surface",
            IsChecked = isChecked,
            IsEnabled = enabled
        };

    private static Stack Group(params RadioButton[] members)
    {
        var group = new Stack { Orientation = Orientation.Vertical };

        foreach (var member in members)
        {
            group.Children.Add(member);
        }

        return group;
    }
}
