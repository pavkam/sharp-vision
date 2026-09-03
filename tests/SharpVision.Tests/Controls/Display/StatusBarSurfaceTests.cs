// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies StatusBar rendering and retained-content behavior through a mounted terminal surface.</summary>
public sealed class StatusBarSurfaceTests
{
    /// <summary>Verifies the semantic bar plane fills status faces and unused cells while disabled
    /// foregrounds and complete local item styles retain precedence.</summary>
    [Fact]
    public async Task BarAppearance_WhenThemeAndLocalStyleVary_PreservesTheRequiredPrecedenceAsync()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            bar: "#345678",
            controlExtra: """, "disabled": { "face": { "foreground":"disabledText", "background":"disabledControl" } }"""));
        var normal = Item("Normal");
        var disabled = Item("Disabled");
        disabled.IsEnabled = false;
        var localBackground = Color.Rgb(120, 30, 60);
        var local = Item("Local");
        local.Style = StatusBarItemStyle.Default with
        {
            Face = StatusBarItemStyle.Default.Face with { Background = localBackground }
        };
        var bar = new StatusBar { Spacing = 1 };
        bar.Items.Add(normal);
        bar.Items.Add(disabled);
        bar.Items.Add(local);
        var colorDepth = ColorDepth.TrueColor;
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { ColorDepth = colorDepth }
        };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(28, 1),
            options,
            TestContext.Current.CancellationToken);

        await surface.UpdateAsync(() => surface.Application.Theme = theme, "apply semantic bar theme");

        var expectedBar = TerminalPalette.Project(theme.ResolveColor(SemanticColor.Bar), colorDepth);
        surface.Cell(new Point(normal.Bounds.X, normal.Bounds.Y)).Style.Background.ShouldBe(expectedBar);
        surface.Cell(new Point(normal.Bounds.Right, normal.Bounds.Y)).Style.Background.ShouldBe(expectedBar);
        surface.Cell(new Point(bar.Bounds.Right - 1, 0)).Style.Background.ShouldBe(expectedBar);
        surface.Cell(new Point(disabled.Bounds.X, disabled.Bounds.Y)).Style.Background.ShouldBe(expectedBar);
        surface.Cell(new Point(disabled.Bounds.X, disabled.Bounds.Y)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(theme.ResolveColor(SemanticColor.DisabledText), colorDepth));
        surface.Cell(new Point(local.Bounds.X, local.Bounds.Y)).Style.Background.ShouldBe(
            TerminalPalette.Project(localBackground, colorDepth));

        await surface.UpdateAsync(() => bar.IsEnabled = false, "disable the whole StatusBar");

        surface.Cell(new Point(bar.Bounds.Right - 1, 0)).Style.Background.ShouldBe(expectedBar);
    }

    /// <summary>Verifies exact edge layout while the bar and item remain passive focus exclusions.</summary>
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
        bar.IsPointerOver.ShouldBeTrue();
        ready.IsPointerOver.ShouldBeTrue();
        bar.CanFocus.ShouldBeFalse();
        ready.CanFocus.ShouldBeFalse();
        bar.IsPressed.ShouldBeFalse();
        ready.IsPressed.ShouldBeFalse();
    }

    /// <summary>Verifies a StatusBar proves direct disable, inherits Disabled from a disabled
    /// ancestor, keeps stable geometry across a genuine resize while disabled, and resumes
    /// Normal once re-enabled.</summary>
    [Fact]
    public async Task IsEnabled_WhenBarIsDisabledDirectlyOrByAncestor_ReflectsDisabledAndRecoversAsync()
    {
        // Arrange
        var bar = new StatusBar { Spacing = 1 };
        bar.Items.Add(Item("Ready"));
        var host = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children = { bar }
        };
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(20, 1),
            TestContext.Current.CancellationToken);

        // Act direct disable
        await surface.UpdateAsync(() => bar.IsEnabled = false, "disable StatusBar directly");

        // Assert direct disable
        surface.ShouldHaveState(bar, VisualState.Disabled);

        // Act re-enable before proving ancestor inheritance in isolation
        await surface.UpdateAsync(() => bar.IsEnabled = true, "re-enable StatusBar directly");
        surface.ShouldHaveState(bar, VisualState.Normal);

        // Act ancestor disable
        await surface.UpdateAsync(() => host.IsEnabled = false, "disable ancestor Stack");

        // Assert the bar inherits Disabled without its own IsEnabled flag changing
        bar.IsEnabled.ShouldBeTrue();
        bar.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(bar, VisualState.Disabled);

        // Act a genuine resize while disabled and assert geometry stability against an
        // independently mounted, otherwise-identical enabled bar at the same new size.
        await surface.ResizeAsync(new Size(26, 1));
        var disabledBounds = bar.Bounds;
        var disabledDesiredSize = bar.DesiredSize;

        var referenceBar = new StatusBar { Spacing = 1 };
        referenceBar.Items.Add(Item("Ready"));
        var referenceHost = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children = { referenceBar }
        };
        await using var referenceSurface = await ComponentSurface.MountAsync(
            referenceHost,
            new Size(26, 1),
            TestContext.Current.CancellationToken);

        referenceBar.Bounds.ShouldBe(disabledBounds);
        referenceBar.DesiredSize.ShouldBe(disabledDesiredSize);

        // Act re-enable recovery
        await surface.UpdateAsync(() => host.IsEnabled = true, "re-enable ancestor Stack");

        // Assert Normal state and resumed interaction
        surface.ShouldHaveState(bar, VisualState.Normal);
        await surface.Pointer.MoveToAsync(bar);
        bar.IsPointerOver.ShouldBeTrue();
    }

    /// <summary>Verifies a StatusBarItem proves direct disable, inherits Disabled from its owning
    /// StatusBar, keeps stable geometry across a genuine resize while disabled, and resumes
    /// Normal once re-enabled.</summary>
    [Fact]
    public async Task IsEnabled_WhenItemIsDisabledDirectlyOrByOwningBar_ReflectsDisabledAndRecoversAsync()
    {
        // Arrange
        var ready = Item("Ready");
        var encoding = Item("UTF-8", StatusBarItemAlignment.Right);
        var bar = new StatusBar { Spacing = 1 };
        bar.Items.Add(ready);
        bar.Items.Add(encoding);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(20, 1),
            TestContext.Current.CancellationToken);

        // Act direct disable
        await surface.UpdateAsync(() => ready.IsEnabled = false, "disable StatusBarItem directly");

        // Assert direct disable
        surface.ShouldHaveState(ready, VisualState.Disabled);

        // Act re-enable before proving ancestor inheritance in isolation
        await surface.UpdateAsync(() => ready.IsEnabled = true, "re-enable StatusBarItem directly");
        surface.ShouldHaveState(ready, VisualState.Normal);

        // Act ancestor disable
        await surface.UpdateAsync(() => bar.IsEnabled = false, "disable owning StatusBar");

        // Assert a never-directly-disabled StatusBarItem inherits Disabled from its owning
        // StatusBar rather than only from its own IsEnabled flag.
        encoding.IsEnabled.ShouldBeTrue();
        encoding.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(encoding, VisualState.Disabled);

        // Act a genuine resize while disabled and assert geometry stability against an
        // independently mounted, otherwise-identical enabled bar at the same new size.
        await surface.ResizeAsync(new Size(26, 1));
        var disabledBounds = encoding.Bounds;
        var disabledDesiredSize = encoding.DesiredSize;

        var referenceReady = Item("Ready");
        var referenceEncoding = Item("UTF-8", StatusBarItemAlignment.Right);
        var referenceBar = new StatusBar { Spacing = 1 };
        referenceBar.Items.Add(referenceReady);
        referenceBar.Items.Add(referenceEncoding);
        await using var referenceSurface = await ComponentSurface.MountAsync(
            referenceBar,
            new Size(26, 1),
            TestContext.Current.CancellationToken);

        referenceEncoding.Bounds.ShouldBe(disabledBounds);
        referenceEncoding.DesiredSize.ShouldBe(disabledDesiredSize);

        // Act re-enable recovery
        await surface.UpdateAsync(() => bar.IsEnabled = true, "re-enable owning StatusBar");

        // Assert Normal state and resumed interaction
        surface.ShouldHaveState(encoding, VisualState.Normal);
        await surface.Pointer.MoveToAsync(encoding);
        encoding.IsPointerOver.ShouldBeTrue();
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
        item.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies Tab skips the passive bar and item, enters retained interactive content,
    /// and Space toggles that content without punching a background hole in the bar plane.</summary>
    [Fact]
    public async Task Keyboard_WhenItemContainsCheckBox_FocusesAndActivatesRetainedContentAsync()
    {
        // Arrange
        var checkBox = new CheckBox
        {
            Height = Length.Cells(1),
            Padding = default,
            Text = "Auto"
        };
        var item = new StatusBarItem
        {
            Alignment = StatusBarItemAlignment.Right,
            Content = checkBox
        };
        var bar = new StatusBar();
        bar.Items.Add(new StatusBarItem { Content = new ControlText("Ready") });
        bar.Items.Add(item);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(14, 1),
            TestContext.Current.CancellationToken);
        var expectedBackground = surface.Cell(default).Style.Background;

        // Act focus and activate
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressCharacterAsync(new Rune(' '));

        // Assert
        surface.ShouldHaveFocus(checkBox);
        checkBox.IsChecked.ShouldBe(true);
        bar.IsFocused.ShouldBeFalse();
        item.IsFocused.ShouldBeFalse();
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Background.ShouldBe(expectedBackground);
    }

    /// <summary>Verifies an accent status surface remains one continuous background through a
    /// retained CheckBox in every interactive state without suppressing its themed foreground.</summary>
    [Fact]
    public async Task Pointer_WhenAccentStatusBarContainsCheckBox_UsesThemeSafeInteractiveStatesAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Autosave" };
        var context = new ControlText("Ready");
        var spinner = new Spinner { IsPlaying = false };
        var activityText = new ControlText("Index");
        var activity = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children =
            {
                spinner,
                activityText
            }
        };
        var bar = new StatusBar
        {
            Face = AppearanceTestValues.Face(
                foreground: ThemeColorHelper.Background(ThemeCatalog.Dark),
                background: ThemeColorHelper.Accent(ThemeCatalog.Dark)),
        };
        bar.Items.Add(new StatusBarItem { Content = activity });
        bar.Items.Add(new StatusBarItem { Content = checkBox });
        bar.Items.Add(new StatusBarItem { Content = context });
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(30, 1),
            TestContext.Current.CancellationToken);
        var barBackground = TerminalPalette.Project(ThemeColorHelper.Accent(ThemeCatalog.Dark), ColorDepth.Basic16);

        // Assert normal state needs no child appearance configuration
        checkBox.Face.Foreground.IsLiteral.ShouldBeFalse();
        checkBox.Face.Foreground.SemanticColor.ShouldBe(SemanticColor.ControlText);
        checkBox.AppearanceSets.ShouldBeEmpty();
        var normalAppearance = checkBox.GetResolvedAppearance(VisualState.Normal);
        normalAppearance.BackgroundMode.ShouldBe(BackgroundMode.Transparent);
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Foreground.IsRgb.ShouldBeTrue();
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Background.ShouldBe(barBackground);
        surface.Cell(new Point(checkBox.Bounds.X + 4, checkBox.Bounds.Y)).Style.Background.ShouldBe(barBackground);
        surface.Cell(new Point(context.Bounds.X, context.Bounds.Y)).Style.Background.ShouldBe(barBackground);
        surface.Cell(new Point(spinner.Bounds.X, spinner.Bounds.Y)).Style.Background.ShouldBe(barBackground);
        surface.Cell(new Point(activityText.Bounds.X, activityText.Bounds.Y)).Style.Background.ShouldBe(barBackground);

        // Act and assert hover
        await surface.Pointer.MoveToAsync(checkBox);

        surface.ShouldHaveState(checkBox, VisualState.IsPointerOver);
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Foreground.IsRgb.ShouldBeTrue();
        surface.Cell(new Point(checkBox.Bounds.X + 4, checkBox.Bounds.Y)).Style.Foreground.IsRgb.ShouldBeTrue();
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Background.ShouldBe(barBackground);
        surface.Cell(new Point(checkBox.Bounds.X + 4, checkBox.Bounds.Y)).Style.Background.ShouldBe(barBackground);

        // Act and assert focused checked state
        await surface.Pointer.ClickAsync(checkBox);

        checkBox.IsChecked.ShouldBe(true);
        surface.ShouldHaveFocus(checkBox);
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Foreground.IsRgb.ShouldBeTrue();
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Background.ShouldBe(barBackground);

        // Act and assert checked state after focus and hover leave
        await surface.Pointer.MoveToAsync(context);
        await surface.UpdateAsync(() => checkBox.IsFocusable = false, "remove CheckBox focus eligibility");

        checkBox.GetAppearanceState().ShouldBe(VisualState.Checked);
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Foreground.IsRgb.ShouldBeTrue();
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Background.ShouldBe(barBackground);

        // Act and assert disabled precedence
        await surface.UpdateAsync(() => checkBox.IsEnabled = false, "disable checked CheckBox");

        checkBox.GetAppearanceState().ShouldBe(VisualState.Checked | VisualState.Disabled);
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Foreground.IsRgb.ShouldBeTrue();
        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Background.ShouldBe(barBackground);

        // Act and assert explicit child presentation remains authoritative
        var localBackground = Color.Rgb(91, 42, 117);
        await surface.UpdateAsync(
            () => checkBox.Face = checkBox.Face with { Background = localBackground },
            "author a local CheckBox background");

        surface.Cell(new Point(checkBox.Bounds.X, checkBox.Bounds.Y)).Style.Background.ShouldBe(
            TerminalPalette.Project(localBackground, ColorDepth.Basic16));
    }

    /// <summary>Verifies continuous-background participation follows retained ancestry and is
    /// released when the same default control moves back to an ordinary surface.</summary>
    [Fact]
    public async Task Content_WhenMovedBetweenStatusBarAndOrdinarySurface_UpdatesBackgroundParticipationAsync()
    {
        // Arrange
        var checkBox = new CheckBox { Text = "Autosave" };
        var item = new StatusBarItem { Content = checkBox };
        var bar = new StatusBar();
        bar.Items.Add(item);
        var ordinarySurface = new Stack { Height = Length.Cells(1) };
        var root = new Stack
        {
            Orientation = Orientation.Vertical,
            Children = { bar, ordinarySurface }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 2),
            TestContext.Current.CancellationToken);

        // Assert continuous bar context
        checkBox.GetResolvedAppearance(VisualState.Normal).BackgroundMode.ShouldBe(BackgroundMode.Transparent);

        // Act ordinary context
        await surface.UpdateAsync(
            () =>
            {
                item.Content = null;
                ordinarySurface.Children.Add(checkBox);
            },
            "move retained CheckBox out of StatusBar");

        // Assert ordinary context
        checkBox.GetResolvedAppearance(VisualState.Normal).BackgroundMode.ShouldBe(BackgroundMode.Opaque);

        // Act restored bar context
        await surface.UpdateAsync(
            () =>
            {
                _ = ordinarySurface.Children.Remove(checkBox);
                item.Content = checkBox;
            },
            "move retained CheckBox back into StatusBar");

        // Assert restored bar context
        checkBox.GetResolvedAppearance(VisualState.Normal).BackgroundMode.ShouldBe(BackgroundMode.Transparent);
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

    /// <summary>Verifies a runtime IsVisible -&gt; Collapsed -&gt; IsVisible transition on a mounted item
    /// leaves no stale rendered cells behind and keeps pointer hit targets synchronized with the
    /// item's live position at every step, not merely its initial or final one.</summary>
    [Fact]
    public async Task Pointer_WhenLeftItemTogglesCollapsedThenVisible_ClearsStaleCellsAndHitTargetsAsync()
    {
        // Arrange
        var first = Item("AAA");
        var middle = Item("BBB");
        var last = Item("CCC");
        var bar = new StatusBar { Spacing = 1 };
        bar.Items.Add(first);
        bar.Items.Add(middle);
        bar.Items.Add(last);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(11, 1),
            TestContext.Current.CancellationToken);

        // Assert baseline
        surface.ShouldRender("AAA BBB CCC");

        // Act - collapse the middle item.
        await surface.UpdateAsync(() => middle.Visibility = Visibility.Collapsed, "collapse middle status item");

        // Assert - "CCC" fully occupies the space "BBB" and its spacing used to hold, with no
        // stale "BBB" glyph or orphaned gap left behind.
        surface.ShouldRender("AAA CCC    ");
        var lastHit = bar.HitTest(new Point(last.Bounds.X, 0));
        lastHit.ShouldBeSameAs(last.Content);
        await surface.Pointer.MoveToAsync(last);
        last.IsPointerOver.ShouldBeTrue();
        middle.IsPointerOver.ShouldBeFalse();

        // Act - restore visibility.
        await surface.UpdateAsync(() => middle.Visibility = Visibility.Visible, "restore middle status item");

        // Assert - the original three-item layout is exactly restored, byte for byte.
        surface.ShouldRender("AAA BBB CCC");
        var middleHit = bar.HitTest(new Point(middle.Bounds.X, 0));
        middleHit.ShouldBeSameAs(middle.Content);
    }

    private static StatusBarItem Item(
        string content,
        StatusBarItemAlignment alignment = StatusBarItemAlignment.Left) => new()
        {
            Alignment = alignment,
            Content = new ControlText(content)
        };
}
