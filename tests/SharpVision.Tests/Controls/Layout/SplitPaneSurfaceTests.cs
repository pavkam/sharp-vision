// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies SplitPane's owner-rendered divider, visible clipping, and divider-specific hover through mounted surfaces.</summary>
public sealed class SplitPaneSurfaceTests
{
    /// <summary>Verifies divider hover is retained only for the divider cell, never for an ordinary pane.</summary>
    [Fact]
    public async Task Pointer_WhenOverDivider_SetsDividerHoverWithoutTreatingPaneHoverAsDividerHoverAsync()
    {
        // Arrange
        var pane = new SplitPane
        {
            Children = { new Button { Text = "A" }, new Button { Text = "B" } }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 3),
            TestContext.Current.CancellationToken);

        // Act and assert pane hover
        await surface.Pointer.MoveToAsync(pane.Children[0]);
        pane.HasDividerPointerOver().ShouldBeFalse();

        // Act and assert divider hover
        await surface.Pointer.MoveToAsync(pane, new Point(5, 1));
        pane.HasDividerPointerOver().ShouldBeTrue();
    }

    /// <summary>Verifies a handled descendant move replaces an earlier divider cell and clears divider-specific hover.</summary>
    [Fact]
    public async Task Pointer_WhenHandledDescendantMoveLeavesDivider_ClearsDividerHoverAsync()
    {
        // Arrange
        var second = new Button { Text = "B" };
        second.PointerMoved += (_, eventArgs) => eventArgs.IsHandled = true;
        var pane = new SplitPane
        {
            Children = { new Button { Text = "A" }, second }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(pane, new Point(5, 1));
        pane.HasDividerPointerOver().ShouldBeTrue();

        // Act
        await surface.Pointer.MoveToAsync(second);

        // Assert
        pane.IsPointerOver.ShouldBeTrue();
        pane.IsPointerDirectlyOver.ShouldBeFalse();
        pane.HasDividerPointerOver().ShouldBeFalse();
    }

    /// <summary>Verifies a horizontal split paints the code-owned vertical divider over its pane descendants.</summary>
    [Fact]
    public async Task Render_WhenHorizontalSplitHasTwoPanes_PaintsExactVerticalDividerCellsAsync()
    {
        // Arrange
        var pane = CreatePane();
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 3),
            TestContext.Current.CancellationToken);

        // Assert
        for (var y = 0; y < 3; y++)
        {
            surface.Cell(new Point(5, y)).Text.ShouldBe("│");
        }
    }

    /// <summary>Verifies SplitPane preserves the framework text-selection adornment beneath its owner-rendered divider.</summary>
    [Fact]
    public async Task Render_WhenFrameworkTextSelectionIsActive_PreservesBaseAdornmentAsync()
    {
        // Arrange
        var pane = new SplitPane
        {
            IsTextSelectionEnabled = true,
            Children = { new ControlText("Alpha"), new ControlText("Bravo") }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(
            () => pane.SetTextSelection(new Selection(0, 1)),
            "select the first SplitPane text cell");

        // Assert
        var selectedBackground = TerminalPalette.Project(
            surface.Application.Theme.ResolveColor(SemanticColor.SelectedControl),
            ColorDepth.Basic16);
        surface.Cell(new Point(0, 0)).Style.Background.ShouldBe(selectedBackground);
        surface.Cell(new Point(5, 0)).Text.ShouldBe("│");
    }

    /// <summary>Verifies a vertical split paints the code-owned horizontal divider over its pane descendants.</summary>
    [Fact]
    public async Task Render_WhenVerticalSplitHasTwoPanes_PaintsExactHorizontalDividerCellsAsync()
    {
        // Arrange
        var pane = CreatePane();
        pane.Orientation = Orientation.Vertical;
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 3),
            TestContext.Current.CancellationToken);

        // Assert
        for (var x = 0; x < 11; x++)
        {
            surface.Cell(new Point(x, 1)).Text.ShouldBe("─");
        }
    }

    /// <summary>Verifies ambiguous box-drawing divider glyphs use their one-cell ASCII repair under a wide policy.</summary>
    [Theory]
    [InlineData(Orientation.Horizontal, "|")]
    [InlineData(Orientation.Vertical, "-")]
    public async Task Render_WhenDividerGlyphIsAmbiguousUnderWidePolicy_UsesSingleCellFallbackAsync(
        Orientation orientation,
        string expected)
    {
        // Arrange
        var pane = CreatePane();
        pane.Orientation = orientation;
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 3),
            TerminalOptions.Minimal with
            {
                Capabilities = TerminalCapabilities.Conservative with { AmbiguousWidth = Ambiguous.Wide }
            },
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(5, 1)).Text.ShouldBe(expected);
    }

    /// <summary>Verifies a descendant pane and the uncovered owner margin do not masquerade as divider hover.</summary>
    [Fact]
    public async Task Pointer_WhenOverOpaquePaneOrUncoveredOwner_KeepsDividerHoverFalseAsync()
    {
        // Arrange
        var first = new Button { Text = "A", Margin = new Thickness(0, 0, 1, 0) };
        var pane = new SplitPane { Children = { first, new Button { Text = "B" } } };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 3),
            TestContext.Current.CancellationToken);

        // Act and assert opaque descendant precedence
        await surface.Pointer.MoveToAsync(first);
        pane.IsPointerDirectlyOver.ShouldBeFalse();
        pane.HasDividerPointerOver().ShouldBeFalse();

        // Act and assert the owner's uncovered trailing margin
        await surface.Pointer.MoveToAsync(pane, new Point(first.Bounds.Right, 1));
        pane.IsPointerDirectlyOver.ShouldBeTrue();
        pane.HasDividerPointerOver().ShouldBeFalse();
    }

    /// <summary>Verifies an empty cross axis suppresses divider output rather than drawing beyond the arranged pane.</summary>
    [Fact]
    public async Task Render_WhenCrossAxisIsEmpty_SuppressesDividerAsync()
    {
        // Arrange
        var pane = CreatePane();
        pane.Height = Length.Cells(0);
        pane.VerticalAlignment = VerticalAlignment.Top;
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 3),
            TestContext.Current.CancellationToken);

        // Assert
        pane.LogicalDividerBounds.ShouldBe(default);
        surface.ShouldRender(string.Empty);
    }

    /// <summary>Verifies the cached pointer cell is reconciled against resized divider geometry without a new pointer event.</summary>
    [Fact]
    public async Task ResizeAsync_WhenCachedPointerLeavesMovedDivider_ClearsDividerHoverWithoutPointerMotionAsync()
    {
        // Arrange
        var pane = CreatePane();
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(pane, new Point(5, 1));
        pane.HasDividerPointerOver().ShouldBeTrue();

        // Act
        await surface.ResizeAsync(new Size(13, 3));

        // Assert
        pane.LogicalDividerBounds.ShouldBe(new Rect(6, 0, 1, 3));
        pane.HasDividerPointerOver().ShouldBeFalse();
    }

    /// <summary>Verifies a leave report clears divider-specific hover even when the framework retains ancestor hover.</summary>
    [Fact]
    public async Task Pointer_WhenLeavingTerminal_ClearsDividerHoverAsync()
    {
        // Arrange
        var pane = CreatePane();
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(pane, new Point(5, 1));
        pane.HasDividerPointerOver().ShouldBeTrue();

        // Act
        await surface.Pointer.LeaveAsync();
        await surface.ResizeAsync(new Size(13, 3));

        // Assert
        pane.HasDividerPointerOver().ShouldBeFalse();
    }

    /// <summary>Verifies divider-specific hover is reconciled when a mutation removes its visible, interactive divider.</summary>
    [Theory]
    [InlineData("resizability")]
    [InlineData("orientation")]
    [InlineData("structure")]
    [InlineData("visibility")]
    public async Task Hover_WhenMutationRemovesDivider_ClearsWithoutPointerMotionAsync(string mutation)
    {
        // Arrange
        var pane = CreatePane();
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(pane, new Point(5, 0));
        pane.HasDividerPointerOver().ShouldBeTrue();

        // Act
        await surface.UpdateAsync(
            () =>
            {
                switch (mutation)
                {
                    case "resizability":
                        pane.IsResizable = false;
                        break;
                    case "orientation":
                        pane.Orientation = Orientation.Vertical;
                        break;
                    case "structure":
                        pane.Children[1].Visibility = Visibility.Collapsed;
                        break;
                    case "visibility":
                        pane.Visibility = Visibility.Hidden;
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown divider mutation '{mutation}'.");
                }
            },
            $"remove SplitPane divider through {mutation}");

        // Assert
        pane.HasDividerPointerOver().ShouldBeFalse();
    }

    /// <summary>Verifies horizontal and vertical scroll rails clip their perpendicular divider ends before the rails paint.</summary>
    [Theory]
    [InlineData(Orientation.Horizontal, ScrollBarVisibility.Always)]
    [InlineData(Orientation.Horizontal, ScrollBarVisibility.Auto)]
    [InlineData(Orientation.Vertical, ScrollBarVisibility.Always)]
    [InlineData(Orientation.Vertical, ScrollBarVisibility.Auto)]
    public async Task Render_WhenPerpendicularScrollRailIsVisible_ClipsDividerToViewportAsync(
        Orientation orientation,
        ScrollBarVisibility railVisibility)
    {
        // Arrange
        var pane = new SplitPane
        {
            Orientation = orientation,
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = orientation == Orientation.Horizontal
                ? railVisibility
                : ScrollBarVisibility.Hidden,
            VerticalBarVisibility = orientation == Orientation.Vertical
                ? railVisibility
                : ScrollBarVisibility.Hidden,
            Children =
            {
                orientation == Orientation.Horizontal
                    ? new ProbeControl(new Size(12, 1))
                    : new ProbeControl(new Size(1, 4)),
                orientation == Orientation.Horizontal
                    ? new ProbeControl(new Size(12, 1))
                    : new ProbeControl(new Size(1, 4))
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 3),
            TestContext.Current.CancellationToken);

        // Assert
        var expected = orientation == Orientation.Horizontal ? "│" : "─";
        var visiblePoint = new Point(5, 1);
        var railPoint = orientation == Orientation.Horizontal ? new Point(5, 2) : new Point(10, 1);
        pane.VisibleDividerBounds.Contains(visiblePoint).ShouldBeTrue();
        pane.VisibleDividerBounds.Contains(railPoint).ShouldBeFalse();
        surface.Cell(visiblePoint).Text.ShouldBe(expected);
        surface.Cell(railPoint).Text.ShouldNotBe(expected);
    }

    /// <summary>Creates one split with two opaque panes suitable for mounted divider rendering.</summary>
    /// <returns>The initialized split pane.</returns>
    private static SplitPane CreatePane() => new()
    {
        Children = { new Button { Text = "A" }, new Button { Text = "B" } }
    };
}
