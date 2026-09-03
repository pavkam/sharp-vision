// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies pointer routing through real mounted controls: hover reconciliation across
/// disabled and hidden subtrees, capture lifetime under disposal and focus loss, focus resolution for
/// presses on non-focusable targets, release-outside cancellation, and wheel delivery to the nearest
/// scrollable ancestor.</summary>
public sealed class PointerRoutingInteractionTests
{
    /// <summary>Verifies disabling the hovered button's ancestor clears the button's hover state and
    /// visual immediately, and that re-enabling does not resurrect hover until the pointer moves.</summary>
    [Theory]
    [InlineData("disable")]
    [InlineData("hide")]
    public async Task Hover_WhenAncestorBecomesUnavailable_ClearsHoverUntilPointerMovesAsync(string transition)
    {
        // Arrange
        var button = new Button("Hover");
        var stack = new Stack();
        stack.Children.Add(button);
        var outer = new Stack();
        outer.Children.Add(stack);
        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(12, 4),
            TestContext.Current.CancellationToken);
        var exits = 0;
        button.PointerExited += (_, _) => exits++;
        await surface.Pointer.MoveToAsync(button);
        surface.ShouldHaveState(button, VisualState.IsPointerOver);
        button.IsPointerOver.ShouldBeTrue();
        stack.IsPointerOver.ShouldBeTrue();

        // Act
        await surface.UpdateAsync(
            () =>
            {
                if (transition == "disable")
                {
                    stack.IsEnabled = false;
                }
                else
                {
                    stack.Visibility = Visibility.Hidden;
                }
            },
            $"make the hovered subtree unavailable via {transition}");

        // Assert
        button.IsPointerOver.ShouldBeFalse();
        button.IsPointerDirectlyOver.ShouldBeFalse();
        stack.IsPointerOver.ShouldBeFalse();
        exits.ShouldBe(1);
        surface.Application.Capture.Hovered.ShouldNotBeSameAs(button);
        surface.Application.Capture.Hovered.ShouldNotBeSameAs(stack);

        // Act restore without pointer motion
        await surface.UpdateAsync(
            () =>
            {
                stack.IsEnabled = true;
                stack.Visibility = Visibility.Visible;
            },
            "restore the subtree");

