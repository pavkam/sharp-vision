// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves ComboBox and its transient list through mounted terminal surfaces.</summary>
public sealed class ComboBoxSurfaceTests
{
    /// <summary>Verifies an open owner-managed drop-down preserves its presentation while an
    /// unrelated ancestor is disabled and enters one fresh modal scope after recovery.</summary>
    [Fact]
    public async Task IsOpen_WhenAncestorIsDisabledAndRecovers_PreservesDropDownAndRestoresScopeAsync()
    {
        // Arrange
        var combo = new ComboBox { Items = ["One", "Two"], SelectedIndex = 0 };
        var root = new Overlay { Children = { combo } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => combo.IsOpen = true, "open ComboBox drop-down");
        var first = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(() => root.IsEnabled = false, "disable ComboBox ancestor");

        // Assert
        combo.IsOpen.ShouldBeTrue();
        first.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();

        // Act
        await surface.UpdateAsync(() => root.IsEnabled = true, "restore ComboBox ancestor");

        // Assert
        var restored = surface.Application.Modality.Active.ShouldNotBeNull();
        restored.ShouldNotBeSameAs(first);
        restored.Root.ShouldBeSameAs(combo);
        restored.IsActive.ShouldBeTrue();
    }

    /// <summary>Verifies the shared owner-managed input popup does not close an unrelated
    /// owner-managed popup elsewhere in the same tree.</summary>
    [Fact]
    public async Task IsOpen_WhenOpened_DoesNotCloseUnrelatedOwnerManagedPopupAsync()
    {
        // Arrange
        var pinned = new Popup
        {
            Content = new ControlText("Pinned"),
            ModalBehavior = PopupModalBehavior.None,
            FocusOnOpen = false
        };
        var combo = new ComboBox { Items = ["One", "Two"], SelectedIndex = 0 };
        var root = new Overlay { Children = { pinned, combo } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 12),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => pinned.IsOpen = true, "open unrelated popup");

        // Act
        await surface.UpdateAsync(() => combo.IsOpen = true, "open ComboBox popup");

