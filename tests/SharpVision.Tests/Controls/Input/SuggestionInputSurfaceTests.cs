// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves suggestion editing, popup navigation, acceptance, and cleanup through mounted terminal input.</summary>
public sealed class SuggestionInputSurfaceTests
{
    /// <summary>Verifies Unicode terminal text edits the retained editor, resolves the complete
    /// grapheme text, opens non-empty results, and paints wide suggestion cells correctly.</summary>
    [Fact]
    public async Task Input_WhenUnicodeTextTyped_OpensCurrentNonemptySuggestionsAsync()
    {
        // Arrange
        var queries = new List<string>();
        var input = new SuggestionInput
        {
            Width = Length.Cells(16),
            DropDownHeight = Length.Cells(2),
            Resolver = (searchTerms, _) =>
            {
                queries.Add(searchTerms);
                return ValueTask.FromResult<IReadOnlyList<object?>>(["界e\u0301"]);
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.TypeAsync("e\u0301👩‍💻");

        // Assert
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();
        input.Text.ShouldBe("e\u0301👩‍💻");
        queries.ShouldNotBeEmpty();
        queries[^1].ShouldBe("e\u0301👩‍💻");
        input.Suggestions.ShouldBe(["界e\u0301"]);
        input.IsOpen.ShouldBeTrue();
        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(0);
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y)).Text.ShouldBe("界");
        surface.Cell(new Point(list.Bounds.X + 1, list.Bounds.Y)).Continuation.ShouldBeTrue();
        surface.Cell(new Point(list.Bounds.X + 2, list.Bounds.Y)).Text.ShouldBe("e\u0301");
    }