        // Assert hover does not come back on its own, but does on the next motion
        button.IsPointerOver.ShouldBeFalse();
        surface.ShouldHaveState(button, VisualState.Normal);
        await surface.Pointer.MoveToAsync(button, new Point(1, 1));
        button.IsPointerOver.ShouldBeTrue();
        surface.ShouldHaveState(button, VisualState.IsPointerOver);
        exits.ShouldBe(1);
    }

    /// <summary>Verifies the hover chain stays coherent when the pointer moves from one button to a
    /// sibling: the shared ancestor never loses IsPointerOver while each leaf flips exactly once.</summary>
    [Fact]
    public async Task Hover_WhenPointerMovesBetweenSiblings_KeepsSharedAncestorHoveredAsync()
    {
        // Arrange
        var first = new Button("A");
        var second = new Button("B");
        var stack = new Stack();
        stack.Children.Add(first);
        stack.Children.Add(second);
        // Pixel coordinates are the only profile under which the terminal pointer-leave report is
        // decoded as a leave rather than as an ordinary move to the top-left cell.
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 8),
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel },
            TestContext.Current.CancellationToken);
        var log = new List<string>();
        first.PointerEntered += (_, _) => log.Add("A+");
        first.PointerExited += (_, _) => log.Add("A-");
        second.PointerEntered += (_, _) => log.Add("B+");
        second.PointerExited += (_, _) => log.Add("B-");
        stack.PointerExited += (_, _) => log.Add("stack-");

        // Act
        await surface.Pointer.MoveToAsync(first);
        await surface.Pointer.MoveToAsync(second);
        await surface.Pointer.LeaveAsync();

        // Assert
        log.ShouldBe(["A+", "A-", "B+", "B-", "stack-"]);
        first.IsPointerOver.ShouldBeFalse();
        second.IsPointerOver.ShouldBeFalse();
        stack.IsPointerOver.ShouldBeFalse();
        surface.ShouldHaveState(first, VisualState.Normal);
        surface.ShouldHaveState(second, VisualState.Normal);
    }

    /// <summary>Verifies disposing a button while its primary press holds capture releases capture,
    /// clears the pressed state, and lets the trailing release route harmlessly. A focusable button
    /// is focused by its own press, and unavailability clears focus before capture, so its press
    /// behavior releases capture explicitly on focus loss; a non-focusable button reaches the
    /// manager's own unavailability path and reports the coarser Unavailable reason.</summary>
    [Theory]
    [InlineData(true, PointerCaptureLossReason.Explicit)]
    [InlineData(false, PointerCaptureLossReason.Unavailable)]
    public async Task Capture_WhenPressedButtonIsDisposed_ReleasesCaptureWithoutClickAsync(
        bool focusable,
        PointerCaptureLossReason expectedReason)
    {
        // Arrange
        var button = new Button("Press") { IsFocusable = focusable };
        var sibling = new Button("Other");
        var stack = new Stack();
        stack.Children.Add(button);
        stack.Children.Add(sibling);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 8),
            TestContext.Current.CancellationToken);
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        var lossReasons = new List<PointerCaptureLossReason>();
        button.LostPointerCapture += (_, eventArgs) => lossReasons.Add(eventArgs.Reason);
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(button);
        button.IsPressed.ShouldBeTrue();

        // Act
        await surface.UpdateAsync(button.Dispose, "dispose the pressed button");
        await surface.Pointer.ReleaseAsync();

        // Assert
        surface.ShouldHaveCapture(null);
        lossReasons.ShouldBe([expectedReason]);
        clicks.ShouldBe(0);
        stack.Children.Count.ShouldBe(1);
        await surface.Pointer.ClickAsync(sibling);
        surface.ShouldHaveFocus(sibling);
    }

    /// <summary>Verifies programmatic focus loss while a button is pressed cancels the press and
    /// releases its capture, so the later release never clicks.</summary>
    [Fact]
    public async Task Capture_WhenPressedButtonLosesFocus_CancelsPressAndReleasesCaptureAsync()
    {
        // Arrange
        var button = new Button("Press");
        var other = new Button("Other");
        var stack = new Stack();
        stack.Children.Add(button);
        stack.Children.Add(other);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 8),
            TestContext.Current.CancellationToken);
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        var lossReasons = new List<PointerCaptureLossReason>();
        button.LostPointerCapture += (_, eventArgs) => lossReasons.Add(eventArgs.Reason);
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(button);
        surface.ShouldHaveFocus(button);
        surface.ShouldHaveState(button, VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);

        // Act
        await surface.UpdateAsync(() => other.Focus().ShouldBeTrue(), "move focus away while pressed");

        // Assert
        surface.ShouldHaveCapture(null);
        button.IsPressed.ShouldBeFalse();
        lossReasons.ShouldBe([PointerCaptureLossReason.Explicit]);
        surface.ShouldHaveState(button, VisualState.IsPointerOver);
        await surface.Pointer.ReleaseAsync();
        clicks.ShouldBe(0);
        surface.ShouldHaveFocus(other);
    }

    /// <summary>Verifies disabling a pressed button cancels its press and releases capture. The
    /// focused (focusable) button loses focus before capture cleanup, so its press behavior releases
    /// capture explicitly; a non-focusable button reports the manager's coarser Unavailable reason.</summary>
    [Theory]
    [InlineData(true, PointerCaptureLossReason.Explicit)]
    [InlineData(false, PointerCaptureLossReason.Unavailable)]
    public async Task Capture_WhenPressedButtonIsDisabled_ReleasesCaptureAndCancelsPressAsync(
        bool focusable,
        PointerCaptureLossReason expectedReason)
    {
        // Arrange
        var button = new Button("Press") { IsFocusable = focusable };
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 3),
            TestContext.Current.CancellationToken);
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        var lossReasons = new List<PointerCaptureLossReason>();
        button.LostPointerCapture += (_, eventArgs) => lossReasons.Add(eventArgs.Reason);
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(button);

        // Act
        await surface.UpdateAsync(() => button.IsEnabled = false, "disable the pressed button");
        await surface.Pointer.ReleaseAsync();

        // Assert
        surface.ShouldHaveCapture(null);
        button.IsPressed.ShouldBeFalse();
        lossReasons.ShouldBe([expectedReason]);
        clicks.ShouldBe(0);
        surface.ShouldHaveState(button, VisualState.Disabled);
        button.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies pressing a button and releasing outside its bounds cancels the click while
    /// capture still delivered the motion and release to the pressed owner.</summary>
    [Fact]
    public async Task Press_WhenReleasedOutsideTheButton_DoesNotClickAndReleasesCaptureAsync()
    {
        // Arrange
        var button = new Button("Press");
        var text = new ControlText("Elsewhere");
        var stack = new Stack();
        stack.Children.Add(button);
        stack.Children.Add(text);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 6),
            TestContext.Current.CancellationToken);
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        var releasedSenders = new List<ControlBase?>();
        button.PointerReleased += (sender, _) => releasedSenders.Add(sender as ControlBase);
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(button);

        // Act
        await surface.Pointer.MovePressedToAsync(text, new Point(0, 0));
        surface.ShouldHaveCapture(button);
        button.IsPointerOver.ShouldBeFalse();
        await surface.Pointer.ReleaseAsync();

        // Assert
        clicks.ShouldBe(0);
        surface.ShouldHaveCapture(null);
        button.IsPressed.ShouldBeFalse();
        releasedSenders.ShouldBe([button]);
        surface.ShouldHaveState(button, VisualState.Focused);
    }

    /// <summary>Verifies a primary press on a non-focusable control moves focus to the nearest
    /// focusable ancestor (here the mounted host) and away from the previously focused button.</summary>
    [Fact]
    public async Task Press_WhenTargetIsNotFocusable_FocusesNearestFocusableAncestorAsync()
    {
        // Arrange
        var button = new Button("Focus me");
        var text = new ControlText("Static");
        var stack = new Stack();
        stack.Children.Add(button);
        stack.Children.Add(text);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 6),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(button);
        surface.ShouldHaveFocus(button);
        var pressed = 0;
        text.PointerPressed += (_, _) => pressed++;

        // Act
        await surface.Pointer.ClickAsync(text);

        // Assert
        pressed.ShouldBe(1);
        button.IsFocused.ShouldBeFalse();
        surface.Application.Focus.Focused.ShouldBeSameAs(stack.Parent);
        surface.ShouldHaveState(button, VisualState.Normal);
        text.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies a press on a non-focusable control nested inside a focusable container
    /// focuses that container rather than a farther ancestor.</summary>
    [Fact]
    public async Task Press_WhenTargetIsInsideFocusableContainer_FocusesTheContainerAsync()
    {
        // Arrange
        var text = new ControlText("Inner");
        var container = new Stack { IsFocusable = true };
        container.Children.Add(text);
        var button = new Button("Other");
        var stack = new Stack();
        stack.Children.Add(button);
        stack.Children.Add(container);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 6),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(button);
        surface.ShouldHaveFocus(button);

        // Act
        await surface.Pointer.ClickAsync(text);

        // Assert
        surface.ShouldHaveFocus(container);
        container.IsFocused.ShouldBeTrue();
        text.IsPointerDirectlyOver.ShouldBeTrue();
        container.IsPointerOver.ShouldBeTrue();
    }

    /// <summary>Verifies a press on a disabled button neither focuses nor clicks it, and neither
    /// takes capture.</summary>
    [Fact]
    public async Task Press_WhenButtonIsDisabled_DoesNotFocusClickOrCaptureAsync()
    {
        // Arrange
        var disabled = new Button("Off") { IsEnabled = false };
        var other = new Button("On");
        var stack = new Stack();
        stack.Children.Add(other);
        stack.Children.Add(disabled);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 8),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(other);
        surface.ShouldHaveFocus(other);
        var clicks = 0;
        disabled.Click += (_, _) => clicks++;

        // Act
        await surface.Pointer.MoveToAsync(disabled);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(null);
        disabled.IsPressed.ShouldBeFalse();
        await surface.Pointer.ReleaseAsync();

        // Assert
        clicks.ShouldBe(0);
        disabled.IsFocused.ShouldBeFalse();
        surface.ShouldHaveState(disabled, VisualState.Disabled);
        disabled.IsPointerOver.ShouldBeFalse();
    }

    /// <summary>Verifies the terminal pointer-leave report is an explicit cancellation for a
    /// capture-backed press: capture is released, hover clears, and the later release never clicks.</summary>
    [Fact]
    public async Task Leave_WhenPointerLeavesWhilePressed_CancelsCaptureBackedPressAsync()
    {
        // Arrange
        var button = new Button("Press");
        await using var surface = await ComponentSurface.MountAsync(
            button,
            new Size(12, 3),
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel },
            TestContext.Current.CancellationToken);
        var clicks = 0;
        button.Click += (_, _) => clicks++;
        await surface.Pointer.MoveToAsync(button);
        await surface.Pointer.PressAsync();
        surface.ShouldHaveCapture(button);

        // Act
        await surface.Pointer.LeaveAsync();

        // Assert
        surface.ShouldHaveCapture(null);
        button.IsPressed.ShouldBeFalse();
        button.IsPointerOver.ShouldBeFalse();
        surface.Application.Capture.PressOrigin.ShouldBeNull();

        // The harness forgets its held button on leave, so the trailing physical release is sent raw.
        var point = await surface.ResolvePointAsync(button);
        await surface.Pointer.MoveToAsync(button);
        byte[] release = [0x1b, .. Encoding.ASCII.GetBytes(FormattableString.Invariant($"[<0;{point.X + 1};{point.Y + 1}m"))];
        await surface.SendAsync(release, "release the primary button after the leave report");
        clicks.ShouldBe(0);
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveState(button, VisualState.IsPointerOver | VisualState.Focused);
    }

    /// <summary>Verifies wheel input over a non-scrollable leaf scrolls the nearest scrollable
    /// ancestor, and a nested scrollable container consumes wheel records until its own endpoint
    /// before the outer container scrolls.</summary>
    [Fact]
    public async Task Wheel_WhenOverNestedScrollables_ScrollsInnermostThenBubblesAtEndpointAsync()
    {
        // Arrange
        var innerTexts = Enumerable.Range(0, 5).Select(index => new ControlText($"in{index}")).ToArray();
        var inner = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            Height = Length.Cells(2)
        };

        foreach (var text in innerTexts)
        {
            inner.Children.Add(text);
        }

        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never
        };
        outer.Children.Add(inner);

        for (var index = 0; index < 6; index++)
        {
            outer.Children.Add(new ControlText($"out{index}"));
        }

        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(8, 4),
            TestContext.Current.CancellationToken);
        var innerChanges = 0;
        var outerChanges = 0;
        inner.ScrollChanged += (_, _) => innerChanges++;
        outer.ScrollChanged += (_, _) => outerChanges++;
        surface.ShouldRender("""
                             in0
                             in1
                             out0
                             out1
                             """);

        // Act scroll the inner container to its endpoint
        await surface.Pointer.WheelAsync(innerTexts[0], default, wheelY: -1);
        inner.VerticalOffset.ShouldBe(1);
        outer.VerticalOffset.ShouldBe(0);
        await surface.Pointer.WheelAsync(innerTexts[1], default, wheelY: -1);
        await surface.Pointer.WheelAsync(innerTexts[2], default, wheelY: -1);
        inner.VerticalOffset.ShouldBe(3);
        outer.VerticalOffset.ShouldBe(0);
        surface.ShouldRender("""
                             in3
                             in4
                             out0
                             out1
                             """);

        // Act one more wheel at the inner endpoint bubbles to the outer container
        await surface.Pointer.WheelAsync(innerTexts[3], default, wheelY: -1);

        // Assert
        inner.VerticalOffset.ShouldBe(3);
        outer.VerticalOffset.ShouldBe(1);
        innerChanges.ShouldBe(3);
        outerChanges.ShouldBe(1);
        surface.ShouldRender("""
                             in4
                             out0
                             out1
                             out2
                             """);

        // Act a reverse wheel over an outer leaf scrolls the outer container back
        await surface.Pointer.WheelAsync(outer, new Point(0, 3), wheelY: 1);
        outer.VerticalOffset.ShouldBe(0);
        inner.VerticalOffset.ShouldBe(3);
    }

    /// <summary>Verifies a wheel record over a leaf whose ancestors cannot scroll is left unhandled
    /// without moving anything.</summary>
    [Fact]
    public async Task Wheel_WhenNoAncestorScrolls_LeavesOffsetsUntouchedAsync()
    {
        // Arrange
        var text = new ControlText("Static");
        var stack = new Stack();
        stack.Children.Add(text);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        var changes = 0;
        stack.ScrollChanged += (_, _) => changes++;

        // Act
        await surface.Pointer.WheelAsync(text, default, wheelY: -1);
        await surface.Pointer.WheelAsync(text, default, wheelY: 1);

        // Assert
        stack.VerticalOffset.ShouldBe(0);
        changes.ShouldBe(0);
        surface.ShouldRender("""
                             Static

                             """);
    }

    /// <summary>Verifies pointer motion over a focusable but non-interactive leaf reports hover on
    /// the leaf and every ancestor, then the terminal leave report clears the whole chain.</summary>
    [Fact]
    public async Task Hover_WhenPointerLeavesTerminal_ClearsEveryAncestorAsync()
    {
        // Arrange
        var button = new Button("Deep");
        var inner = new Stack();
        inner.Children.Add(button);
        var outer = new Stack();
        outer.Children.Add(inner);
        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(12, 4),
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel },
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(button);
        button.IsPointerOver.ShouldBeTrue();
        inner.IsPointerOver.ShouldBeTrue();
        outer.IsPointerOver.ShouldBeTrue();
        inner.IsPointerDirectlyOver.ShouldBeFalse();
        outer.IsPointerDirectlyOver.ShouldBeFalse();

        // Act
        await surface.Pointer.LeaveAsync();

        // Assert
        button.IsPointerOver.ShouldBeFalse();
        inner.IsPointerOver.ShouldBeFalse();
        outer.IsPointerOver.ShouldBeFalse();
        surface.Application.Capture.Hovered.ShouldBeNull();
        surface.ShouldHaveState(button, VisualState.Normal);
    }
}
