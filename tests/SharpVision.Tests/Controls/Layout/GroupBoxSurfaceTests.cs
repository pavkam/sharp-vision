// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies GroupBox header, border, content, style, clipping, and resize through mounted surfaces.</summary>
public sealed class GroupBoxSurfaceTests
{
    /// <summary>Verifies a wide mnemonic header omits its marker and underlines the complete key grapheme.</summary>
    [Fact]
    public async Task Render_WhenHeaderContainsMnemonic_MeasuresAndDrawsVisibleHeaderAsync()
    {
        // Arrange
        var group = new GroupBox
        {
            HeaderText = "&甌 Tools",
            Content = new ControlText("Body"),
            Width = Length.Cells(12),
            Height = Length.Cells(3)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             ┌ 甌 Tools ┐
                             │Body      │
                             └──────────┘
                             """);
        surface.Cell(new Point(2, 0)).Text.ShouldBe("甌");
        surface.Cell(new Point(2, 0)).Style.Attributes.ShouldBe(TerminalAttributes.Underline);
        var accessKey = ThemeColorHelper.AccessKey(ThemeCatalog.Dark);
        surface.Cell(new Point(2, 0)).Style.Foreground.ShouldBe(accessKey);
        surface.Cell(new Point(3, 0)).Continuation.ShouldBeTrue();
    }

    /// <summary>Verifies a disabled group keeps its unavailable caption color while retaining
    /// mnemonic syntax, cascades disabled appearance to composed content — GroupBox's natural
    /// "ancestor-inherited" leg since it is content-composing — keeps geometry stable across a
    /// genuine resize, and recovers on re-enable.</summary>
    [Fact]
    public async Task Render_WhenMnemonicOwnerIsDisabled_PreservesDisabledForegroundAsync()
    {
        // Arrange
        var content = new ControlText("Body");
        var group = new GroupBox
        {
            HeaderText = "&Options",
            Content = content,
            Width = Length.Cells(12),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Act — direct disable
        await surface.UpdateAsync(() => group.IsEnabled = false, "disable GroupBox");

        // Assert
        var disabled = ThemeColorHelper.Disabled(ThemeCatalog.Dark);
        var mnemonic = surface.Cell(new Point(2, 0));
        mnemonic.Text.ShouldBe("O");
        mnemonic.Style.Foreground.ShouldBe(disabled);
        mnemonic.Style.Attributes.ShouldBe(TerminalAttributes.Underline);
        surface.ShouldHaveState(group, VisualState.Disabled);

        // Assert — composed content cascades the disabled state
        content.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(content, VisualState.Disabled);

        // Act — geometry stability across a genuine resize
        await surface.ResizeAsync(new Size(16, 4));
        var enabledGroup = new GroupBox
        {
            HeaderText = "&Options",
            Content = new ControlText("Body"),
            Width = Length.Cells(12),
            Height = Length.Cells(3)
        };
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabledGroup,
            new Size(16, 4),
            TestContext.Current.CancellationToken);

        // Assert
        group.Bounds.ShouldBe(enabledGroup.Bounds);
        group.DesiredSize.ShouldBe(enabledGroup.DesiredSize);

        // Act — re-enable recovery
        await surface.UpdateAsync(() => group.IsEnabled = true, "re-enable GroupBox");

        // Assert
        surface.ShouldHaveState(group, VisualState.Normal);
    }

    /// <summary>Verifies the built-in dark theme paints a visible frame and fills the interior with
    /// the group's own themed opaque surface.</summary>
    [Fact]
    public void Render_WhenDefaultDarkThemeIsApplied_FillsFrameInteriorWithThemedSurface()
    {
        // Arrange
        var group = new GroupBox
        {
            HeaderText = "Profile",
            Content = new ControlText("Body"),
            Width = Length.Cells(10),
            Height = Length.Cells(3)
        };
        group.SetTheme(ThemeCatalog.Dark);
        var borderColor = ThemeCatalog.Dark.ResolveColor(SemanticColor.ReliefShade);
        var size = new Size(10, 3);
        new LayoutEngine().Layout(group, size);
        using Frame frame = new(size);

        // Act
        group.Render(frame.Canvas);

        // Assert
        frame.GetCell(default).Style.Foreground.ShouldBe(borderColor);
        group.GetResolvedAppearance(VisualState.Normal).BackgroundMode.ShouldBe(BackgroundMode.Opaque);
        frame.GetCell(default).Style.Background.ShouldNotBe(Color.Default);

        // The interior carries the group's own authored opaque surface fill. The stretched Text
        // content no longer scrubs it: Text never paints its own background, so the cells the
        // caption does not cover keep the fill the opaque group cleared them with.
        frame.GetCell(new Point(8, 1)).Style.Background
            .ShouldBe(ThemeCatalog.Dark.ResolveColor(SemanticColor.Surface));
    }

    /// <summary>Verifies a mounted GroupBox observes descendant hover without taking focus or press state.</summary>
    [Fact]
    public async Task Pointer_WhenContentIsHovered_TracksComposedAncestryWithoutInteractionAsync()
    {
        // Arrange
        var content = new ControlText("Body");
        var group = new GroupBox
        {
            HeaderText = "Details",
            Content = content,
            Width = Length.Cells(12),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(content);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        content.Parent.ShouldBeSameAs(group);
        group.IsPointerOver.ShouldBeTrue();
        group.IsPointerDirectlyOver.ShouldBeFalse();
        content.IsPointerDirectlyOver.ShouldBeTrue();
        group.IsFocused.ShouldBeFalse();
        group.IsPressed.ShouldBeFalse();
        content.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies hovering grouped content leaves the passive group surface unchanged.</summary>
    [Fact]
    public async Task Pointer_WhenContentIsHovered_PreservesPassiveSurfaceAsync()
    {
        // Arrange
        var content = new ControlText("Body");
        var group = new GroupBox
        {
            HeaderText = "Details",
            Content = content,
            Width = Length.Cells(12),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        var normalBackground = surface.Cell(new Point(1, 1)).Style.Background;
        var normalBorder = surface.Cell(default).Style.Foreground;

        // Act
        await surface.Pointer.MoveToAsync(content);

        // Assert
        var hoveredBorder = surface.Cell(default).Style.Foreground;
        surface.Cell(new Point(1, 1)).Style.Background.ShouldBe(normalBackground);
        hoveredBorder.ShouldBe(normalBorder);
    }

    /// <summary>Verifies descendant focus does not paint the passive group frame as directly focused.</summary>
    [Fact]
    public async Task Focus_WhenDescendantOwnsFocus_KeepsGroupFrameInactiveAsync()
    {
        // Arrange
        var input = new TextInput { Text = "Value" };
        var group = new GroupBox
        {
            HeaderText = "Details",
            Content = input,
            Width = Length.Cells(12),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        var inactiveBorder = TerminalPalette.Project(ThemeColorHelper.InactiveBorder(ThemeCatalog.Dark), ColorDepth.Basic16);
        surface.Cell(default).Style.Foreground.ShouldBe(inactiveBorder);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        input.IsFocused.ShouldBeTrue();
        group.IsFocused.ShouldBeFalse();
        group.ContainsFocus.ShouldBeTrue();
        (group.GetAppearanceState() & VisualState.FocusWithin).ShouldBe(VisualState.FocusWithin);
        surface.Cell(default).Style.Foreground.ShouldBe(inactiveBorder);
    }

    /// <summary>Verifies empty and wide headers draw exact continuous or interrupted rounded frames.</summary>
    [Fact]
    public async Task Render_WhenHeaderIsEmptyOrWide_DrawsExactFrameAndContentAsync()
    {
        // Arrange empty header
        var empty = new GroupBox
        {
            Content = new ControlText("Hi"),
            Width = Length.Cells(8),
            Height = Length.Cells(3)
        };
        await using var emptySurface = await ComponentSurface.MountAsync(
            empty,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert empty header
        empty.Content.Bounds.ShouldBe(new Rect(1, 1, 6, 1));
        emptySurface.ShouldRender("""
                                  ┌──────┐
                                  │Hi    │
                                  └──────┘
                                  """);

        // Arrange wide header
        var wide = new GroupBox
        {
            HeaderText = "甌 Tools",
            Content = new ControlText("Body"),
            Width = Length.Cells(12),
            Height = Length.Cells(3)
        };
        await using var wideSurface = await ComponentSurface.MountAsync(
            wide,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Assert wide header
        wideSurface.ShouldRender("""
                                 ┌ 甌 Tools ┐
                                 │Body      │
                                 └──────────┘
                                 """);
        wideSurface.Cell(new Point(2, 0)).Text.ShouldBe("甌");
        wideSurface.Cell(new Point(3, 0)).Continuation.ShouldBeTrue();
    }

    /// <summary>Verifies a tiny header clips between preserved corners and resize reveals content without stale cells.</summary>
    [Fact]
    public async Task ResizeAsync_WhenHeaderStartsTiny_PreservesCornersThenRevealsFrameAsync()
    {
        // Arrange
        var group = new GroupBox
        {
            HeaderText = "Long title",
            Content = new ControlText("Body"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(5, 2),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
                             ┌ Lo┐
                             └───┘
                             """);

        // Act
        await surface.ResizeAsync(new Size(14, 3));

        // Assert
        group.Content.Bounds.ShouldBe(new Rect(1, 1, 12, 1));
        surface.ShouldRender("""
                             ┌ Long title ┐
                             │Body        │
                             └────────────┘
                             """);
    }