    /// <summary>Verifies pasted graphemes use the same edit-and-resolve route as typed text.</summary>
    [Fact]
    public async Task Input_WhenUnicodeTextIsPasted_ResolvesAtomicEditorCommitAsync()
    {
        // Arrange
        var queries = new List<string>();
        var input = new SuggestionInput
        {
            Width = Length.Cells(16),
            Resolver = (searchTerms, _) =>
            {
                queries.Add(searchTerms);
                return ValueTask.FromResult<IReadOnlyList<object?>>([searchTerms]);
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PasteAsync("A\u0301界");

        // Assert
        input.Text.ShouldBe("A\u0301界");
        queries.ShouldBe(["A\u0301界"]);
        input.Suggestions.ShouldBe(["A\u0301界"]);
        input.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies each initial and repeated directional key reaches ListView's canonical
    /// current-row navigation exactly once while editor focus remains stable.</summary>
    [Theory]
    [InlineData(Code.Up, KeyAction.Press, 2, 1)]
    [InlineData(Code.Up, KeyAction.Repeat, 2, 1)]
    [InlineData(Code.Down, KeyAction.Press, 2, 3)]
    [InlineData(Code.Down, KeyAction.Repeat, 2, 3)]
    [InlineData(Code.Left, KeyAction.Press, 2, 1)]
    [InlineData(Code.Left, KeyAction.Repeat, 2, 1)]
    [InlineData(Code.Right, KeyAction.Press, 2, 3)]
    [InlineData(Code.Right, KeyAction.Repeat, 2, 3)]
    [InlineData(Code.Home, KeyAction.Press, 5, 0)]
    [InlineData(Code.Home, KeyAction.Repeat, 5, 0)]
    [InlineData(Code.End, KeyAction.Press, 5, 9)]
    [InlineData(Code.End, KeyAction.Repeat, 5, 9)]
    [InlineData(Code.PageUp, KeyAction.Press, 5, 2)]
    [InlineData(Code.PageUp, KeyAction.Repeat, 5, 2)]
    [InlineData(Code.PageDown, KeyAction.Press, 5, 8)]
    [InlineData(Code.PageDown, KeyAction.Repeat, 5, 8)]
    public async Task Navigation_WhenEditorHasFocus_MovesProvisionalRowExactlyOnceAsync(
        Code code,
        KeyAction action,
        int startingIndex,
        int expectedIndex)
    {
        // Arrange
        var input = new SuggestionInput
        {
            Width = Length.Cells(18),
            DropDownHeight = Length.Cells(3),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(
                Enumerable.Range(0, 10).Select(index => (object?) $"Item {index}").ToArray())
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(22, 8),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("q");
        var editor = OwnedTree.Find<TextInput>(input).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();

        for (var press = 0; press < startingIndex; press++)
        {
            await surface.Keyboard.PressAsync(Code.Down);
        }

        // Act
        if (action == KeyAction.Repeat)
        {
            await surface.Keyboard.RepeatAsync(code);
        }
        else
        {
            await surface.Keyboard.PressAsync(code);
        }

        // Assert
        surface.ShouldHaveFocus(editor);
        input.Text.ShouldBe("q");
        list.SelectedIndex.ShouldBe(expectedIndex);
        list.ActiveIndex.ShouldBe(expectedIndex);
    }

    /// <summary>Verifies Enter accepts the first provisional row without moving focus into the list.</summary>
    [Fact]
    public async Task Input_WhenEnterAcceptsCurrentSuggestion_CommitsAndClosesAsync()
    {
        // Arrange
        ItemInvokedEventArgs? accepted = null;
        var input = new SuggestionInput
        {
            Width = Length.Cells(16),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["Alpha", "Beta"])
        };
        input.SuggestionAccepted += (_, eventArgs) => accepted = eventArgs;
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("a");
        var editor = OwnedTree.Find<TextInput>(input).ShouldNotBeNull();

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        input.Text.ShouldBe("Alpha");
        input.IsOpen.ShouldBeFalse();
        accepted.ShouldNotBeNull().Cause.ShouldBe(ActivationCause.Keyboard);
        surface.ShouldHaveFocus(editor);
    }

    /// <summary>Verifies a primary pointer release accepts the exact row whose activation identity
    /// belongs to the live popup session.</summary>
    [Fact]
    public async Task Input_WhenSuggestionRowIsReleased_CommitsExactPointerItemAsync()
    {
        // Arrange
        ItemInvokedEventArgs? accepted = null;
        var input = new SuggestionInput
        {
            Width = Length.Cells(16),
            DropDownHeight = Length.Cells(3),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["Alpha", "Beta", "Gamma"])
        };
        input.SuggestionAccepted += (_, eventArgs) => accepted = eventArgs;
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(18, 8),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("a");
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();

        // Act
        await surface.Pointer.ClickAsync(list, new Point(1, 1));

        // Assert
        input.Text.ShouldBe("Beta");
        input.IsOpen.ShouldBeFalse();
        accepted.ShouldNotBeNull().Index.ShouldBe(1);
        accepted.Cause.ShouldBe(ActivationCause.Pointer);
    }

    /// <summary>Verifies Escape consumes the open session, restores the opening provisional state,
    /// and leaves editor text untouched.</summary>
    [Fact]
    public async Task Input_WhenEscapeClosesSuggestions_ConsumesWithoutEditingAsync()
    {
        // Arrange
        var escapeWasHandled = false;
        var input = new SuggestionInput
        {
            Width = Length.Cells(16),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["Alpha", "Beta"])
        };
        _ = input.AddHandler(
            Events.Key,
            (_, eventArgs) =>
            {
                if (eventArgs is KeyEventArgs key &&
                    key.Phase == RoutingPhase.Bubble &&
                    key.Stroke.Code == Code.Escape)
                {
                    escapeWasHandled = key.IsHandled;
                }
            },
            handledEventsToo: true);
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("a");
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();
        await surface.Keyboard.PressAsync(Code.Down);

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        escapeWasHandled.ShouldBeTrue();
        input.Text.ShouldBe("a");
        input.IsOpen.ShouldBeFalse();
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldBe(-1);
    }

    /// <summary>Verifies plain Tab closes the popup but stays available to normal forward and
    /// reverse focus traversal; the owned list never becomes a Tab stop.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Input_WhenPlainTabClosesSuggestions_TraversesOutsideOwnedListAsync(bool reverse)
    {
        // Arrange
        var before = new TextInput { Text = "before" };
        var input = new SuggestionInput
        {
            Width = Length.Cells(16),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["result"])
        };
        var after = new TextInput { Text = "after" };
        var root = new Stack { Children = { before, input, after } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(20, 10),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Tab);
        var editor = OwnedTree.Find<TextInput>(input).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();
        surface.ShouldHaveFocus(editor);
        await surface.Keyboard.TypeAsync("q");

        // Act
        await surface.Keyboard.PressAsync(Code.Tab, reverse ? Modifiers.Shift : Modifiers.None);

        // Assert
        input.IsOpen.ShouldBeFalse();
        surface.ShouldHaveFocus(reverse ? before : after);
        list.IsFocused.ShouldBeFalse();
        list.IsTabStop.ShouldBeFalse();
    }

