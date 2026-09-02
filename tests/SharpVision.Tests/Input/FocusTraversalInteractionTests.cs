// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies keyboard focus traversal and focus-transaction guarantees through real mounted
/// controls: Tab order across nested containers, tab-stop exclusions, ineligible targets, cancellation,
/// single delivery of focus notifications, and recovery when the focused control loses eligibility.</summary>
public sealed class FocusTraversalInteractionTests
{
    /// <summary>Verifies the documented eligibility contract deliberately ignores arranged bounds: a
    /// fixed-width button that an overflowing horizontal Stack clamps to an empty slot still takes its
    /// Tab stop and still activates on Enter, because focus eligibility is attached + visible + enabled
    /// + CanFocus and never consults layout geometry.</summary>
    [Fact]
    public async Task Tab_WhenNextTabStopIsArrangedToEmptyBounds_StillReceivesFocusAndActivatesAsync()
    {
        // Arrange
        var first = new Button("First") { Width = Length.Cells(12) };
        var second = new Button("Second") { Width = Length.Cells(12) };
        var third = new Button("Third") { Width = Length.Cells(12) };
        var clicks = new List<string>();
        first.Click += (_, _) => clicks.Add("first");
        second.Click += (_, _) => clicks.Add("second");
        third.Click += (_, _) => clicks.Add("third");
        var stack = new Stack { Orientation = Orientation.Horizontal };
        stack.Children.Add(first);
        stack.Children.Add(second);
        stack.Children.Add(third);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(20, 3),
            TestContext.Current.CancellationToken);
        var clamped = new[] { first, second, third }.Single(button => button.Bounds.Width == 0);
        clamped.CanTabStop.ShouldBeTrue();

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(clamped);
        clamped.Bounds.Width.ShouldBe(0);
        await surface.Keyboard.PressAsync(Code.Enter);
        clicks.ShouldBe([clamped.Text.ToLowerInvariant()]);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
    }

    /// <summary>Verifies Tab and Shift+Tab walk nested containers in tree order, skip an
    /// IsTabStop=false control, and wrap at both ends.</summary>
    [Fact]
    public async Task Tab_WhenContainersAreNested_WalksTreeOrderSkippingNonTabStopsAndWrapsAsync()
    {
        // Arrange
        var first = new Button("A");
        var excluded = new Button("B") { IsTabStop = false };
        var inner = new Button("C");
        var last = new Button("D");
        var nested = new Stack();
        nested.Children.Add(excluded);
        nested.Children.Add(inner);
        var stack = new Stack();
        stack.Children.Add(first);
        stack.Children.Add(nested);
        stack.Children.Add(last);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 12),
            TestContext.Current.CancellationToken);

        // Act and assert forward
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(inner);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(last);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);

        // Act and assert reverse
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(last);
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(inner);
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(first);
        excluded.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies Shift+Tab from an unfocused surface enters at the last tab stop while Tab
    /// enters at the first.</summary>
    [Fact]
    public async Task Tab_WhenNothingIsFocused_EntersAtEitherEndByDirectionAsync()
    {
        // Arrange
        var first = new Button("A");
        var last = new Button("B");
        var stack = new Stack();
        stack.Children.Add(first);
        stack.Children.Add(last);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 8),
            TestContext.Current.CancellationToken);

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(last);
        await surface.UpdateAsync(() => last.IsEnabled = false, "drop focus by disabling the owner");
        surface.ShouldHaveFocus(null);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
    }

    /// <summary>Verifies a pointer-focused IsTabStop=false control still anchors traversal at its
    /// tree position: Tab continues to the following stop and Shift+Tab to the preceding one.</summary>
    [Fact]
    public async Task Tab_WhenAnchorIsAPointerFocusedNonTabStop_ContinuesFromItsTreePositionAsync()
    {
        // Arrange
        var before = new Button("A");
        var anchor = new Button("B") { IsTabStop = false };
        var after = new Button("C");
        var stack = new Stack();
        stack.Children.Add(before);
        stack.Children.Add(anchor);
        stack.Children.Add(after);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 10),
            TestContext.Current.CancellationToken);

        // Act and assert forward
        await surface.Pointer.ClickAsync(anchor);
        surface.ShouldHaveFocus(anchor);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(after);

        // Act and assert reverse
        await surface.Pointer.ClickAsync(anchor);
        surface.ShouldHaveFocus(anchor);
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(before);
    }

    /// <summary>Verifies TabIndex orders siblings ahead of insertion order and ties fall back to
    /// insertion order.</summary>
    [Fact]
    public async Task Tab_WhenTabIndexIsAssigned_OrdersByIndexThenInsertionAsync()
    {
        // Arrange
        var third = new Button("A") { TabIndex = 2 };
        var first = new Button("B") { TabIndex = 1 };
        var fourth = new Button("C") { TabIndex = 2 };
        var second = new Button("D") { TabIndex = 1 };
        var stack = new Stack();
        stack.Children.Add(third);
        stack.Children.Add(first);
        stack.Children.Add(fourth);
        stack.Children.Add(second);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 12),
            TestContext.Current.CancellationToken);

        // Act and assert
        var visited = new List<Button>();

        for (var index = 0; index < 4; index++)
        {
            await surface.Keyboard.PressAsync(Code.Tab);
            visited.Add((Button) surface.Application.Focus.Focused!);
        }

        visited.ShouldBe([first, second, third, fourth]);
    }

    /// <summary>Verifies Tab skips disabled, hidden, collapsed, and non-focusable siblings while
    /// still walking into an enabled nested container.</summary>
    [Fact]
    public async Task Tab_WhenSiblingsAreIneligible_SkipsEveryIneligibleKindAsync()
    {
        // Arrange
        var first = new Button("A");
        var disabled = new Button("B") { IsEnabled = false };
        var hidden = new Button("C") { Visibility = Visibility.Hidden };
        var collapsed = new Button("D") { Visibility = Visibility.Collapsed };
        var unfocusable = new Button("E") { IsFocusable = false };
        var insideDisabled = new Button("F");
        var disabledStack = new Stack { IsEnabled = false };
        disabledStack.Children.Add(insideDisabled);
        var last = new Button("G");
        var stack = new Stack();
        stack.Children.Add(first);
        stack.Children.Add(disabled);
        stack.Children.Add(hidden);
        stack.Children.Add(collapsed);
        stack.Children.Add(unfocusable);
        stack.Children.Add(disabledStack);
        stack.Children.Add(last);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 24),
            TestContext.Current.CancellationToken);

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(last);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
        insideDisabled.CanFocus.ShouldBeFalse();
        insideDisabled.EffectiveIsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies Focus() reports false and leaves focus untouched for hidden, collapsed,
    /// disabled, ancestor-disabled, non-focusable, and detached targets, and throws for a disposed one.</summary>
    [Fact]
    public async Task Focus_WhenTargetIsIneligible_ReturnsFalseWithoutMovingFocusAsync()
    {
        // Arrange
        var owner = new Button("Owner");
        var hidden = new Button("Hidden") { Visibility = Visibility.Hidden };
        var collapsed = new Button("Collapsed") { Visibility = Visibility.Collapsed };
        var disabled = new Button("Disabled") { IsEnabled = false };
        var unfocusable = new Button("Plain") { IsFocusable = false };
        var insideDisabled = new Button("Inside");
        var disabledStack = new Stack { IsEnabled = false };
        disabledStack.Children.Add(insideDisabled);
        var detached = new Button("Detached");
        var disposed = new Button("Disposed");
        var stack = new Stack();
        stack.Children.Add(owner);
        stack.Children.Add(hidden);
        stack.Children.Add(collapsed);
        stack.Children.Add(disabled);
        stack.Children.Add(unfocusable);
        stack.Children.Add(disabledStack);
        stack.Children.Add(disposed);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(12, 24),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => owner.Focus().ShouldBeTrue(), "focus the owner");

        // Act and assert
        await surface.UpdateAsync(
            () =>
            {
                hidden.Focus().ShouldBeFalse();
                collapsed.Focus().ShouldBeFalse();
                disabled.Focus().ShouldBeFalse();
                unfocusable.Focus().ShouldBeFalse();
                insideDisabled.Focus().ShouldBeFalse();
                detached.Focus().ShouldBeFalse();
                _ = Should.Throw<ArgumentException>(() => surface.Application.Focus.Focus(detached));
                disposed.Dispose();
                _ = Should.Throw<ObjectDisposedException>(() => disposed.Focus());
                // Disposal severs tree membership first, so the manager sees a foreign control.
                _ = Should.Throw<ArgumentException>(() => surface.Application.Focus.Focus(disposed));
            },
            "request focus for every ineligible target");
        surface.ShouldHaveFocus(owner);
        owner.IsFocused.ShouldBeTrue();
    }

    /// <summary>Verifies a Changing subscriber can veto a Tab transition: focus stays put and no
    /// Lost, Gained, LostFocus, or GotFocus notification fires for the vetoed proposal.</summary>
    [Fact]
    public async Task Tab_WhenChangingCancels_KeepsFocusAndSuppressesNotificationsAsync()
    {
        // Arrange
        var first = new Button("A");
        var second = new Button("B");
        var stack = new Stack();
        stack.Children.Add(first);
        stack.Children.Add(second);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 8),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
        var observations = new List<string>();
        var proposals = new List<(ControlBase? Previous, ControlBase? Next, FocusReason Reason)>();
        var focus = surface.Application.Focus;
        focus.Changing += (_, eventArgs) =>
        {
            proposals.Add((eventArgs.Previous, eventArgs.Next, eventArgs.Reason));
            eventArgs.Cancel = ReferenceEquals(eventArgs.Next, second);
        };
        focus.Lost += (_, _) => observations.Add("lost");
        focus.Gained += (_, _) => observations.Add("gained");
        first.LostFocus += (_, _) => observations.Add("first-lost");
        second.GotFocus += (_, _) => observations.Add("second-got");

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(first);
        proposals.ShouldBe([(first, second, FocusReason.Keyboard)]);
        observations.ShouldBeEmpty();
        first.IsFocused.ShouldBeTrue();
        second.IsFocused.ShouldBeFalse();
        surface.ShouldHaveState(first, VisualState.Focused);
    }

    /// <summary>Verifies one committed Tab delivers LostFocus, FocusLeft, Lost, FocusEntered, GotFocus,
    /// and Gained exactly once in the documented order, and that later invalidation or re-rendering
    /// never redelivers them.</summary>
    [Fact]
    public async Task Tab_WhenFocusCommits_DeliversEachNotificationOnceInDocumentedOrderAsync()
    {
        // Arrange
        var first = new Button("A");
        var second = new Button("B");
        var stack = new Stack();
        stack.Children.Add(first);
        stack.Children.Add(second);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 8),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
        var order = new List<string>();
        var focus = surface.Application.Focus;
        first.LostFocus += (_, _) => order.Add("first.LostFocus");
        first.FocusLeft += (_, _) => order.Add("first.FocusLeft");
        second.FocusEntered += (_, _) => order.Add("second.FocusEntered");
        second.GotFocus += (_, _) => order.Add("second.GotFocus");
        focus.Lost += (_, eventArgs) => order.Add(
            $"Lost({eventArgs.Previous?.GetType().Name}->{eventArgs.Current?.GetType().Name},{eventArgs.Reason})");
        focus.Gained += (_, eventArgs) => order.Add(
            $"Gained({eventArgs.Previous?.GetType().Name}->{eventArgs.Current?.GetType().Name},{eventArgs.Reason})");

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        var afterCommit = order.ToArray();
        await surface.UpdateAsync(
            () =>
            {
                second.Text = "B!";
                first.Text = "A!";
                stack.Invalidate(Invalidation.Measure);
            },
            "invalidate both controls after the commit");
        await surface.ResizeAsync(new Size(12, 9));

        // Assert
        afterCommit.ShouldBe(
        [
            "first.LostFocus",
            "first.FocusLeft",
            "Lost(Button->Button,Keyboard)",
            "second.FocusEntered",
            "second.GotFocus",
            "Gained(Button->Button,Keyboard)"
        ]);
        order.ShouldBe(afterCommit);
        surface.ShouldHaveFocus(second);
        surface.ShouldHaveState(second, VisualState.Focused);
        surface.ShouldHaveState(first, VisualState.Normal);
    }

    /// <summary>Verifies that disabling, hiding, collapsing, or making the focused control non-focusable
    /// clears focus synchronously, delivers Lost with the Unavailable reason, and lets the next Tab
    /// start again from the first eligible stop.</summary>
    [Theory]
    [InlineData("disable")]
    [InlineData("hide")]
    [InlineData("collapse")]
    [InlineData("unfocusable")]
    [InlineData("disable-ancestor")]
    [InlineData("hide-ancestor")]
    public async Task Tab_WhenFocusedControlLosesEligibility_ClearsFocusThenRestartsTraversalAsync(string transition)
    {
        // Arrange
        var first = new Button("A");
        var second = new Button("B");
        var third = new Button("C");
        var nested = new Stack();
        nested.Children.Add(second);
        var stack = new Stack();
        stack.Children.Add(first);
        stack.Children.Add(nested);
        stack.Children.Add(third);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 12),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(second);
        var lost = new List<(ControlBase? Previous, ControlBase? Current, FocusReason Reason)>();
        surface.Application.Focus.Lost += (_, eventArgs) => lost.Add((eventArgs.Previous, eventArgs.Current, eventArgs.Reason));
        var secondLost = 0;
        second.LostFocus += (_, _) => secondLost++;

        // Act
        await surface.UpdateAsync(
            () =>
            {
                switch (transition)
                {
                    case "disable":
                        second.IsEnabled = false;
                        break;
                    case "hide":
                        second.Visibility = Visibility.Hidden;
                        break;
                    case "collapse":
                        second.Visibility = Visibility.Collapsed;
                        break;
                    case "unfocusable":
                        second.IsFocusable = false;
                        break;
                    case "disable-ancestor":
                        nested.IsEnabled = false;
                        break;
                    default:
                        nested.Visibility = Visibility.Hidden;
                        break;
                }
            },
            $"make the focused control ineligible via {transition}");

        // Assert
        surface.ShouldHaveFocus(null);
        second.IsFocused.ShouldBeFalse();
        lost.ShouldBe([(second, null, FocusReason.Unavailable)]);
        secondLost.ShouldBe(1);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(third);
    }

    /// <summary>Verifies disposing the focused control clears focus without throwing and the
    /// remaining stops keep a coherent Tab cycle.</summary>
    [Fact]
    public async Task Tab_WhenFocusedControlIsDisposed_ClearsFocusAndKeepsRemainingCycleAsync()
    {
        // Arrange
        var first = new Button("A");
        var second = new Button("B");
        var third = new Button("C");
        var stack = new Stack();
        stack.Children.Add(first);
        stack.Children.Add(second);
        stack.Children.Add(third);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 12),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(second);

        // Act
        await surface.UpdateAsync(second.Dispose, "dispose the focused button");

        // Assert
        surface.ShouldHaveFocus(null);
        stack.Children.Count.ShouldBe(2);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(third);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
    }

    /// <summary>Verifies a control added after mount joins traversal at its tree position and a
    /// removed (not disposed) control leaves it, without disturbing the current focus owner.</summary>
    [Fact]
    public async Task Tab_WhenChildrenChangeAfterMount_ReflectsTheLiveTreeAsync()
    {
        // Arrange
        var first = new Button("A");
        var last = new Button("C");
        var stack = new Stack();
        stack.Children.Add(first);
        stack.Children.Add(last);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 12),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
        var inserted = new Button("B");

        // Act insert
        await surface.UpdateAsync(() => stack.Children.Insert(1, inserted), "insert a button between the stops");

        // Assert insert
        surface.ShouldHaveFocus(first);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(inserted);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(last);

        // Act remove the focused control's predecessor
        await surface.UpdateAsync(() => stack.Children.Remove(inserted).ShouldBeTrue(), "remove the middle button");

        // Assert remove
        surface.ShouldHaveFocus(last);
        inserted.IsFocused.ShouldBeFalse();
        inserted.Parent.ShouldBeNull();
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
    }

    /// <summary>Verifies removing the focused control from its parent (without disposing it)
    /// clears focus and clears the control's own IsFocused flag.</summary>
    [Fact]
    public async Task Focus_WhenFocusedControlIsDetached_ClearsFocusFlagsAsync()
    {
        // Arrange
        var first = new Button("A");
        var second = new Button("B");
        var stack = new Stack();
        stack.Children.Add(first);
        stack.Children.Add(second);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 8),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(second);

        // Act
        await surface.UpdateAsync(() => stack.Children.Remove(second).ShouldBeTrue(), "detach the focused button");

        // Assert
        surface.ShouldHaveFocus(null);
        second.IsFocused.ShouldBeFalse();
        second.Focus().ShouldBeFalse();
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
    }

    /// <summary>Verifies Tab and Shift+Tab traverse a Cycle scope's own stops only, while a stop
    /// outside the scope is reachable by pointer and then re-enters the scope on Tab.</summary>
    [Fact]
    public async Task Tab_WhenScopeIsCycle_WrapsInsideTheScopeUntilLeftByPointerAsync()
    {
        // Arrange
        var outside = new Button("Out");
        var one = new Button("One");
        var two = new Button("Two");
        var scope = new Stack { TabNavigation = TabNavigation.Cycle };
        scope.Children.Add(one);
        scope.Children.Add(two);
        var stack = new Stack();
        stack.Children.Add(outside);
        stack.Children.Add(scope);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 12),
            TestContext.Current.CancellationToken);

        // Act and assert
        await surface.Pointer.ClickAsync(one);
        surface.ShouldHaveFocus(one);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(two);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(one);
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
        surface.ShouldHaveFocus(two);
        await surface.Pointer.ClickAsync(outside);
        surface.ShouldHaveFocus(outside);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(one);
    }

    /// <summary>Verifies a TabNavigation.None container contributes only itself and Tab never enters
    /// its focusable descendants.</summary>
    [Fact]
    public async Task Tab_WhenContainerIsTabNavigationNone_NeverEntersDescendantsAsync()
    {
        // Arrange
        var before = new Button("A");
        var inside = new Button("B");
        var owner = new Stack { TabNavigation = TabNavigation.None, IsFocusable = true };
        owner.Children.Add(inside);
        var after = new Button("C");
        var stack = new Stack();
        stack.Children.Add(before);
        stack.Children.Add(owner);
        stack.Children.Add(after);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 12),
            TestContext.Current.CancellationToken);

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(before);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(owner);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(after);
        inside.IsFocused.ShouldBeFalse();
        await surface.Pointer.ClickAsync(inside);
        surface.ShouldHaveFocus(inside);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(after);
    }

    /// <summary>Verifies a Gained handler that re-points focus makes the keyboard request land on
    /// the redirected target and the original target never renders as focused.</summary>
    [Fact]
    public async Task Tab_WhenGainedRedirectsFocus_LandsOnRedirectedTargetAsync()
    {
        // Arrange
        var first = new Button("A");
        var second = new Button("B");
        var third = new Button("C");
        var stack = new Stack();
        stack.Children.Add(first);
        stack.Children.Add(second);
        stack.Children.Add(third);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 12),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);
        second.GotFocus += (_, _) => _ = third.Focus();

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(third);
        second.IsFocused.ShouldBeFalse();
        surface.ShouldHaveState(second, VisualState.Normal);
        surface.ShouldHaveState(third, VisualState.Focused);
    }

    /// <summary>Verifies a control that is hidden while it is the traversal anchor is left out and
    /// the following Tab moves to the next stop after its tree position rather than restarting.</summary>
    [Fact]
    public async Task Tab_WhenNonFocusedStopHidesAndShows_TraversalTracksVisibilityAsync()
    {
        // Arrange
        var first = new Button("A");
        var second = new Button("B");
        var third = new Button("C");
        var stack = new Stack();
        stack.Children.Add(first);
        stack.Children.Add(second);
        stack.Children.Add(third);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 12),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(first);

        // Act hide then Tab
        await surface.UpdateAsync(() => second.Visibility = Visibility.Collapsed, "collapse the second stop");
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(third);

        // Act show then Shift+Tab
        await surface.UpdateAsync(() => second.Visibility = Visibility.Visible, "restore the second stop");
        await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);

        // Assert
        surface.ShouldHaveFocus(second);
    }
}
