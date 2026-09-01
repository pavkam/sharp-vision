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

    /// <summary>Verifies focused arrow, paging, and endpoint commands use the arranged feasible cell range.</summary>
    [Theory]
    [InlineData(Orientation.Horizontal, Code.Right, 7)]
    [InlineData(Orientation.Horizontal, Code.Left, 3)]
    [InlineData(Orientation.Vertical, Code.Up, 7)]
    [InlineData(Orientation.Vertical, Code.Down, 3)]
    [InlineData(Orientation.Horizontal, Code.PageUp, 8)]
    [InlineData(Orientation.Horizontal, Code.PageDown, 2)]
    [InlineData(Orientation.Horizontal, Code.Home, 0)]
    [InlineData(Orientation.Horizontal, Code.End, 10)]
    public async Task Keyboard_WhenSplitPaneIsFocused_AppliesOrientationAndRangeCommandAsync(
        Orientation orientation,
        Code code,
        int expectedExtent)
    {
        // Arrange
        var pane = new SplitPane
        {
            Orientation = orientation,
            FirstPaneLength = Length.Cells(5),
            SmallChange = 2,
            LargeChange = 3,
            Children = { new ProbeControl(), new ProbeControl() }
        };
        var size = orientation == Orientation.Horizontal ? new Size(11, 3) : new Size(3, 11);
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            size,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(pane);

        // Act
        await surface.Keyboard.PressAsync(code);

        // Assert
        pane.FirstPaneLength.ShouldBe(Length.Cells(expectedExtent));
        var actualExtent = orientation == Orientation.Horizontal
            ? pane.Children[0].Bounds.Width
            : pane.Children[0].Bounds.Height;
        actualExtent.ShouldBe(expectedExtent);
    }

    /// <summary>Verifies keyboard commits preserve percentage authorship across odd divider-excluded pools.</summary>
    [Theory]
    [InlineData(10, 6, 66.66666666666667)]
    [InlineData(12, 7, 63.63636363636363)]
    [InlineData(14, 8, 61.53846153846154)]
    public async Task Keyboard_WhenPercentSplitMovesAcrossOddPool_PreservesPercentKindAsync(
        int width,
        int expectedExtent,
        double expectedPercent)
    {
        // Arrange
        var pane = new SplitPane
        {
            FirstPaneLength = Length.Percent(50),
            Children = { new ProbeControl(), new ProbeControl() }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(width, 2),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert
        pane.FirstPaneLength.Kind.ShouldBe(LengthKind.Percent);
        pane.FirstPaneLength.Value.ShouldBe(expectedPercent, 0.000000000001);
        pane.Children[0].Bounds.Width.ShouldBe(expectedExtent);
    }

    /// <summary>Verifies recognized zero and endpoint commands are handled without publishing an effective-cell no-op.</summary>
    [Theory]
    [InlineData(Code.Right, 0)]
    [InlineData(Code.End, 1)]
    public async Task Keyboard_WhenCommandCannotChangeEffectiveCell_HandlesWithoutPublishingAsync(
        Code code,
        int smallChange)
    {
        // Arrange
        var pane = new SplitPane
        {
            FirstPaneLength = Length.Cells(10),
            SmallChange = smallChange,
            Children = { new ProbeControl(), new ProbeControl() }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 2),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        var propertyChanges = 0;
        var splitChanges = 0;
        pane.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SplitPane.FirstPaneLength))
            {
                propertyChanges++;
            }
        };
        pane.SplitChanged += (_, _) => splitChanges++;
        KeyEventArgs? routed = null;

        // Act
        await surface.UpdateAsync(
            () =>
            {
                routed = Key(code);
                _ = Router.Route(pane, Events.Key, routed);
            },
            $"route no-op SplitPane {code}");

        // Assert
        routed.ShouldNotBeNull().IsHandled.ShouldBeTrue();
        pane.FirstPaneLength.ShouldBe(Length.Cells(10));
        propertyChanges.ShouldBe(0);
        splitChanges.ShouldBe(0);
    }

    /// <summary>Verifies an arrow routed from a focused descendant remains the descendant's input.</summary>
    [Fact]
    public async Task Keyboard_WhenPaneDescendantIsFocused_DoesNotRunSplitPaneCommandAsync()
    {
        // Arrange
        var first = new Button { Text = "First" };
        var pane = new SplitPane
        {
            FirstPaneLength = Length.Cells(5),
            Children = { first, new Button { Text = "Second" } }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 2),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
        var descendantKeys = 0;
        first.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Stroke.Code == Code.Right)
            {
                descendantKeys++;
            }
        };

        // Act
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert
        pane.FirstPaneLength.ShouldBe(Length.Cells(5));
        descendantKeys.ShouldBe(1);
        surface.ShouldHaveFocus(first);
    }

    /// <summary>Verifies modified, unknown, released, and wheel records remain available to normal routing.</summary>
    [Fact]
    public async Task Input_WhenRecordIsNotAPlainSplitCommand_DoesNotConsumeItAsync()
    {
        // Arrange
        var pane = new SplitPane
        {
            FirstPaneLength = Length.Cells(5),
            Children = { new Button { Text = "First" }, new Button { Text = "Second" } }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 2),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        var records = new List<RoutedEventArgs>();

        // Act
        await surface.UpdateAsync(
            () =>
            {
                records.Add(Route(pane, Key(Code.Right, Modifiers.Shift)));
                records.Add(Route(pane, Key(Code.F1)));
                records.Add(Route(pane, Key(Code.Right, Modifiers.None, KeyAction.Release)));
                records.Add(Route(pane, Wheel()));
            },
            "route non-command SplitPane input");

        // Assert
        records.ShouldAllBe(record => !record.IsHandled);
        pane.FirstPaneLength.ShouldBe(Length.Cells(5));

        // Act and assert: Tab remains owned by normal traversal.
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(pane.Children[0]);
    }

    /// <summary>Verifies losing sequential eligibility does not evict existing programmatic focus.</summary>
    [Fact]
    public async Task CanTabStop_WhenDividerBecomesUnavailable_PreservesExistingFocusAsync()
    {
        // Arrange
        var pane = CreatePane();
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 3),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => _ = pane.Focus(), "focus SplitPane programmatically");
        surface.ShouldHaveFocus(pane);

        // Act and assert: resizability
        await surface.UpdateAsync(() => pane.IsResizable = false, "disable SplitPane resizing");
        pane.CanTabStop.ShouldBeFalse();
        surface.ShouldHaveFocus(pane);

        // Act and assert: pane visibility
        await surface.UpdateAsync(
            () =>
            {
                pane.IsResizable = true;
                pane.Children[0].Visibility = Visibility.Hidden;
            },
            "hide one SplitPane pane");
        pane.CanTabStop.ShouldBeFalse();
        surface.ShouldHaveFocus(pane);
    }

    /// <summary>Verifies sequential traversal skips an unavailable divider while retaining its pane descendants.</summary>
    [Fact]
    public async Task Keyboard_WhenDividerIsNotResizable_TabsDirectlyToPaneContentAsync()
    {
        // Arrange
        var pane = CreatePane();
        pane.IsResizable = false;
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        pane.CanTabStop.ShouldBeFalse();
        surface.ShouldHaveFocus(pane.Children[0]);
    }

    /// <summary>Creates one split with two opaque panes suitable for mounted divider rendering.</summary>
    /// <returns>The initialized split pane.</returns>
    private static SplitPane CreatePane() => new()
    {
        Children = { new Button { Text = "A" }, new Button { Text = "B" } }
    };

    /// <summary>Creates one routed key record for direct mounted routing assertions.</summary>
    private static KeyEventArgs Key(
        Code code,
        Modifiers modifiers = Modifiers.None,
        KeyAction action = KeyAction.Press) =>
        new(new Stroke(code, character: null, nativeCode: 0, modifiers, action));

    /// <summary>Creates one wheel record that SplitPane must leave to normal routing.</summary>
    private static PointerEventArgs Wheel() => new(new Pointer(
        new Point(5, 0),
        pixels: null,
        Buttons.None,
        PointerAction.Wheel,
        wheelX: 0,
        wheelY: 1,
        Modifiers.None,
        isMotion: false,
        isCellPositionInferred: false));

    /// <summary>Routes one record and returns the same instance for handled-state assertions.</summary>
    private static T Route<T>(ControlBase target, T eventArgs)
        where T : RoutedEventArgs
    {
        _ = eventArgs switch
        {
            KeyEventArgs key => Router.Route(target, Events.Key, key),
            PointerEventArgs pointer => Router.Route(target, Events.Pointer, pointer),
            _ => throw new UnreachableException()
        };
        return eventArgs;
    }
}