    /// <summary>Verifies Space remains ordinary editor input while suggestions are open.</summary>
    [Fact]
    public async Task Input_WhenSpaceIsTyped_EditsInsteadOfAcceptingAsync()
    {
        // Arrange
        var queries = new List<string>();
        var accepted = 0;
        var input = new SuggestionInput
        {
            Width = Length.Cells(16),
            Resolver = (searchTerms, _) =>
            {
                queries.Add(searchTerms);
                return ValueTask.FromResult<IReadOnlyList<object?>>([searchTerms]);
            }
        };
        input.SuggestionAccepted += (_, _) => accepted++;
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("q");

        // Act
        await surface.Keyboard.TypeAsync(" ");

        // Assert
        input.Text.ShouldBe("q ");
        queries.ShouldBe(["q", "q "]);
        accepted.ShouldBe(0);
        input.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies neither Enter nor a primary release can accept a visible row from the
    /// superseded snapshot while the newer resolver generation is pending.</summary>
    [Theory]
    [InlineData("enter")]
    [InlineData("pointer")]
    public async Task Input_WhenNewerResolutionIsPending_DeniesStaleVisibleRowAsync(string activation)
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var accepted = 0;
        var input = new SuggestionInput
        {
            Width = Length.Cells(16),
            Resolver = (searchTerms, _) => searchTerms == "a"
                ? ValueTask.FromResult<IReadOnlyList<object?>>(["old"])
                : new ValueTask<IReadOnlyList<object?>>(completion.Task)
        };
        input.SuggestionAccepted += (_, _) => accepted++;
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("a");
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();
        await surface.Keyboard.TypeAsync("b");
        var observation = input.LastResolutionObservation.ShouldNotBeNull();

        // Act
        if (activation == "enter")
        {
            await surface.Keyboard.PressAsync(Code.Enter);
        }
        else
        {
            await surface.Pointer.ClickAsync(list, new Point(1, 0));
        }

        // Assert stale activation
        input.Text.ShouldBe("ab");
        input.IsResolving.ShouldBeTrue();
        input.IsOpen.ShouldBeTrue();
        accepted.ShouldBe(0);

        // Act current completion
        completion.SetResult(["new"]);
        await observation.WaitAsync(TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "render current suggestion completion");

        // Assert current snapshot
        input.Suggestions.ShouldBe(["new"]);
        input.IsResolving.ShouldBeFalse();
        accepted.ShouldBe(0);
    }

    /// <summary>Verifies in-plane wheel input scrolls the result list when possible and remains
    /// consumed at an endpoint instead of dismissing the popup or leaking to a parent scroller.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Input_WhenSuggestionWheelMovesOrHitsEndpoint_RemainsInPopupPlaneAsync(bool canScroll)
    {
        // Arrange
        var count = canScroll ? 8 : 2;
        var input = new SuggestionInput
        {
            Width = Length.Cells(14),
            DropDownHeight = Length.Cells(3),
            Resolver = (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(
                Enumerable.Range(0, count).Select(index => (object?) $"Item {index}").ToArray())
        };
        var background = new ControlText(string.Join('\n', Enumerable.Repeat("Background", 12)));
        var root = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { input, background }
        };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(18, 9),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("q");
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();

        // Act
        await surface.Pointer.WheelAsync(list, default, wheelY: -1);

        // Assert
        input.IsOpen.ShouldBeTrue();
        list.VerticalOffset.ShouldBe(canScroll ? 1 : 0);
        root.VerticalOffset.ShouldBe(0);
        var scope = surface.Application.Modality.Active.ShouldNotBeNull();
        scope.Root.ShouldBeSameAs(input);
        scope.IsActive.ShouldBeTrue();
    }