        // Assert
        combo.IsOpen.ShouldBeTrue();
        pinned.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies Turbo Vision preserves the bright bottom edge of a disabled sunken field.</summary>
    [Fact]
    public async Task Render_WhenTurboVisionComboBoxIsDisabled_KeepsBottomBorderVisibleAsync()
    {
        // Arrange
        var combo = new ComboBox
        {
            Items = ["Locked choice"],
            SelectedIndex = 0,
            IsEnabled = false,
            Width = Length.Cells(12),
            Height = Length.Cells(3)
        };
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor }
        };
        await using var surface = await ComponentSurface.MountAsync(
            combo,
            new Size(12, 3),
            options,
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(
            () => surface.Application.Theme = ThemeCatalog.Load("turbo-vision"),
            "apply Turbo Vision to the disabled ComboBox");

        // Assert
        var bottom = surface.Cell(new Point(5, 2)).Style;
        bottom.Foreground.ShouldBe(Color.FromHex("#ffffff"));
        bottom.Foreground.ShouldNotBe(bottom.Background);
    }

    /// <summary>Verifies selectable and editable inputs paint the theme's raised input surface.</summary>
    [Fact]
    public async Task Theme_WhenInputControlsAreMounted_UsesSurfaceBackgroundAsync()
    {
        // Arrange
        var combo = new ComboBox
        {
            Items = ["Dark"],
            SelectedIndex = 0,
            Width = Length.Cells(10),
            Height = Length.Cells(3)
        };
        var input = new TextInput
        {
            Text = "Dark",
            Width = Length.Cells(10),
            Height = Length.Cells(3)
        };
        var controls = new Stack { Children = { combo, input } };
        var trueColor = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor };
        var options = TerminalOptions.Minimal with { Capabilities = trueColor };
        await using var surface = await ComponentSurface.MountAsync(
            controls,
            new Size(10, 6),
            options,
            TestContext.Current.CancellationToken);
        var theme = ThemeCatalog.Load("catppuccin-mocha");

        // Act
        await surface.UpdateAsync(
            () => surface.Application.Theme = theme,
            "apply Catppuccin Mocha to true-color inputs");

        // Assert
        var expected = ThemeColorHelper.Surface(theme);
        expected.ShouldNotBe(ThemeColorHelper.Background(theme));
        surface.Cell(new Point(1, 1)).Style.Background.ShouldBe(expected);
        surface.Cell(new Point(1, 4)).Style.Background.ShouldBe(expected);
    }

    /// <summary>Verifies the connected first row renders and remains pointer-activatable through a mounted surface.</summary>
    [Fact]
    public async Task Input_WhenConnectedDropDownRowIsClicked_CommitsChoiceAsync()
    {
        // Arrange
        var combo = new ComboBox
        {
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            DropDownHeight = 2,
            Items = ["One", "Two"],
            SelectedIndex = 1
        };
        var root = new Overlay { Children = { combo } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 7),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => combo.IsOpen = true, "open connected ComboBox drop-down");
        var list = OwnedTree.Find<UiListView>(combo).ShouldNotBeNull();

        // Assert connected mounted geometry
        list.Bounds.Y.ShouldBe(combo.Bounds.Bottom);
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y)).Text.ShouldBe("O");

        // Act
        await surface.Pointer.ClickAsync(list, new Point(1, 0));

        // Assert committed interaction
        combo.SelectedIndex.ShouldBe(0);
        combo.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies PopupChrome's border override reaches the rendered open drop-down frame,
    /// not just the property value.</summary>
    [Fact]
    public async Task PopupStyle_WhenSetAndOpen_RendersOverriddenBorderGlyphAsync()
    {
        // Arrange
        var combo = new ComboBox
        {
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            DropDownHeight = 2,
            Items = ["One", "Two"],
            PopupChrome = new PopupChrome
            {
                Border = new Border(BorderSide.All, BorderGlyphStyle.Ascii, Color.Rgb(65, 43, 21), Color.Transparent, TerminalAttributes.None)
            }
        };
        var root = new Overlay { Children = { combo } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 7),
            TestContext.Current.CancellationToken);
        var popup = OwnedTree.Find<Popup>(combo).ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(() => combo.IsOpen = true, "open drop-down with overridden popup style");

        // Assert
        // ConnectsToAnchor omits the popup's top edge, so the bottom-left corner is checked instead.
        surface.Cell(new Point(popup.SurfaceBounds.X, popup.SurfaceBounds.Bottom - 1)).Text.ShouldBe("+");
    }

    /// <summary>Verifies a wheel with no ListView range to scroll is swallowed rather than
    /// completed by modal dismissal without leaking to the parent's own scroll.</summary>
    [Fact]
    public async Task Input_WhenDropDownWheelCannotScroll_ClosesWithoutParentScrollAsync()
    {
        // Arrange
        var combo = new ComboBox
        {
            Items = ["One", "Two"],
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            DropDownHeight = 3
        };
        var background = new ControlText(string.Join('\n', Enumerable.Repeat("Background", 12)));
        var parent = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { combo, background }
        };
        await using var surface = await ComponentSurface.MountAsync(
            parent,
            new Size(16, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(combo).ShouldBeTrue(),
            "focus ComboBox");
        await surface.UpdateAsync(() => combo.IsOpen = true, "open non-scrollable ComboBox");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(combo).ShouldNotBeNull();

        // Act
        await surface.Pointer.WheelAsync(list, default, wheelY: -1);

        // Assert
        parent.VerticalOffset.ShouldBe(0);
        combo.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies an outside wheel closes the modal drop-down without scrolling its parent.</summary>
    [Fact]
    public async Task Input_WhenWheelIsOutsideDropDown_ClosesWithoutParentScrollAsync()
    {
        // Arrange
        var combo = new ComboBox
        {
            Items = ["One", "Two"],
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            DropDownHeight = 3
        };
        var background = new ControlText(string.Join('\n', Enumerable.Repeat("Background", 12)));
        var parent = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { combo, background }
        };
        await using var surface = await ComponentSurface.MountAsync(
            parent,
            new Size(16, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(combo).ShouldBeTrue(),
            "focus ComboBox");
        await surface.UpdateAsync(() => combo.IsOpen = true, "open ComboBox");
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();

        // Act
        await surface.Pointer.WheelAsync(background, new Point(0, 4), wheelY: -1);

        // Assert
        parent.VerticalOffset.ShouldBe(0);
        combo.IsOpen.ShouldBeFalse();
        scope.IsActive.ShouldBeFalse();
        scope.Root.ShouldBeSameAs(combo);
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(combo);
    }

    /// <summary>Verifies a scrollable ListView consumes wheel input and keeps its modal drop-down open.</summary>
    [Fact]
    public async Task Input_WhenDropDownListScrolls_KeepsOpenWithoutParentScrollAsync()
    {
        // Arrange
        var combo = new ComboBox
        {
            Items = ["One", "Two", "Three", "Four", "Five", "Six"],
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            DropDownHeight = 3
        };
        var background = new ControlText(string.Join('\n', Enumerable.Repeat("Background", 12)));
        var parent = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { combo, background }
        };
        await using var surface = await ComponentSurface.MountAsync(
            parent,
            new Size(16, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(combo).ShouldBeTrue(),
            "focus ComboBox");
        await surface.UpdateAsync(() => combo.IsOpen = true, "open scrollable ComboBox");
        var list = OwnedTree.Find<UiListView>(combo).ShouldNotBeNull();

        // Act
        await surface.Pointer.WheelAsync(list, default, wheelY: -1);

        // Assert
        combo.IsOpen.ShouldBeTrue();
        list.VerticalOffset.ShouldBeGreaterThan(0);
        parent.VerticalOffset.ShouldBe(0);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        scope.Root.ShouldBeSameAs(combo);
        scope.IsActive.ShouldBeTrue();
    }

    /// <summary>Verifies the closed ComboBox renders the selected item text and dropdown indicator glyph.</summary>
    [Fact]
    public async Task Render_WhenClosedWithSelectedItem_ShowsTextAndDropDownGlyphAsync()
    {
        // Arrange
        var combo = new ComboBox
        {
            Items = ["Alpha", "Beta", "Gamma"],
            SelectedIndex = 0,
            Width = Length.Cells(10),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            combo,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        // Assert closed display with first item
        surface.ShouldRender("""
                             ┏━━━━━━━━┓
                             ┃Alpha  ▼┃
                             ┗━━━━━━━━┛
                             """);
    }

    /// <summary>Verifies an empty mounted field renders its configured placeholder and accepts type-ahead.</summary>
    [Fact]
    public async Task Input_WhenMountedEmpty_ShowsPlaceholderAndTypeAheadSelectsAsync()
    {
        var combo = new ComboBox
        {
            Items = ["Alpha", "Beta", "Gamma"],
            Placeholder = "Choose",
            SelectedIndex = -1,
            Width = Length.Cells(10),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            combo,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        surface.ShouldRender("""
                             ┏━━━━━━━━┓
                             ┃Choose ▼┃
                             ┗━━━━━━━━┛
                             """);

        await surface.UpdateAsync(() => combo.IsOpen = true, "open ComboBox for type ahead");
        await surface.Keyboard.TypeAsync("g");

        combo.SelectedItem.ShouldBe("Gamma");
    }

    /// <summary>Verifies the open ComboBox renders the popup list with all items visible below the field.</summary>
    [Fact]
    public async Task Render_WhenDropDownIsOpen_ShowsPopupListWithAllItemsAsync()
    {
        // Arrange
        var combo = new ComboBox
        {
            Items = ["Alpha", "Beta", "Gamma"],
            SelectedIndex = 0,
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            DropDownHeight = 3
        };
        var root = new Overlay { Children = { combo } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 7),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => combo.IsOpen = true, "open ComboBox drop-down");
        var list = OwnedTree.Find<UiListView>(combo).ShouldNotBeNull();

        // Assert popup list is visible below the field
        list.Bounds.Y.ShouldBe(combo.Bounds.Bottom);
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y)).Text.ShouldBe("A");
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y + 1)).Text.ShouldBe("B");
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y + 2)).Text.ShouldBe("G");
    }

    /// <summary>Verifies selecting a different item updates the closed ComboBox display text.</summary>
    [Fact]
    public async Task Render_WhenSelectionChanges_UpdatesClosedDisplayTextAsync()
    {
        // Arrange
        var combo = new ComboBox
        {
            Items = ["Alpha", "Beta", "Gamma"],
            SelectedIndex = 0,
            Width = Length.Cells(10),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            combo,
            new Size(10, 3),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
                             ┏━━━━━━━━┓
                             ┃Alpha  ▼┃
                             ┗━━━━━━━━┛
                             """);

        // Act
        await surface.UpdateAsync(() => combo.SelectedIndex = 2, "select Gamma");

        // Assert updated display
        surface.ShouldRender("""
                             ┏━━━━━━━━┓
                             ┃Gamma  ▼┃
                             ┗━━━━━━━━┛
                             """);
    }

    /// <summary>Verifies field press, popup navigation, release activation, and unavailable cleanup.</summary>
    [Fact]
    public async Task Input_WhenDropDownNavigates_CommitsReleasedChoiceAndCleanupAsync()
    {
        // Arrange
        var combo = new ComboBox
        {
            Items = ["One", "Two", "Three"],
            Width = Length.Cells(10),
            Height = Length.Cells(3),
            DropDownHeight = 3
        };
        await using var surface = await ComponentSurface.MountAsync(
            combo,
            new Size(12, 6),
            TestContext.Current.CancellationToken);

        // Act focus, hover, and hold the field
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Pointer.MoveToAsync(combo);
        await surface.Pointer.PressAsync();

        // Assert held state before opening
        combo.IsOpen.ShouldBeFalse();
        combo.IsPressed.ShouldBeTrue();
        surface.ShouldHaveFocus(combo);
        surface.ShouldHaveCapture(combo);

        // Act release, navigate, and commit
        await surface.Pointer.ReleaseAsync();
        combo.IsOpen.ShouldBeTrue();
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert committed transient selection
        combo.SelectedIndex.ShouldBe(1);
        combo.IsOpen.ShouldBeFalse();
        combo.IsPressed.ShouldBeFalse();
        surface.ShouldHaveFocus(combo);
        surface.ShouldRender("""
                             ┏━━━━━━━━┓
                             ┃Two    ▼┃
                             ┗━━━━━━━━┛



                             """);

        // Act unavailable while another field press is held
        await surface.Pointer.MoveToAsync(combo);
        await surface.Pointer.PressAsync();
        await surface.UpdateAsync(() => combo.IsEnabled = false, "disable held ComboBox");

        // Assert cleanup
        combo.IsPressed.ShouldBeFalse();
        combo.IsFocused.ShouldBeFalse();
        combo.IsOpen.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveState(combo, VisualState.Disabled);

        // The disable cleanup dropped the control-side capture and press without the test-side
        // pointer driver observing it, so release its own bookkeeping before pressing again.
        await surface.Pointer.ReleaseAsync();

        // Act re-enable and resume interaction
        await surface.UpdateAsync(() => combo.IsEnabled = true, "re-enable ComboBox");
        surface.ShouldHaveState(combo, VisualState.Normal);
        await surface.Pointer.MoveToAsync(combo);
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert normal interaction resumes
        surface.ShouldHaveFocus(combo);
        combo.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a ComboBox inherits disabled state from an ancestor and keeps stable
    /// geometry across a genuine resize while disabled, matching an independently-mounted enabled
    /// instance arranged at the same size.</summary>
    [Fact]
    public async Task Input_WhenAncestorDisablesComboBoxAndResized_InheritsStateAndPreservesGeometryAsync()
    {
        // Arrange a ComboBox disabled only through its ancestor
        var combo = new ComboBox
        {
            Items = ["One", "Two"],
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { combo }
        };
        await using var surface = await ComponentSurface.MountAsync(
            overlay,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        // Act disable the ancestor, not the ComboBox itself
        await surface.UpdateAsync(() => overlay.IsEnabled = false, "disable ComboBox's ancestor");

        // Assert the disabled state is inherited
        combo.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(combo, VisualState.Disabled);

        // Act resize to a genuinely different size while disabled
        await surface.ResizeAsync(new Size(20, 6));

        // Assert geometry matches an independently-mounted enabled instance at the same size
        var reference = new ComboBox
        {
            Items = ["One", "Two"],
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var referenceSurface = await ComponentSurface.MountAsync(
            reference,
            new Size(20, 6),
            TestContext.Current.CancellationToken);

        combo.Bounds.ShouldBe(reference.Bounds);
        combo.DesiredSize.ShouldBe(reference.DesiredSize);
    }

    /// <summary>Verifies a default-height field keeps its closed geometry while its connected drop-down is open.</summary>
    [Fact]
    public async Task Input_WhenDefaultHeightDropDownOpens_KeepsFieldHeightAndConnectsListAsync()
    {
        // Arrange
        var combo = new ComboBox
        {
            Items = ["One", "Two", "Three"],
            SelectedIndex = 0
        };
        var root = new Stack { Children = { combo } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 16),
            TestContext.Current.CancellationToken);
        var closedBounds = combo.Bounds;

        // Act
        await surface.UpdateAsync(() => combo.IsOpen = true, "open default-height ComboBox drop-down");

        // Assert
        var list = OwnedTree.Find<UiListView>(combo).ShouldNotBeNull();
        combo.Bounds.ShouldBe(closedBounds);
        list.Bounds.Y.ShouldBe(combo.Bounds.Bottom);
    }

    /// <summary>Verifies both affixes render pinned inside the field box beside the selected
    /// label, strictly inboard of the drop-down indicator, through a mounted terminal surface.</summary>
    [Fact]
    public async Task Render_WhenComboBoxHasBothAffixes_PinsThemInsideFieldBoxAsync()
    {
        // Arrange
        var combo = new ComboBox
        {
            Items = ["Alpha"],
            SelectedIndex = 0,
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        await using var surface = await ComponentSurface.MountAsync(
            combo,
            new Size(16, 3),
            TestContext.Current.CancellationToken);

        // Assert - border, ">", gap, "Alpha", gap, "<", gap, "▼".
        surface.Cell(new Point(1, 1)).Text.ShouldBe(">");
        surface.Cell(new Point(3, 1)).Text.ShouldBe("A");
        surface.Cell(new Point(7, 1)).Text.ShouldBe("a");
        surface.Cell(new Point(9, 1)).Text.ShouldBe("<");
        surface.Cell(new Point(11, 1)).Text.ShouldBe("▼");
    }

    /// <summary>Verifies the drop-down indicator keeps the same offset from the field's own right
    /// edge whether or not affixes are set, proving an affix is reserved strictly inboard of the
    /// indicator and never shifts or overlaps it.</summary>
    [Fact]
    public async Task Render_WhenAffixesAreSet_KeepsDropDownIndicatorOffsetFromRightEdgeAsync()
    {
        // Arrange
        var bare = new ComboBox { Items = ["Alpha"], SelectedIndex = 0 };
        var affixed = new ComboBox
        {
            Items = ["Alpha"],
            SelectedIndex = 0,
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<")
        };
        await using var bareSurface = await ComponentSurface.MountAsync(
            bare,
            new Size(16, 3),
            TestContext.Current.CancellationToken);
        await using var affixedSurface = await ComponentSurface.MountAsync(
            affixed,
            new Size(16, 3),
            TestContext.Current.CancellationToken);

        // Assert
        bareSurface.Cell(new Point(bare.Bounds.Right - 2, 1)).Text.ShouldBe("▼");
        affixedSurface.Cell(new Point(affixed.Bounds.Right - 2, 1)).Text.ShouldBe("▼");
    }
}
