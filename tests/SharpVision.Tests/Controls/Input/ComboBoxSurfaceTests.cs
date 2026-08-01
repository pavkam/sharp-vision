// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves ComboBox and its transient list through mounted terminal surfaces.</summary>
public sealed class ComboBoxSurfaceTests
{
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
        var trueColor = Capabilities.Conservative with { ColorDepth = ColorDepth.TrueColor };
        var options = TerminalOptions.Minimal with { Capabilities = trueColor };
        await using var surface = await ComponentSurface.MountAsync(
            controls,
            new Size(10, 6),
            options,
            TestContext.Current.CancellationToken);
        var theme = Themes.Load("catppuccin-mocha");

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
        var list = OwnedTree.Find<ListView>(combo).ShouldNotBeNull();

        // Assert connected mounted geometry
        list.Bounds.Y.ShouldBe(combo.Bounds.Bottom);
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y)).Text.ShouldBe("O");

        // Act
        await surface.Pointer.ClickAsync(list, new Point(1, 0));

        // Assert committed interaction
        combo.SelectedIndex.ShouldBe(0);
        combo.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies a wheel with no ListView range to scroll is left unhandled, so the
    /// documented Dismiss policy closes the drop-down instead of leaking to the parent's own
    /// scroll (see #211).</summary>
    [Fact]
    public async Task Input_WhenDropDownWheelCannotScroll_StaysOpenWithoutParentScrollAsync()
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
        var list = OwnedTree.Find<ListView>(combo).ShouldNotBeNull();

        // Act
        await surface.Pointer.WheelAsync(list, default, wheelY: -1);

        // Assert - an endpoint/no-range wheel over the open drop-down is swallowed, not treated
        // as outside interaction: it neither scrolls the enclosing parent nor closes the drop-down
        // (see #225).
        parent.VerticalOffset.ShouldBe(0);
        combo.IsOpen.ShouldBeTrue();
        scope.IsActive.ShouldBeTrue();
        surface.Application.Modality.Active.ShouldBeSameAs(scope);
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
        var list = OwnedTree.Find<ListView>(combo).ShouldNotBeNull();

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
        var list = OwnedTree.Find<ListView>(combo).ShouldNotBeNull();

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
    [ComponentBehaviorEvidence(
        typeof(ComboBox),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.Directional |
        ComponentBehavior.PressRelease |
        ComponentBehavior.Activation |
        ComponentBehavior.PointerActivation |
        ComponentBehavior.KeyboardActivation |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Transient |
        ComponentBehavior.Composition)]
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
        var list = OwnedTree.Find<ListView>(combo).ShouldNotBeNull();
        combo.Bounds.ShouldBe(closedBounds);
        list.Bounds.Y.ShouldBe(combo.Bounds.Bottom);
    }
}