    /// <summary>Verifies the owned vertical scrollbar remains pointer-operable without dismissing
    /// the owner-managed popup.</summary>
    [Fact]
    public async Task Input_WhenSuggestionScrollBarIsPressed_ScrollsAndKeepsPopupOpenAsync()
    {
        // Arrange
        var input = new SuggestionInput
        {
            Width = Length.Cells(14),
            DropDownHeight = Length.Cells(4),
            ShowScrollBars = ShowScrollBars.Always,
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(
                Enumerable.Range(0, 10).Select(index => (object?) $"Item {index}").ToArray())
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(18, 9),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("q");
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();

        // Act
        await surface.Pointer.ClickAsync(
            list,
            new Point(list.Bounds.Width - 1, list.Bounds.Height - 1));

        // Assert
        list.VerticalOffset.ShouldBeGreaterThan(0);
        input.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a relative popup and uniform rows reflow on resize while preserving the
    /// live provisional row and rendered content.</summary>
    [Fact]
    public async Task ResizeAsync_WhenSuggestionPopupIsRelative_ReflowsWithoutLosingCurrentRowAsync()
    {
        // Arrange
        var input = new SuggestionInput
        {
            Width = Length.Cells(18),
            Height = Length.Cells(3),
            DropDownHeight = Length.Percent(50),
            RowHeight = Length.Percent(50),
            ItemTemplate = item => new ControlText((string) item!) { Height = Length.Star(1) },
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(
                Enumerable.Range(0, 20).Select(index => (object?) $"Item {index}").ToArray())
        };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 20),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("q");
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();
        await surface.Keyboard.PressAsync(Code.Down);
        var initialHeight = list.Bounds.Height;

        // Act
        await surface.ResizeAsync(new Size(24, 12));

        // Assert
        list.Bounds.Height.ShouldBeLessThan(initialHeight);
        list.SelectedIndex.ShouldBe(1);
        list.ActiveIndex.ShouldBe(1);
        OwnedTree.FindAll<ListItem>(list).ShouldAllBe(item => item.Bounds.Height > 0);
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y)).Text.ShouldNotBeEmpty();
    }

    /// <summary>Verifies tiny terminal bounds keep popup geometry and rendering bounded.</summary>
    [Fact]
    public async Task ResizeAsync_WhenTerminalBecomesTiny_KeepsSuggestionGeometryBoundedAsync()
    {
        // Arrange
        var input = new SuggestionInput
        {
            Height = Length.Cells(3),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["界"])
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(8, 5),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("q");

        // Act
        await surface.ResizeAsync(new Size(1, 1));

        // Assert
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();
        list.Bounds.Width.ShouldBeGreaterThanOrEqualTo(0);
        list.Bounds.Height.ShouldBeGreaterThanOrEqualTo(0);
        list.Bounds.Right.ShouldBeLessThanOrEqualTo(1);
        list.Bounds.Bottom.ShouldBeLessThanOrEqualTo(1);
        _ = surface.Cell(default);
    }

    /// <summary>Verifies direct hidden and disabled transitions close interaction but do not
    /// cancel a current resolver; its completion publishes without reopening while unavailable.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Availability_WhenOwnerBecomesUnavailable_PreservesCurrentResolutionAsync(bool hide)
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken pendingToken = default;
        var input = new SuggestionInput
        {
            Width = Length.Cells(16),
            Resolver = (searchTerms, cancellationToken) =>
            {
                if (searchTerms == "a")
                {
                    return ValueTask.FromResult<IReadOnlyList<object?>>(["initial"]);
                }

                pendingToken = cancellationToken;
                return new ValueTask<IReadOnlyList<object?>>(completion.Task);
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("a");
        await surface.Keyboard.TypeAsync("b");
        var observation = input.LastResolutionObservation.ShouldNotBeNull();

        // Act unavailable
        await surface.UpdateAsync(
            () =>
            {
                if (hide)
                {
                    input.Visibility = Visibility.Hidden;
                }
                else
                {
                    input.IsEnabled = false;
                }
            },
            hide ? "hide resolving suggestion input" : "disable resolving suggestion input");

        // Assert while unavailable
        pendingToken.IsCancellationRequested.ShouldBeFalse();
        input.IsResolving.ShouldBeTrue();
        input.IsOpen.ShouldBeFalse();

        // Act completion
        completion.SetResult(["fresh"]);
        await observation.WaitAsync(TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "render unavailable completion");

        // Assert completion remains current without interaction
        pendingToken.IsCancellationRequested.ShouldBeFalse();
        input.Suggestions.ShouldBe(["fresh"]);
        input.IsResolving.ShouldBeFalse();
        input.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies ancestor unavailability suspends and later restores the owner modal scope
    /// without closing the popup or cancelling its current resolver.</summary>
    [Fact]
    public async Task Availability_WhenAncestorIsDisabledAndRecovers_PreservesPopupResolutionAndScopeAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken pendingToken = default;
        var input = new SuggestionInput
        {
            Width = Length.Cells(16),
            Resolver = (searchTerms, cancellationToken) =>
            {
                if (searchTerms == "a")
                {
                    return ValueTask.FromResult<IReadOnlyList<object?>>(["initial"]);
                }

                pendingToken = cancellationToken;
                return new ValueTask<IReadOnlyList<object?>>(completion.Task);
            }
        };
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("a");
        var openingScope = surface.Application.Modality.Active.ShouldNotBeNull();
        await surface.Keyboard.TypeAsync("b");
        var observation = input.LastResolutionObservation.ShouldNotBeNull();

        // Act suspend
        await surface.UpdateAsync(() => root.IsEnabled = false, "disable suggestion input ancestor");

        // Assert suspension
        input.IsOpen.ShouldBeTrue();
        input.IsResolving.ShouldBeTrue();
        pendingToken.IsCancellationRequested.ShouldBeFalse();
        openingScope.IsActive.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();

        // Act settle and recover
        completion.SetResult(["fresh"]);
        await observation.WaitAsync(TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => root.IsEnabled = true, "restore suggestion input ancestor");

        // Assert recovery
        input.Suggestions.ShouldBe(["fresh"]);
        input.IsOpen.ShouldBeTrue();
        var restoredScope = surface.Application.Modality.Active.ShouldNotBeNull();
        restoredScope.ShouldNotBeSameAs(openingScope);
        restoredScope.Root.ShouldBeSameAs(input);
        restoredScope.IsActive.ShouldBeTrue();
    }

    /// <summary>Verifies cancellation never restores a provisional row captured for an older
    /// result generation over the first row selected for the current replacement snapshot.</summary>
    [Fact]
    public async Task Input_WhenResultsChangeDuringOpenSession_EscapeDoesNotRestoreStaleProvisionalRowAsync()
    {
        // Arrange
        var input = new SuggestionInput
        {
            Width = Length.Cells(16),
            Resolver = (searchTerms, _) => ValueTask.FromResult<IReadOnlyList<object?>>(
                searchTerms == "a" ? ["old 0", "old 1"] : ["new 0", "new 1"])
        };
        await using var surface = await ComponentSurface.MountAsync(
            input,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("a");
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();
        await surface.Keyboard.PressAsync(Code.Down);
        list.SelectedIndex.ShouldBe(1);
        await surface.Keyboard.TypeAsync("b");
        list.SelectedIndex.ShouldBe(0);

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        input.Text.ShouldBe("ab");
        input.Suggestions.ShouldBe(["new 0", "new 1"]);
        input.IsOpen.ShouldBeFalse();
        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(0);
    }

    /// <summary>Verifies detachment cancels the current lease and rejects its non-cooperative late
    /// completion without publishing or accepting detached popup state.</summary>
    [Fact]
    public async Task Lifecycle_WhenDetachedDuringResolution_CancelsAndRejectsLateInteractionAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken pendingToken = default;
        var accepted = 0;
        var input = new SuggestionInput
        {
            Width = Length.Cells(16),
            Resolver = (searchTerms, cancellationToken) => searchTerms == "a"
                ? ValueTask.FromResult<IReadOnlyList<object?>>(["initial"])
                : ResolvePending(cancellationToken)
        };
        input.SuggestionAccepted += (_, _) => accepted++;
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(18, 7),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.TypeAsync("a");
        await surface.Keyboard.TypeAsync("b");
        var observation = input.LastResolutionObservation.ShouldNotBeNull();

        // Act
        await surface.UpdateAsync(() => root.Children.Remove(input), "detach resolving suggestion input");
        completion.SetResult(["late"]);
        await observation.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        pendingToken.IsCancellationRequested.ShouldBeTrue();
        input.Dispatcher.ShouldBeNull();
        input.IsResolving.ShouldBeFalse();
        input.Suggestions.ShouldBe(["initial"]);
        input.IsOpen.ShouldBeFalse();
        accepted.ShouldBe(0);
        input.Dispose();
        return;

        ValueTask<IReadOnlyList<object?>> ResolvePending(CancellationToken cancellationToken)
        {
            pendingToken = cancellationToken;
            return new ValueTask<IReadOnlyList<object?>>(completion.Task);
        }
    }
}