    /// <summary>Verifies changing the header dynamically updates the rendered title.</summary>
    [Fact]
    public async Task UpdateAsync_WhenHeaderChanges_RedrawsTitleInTopBorderAsync()
    {
        // Arrange
        var group = new GroupBox
        {
            HeaderText = "Old",
            Content = new ControlText("Body"),
            Width = Length.Cells(12),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
                             ┌ Old ─────┐
                             │Body      │
                             └──────────┘
                             """);

        // Act
        await surface.UpdateAsync(() => group.HeaderText = "New title", "change GroupBox header");

        // Assert
        surface.ShouldRender("""
                             ┌ New title┐
                             │Body      │
                             └──────────┘
                             """);
    }

    /// <summary>Verifies a direct face foreground does not replace theme-owned frame or header colors.</summary>
    [Fact]
    public async Task Render_WhenFaceIsAssigned_KeepsFrameAndHeaderThemeOwnedAsync()
    {
        // Arrange
        var content = new ControlText("Styled");
        var group = new GroupBox
        {
            HeaderText = "Theme",
            Content = content,
            Face = AppearanceTestValues.Face(foreground: ReferenceColors.Get(2)),
            Width = Length.Cells(10),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(default).Style.Foreground.IsRgb.ShouldBeTrue();
        surface.Cell(default).Style.Foreground.ShouldNotBe(ReferenceColors.Get(2));
        surface.Cell(new Point(2, 0)).Style.Foreground.ShouldBe(surface.Cell(default).Style.Foreground);
        // The transparent content text inherits the group's ambient face, so the locally
        // assigned foreground reaches it while the frame and header stay theme-owned above.
        surface.Cell(new Point(1, 1)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(ReferenceColors.Get(2), ColorDepth.Basic16));
    }

    /// <summary>Verifies content collapsing at runtime clears its committed cells and hit target
    /// while the border and header keep rendering, and that restoring it redraws exactly the
    /// original cells - proving the frame never leaves stale content cells behind either way.</summary>
    [Fact]
    public async Task UpdateAsync_WhenContentCollapsesAndRestores_ClearsThenRedrawsContentCellsAsync()
    {
        // Arrange
        var clicked = 0;
        var content = new Button
        {
            Text = "GO",
            Style = TestButtonStyles.FlatWithPadding(default),
            Padding = default,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        content.Click += (_, _) => clicked++;
        var group = new GroupBox
        {
            HeaderText = "Details",
            Content = content,
            Width = Length.Cells(12),
            Height = Length.Cells(3)
        };
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(content);
        clicked.ShouldBe(1);
        // The button centers "GO" inside its filled 10-cell content face, starting at column 5.
        surface.Cell(new Point(5, 1)).Text.ShouldBe("G");
        surface.Cell(new Point(6, 1)).Text.ShouldBe("O");

        // Act collapse content, leaving the GroupBox's own frame untouched
        await surface.UpdateAsync(() => content.Visibility = Visibility.Collapsed, "collapse GroupBox content");

        // Assert the frame keeps rendering while the content cells and hit target clear
        content.Bounds.ShouldBe(default);
        surface.Cell(default).Text.ShouldBe("┌");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("D");
        surface.Cell(new Point(5, 1)).Text.ShouldBe(" ");
        await surface.Pointer.ClickAsync(group, new Point(1, 1));
        clicked.ShouldBe(1);

        // Act restore content
        await surface.UpdateAsync(() => content.Visibility = Visibility.Visible, "restore GroupBox content");

        // Assert content cells and hit target return without recreating the control
        surface.Cell(new Point(5, 1)).Text.ShouldBe("G");
        surface.Cell(new Point(6, 1)).Text.ShouldBe("O");
        await surface.Pointer.ClickAsync(content);
        clicked.ShouldBe(2);
    }

    /// <summary>Verifies a collapsed text header omits its caption from the rendered top border, matching
    /// the plain border a non-text header renders through the ordinary <see cref="ControlBase.Render(TerminalCanvas)"/>
    /// gate - and matching what <see cref="GroupBox"/>'s own measure pass already assumes by reporting
    /// zero header width once collapsed.</summary>
    [Fact]
    public async Task Render_WhenHeaderIsCollapsed_OmitsCaptionFromBorderAsync()
    {
        // Arrange
        var group = new GroupBox
        {
            HeaderText = "Hidden",
            Content = new ControlText("Body"),
            Width = Length.Cells(12),
            Height = Length.Cells(3)
        };
        group.Header!.Visibility = Visibility.Collapsed;

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             ┌──────────┐
                             │Body      │
                             └──────────┘
                             """);
    }

    /// <summary>Verifies a non-text header renders through the ordinary owned-control pipeline, proving the
    /// header ownership role hosts arbitrary rich content rather than only a text caption.</summary>
    [Fact]
    public async Task Render_WhenHeaderIsARichControl_RendersThroughTheOwnedControlPipelineAsync()
    {
        // Arrange
        var group = new GroupBox
        {
            Header = new ProbeControl(new Size(1, 1)) { Content = "R".AsMemory() },
            Content = new ControlText("Body"),
            Width = Length.Cells(12),
            Height = Length.Cells(3)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            group,
            new Size(12, 3),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             ┌ R ───────┐
                             │Body      │
                             └──────────┘
                             """);
        group.Header.ShouldBeOfType<ProbeControl>().Bounds.ShouldBe(new Rect(2, 0, 1, 1));
    }
}
