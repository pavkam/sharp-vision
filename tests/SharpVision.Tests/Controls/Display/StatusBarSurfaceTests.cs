// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies StatusBar rendering and retained-content behavior through a mounted terminal surface.</summary>
public sealed class StatusBarSurfaceTests
{
    /// <summary>Verifies exact edge layout while the bar and item remain passive focus exclusions.</summary>
    [ComponentBehaviorEvidence(
        typeof(StatusBar),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Composition)]
    [ComponentBehaviorEvidence(
        typeof(StatusBarItem),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Render_WhenItemsUseBothAlignments_DrawsEdgeAnchoredStatusAsync()
    {
        // Arrange
        var bar = new StatusBar { Spacing = 1 };
        var ready = Item("Ready");
        var encoding = Item("UTF-8", StatusBarItemAlignment.Right);
        var position = Item("Ln 1", StatusBarItemAlignment.Right);
        bar.Items.Add(ready);
        bar.Items.Add(encoding);
        bar.Items.Add(position);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 1),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(ready);

        // Assert
        surface.ShouldRender("Ready     UTF-8 Ln 1");
        bar.PointerOver.ShouldBeTrue();
        ready.PointerOver.ShouldBeTrue();
        bar.CanFocus.ShouldBeFalse();
        ready.CanFocus.ShouldBeFalse();
        bar.Pressed.ShouldBeFalse();
        ready.Pressed.ShouldBeFalse();
    }

    /// <summary>Verifies a status item can retain an explicitly interactive child without becoming a command itself.</summary>
    [Fact]
    public async Task Pointer_WhenItemContainsButton_ActivatesRetainedContentAsync()
    {
        // Arrange
        var clicks = 0;
        var button = new Button
        {
            Style = TestButtonStyles.FlatWithPadding(default),
            Padding = default,
            Height = Length.Cells(1),
            Text = "Sync"
        };
        button.Click += (_, _) => clicks++;
        var item = new StatusBarItem
        {
            Alignment = StatusBarItemAlignment.Right,
            Content = button
        };
        var bar = new StatusBar();
        bar.Items.Add(item);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(button);

        // Assert
        clicks.ShouldBe(1);
        surface.ShouldRender("      Sync");
        surface.ShouldHaveFocus(button);
        item.Focused.ShouldBeFalse();
    }

    /// <summary>Verifies an accent status surface gives a retained CheckBox legible themed states automatically.</summary>
    [Fact]
    public async Task Pointer_WhenAccentStatusBarContainsCheckBox_UsesThemeSafeInteractiveStatesAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Autosave" };
        var context = new ControlText("Ready");
        var bar = new StatusBar
        {
            Face = AppearanceTestValues.Face(
                foreground: ThemeColorHelper.Background(ThemeCatalog.Dark),
                background: ThemeColorHelper.Accent(ThemeCatalog.Dark)),
        };
        bar.Items.Add(new StatusBarItem { Content = checkBox });
        bar.Items.Add(new StatusBarItem { Content = context });
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 1),
            TestContext.Current.CancellationToken);
        var hoveredForeground = TerminalPalette.Project(ThemeColorHelper.HoveredForeground(ThemeCatalog.Dark), ColorDepth.Basic16);
        var focusedForeground = TerminalPalette.Project(ThemeColorHelper.FocusedForeground(ThemeCatalog.Dark), ColorDepth.Basic16);
        var barForeground = TerminalPalette.Project(ThemeColorHelper.Background(ThemeCatalog.Dark), ColorDepth.Basic16);
        var barBackground = TerminalPalette.Project(ThemeColorHelper.Accent(ThemeCatalog.Dark), ColorDepth.Basic16);

        // Assert normal state needs no child appearance configuration
        checkBox.Face.Foreground.IsLiteral.ShouldBeFalse();
        checkBox.Face.Foreground.SemanticColor.ShouldBe(SemanticColor.ControlText);
        checkBox.AppearanceSets.ShouldBeEmpty();
        var normalAppearance = checkBox.GetResolvedAppearance(VisualState.Normal);
        normalAppearance.BackgroundMode.ShouldBe(BackgroundMode.Opaque);
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Foreground.IsRgb.ShouldBeTrue();
        var normalBackground = surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Background;
        normalBackground.IsRgb.ShouldBeTrue();

        // Act and assert hover
        await surface.Pointer.MoveToAsync(checkBox);

        surface.ShouldHaveState(checkBox, VisualState.PointerOver);
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Foreground.IsRgb.ShouldBeTrue();
        surface.Cell(new Point(checkBox.Bounds.X + 4, checkBox.Bounds.Y)).Style.Foreground.IsRgb.ShouldBeTrue();

        // Act and assert focused checked state
        await surface.Pointer.ClickAsync(checkBox);

        checkBox.IsChecked.ShouldBe(true);
        surface.ShouldHaveFocus(checkBox);
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Foreground.IsRgb.ShouldBeTrue();
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Background.IsRgb.ShouldBeTrue();

        // Act and assert checked state after focus and hover leave
        await surface.Pointer.MoveToAsync(context);
        await surface.UpdateAsync(() => checkBox.Focusable = false, "remove CheckBox focus eligibility");

        checkBox.GetAppearanceState().ShouldBe(VisualState.Checked);
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Foreground.IsRgb.ShouldBeTrue();
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Background.IsRgb.ShouldBeTrue();

        // Act and assert disabled precedence
        await surface.UpdateAsync(() => checkBox.Enabled = false, "disable checked CheckBox");

        checkBox.GetAppearanceState().ShouldBe(VisualState.Checked | VisualState.Disabled);
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Foreground.IsRgb.ShouldBeTrue();
    }

    /// <summary>Verifies left and right separators render around retained content in owned cells.</summary>
    [Fact]
    public async Task Render_WhenItemHasBothSeparators_DrawsExactFramedContentAsync()
    {
        // Arrange
        var item = new StatusBarItem
        {
            ShowLeftSeparator = true,
            ShowRightSeparator = true,
            LeftSeparator = StatusBarSeparatorGlyphs.Bar,
            RightSeparator = StatusBarSeparatorGlyphs.Chevron,
            Content = new ControlText("Ready")
        };
        var bar = new StatusBar();
        bar.Items.Add(item);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(7, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("│Ready›");
        surface.Cell(default).Continuation.ShouldBeFalse();
        surface.Cell(new Point(6, 0)).Continuation.ShouldBeFalse();

        // Act tiny width
        await surface.ResizeAsync(new Size(1, 1));

        // Assert tiny width
        surface.ShouldRender("│");
        item.Content.Bounds.Width.ShouldBe(0);
    }

    private static StatusBarItem Item(
        string content,
        StatusBarItemAlignment alignment = StatusBarItemAlignment.Left) => new()
        {
            Alignment = alignment,
            Content = new ControlText(content)
        };
}
