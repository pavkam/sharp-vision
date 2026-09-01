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
        pane.GetAppearanceState().HasFlag(VisualState.IsPointerOver).ShouldBeFalse();

        // Act and assert divider hover
        await surface.Pointer.MoveToAsync(pane, new Point(5, 1));
        pane.HasDividerPointerOver().ShouldBeTrue();
        pane.GetAppearanceState().HasFlag(VisualState.IsPointerOver).ShouldBeTrue();
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

    /// <summary>Verifies a retained Hidden pane clears divider presentation under a stationary pointer
    /// even though the pane keeps its arranged track and the logical divider stays in place.</summary>
    [Fact]
    public async Task Hover_WhenPaneBecomesHidden_ClearsWithoutPointerMotionAsync()
    {
        // Arrange
        var pane = CreatePane();
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(pane, new Point(5, 1));
        pane.HasDividerPointerOver().ShouldBeTrue();
        var dividerBounds = pane.LogicalDividerBounds;

        // Act
        await surface.UpdateAsync(
            () => pane.Children[0].Visibility = Visibility.Hidden,
            "hide the hovered SplitPane pane");

        // Assert
        pane.Children[0].Bounds.ShouldBe(new Rect(0, 0, 5, 3));
        pane.LogicalDividerBounds.ShouldBe(dividerBounds);
        pane.HasDividerPointerOver().ShouldBeFalse();
        pane.GetAppearanceState().HasFlag(VisualState.IsPointerOver).ShouldBeFalse();
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
    [InlineData(Orientation.Vertical, Code.Up, 3)]
    [InlineData(Orientation.Vertical, Code.Down, 7)]
    [InlineData(Orientation.Horizontal, Code.PageUp, 2)]
    [InlineData(Orientation.Horizontal, Code.PageDown, 8)]
    [InlineData(Orientation.Vertical, Code.PageUp, 2)]
    [InlineData(Orientation.Vertical, Code.PageDown, 8)]
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

    /// <summary>Verifies a divider press focuses before capture, preserves the authored split,
    /// and applies snapshot-relative movement even after the pointer leaves the divider.</summary>
    [Fact]
    public async Task Pointer_WhenPrimaryDividerDragMoves_UsesFocusedCapturedSnapshotWithoutPressJumpAsync()
    {
        // Arrange
        var pane = new SplitPane
        {
            FirstPaneLength = Length.Cells(5),
            Children = { new ProbeControl(), new ProbeControl() }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 3),
            TestContext.Current.CancellationToken);
        var changes = 0;
        var focusObservedWithoutCapture = false;
        pane.SplitChanged += (_, _) => changes++;
        pane.GotFocus += (_, _) => focusObservedWithoutCapture = !pane.HasPointerCapture;
        await surface.Pointer.MoveToAsync(pane, new Point(5, 1));

        // Act and assert: press
        await surface.Pointer.PressAsync();
        surface.ShouldHaveFocus(pane);
        surface.ShouldHaveCapture(pane);
        focusObservedWithoutCapture.ShouldBeTrue();
        pane.IsPressed.ShouldBeTrue();
        pane.FirstPaneLength.ShouldBe(Length.Cells(5));
        changes.ShouldBe(0);

        // Act and assert: same-cell no-op followed by movement beyond the divider under capture
        await surface.Pointer.MovePressedToAsync(pane, new Point(5, 1));
        changes.ShouldBe(0);
        await surface.Pointer.MovePressedToAsync(pane, new Point(8, 1));
        pane.FirstPaneLength.ShouldBe(Length.Cells(8));
        changes.ShouldBe(1);
        surface.ShouldHaveCapture(pane);

        // Act and assert: explicit primary release
        await surface.Pointer.ReleaseAsync();
        surface.ShouldHaveCapture(null);
        pane.IsPressed.ShouldBeFalse();
    }

    /// <summary>Verifies an active divider capture leaves wheel input available to ordinary routed
    /// ancestry without changing or ending the drag.</summary>
    [Fact]
    public async Task Pointer_WhenWheelArrivesDuringDividerDrag_LeavesWheelUnhandledAndCaptureActiveAsync()
    {
        // Arrange
        var pane = new SplitPane
        {
            Width = Length.Cells(11),
            Height = Length.Cells(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            FirstPaneLength = Length.Cells(5),
            Children = { new ProbeControl(), new ProbeControl() }
        };
        var root = new Overlay { Children = { pane } };
        var routedWheels = 0;
        _ = root.AddHandler(Events.Pointer, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Bubble && eventArgs.Pointer.Action == PointerAction.Wheel)
            {
                routedWheels++;
            }
        });
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(pane, new Point(5, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(pane);

        // Act
        await surface.Pointer.WheelAsync(pane, new Point(5, 0), wheelY: 1);

        // Assert
        routedWheels.ShouldBe(1);
        surface.ShouldHaveCapture(pane);
        pane.IsPressed.ShouldBeTrue();
        pane.FirstPaneLength.ShouldBe(Length.Cells(5));
        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies drag movement clamps through the arranged joint pane range and preserves
    /// percentage authorship against the divider-excluded pool.</summary>
    [Theory]
    [InlineData(Orientation.Horizontal, 10, 2, 60d)]
    [InlineData(Orientation.Vertical, 2, 10, 60d)]
    public async Task Pointer_WhenPercentDividerDragExceedsFeasibleRange_ClampsAndPreservesPercentKindAsync(
        Orientation orientation,
        int dragX,
        int dragY,
        double expectedPercent)
    {
        // Arrange
        var first = new ProbeControl
        {
            MinWidth = Length.Cells(2),
            MaxWidth = Length.Cells(6),
            MinHeight = Length.Cells(2),
            MaxHeight = Length.Cells(6)
        };
        var second = new ProbeControl { MinWidth = Length.Cells(4), MinHeight = Length.Cells(4) };
        var pane = new SplitPane
        {
            Orientation = orientation,
            FirstPaneLength = Length.Percent(50),
            Children = { first, second }
        };
        var size = orientation == Orientation.Horizontal ? new Size(11, 3) : new Size(3, 11);
        var divider = orientation == Orientation.Horizontal ? new Point(5, 1) : new Point(1, 5);
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            size,
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.MoveToAsync(pane, divider);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(pane);
        await surface.Pointer.MovePressedToAsync(new Point(dragX, dragY));

        // Assert
        pane.FirstPaneLength.Kind.ShouldBe(LengthKind.Percent);
        pane.FirstPaneLength.Value.ShouldBe(expectedPercent, 0.000000000001);
        (orientation == Orientation.Horizontal ? first.Bounds.Width : first.Bounds.Height).ShouldBe(6);
        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies a captured drag reads a newly narrowed feasible range rather than the
    /// endpoints that existed when capture began.</summary>
    [Fact]
    public async Task Pointer_WhenPaneConstraintNarrowsDuringDrag_ClampsMovementToLatestFeasibleRangeAsync()
    {
        // Arrange
        var first = new ProbeControl();
        var second = new ProbeControl();
        var pane = new SplitPane
        {
            FirstPaneLength = Length.Cells(5),
            Children = { first, second }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(pane, new Point(5, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(pane);
        pane.MaximumFirstPaneExtent.ShouldBe(10);

        // Act: keep the current five-cell split feasible while narrowing its upper endpoint.
        await surface.UpdateAsync(
            () => second.MinWidth = Length.Cells(3),
            "narrow SplitPane feasible range during capture");
        pane.MaximumFirstPaneExtent.ShouldBe(7);
        first.Bounds.Width.ShouldBe(5);
        surface.ShouldHaveCapture(pane);
        await surface.Pointer.MovePressedToAsync(pane, new Point(9, 0));

        // Assert
        pane.FirstPaneLength.ShouldBe(Length.Cells(7));
        first.Bounds.Width.ShouldBe(7);
        surface.ShouldHaveCapture(pane);
        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies an auxiliary release does not terminate a captured primary divider drag.</summary>
    [Fact]
    public async Task Pointer_WhenSecondaryReleaseArrivesDuringDividerDrag_PreservesCaptureAndContinuesAsync()
    {
        // Arrange
        var pane = new SplitPane
        {
            FirstPaneLength = Length.Cells(5),
            Children = { new ProbeControl(), new ProbeControl() }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(pane, new Point(5, 0));
        await surface.Pointer.PressAsync();

        // Act and assert
        await surface.Pointer.ReleaseSecondaryWhilePrimaryHeldAsync();
        surface.ShouldHaveCapture(pane);
        pane.IsPressed.ShouldBeTrue();
        await surface.Pointer.MovePressedToAsync(pane, new Point(7, 0));
        pane.FirstPaneLength.ShouldBe(Length.Cells(7));
        await surface.Pointer.ReleaseAsync();
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies both an explicit primary release and terminal Leave cancel the gesture
    /// without inventing another split commit.</summary>
    [Theory]
    [InlineData("primary")]
    [InlineData("buttonless")]
    [InlineData("leave")]
    public async Task Pointer_WhenPrimaryReleaseOrLeaveEndsDividerDrag_ClearsCaptureWithoutCommitAsync(string completion)
    {
        // Arrange
        var pane = new SplitPane
        {
            FirstPaneLength = Length.Cells(5),
            Children = { new ProbeControl(), new ProbeControl() }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(pane, new Point(5, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(pane);
        var changes = 0;
        pane.SplitChanged += (_, _) => changes++;

        // Act
        if (completion == "leave")
        {
            await surface.UpdateAsync(
                () => _ = surface.Application.Capture.Dispatch(new Pointer(
                    cells: null,
                    pixels: null,
                    Buttons.None,
                    PointerAction.Leave,
                    wheelX: 0,
                    wheelY: 0,
                    Modifiers.None,
                    isMotion: true,
                    isCellPositionInferred: false)),
                "route terminal pointer Leave to captured SplitPane");
        }
        else if (completion == "primary")
        {
            await surface.Pointer.ReleaseAsync();
        }
        else
        {
            await surface.SendAsync(
                "\u001b[<3;6;1M"u8.ToArray(),
                "release buttonless pointer over SplitPane divider");
        }

        // Assert
        surface.ShouldHaveCapture(null);
        pane.IsPressed.ShouldBeFalse();
        pane.FirstPaneLength.ShouldBe(Length.Cells(5));
        changes.ShouldBe(0);
    }

    /// <summary>Verifies focus or capture loss clears every divider gesture fact and later held
    /// motion cannot resume the cancelled snapshot.</summary>
    [Theory]
    [InlineData("focus")]
    [InlineData("capture")]
    public async Task Pointer_WhenFocusOrCaptureIsLost_CancelsDividerDragAsync(string loss)
    {
        // Arrange
        var next = new Button { Text = "Next" };
        var pane = new SplitPane
        {
            Width = Length.Cells(11),
            Height = Length.Cells(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            FirstPaneLength = Length.Cells(5),
            Children = { new ProbeControl(), new ProbeControl() }
        };
        var root = new Overlay { Children = { pane, next } };
        Overlay.SetLeft(next, Length.Cells(12));
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(pane, new Point(5, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(pane);

        // Act
        await surface.UpdateAsync(
            () =>
            {
                if (loss == "focus")
                {
                    next.Focus().ShouldBeTrue();
                }
                else
                {
                    surface.Application.Capture.Release();
                }
            },
            $"cancel SplitPane drag through {loss} loss");

        // Assert
        surface.ShouldHaveCapture(null);
        pane.IsPressed.ShouldBeFalse();
        await surface.Pointer.MovePressedToAsync(pane, new Point(8, 0));
        pane.FirstPaneLength.ShouldBe(Length.Cells(5));
        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies divider-specific mutations and framework availability transitions cancel
    /// capture while preserving the last committed authored length.</summary>
    [Theory]
    [InlineData("resizability")]
    [InlineData("orientation")]
    [InlineData("disable")]
    [InlineData("hide")]
    [InlineData("collapse-owner")]
    [InlineData("hide-pane")]
    [InlineData("collapse-pane")]
    [InlineData("remove-pane")]
    [InlineData("detach")]
    [InlineData("reparent")]
    public async Task Pointer_WhenDividerLifecycleBecomesInvalid_CancelsDragAsync(string transition)
    {
        // Arrange
        var pane = new SplitPane
        {
            Width = Length.Cells(11),
            Height = Length.Cells(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            FirstPaneLength = Length.Cells(5),
            Children = { new ProbeControl(), new ProbeControl() }
        };
        var source = new Overlay
        {
            Width = Length.Cells(11),
            Height = Length.Cells(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { pane }
        };
        var destination = new Overlay
        {
            Width = Length.Cells(11),
            Height = Length.Cells(2),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var root = new Overlay { Children = { source, destination } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(pane, new Point(5, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(pane);

        // Act
        await surface.UpdateAsync(
            () =>
            {
                switch (transition)
                {
                    case "resizability":
                        pane.IsResizable = false;
                        break;
                    case "orientation":
                        pane.Orientation = Orientation.Vertical;
                        break;
                    case "disable":
                        pane.IsEnabled = false;
                        break;
                    case "hide":
                        pane.Visibility = Visibility.Hidden;
                        break;
                    case "collapse-owner":
                        pane.Visibility = Visibility.Collapsed;
                        break;
                    case "hide-pane":
                        pane.Children[0].Visibility = Visibility.Hidden;
                        break;
                    case "collapse-pane":
                        pane.Children[0].Visibility = Visibility.Collapsed;
                        break;
                    case "remove-pane":
                        pane.Children.RemoveAt(0);
                        break;
                    case "detach":
                        source.Children.Remove(pane).ShouldBeTrue();
                        break;
                    case "reparent":
                        source.Children.Remove(pane).ShouldBeTrue();
                        destination.Children.Add(pane);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown transition '{transition}'.");
                }
            },
            $"invalidate SplitPane divider through {transition}");

        // Assert
        surface.ShouldHaveCapture(null);
        pane.IsPressed.ShouldBeFalse();
        pane.FirstPaneLength.ShouldBe(Length.Cells(5));
    }

    /// <summary>Verifies divider-affecting property observers see capture and pressed state already
    /// cleared, so reentrant motion cannot consume a stale drag snapshot.</summary>
    [Theory]
    [InlineData("orientation")]
    [InlineData("resizability")]
    public async Task Pointer_WhenDividerPropertyPublishes_CancelsDragBeforeObserverAndReentrantMotionAsync(
        string property)
    {
        // Arrange
        var pane = new SplitPane
        {
            FirstPaneLength = Length.Cells(5),
            Children = { new ProbeControl(), new ProbeControl() }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(pane, new Point(5, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(pane);
        bool? observerHadCapture = null;
        bool? observerSawPressed = null;
        var expectedProperty = property == "orientation"
            ? nameof(SplitPane.Orientation)
            : nameof(SplitPane.IsResizable);
        pane.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName != expectedProperty)
            {
                return;
            }

            observerHadCapture = pane.HasPointerCapture;
            observerSawPressed = pane.IsPressed;
            _ = surface.Application.Capture.Dispatch(new Pointer(
                new Point(8, 0),
                pixels: null,
                Buttons.Primary,
                PointerAction.Move,
                wheelX: 0,
                wheelY: 0,
                Modifiers.None,
                isMotion: true,
                isCellPositionInferred: false));
        };

        // Act
        await surface.UpdateAsync(
            () =>
            {
                if (property == "orientation")
                {
                    pane.Orientation = Orientation.Vertical;
                }
                else
                {
                    pane.IsResizable = false;
                }
            },
            $"publish SplitPane {property} during captured drag");

        // Assert
        observerHadCapture.ShouldBe(false);
        observerSawPressed.ShouldBe(false);
        surface.ShouldHaveCapture(null);
        pane.IsPressed.ShouldBeFalse();
        pane.FirstPaneLength.ShouldBe(Length.Cells(5));
        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies an orientation observer cannot route a primary press through the previous
    /// arrangement's divider geometry before the new orientation receives layout.</summary>
    [Fact]
    public async Task Pointer_WhenOrientationPublishes_RejectsReentrantPressAtPreviousDividerAsync()
    {
        // Arrange
        var pane = new SplitPane
        {
            FirstPaneLength = Length.Cells(5),
            Children = { new ProbeControl(), new ProbeControl() }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 3),
            TestContext.Current.CancellationToken);
        var routedPresses = 0;
        pane.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName != nameof(SplitPane.Orientation))
            {
                return;
            }

            routedPresses++;
            _ = Route(pane, new PointerEventArgs(new Pointer(
                new Point(5, 1),
                pixels: null,
                Buttons.Primary,
                PointerAction.Press,
                wheelX: 0,
                wheelY: 0,
                Modifiers.None,
                isMotion: false,
                isCellPositionInferred: false)));
        };

        // Act
        await surface.UpdateAsync(
            () => pane.Orientation = Orientation.Vertical,
            "change SplitPane orientation with a reentrant old-divider press");

        // Assert
        routedPresses.ShouldBe(1);
        pane.Orientation.ShouldBe(Orientation.Vertical);
        surface.ShouldHaveCapture(null);
        pane.IsPressed.ShouldBeFalse();
        pane.FirstPaneLength.ShouldBe(Length.Cells(5));
    }

    /// <summary>Verifies owner unavailability clears focused divider state together with capture and
    /// pressed presentation in one mounted transition.</summary>
    [Fact]
    public async Task Pointer_WhenCapturedSplitPaneBecomesHidden_ClearsFocusCaptureAndPressedStateAsync()
    {
        // Arrange
        var pane = new SplitPane
        {
            FirstPaneLength = Length.Cells(5),
            Children = { new ProbeControl(), new ProbeControl() }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(pane, new Point(5, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveFocus(pane);
        surface.ShouldHaveCapture(pane);
        pane.IsPressed.ShouldBeTrue();

        // Act
        await surface.UpdateAsync(
            () => pane.Visibility = Visibility.Hidden,
            "hide the focused and captured SplitPane");

        // Assert
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveCapture(null);
        pane.IsPressed.ShouldBeFalse();
        pane.FirstPaneLength.ShouldBe(Length.Cells(5));
    }

    /// <summary>Verifies entering a sibling modal plane excludes SplitPane and cancels its active capture.</summary>
    [Fact]
    public async Task Pointer_WhenSiblingModalPlaneExcludesSplitPane_CancelsDragAsync()
    {
        // Arrange
        var pane = new SplitPane
        {
            Width = Length.Cells(11),
            Height = Length.Cells(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { new ProbeControl(), new ProbeControl() }
        };
        var modal = new Window
        {
            Visibility = Visibility.Collapsed,
            Width = Length.Cells(8),
            Height = Length.Cells(2),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var root = new Overlay { Children = { pane, modal } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(pane, new Point(5, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(pane);
        ModalScope? scope = null;

        // Act
        await surface.UpdateAsync(() => scope = modal.ShowModal(), "exclude SplitPane with sibling modal Window");

        // Assert
        surface.ShouldHaveCapture(null);
        pane.IsPressed.ShouldBeFalse();
        await surface.UpdateAsync(scope.ShouldNotBeNull().Dispose, "end sibling modal Window");
    }

    /// <summary>Verifies disposal releases capture and leaves no active divider gesture in the surviving surface.</summary>
    [Fact]
    public async Task Pointer_WhenCapturedSplitPaneIsDisposed_CleansUpGestureAsync()
    {
        // Arrange
        var pane = new SplitPane
        {
            Width = Length.Cells(11),
            Height = Length.Cells(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { new ProbeControl(), new ProbeControl() }
        };
        var root = new Overlay { Children = { pane } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(12, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(pane, new Point(5, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(pane);

        // Act
        await surface.UpdateAsync(pane.Dispose, "dispose captured SplitPane");

        // Assert
        surface.ShouldHaveCapture(null);
        pane.IsDisposed.ShouldBeTrue();
        pane.IsPressed.ShouldBeFalse();
    }

    /// <summary>Verifies focus callbacks can invalidate divider interaction before capture without
    /// leaving a pressed state or changing the split.</summary>
    [Fact]
    public async Task Pointer_WhenFocusObserverDisablesResizing_RevalidatesBeforeCaptureAsync()
    {
        // Arrange
        var pane = new SplitPane
        {
            FirstPaneLength = Length.Cells(5),
            Children = { new ProbeControl(), new ProbeControl() }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 2),
            TestContext.Current.CancellationToken);
        pane.GotFocus += (_, _) => pane.IsResizable = false;

        // Act
        await surface.Pointer.MoveToAsync(pane, new Point(5, 0));
        await surface.Pointer.PressAsync();

        // Assert
        surface.ShouldHaveFocus(pane);
        surface.ShouldHaveCapture(null);
        pane.IsPressed.ShouldBeFalse();
        pane.FirstPaneLength.ShouldBe(Length.Cells(5));
        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies a reentrant split observer that removes divider eligibility cancels
    /// capture before later held motion can reuse the old drag snapshot.</summary>
    [Fact]
    public async Task Pointer_WhenSplitObserverDisablesResizing_CancelsCapturedSnapshotAsync()
    {
        // Arrange
        var pane = new SplitPane
        {
            FirstPaneLength = Length.Cells(5),
            Children = { new ProbeControl(), new ProbeControl() }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 2),
            TestContext.Current.CancellationToken);
        pane.SplitChanged += (_, _) => pane.IsResizable = false;
        await surface.Pointer.MoveToAsync(pane, new Point(5, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(pane);

        // Act
        await surface.Pointer.MovePressedToAsync(pane, new Point(6, 0));

        // Assert
        pane.FirstPaneLength.ShouldBe(Length.Cells(6));
        pane.IsResizable.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        pane.IsPressed.ShouldBeFalse();
        await surface.Pointer.MovePressedToAsync(pane, new Point(8, 0));
        pane.FirstPaneLength.ShouldBe(Length.Cells(6));
        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies pane clicks keep their descendant route, while an empty non-focusable pane
    /// background may use the framework's nearest-focusable-ancestor fallback.</summary>
    [Fact]
    public async Task Pointer_WhenPaneContentOrEmptyBackgroundIsPressed_PreservesPaneRoutingAndFocusFallbackAsync()
    {
        // Arrange
        var button = new Button { Text = "First" };
        var empty = new ProbeControl();
        var pane = new SplitPane
        {
            FirstPaneLength = Length.Cells(5),
            Children = { button, empty }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 2),
            TestContext.Current.CancellationToken);
        var clicks = 0;
        button.Click += (_, _) => clicks++;

        // Act and assert descendant routing
        await surface.Pointer.ClickAsync(button);
        clicks.ShouldBe(1);
        surface.ShouldHaveFocus(button);
        surface.ShouldHaveCapture(null);

        // Act and assert shared focus fallback on non-focusable pane background
        await surface.Pointer.ClickAsync(empty);
        surface.ShouldHaveFocus(pane);
        surface.ShouldHaveCapture(null);
        pane.FirstPaneLength.ShouldBe(Length.Cells(5));
    }

    /// <summary>Verifies disabling divider resizing leaves pane input and pane focus behavior intact.</summary>
    [Fact]
    public async Task Pointer_WhenSplitPaneIsNotResizable_LeavesPaneInputAndFocusIntactAsync()
    {
        // Arrange
        var first = new Button { Text = "First" };
        var pane = new SplitPane
        {
            IsResizable = false,
            Children = { first, new Button { Text = "Second" } }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 2),
            TestContext.Current.CancellationToken);
        var clicks = 0;
        first.Click += (_, _) => clicks++;

        // Act
        await surface.Pointer.ClickAsync(first);

        // Assert
        clicks.ShouldBe(1);
        surface.ShouldHaveFocus(first);
        surface.ShouldHaveCapture(null);

        // Act and assert: a secondary divider press remains ordinary unhandled pointer input.
        await surface.Pointer.RightClickAsync(pane, new Point(5, 0));
        surface.ShouldHaveFocus(first);
        surface.ShouldHaveCapture(null);
        pane.FirstPaneLength.ShouldBe(Length.Percent(50));
    }

    /// <summary>Verifies one pane's full Visible-to-Hidden-to-Collapsed-to-Visible lifecycle keeps
    /// exact split geometry, final cells, and pointer targets synchronized after every frame.</summary>
    [Fact]
    public async Task Pointer_WhenPaneTransitionsThroughVisibleHiddenCollapsedVisible_CommitsExactGeometryCellsAndTargetsAsync()
    {
        // Arrange
        var firstClicks = 0;
        var secondClicks = 0;
        var first = new Button
        {
            Text = "AAAAAAAAAAA",
            Style = TestButtonStyles.FlatWithPadding(default),
            Padding = default,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var second = new Button
        {
            Text = "BBBBBBBBBBB",
            Style = TestButtonStyles.FlatWithPadding(default),
            Padding = default,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        first.Click += (_, _) => firstClicks++;
        second.Click += (_, _) => secondClicks++;
        var pane = new SplitPane
        {
            Height = Length.Cells(1),
            Children = { first, second }
        };
        await using var surface = await ComponentSurface.MountAsync(
            pane,
            new Size(11, 1),
            TestContext.Current.CancellationToken);
        var firstVisibleBounds = new Rect(0, 0, 5, 1);
        var secondSplitBounds = new Rect(6, 0, 5, 1);

        // Assert the initial visible frame.
        first.Bounds.ShouldBe(firstVisibleBounds);
        second.Bounds.ShouldBe(secondSplitBounds);
        pane.LogicalDividerBounds.ShouldBe(new Rect(5, 0, 1, 1));
        surface.ShouldRender("AAAAA│BBBBB");
        pane.HitTest(default).ShouldNotBeNull().Parent.ShouldBeSameAs(first);
        pane.HitTest(new Point(6, 0)).ShouldNotBeNull().Parent.ShouldBeSameAs(second);
        await surface.Pointer.ClickAsync(first);
        firstClicks.ShouldBe(1);

        // Act: Hidden retains the split track and divider but removes the pane from rendering and hit testing.
        await surface.UpdateAsync(() => first.Visibility = Visibility.Hidden, "hide the first SplitPane pane");

        // Assert the hidden frame.
        first.Bounds.ShouldBe(firstVisibleBounds);
        second.Bounds.ShouldBe(secondSplitBounds);
        pane.LogicalDividerBounds.ShouldBe(new Rect(5, 0, 1, 1));
        surface.ShouldRender("     │BBBBB");
        pane.HitTest(default).ShouldBeSameAs(pane);
        pane.HitTest(new Point(6, 0)).ShouldNotBeNull().Parent.ShouldBeSameAs(second);

        // Act: Collapsed removes the first track and divider so the second pane fills the owner.
        await surface.UpdateAsync(() => first.Visibility = Visibility.Collapsed, "collapse the first SplitPane pane");

        // Assert the collapsed frame.
        first.Bounds.ShouldBe(default);
        second.Bounds.ShouldBe(new Rect(0, 0, 11, 1));
        pane.LogicalDividerBounds.ShouldBe(default);
        surface.ShouldRender("BBBBBBBBBBB");
        pane.HitTest(default).ShouldNotBeNull().Parent.ShouldBeSameAs(second);
        await surface.Pointer.ClickAsync(second);
        secondClicks.ShouldBe(1);

        // Act: Visible restores the original split geometry, cells, and pointer target.
        await surface.UpdateAsync(() => first.Visibility = Visibility.Visible, "restore the first SplitPane pane");

        // Assert the restored frame.
        first.Bounds.ShouldBe(firstVisibleBounds);
        second.Bounds.ShouldBe(secondSplitBounds);
        pane.LogicalDividerBounds.ShouldBe(new Rect(5, 0, 1, 1));
        surface.ShouldRender("AAAAA│BBBBB");
        pane.HitTest(default).ShouldNotBeNull().Parent.ShouldBeSameAs(first);
        await surface.Pointer.ClickAsync(first);
        firstClicks.ShouldBe(2);
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
