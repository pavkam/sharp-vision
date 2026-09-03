// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves CommandPalette interactions through a mounted terminal surface and detached
/// property conditions: typing that opens results, Escape and Space semantics, asynchronous
/// resolver faults and cancellations, stale completions, empty and very long result sets,
/// activation focus, chrome overrides, and availability or lifetime changes mid-resolution.</summary>
public sealed class CommandPaletteInteractionTests
{
    #region Typing, Escape, Space, and activation

    /// <summary>Verifies the first typed character resolves, opens the results, and publishes
    /// ResultsChanged before PropertyChanged(IsOpen) and Opened, with the first row selected.</summary>
    [Fact]
    public async Task Typing_WhenTextIsTypedWhileClosed_OpensResultsAndPublishesInOrderAsync()
    {
        var palette = NewPalette(static terms => terms.Length == 0 ? [] : [$"{terms} file", $"{terms} folder"]);
        var events = new List<string>();
        Observe(palette, events);
        await using var surface = await MountAsync(palette, new Size(24, 8));
        var editor = OwnedTree.Find<TextInput>(palette).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(editor);
        palette.IsOpen.ShouldBeFalse();
        events.Clear();

        await surface.Keyboard.TypeAsync("o");

        palette.Text.ShouldBe("o");
        palette.IsOpen.ShouldBeTrue();
        events.ShouldBe(["ResultsChanged", "PropertyChanged:IsOpen", "Opened"]);
        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(0);
        surface.ShouldHaveFocus(editor);
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y)).Text.ShouldBe("o");
        surface.Application.Modality.Active.ShouldNotBeNull().Root.ShouldBeSameAs(palette);
    }

    /// <summary>Verifies Escape closes the results while preserving the query text and items and
    /// restores the focus that preceded the open: the button that called Open(), or the editor
    /// itself when typing into the focused editor opened the results. A second Escape with nothing
    /// open is harmless.</summary>
    [Theory]
    [InlineData("button")]
    [InlineData("typed")]
    public async Task Escape_WhenPressedWithResultsOpen_ClosesPreservesTextAndRestoresPriorFocusAsync(string openPath)
    {
        var palette = NewPalette(static terms => [$"{terms}-1", $"{terms}-2"]);
        var events = new List<string>();
        Observe(palette, events);
        var button = new Button { Text = "Go" };
        var root = new Stack { Children = { button, palette } };
        await using var surface = await MountAsync(root, new Size(24, 12));
        var editor = OwnedTree.Find<TextInput>(palette).ShouldNotBeNull();
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(button).ShouldBeTrue(), "focus the button");

        if (openPath == "button")
        {
            await surface.UpdateAsync(() => palette.Open(), "open from the button");
        }
        else
        {
            await surface.UpdateAsync(() => surface.Application.Focus.Focus(editor).ShouldBeTrue(), "focus the editor");
        }

        await surface.Keyboard.TypeAsync("ab");
        palette.IsOpen.ShouldBeTrue();
        surface.ShouldHaveFocus(editor);
        events.Clear();

        await surface.Keyboard.PressAsync(Code.Escape);

        palette.IsOpen.ShouldBeFalse();
        palette.Text.ShouldBe("ab");
        palette.Items.ShouldBe(["ab-1", "ab-2"]);
        events.ShouldBe(["PropertyChanged:IsOpen", "Closed"]);
        surface.ShouldHaveFocus(openPath == "button" ? button : editor);
        surface.Application.Modality.Active.ShouldBeNull();
        surface.Cell(new Point(palette.Bounds.X + 1, palette.Bounds.Y + 1)).Text.ShouldBe("a");
        events.Clear();

        await surface.Keyboard.PressAsync(Code.Escape);

        palette.IsOpen.ShouldBeFalse();
        palette.Text.ShouldBe("ab");
        events.ShouldBeEmpty();
    }

    /// <summary>Verifies Space is ordinary query text: it reaches the resolver and never accepts
    /// the highlighted result.</summary>
    [Fact]
    public async Task Space_WhenTyped_EditsTheQueryWithoutAcceptingAsync()
    {
        var queries = new List<string>();
        var palette = NewPalette(terms =>
        {
            queries.Add(terms);
            return ["Open file", "Open folder"];
        });
        var invoked = 0;
        palette.ItemInvoked += (_, _) => invoked++;
        await using var surface = await MountAsync(palette, new Size(24, 8));
        await surface.UpdateAsync(() => palette.Open(), "open the palette");
        queries.Clear();

        await surface.Keyboard.TypeAsync(" ");

        palette.Text.ShouldBe(" ");
        queries.ShouldBe([" "]);
        invoked.ShouldBe(0);
        palette.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies Enter and pointer activation both preserve the query, release the modal
    /// scope, report the invoked row, and return focus to the control that had it before Open().</summary>
    [Theory]
    [InlineData("keyboard")]
    [InlineData("pointer")]
    public async Task Activation_WhenResultIsInvoked_PreservesQueryAndRestoresPriorFocusAsync(string path)
    {
        var palette = NewPalette(static _ => ["Open file", "Open folder"]);
        ItemInvokedEventArgs? invoked = null;
        palette.ItemInvoked += (_, eventArgs) => invoked = eventArgs;
        var button = new Button { Text = "Go" };
        var root = new Stack { Children = { button, palette } };
        await using var surface = await MountAsync(root, new Size(24, 12));
        var editor = OwnedTree.Find<TextInput>(palette).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(button).ShouldBeTrue(), "focus the button");
        await surface.UpdateAsync(() => palette.Open(), "open the palette");
        surface.ShouldHaveFocus(editor);
        await surface.Keyboard.TypeAsync("op");

        if (path == "pointer")
        {
            await surface.Pointer.ClickAsync(list, new Point(1, 1));
        }
        else
        {
            await surface.Keyboard.PressAsync(Code.Down);
            await surface.Keyboard.PressAsync(Code.Enter);
        }

        var actual = invoked.ShouldNotBeNull();
        actual.Index.ShouldBe(1);
        actual.Item.ShouldBe("Open folder");
        actual.Cause.ShouldBe(path == "pointer" ? ActivationCause.Pointer : ActivationCause.Keyboard);
        palette.IsOpen.ShouldBeFalse();
        palette.Text.ShouldBe("op");
        surface.ShouldHaveFocus(button);
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies Up at the first row and Down at the last row clamp instead of wrapping,
    /// and the popup stays open.</summary>
    [Fact]
    public async Task Navigation_WhenAtEitherEnd_ClampsWithoutWrappingAsync()
    {
        var palette = NewPalette(static _ => ["One", "Two", "Three"]);
        await using var surface = await MountAsync(palette, new Size(24, 8));
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        await surface.UpdateAsync(() => palette.Open(), "open the palette");
        list.SelectedIndex.ShouldBe(0);

        await surface.Keyboard.PressAsync(Code.Up);

        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(0);
        palette.IsOpen.ShouldBeTrue();

        await surface.Keyboard.PressAsync(Code.End);
        await surface.Keyboard.PressAsync(Code.Down);

        list.SelectedIndex.ShouldBe(2);
        list.ActiveIndex.ShouldBe(2);
        palette.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies Tab with results open closes them, leaves the query, and moves focus to
    /// the next sibling through ordinary traversal; Shift+Tab moves to the previous one.</summary>
    [Theory]
    [InlineData(Modifiers.None)]
    [InlineData(Modifiers.Shift)]
    public async Task Tab_WhenPressedWithResultsOpen_ClosesAndMovesFocusToSiblingAsync(Modifiers modifiers)
    {
        var palette = NewPalette(static _ => ["One", "Two"]);
        var before = new Button { Text = "Before" };
        var after = new Button { Text = "After" };
        var root = new Stack { Children = { before, palette, after } };
        await using var surface = await MountAsync(root, new Size(24, 12));
        await surface.UpdateAsync(() => palette.Open(), "open the palette");
        await surface.Keyboard.TypeAsync("q");
        palette.IsOpen.ShouldBeTrue();

        await surface.Keyboard.PressAsync(Code.Tab, modifiers);

        palette.IsOpen.ShouldBeFalse();
        palette.Text.ShouldBe("q");
        surface.ShouldHaveFocus(modifiers == Modifiers.Shift ? before : after);
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies a wheel outside the results dismisses them, preserves the query, and
    /// restores the focus that preceded Open().</summary>
    [Fact]
    public async Task Wheel_WhenOutsideResults_ClosesAndRestoresPriorFocusAsync()
    {
        var palette = NewPalette(static _ => ["One", "Two"]);
        var outside = new ControlText(string.Join('\n', Enumerable.Repeat("outside", 3)));
        Overlay.SetTop(outside, Length.Cells(7));
        var root = new Overlay { Children = { palette, outside } };
        await using var surface = await MountAsync(root, new Size(24, 10));
        var editor = OwnedTree.Find<TextInput>(palette).ShouldNotBeNull();
        var priorFocus = surface.Application.Focus.Focused.ShouldNotBeNull();
        await surface.UpdateAsync(() => palette.Open(), "open the palette");
        await surface.Keyboard.TypeAsync("q");
        surface.ShouldHaveFocus(editor);

        await surface.Pointer.WheelAsync(outside, new Point(0, 1), wheelY: -1);

        palette.IsOpen.ShouldBeFalse();
        palette.Text.ShouldBe("q");
        surface.Application.Focus.Focused.ShouldBeSameAs(priorFocus);
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies the light-dismiss registration survives a disable/enable cycle: a click
    /// outside still closes results opened after re-enabling.</summary>
    [Fact]
    public async Task LightDismiss_WhenPaletteWasDisabledAndReEnabled_StillClosesOnOutsideClickAsync()
    {
        var palette = NewPalette(static _ => ["One", "Two"]);
        var outside = new ControlText("outside");
        Overlay.SetTop(outside, Length.Cells(7));
        var root = new Overlay { Children = { palette, outside } };
        var closed = 0;
        palette.Closed += (_, _) => closed++;
        await using var surface = await MountAsync(root, new Size(24, 9));
        var editor = OwnedTree.Find<TextInput>(palette).ShouldNotBeNull();
        await surface.UpdateAsync(() => palette.Open(), "open the palette");
        await surface.UpdateAsync(() => palette.IsEnabled = false, "disable the open palette");
        closed.ShouldBe(1);
        await surface.UpdateAsync(() => palette.IsEnabled = true, "re-enable the palette");
        var priorFocus = surface.Application.Focus.Focused;
        await surface.UpdateAsync(() => palette.Open(), "reopen the palette");
        palette.IsOpen.ShouldBeTrue();
        surface.ShouldHaveFocus(editor);

        await surface.Pointer.ClickAsync(outside);

        palette.IsOpen.ShouldBeFalse();
        closed.ShouldBe(2);
        surface.Application.Modality.Active.ShouldBeNull();
        surface.Application.Focus.Focused.ShouldBeSameAs(priorFocus);
        surface.Application.Focus.Focused.ShouldNotBeSameAs(editor);
    }

    #endregion

    #region Asynchronous resolution outcomes

    /// <summary>Verifies an asynchronous resolver fault for the still-current query raises
    /// ResolutionFailed with the terms and exception, clears the results, closes the popup, and
    /// leaves the editor focused and editable even though the results were opened from elsewhere:
    /// a close the user did not request must not move focus away from the query.</summary>
    [Fact]
    public async Task Resolver_WhenAsyncTaskFaults_RaisesResolutionFailedAndClosesResultsAsync()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = (terms, _) => terms == "op"
                ? new ValueTask<IReadOnlyList<object?>>(completion.Task)
                : ValueTask.FromResult<IReadOnlyList<object?>>(["Open file"])
        };
        var failed = new TaskCompletionSource<CommandPaletteResolutionFailedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        palette.ResolutionFailed += (_, eventArgs) => failed.TrySetResult(eventArgs);
        var closed = 0;
        palette.Closed += (_, _) => closed++;
        var button = new Button { Text = "Go" };
        var root = new Stack { Children = { button, palette } };
        await using var surface = await MountAsync(root, new Size(24, 12));
        var editor = OwnedTree.Find<TextInput>(palette).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(button).ShouldBeTrue(), "focus the button");
        await surface.UpdateAsync(() => palette.Open(), "open the palette");
        await surface.UpdateAsync(() => palette.Text = "o", "resolve synchronously");
        palette.IsOpen.ShouldBeTrue();
        await surface.UpdateAsync(() => palette.Text = "op", "start the asynchronous query");
        palette.IsResolving.ShouldBeTrue();
        var exception = new InvalidOperationException("backend unavailable");

        completion.SetException(exception);
        var failure = await failed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "settle after the failure");

        failure.SearchTerms.ShouldBe("op");
        failure.Exception.ShouldBeSameAs(exception);
        palette.Items.ShouldBeEmpty();
        palette.IsResolving.ShouldBeFalse();
        palette.IsOpen.ShouldBeFalse();
        closed.ShouldBe(1);
        palette.Text.ShouldBe("op");
        surface.ShouldHaveFocus(editor);
        surface.Cell(new Point(list.Bounds.X, palette.Bounds.Bottom)).Text.ShouldNotBe("O");

        await surface.Keyboard.TypeAsync("x");

        palette.Text.ShouldBe("opx");
        palette.Items.ShouldBe(["Open file"]);
        palette.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a superseded asynchronous query is cancelled through its token and its
    /// cancelled completion is discarded silently: no failure event, the newer results stand. The
    /// stale source runs its continuation inline, so the palette's own completion path has run to
    /// its dispatcher post before the settle that precedes the assertions.</summary>
    [Fact]
    public async Task Resolver_WhenSupersededTaskObservesCancellation_IsDiscardedSilentlyAsync()
    {
        var first = new TaskCompletionSource<IReadOnlyList<object?>>();
        CancellationToken firstToken = default;
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = (terms, token) =>
            {
                if (terms == "a")
                {
                    firstToken = token;
                    _ = token.Register(() => first.TrySetCanceled(token));
                    return new ValueTask<IReadOnlyList<object?>>(first.Task);
                }

                return ValueTask.FromResult<IReadOnlyList<object?>>([terms]);
            }
        };
        var failures = 0;
        palette.ResolutionFailed += (_, _) => failures++;
        var results = 0;
        palette.ResultsChanged += (_, _) => results++;
        await using var surface = await MountAsync(palette, new Size(24, 8));
        await surface.UpdateAsync(() => palette.Open(), "open the palette");
        results = 0;
        await surface.UpdateAsync(() => palette.Text = "a", "start the asynchronous query");
        palette.IsResolving.ShouldBeTrue();

        await surface.UpdateAsync(() => palette.Text = "ab", "supersede it synchronously");
        await first.Task.ContinueWith(static _ => { }, TaskScheduler.Default)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "settle after the cancelled completion");

        firstToken.IsCancellationRequested.ShouldBeTrue();
        first.Task.IsCanceled.ShouldBeTrue();
        failures.ShouldBe(0);
        results.ShouldBe(1);
        palette.Items.ShouldBe(["ab"]);
        palette.IsResolving.ShouldBeFalse();
        palette.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a resolver task that cancels itself while its lease is still current is a
    /// failure, not a silent discard: the palette cannot tell it apart from any other fault.</summary>
    [Fact]
    public async Task Resolver_WhenCurrentTaskCancelsItself_ReportsFailureAsync()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = (_, _) => new ValueTask<IReadOnlyList<object?>>(completion.Task)
        };
        var failed = new TaskCompletionSource<CommandPaletteResolutionFailedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        palette.ResolutionFailed += (_, eventArgs) => failed.TrySetResult(eventArgs);
        await using var surface = await MountAsync(palette, new Size(24, 8));
        await surface.UpdateAsync(() => palette.Text = "q", "start the asynchronous query");
        palette.IsResolving.ShouldBeTrue();

        completion.SetCanceled(TestContext.Current.CancellationToken);
        var failure = await failed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "settle after the self-cancelled completion");

        failure.SearchTerms.ShouldBe("q");
        _ = failure.Exception.ShouldBeAssignableTo<OperationCanceledException>();
        palette.IsResolving.ShouldBeFalse();
        palette.IsOpen.ShouldBeFalse();
        palette.Items.ShouldBeEmpty();
    }

    /// <summary>Verifies an older asynchronous completion arriving after a newer one cannot replace
    /// the newer items, reopen, or raise a second ResultsChanged on a mounted palette. The stale
    /// source is completed only after the newer results are committed and settled, and it runs
    /// its continuation inline, so the palette's stale completion path (up to its dispatcher post)
    /// has already run when the single settle drains the dispatcher before the assertions.</summary>
    [Fact]
    public async Task Resolver_WhenOlderCompletionArrivesLast_IsDiscardedAsync()
    {
        var older = new TaskCompletionSource<IReadOnlyList<object?>>();
        var newer = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = (terms, _) => terms switch
            {
                "a" => new ValueTask<IReadOnlyList<object?>>(older.Task),
                "ab" => new ValueTask<IReadOnlyList<object?>>(newer.Task),
                _ => ValueTask.FromResult<IReadOnlyList<object?>>([])
            }
        };
        var resultsChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var results = 0;
        palette.ResultsChanged += (_, _) =>
        {
            results++;
            _ = resultsChanged.TrySetResult();
        };
        await using var surface = await MountAsync(palette, new Size(24, 8));
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        await surface.UpdateAsync(() => palette.Open(), "open the palette");
        await surface.UpdateAsync(() => palette.Text = "a", "start the older query");
        await surface.UpdateAsync(() => palette.Text = "ab", "start the newer query");
        results = 0;

        newer.SetResult(["newer"]);
        await resultsChanged.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "settle the newer completion");
        palette.Items.ShouldBe(["newer"]);
        older.SetResult(["older"]);
        await surface.UpdateAsync(static () => { }, "settle after the stale completion");

        results.ShouldBe(1);
        palette.Items.ShouldBe(["newer"]);
        palette.IsOpen.ShouldBeTrue();
        list.SelectedIndex.ShouldBe(0);
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y)).Text.ShouldBe("n");
    }

    /// <summary>Verifies a completion that lands while the palette is hidden or disabled commits
    /// its items without presenting a popup, without entering a modal scope for an unavailable
    /// root, and without faulting the dispatcher; Open after restoring availability presents them.</summary>
    [Theory]
    [InlineData("hidden")]
    [InlineData("disabled")]
    public async Task Resolver_WhenCompletionLandsWhileUnavailable_CommitsWithoutPresentingOrFaultingAsync(string state)
    {
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = (_, _) => new ValueTask<IReadOnlyList<object?>>(completion.Task)
        };
        var resultsChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        palette.ResultsChanged += (_, _) => resultsChanged.TrySetResult();
        var opened = 0;
        palette.Opened += (_, _) => opened++;
        var root = new Overlay { Children = { palette } };
        await using var surface = await MountAsync(root, new Size(24, 8));
        var unhandled = new List<Exception>();
        surface.Application.UnhandledException += (_, eventArgs) => unhandled.Add(eventArgs.Exception);
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        await surface.UpdateAsync(() => palette.IsOpen = true, "request results");
        palette.IsResolving.ShouldBeTrue();
        await surface.UpdateAsync(
            () =>
            {
                if (state == "hidden")
                {
                    palette.Visibility = Visibility.Hidden;
                }
                else
                {
                    palette.IsEnabled = false;
                }
            },
            "make the palette unavailable mid-resolution");

        completion.SetResult(["Late result"]);
        await resultsChanged.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await surface.UpdateAsync(static () => { }, "settle the unavailable completion");

        unhandled.ShouldBeEmpty();
        palette.Items.ShouldBe(["Late result"]);
        palette.IsResolving.ShouldBeFalse();
        palette.IsOpen.ShouldBeFalse();
        opened.ShouldBe(0);
        surface.Application.Modality.Active.ShouldBeNull();

        await surface.UpdateAsync(
            () =>
            {
                palette.Visibility = Visibility.Visible;
                palette.IsEnabled = true;
            },
            "restore availability");
        await surface.UpdateAsync(() => palette.Open(), "open the retained results");

        palette.IsOpen.ShouldBeTrue();
        opened.ShouldBe(1);
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y)).Text.ShouldBe("L");
        unhandled.ShouldBeEmpty();
    }

    /// <summary>Verifies an asynchronous completion that arrives after the palette was disposed is
    /// dropped without publishing or faulting the still-running application. The source runs its
    /// continuation inline, so the palette's completion path has run before the single settle.</summary>
    [Fact]
    public async Task Dispose_WhenCompletionArrivesAfterDisposal_IsDroppedWithoutFaultingAsync()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>();
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = (_, _) => new ValueTask<IReadOnlyList<object?>>(completion.Task)
        };
        var published = 0;
        palette.ResultsChanged += (_, _) => published++;
        palette.ResolutionFailed += (_, _) => published++;
        var root = new Overlay { Children = { palette } };
        await using var surface = await MountAsync(root, new Size(24, 8));
        var unhandled = new List<Exception>();
        surface.Application.UnhandledException += (_, eventArgs) => unhandled.Add(eventArgs.Exception);
        await surface.UpdateAsync(() => palette.Text = "q", "start the asynchronous query");
        await surface.UpdateAsync(() => Should.NotThrow(palette.Dispose), "dispose mid-resolution");

        completion.SetResult(["late"]);
        await surface.UpdateAsync(static () => { }, "settle after the late completion");

        palette.IsDisposed.ShouldBeTrue();
        published.ShouldBe(0);
        unhandled.ShouldBeEmpty();
        surface.ShouldRender("");
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies Escape while a query is still resolving with no results open cancels
    /// that query, as the keyboard table documents: the resolving state clears at once and the
    /// late completion neither opens results nor replaces the retained items.</summary>
    [Fact]
    public async Task Escape_WhenPressedDuringResolutionWithNothingOpen_CancelsSoLateResultsNeverOpenAsync()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>();
        CancellationToken token = default;
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = (terms, cancellation) =>
            {
                if (terms == "slow")
                {
                    token = cancellation;
                    return new ValueTask<IReadOnlyList<object?>>(completion.Task);
                }

                return ValueTask.FromResult<IReadOnlyList<object?>>(terms.Length == 0 ? [] : [terms]);
            }
        };
        var events = new List<string>();
        Observe(palette, events);
        await using var surface = await MountAsync(palette, new Size(24, 8));
        var editor = OwnedTree.Find<TextInput>(palette).ShouldNotBeNull();
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(editor).ShouldBeTrue(), "focus the editor");
        await surface.UpdateAsync(() => palette.Text = "slow", "start the slow query");
        palette.IsResolving.ShouldBeTrue();
        palette.IsOpen.ShouldBeFalse();
        events.Clear();

        await surface.Keyboard.PressAsync(Code.Escape);

        palette.IsResolving.ShouldBeFalse();
        token.IsCancellationRequested.ShouldBeTrue();
        palette.IsOpen.ShouldBeFalse();
        palette.Text.ShouldBe("slow");
        surface.ShouldHaveFocus(editor);

        completion.SetResult(["late"]);
        await surface.UpdateAsync(static () => { }, "settle after the late completion");

        palette.IsOpen.ShouldBeFalse();
        palette.Items.ShouldBeEmpty();
        events.ShouldBeEmpty();
        surface.Application.Modality.Active.ShouldBeNull();

        await surface.Keyboard.TypeAsync("x");

        palette.Items.ShouldBe(["slowx"]);
        palette.IsOpen.ShouldBeTrue("a later query still opens normally");
    }

    /// <summary>Verifies hiding, detaching, or disposing the palette while results are open on a
    /// mounted surface tears the results down: the popup closes, the modal scope is released, the
    /// rendered rows disappear, and focus does not linger in the gone editor.</summary>
    [Theory]
    [InlineData("hidden")]
    [InlineData("detached")]
    [InlineData("disposed")]
    public async Task Availability_WhenLostWhileResultsAreOpen_ClosesAndReleasesScopeAsync(string state)
    {
        var palette = NewPalette(static _ => ["One", "Two"]);
        var closed = 0;
        palette.Closed += (_, _) => closed++;
        var root = new Overlay { Children = { palette } };
        await using var surface = await MountAsync(root, new Size(24, 9));
        var editor = OwnedTree.Find<TextInput>(palette).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        await surface.UpdateAsync(() => palette.Open(), "open the palette");
        surface.ShouldHaveFocus(editor);
        var firstRow = new Point(list.Bounds.X, list.Bounds.Y);
        surface.Cell(firstRow).Text.ShouldBe("O");

        await surface.UpdateAsync(
            () =>
            {
                switch (state)
                {
                    case "hidden":
                        palette.Visibility = Visibility.Hidden;
                        break;
                    case "detached":
                        root.Children.Remove(palette).ShouldBeTrue();
                        break;
                    default:
                        palette.Dispose();
                        break;
                }
            },
            $"make the open palette {state}");

        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldRender("");
        editor.IsFocused.ShouldBeFalse();
        closed.ShouldBe(state == "disposed" ? 0 : 1, "a disposed control publishes nothing; the others close once");

        if (state != "disposed")
        {
            palette.IsOpen.ShouldBeFalse();
        }
    }

    #endregion

    #region Result sets

    /// <summary>Verifies a refresh that yields no results while open closes the popup, raises
    /// Closed once, never raises Opened again, clears the rendered rows, and keeps focus in the
    /// editor (even when Open() came from a button) so the next keystroke still edits the query
    /// and non-empty results reopen.</summary>
    [Fact]
    public async Task Results_WhenRefreshedToEmptyWhileOpen_ClosesWithoutOpenedAndKeepsEditorFocusAsync()
    {
        var palette = NewPalette(static terms => terms.Contains('!') ? [] : ["One", "Two"]);
        var opened = 0;
        var closed = 0;
        palette.Opened += (_, _) => opened++;
        palette.Closed += (_, _) => closed++;
        var button = new Button { Text = "Go" };
        var root = new Stack { Children = { button, palette } };
        await using var surface = await MountAsync(root, new Size(24, 12));
        var editor = OwnedTree.Find<TextInput>(palette).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(button).ShouldBeTrue(), "focus the button");
        await surface.UpdateAsync(() => palette.Open(), "open the palette");
        var firstRow = new Point(list.Bounds.X, list.Bounds.Y);
        surface.Cell(firstRow).Text.ShouldBe("O");
        opened.ShouldBe(1);

        await surface.Keyboard.TypeAsync("!");

        palette.IsOpen.ShouldBeFalse();
        palette.Items.ShouldBeEmpty();
        closed.ShouldBe(1);
        opened.ShouldBe(1);
        surface.Cell(firstRow).Text.ShouldNotBe("O");
        surface.Application.Modality.Active.ShouldBeNull();
        surface.ShouldHaveFocus(editor);

        await surface.Keyboard.PressAsync(Code.Backspace);

        palette.Text.ShouldBeEmpty();
        palette.IsOpen.ShouldBeTrue("non-empty results for the still-wanted query reopen");
        opened.ShouldBe(2);
        surface.ShouldHaveFocus(editor);
    }

    /// <summary>Verifies a very long uniform-row result set is virtualized: only a bounded number
    /// of rows is realized, End reaches the last result, and Home returns to the first.</summary>
    [Fact]
    public async Task Results_WhenListIsVeryLong_VirtualizesRowsAndNavigatesToEndsAsync()
    {
        var palette = new CommandPalette
        {
            Width = Length.Cells(20),
            RowHeight = Length.Cells(1),
            DropDownHeight = Length.Cells(5),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(
                [.. Enumerable.Range(0, 5000).Select(index => (object?) $"Command {index}")])
        };
        var root = new Overlay { Children = { palette } };
        await using var surface = await MountAsync(root, new Size(24, 12));
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        await surface.UpdateAsync(() => palette.Open(), "open the long result list");

        palette.Items.Count.ShouldBe(5000);
        list.Bounds.Height.ShouldBe(5);
        OwnedTree.FindAll<ListItem>(list).Count.ShouldBeLessThan(100);

        await surface.Keyboard.PressAsync(Code.End);

        list.SelectedIndex.ShouldBe(4999);
        list.ActiveIndex.ShouldBe(4999);
        list.VerticalOffset.ShouldBeGreaterThan(0);
        OwnedTree.FindAll<ListItem>(list).Count.ShouldBeLessThan(100);
        var lastRow = OwnedTree.FindAll<ListItem>(list).Single(item => item.Index == 4999);
        surface.Cell(new Point(lastRow.Bounds.X + 8, lastRow.Bounds.Y)).Text.ShouldBe("4");

        await surface.Keyboard.PressAsync(Code.Home);

        list.SelectedIndex.ShouldBe(0);
        list.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies Refresh on a closed palette with retained items reopens them with the
    /// first row selected.</summary>
    [Fact]
    public async Task Refresh_WhenClosedWithItems_ReopensWithFirstRowSelectedAsync()
    {
        var palette = NewPalette(static _ => ["One", "Two"]);
        await using var surface = await MountAsync(palette, new Size(24, 8));
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        await surface.UpdateAsync(() => palette.Open(), "open the palette");
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Escape);
        palette.IsOpen.ShouldBeFalse();

        await surface.UpdateAsync(palette.Refresh, "refresh the closed palette");

        palette.IsOpen.ShouldBeTrue();
        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(0);
    }

    /// <summary>Verifies Open without a resolver still focuses the editor but presents nothing.</summary>
    [Fact]
    public async Task Open_WhenResolverIsNull_FocusesEditorWithoutPresentingAsync()
    {
        var palette = new CommandPalette { Width = Length.Cells(18) };
        var opened = 0;
        palette.Opened += (_, _) => opened++;
        await using var surface = await MountAsync(palette, new Size(24, 8));
        var editor = OwnedTree.Find<TextInput>(palette).ShouldNotBeNull();
        var focused = false;

        await surface.UpdateAsync(() => focused = palette.Open(), "open without a resolver");

        focused.ShouldBeTrue();
        surface.ShouldHaveFocus(editor);
        palette.IsOpen.ShouldBeFalse();
        palette.Items.ShouldBeEmpty();
        opened.ShouldBe(0);
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies IsOpen=true while hidden is refused outright and does not linger as open
    /// intent that a later show would honor.</summary>
    [Fact]
    public async Task IsOpen_WhenSetWhileHidden_IsRefusedWithoutLingeringIntentAsync()
    {
        var palette = NewPalette(static _ => ["One"]);
        var root = new Overlay { Children = { palette } };
        await using var surface = await MountAsync(root, new Size(24, 8));
        await surface.UpdateAsync(() => palette.Visibility = Visibility.Hidden, "hide the palette");

        await surface.UpdateAsync(() => palette.IsOpen = true, "request results while hidden");

        palette.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();

        await surface.UpdateAsync(() => palette.Visibility = Visibility.Visible, "show the palette");

        palette.IsOpen.ShouldBeFalse();
    }

    #endregion

    #region Chrome and presentation conditions

    /// <summary>Verifies a PopupChrome override reaches the rendered result frame and the reset
    /// restores the themed glyph, both while the results stay open.</summary>
    [Fact]
    public async Task PopupChrome_WhenSetAndResetWhileOpen_RepaintsResultFrameAsync()
    {
        var palette = NewPalette(static _ => ["One", "Two"]);
        var root = new Overlay { Children = { palette } };
        await using var surface = await MountAsync(root, new Size(24, 9));
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        var popup = OwnedTree.FindAll<Popup>(palette).Single(candidate => ReferenceEquals(candidate.Content, list));
        await surface.UpdateAsync(() => palette.Open(), "open the palette");
        var corner = new Point(popup.SurfaceBounds.X, popup.SurfaceBounds.Bottom - 1);
        var themed = surface.Cell(corner).Text;
        themed.ShouldNotBe("+");
        var published = 0;
        palette.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(CommandPalette.PopupChrome))
            {
                published++;
            }
        };
        var chrome = new PopupChrome
        {
            Border = new Border(BorderSide.All, BorderGlyphStyle.Ascii, Color.Default, Color.Transparent, TerminalAttributes.None)
        };

        await surface.UpdateAsync(() => palette.PopupChrome = chrome, "override the result chrome");

        palette.IsOpen.ShouldBeTrue();
        palette.PopupChrome.ShouldBe(chrome);
        surface.Cell(corner).Text.ShouldBe("+");
        published.ShouldBe(1);

        await surface.UpdateAsync(() => palette.PopupChrome = chrome, "reassign the same chrome");
        published.ShouldBe(1);

        await surface.UpdateAsync(palette.ResetPopupChrome, "reset the result chrome");

        palette.PopupChrome.ShouldBe(default);
        surface.Cell(corner).Text.ShouldBe(themed);
        published.ShouldBe(2);
    }

    /// <summary>Verifies every forwarded presentation setter is a silent no-op for its current
    /// value and publishes exactly once for a change, and the forwarded getters round-trip.</summary>
    [Fact]
    public void Presentation_WhenReassignedSameValue_PublishesNothing()
    {
        ItemTemplate template = static item => new ControlText((string) item!);
        var palette = new CommandPalette
        {
            Placeholder = "Type…",
            StartAffix = new Affix(">"),
            EndAffix = new Affix("<"),
            DropDownHeight = Length.Cells(4),
            RowHeight = Length.Cells(2),
            ItemTemplate = template
        };
        var published = new List<string?>();
        palette.PropertyChanged += (_, eventArgs) => published.Add(eventArgs.PropertyName);

        palette.Placeholder = "Type…";
        palette.StartAffix = new Affix(">");
        palette.EndAffix = new Affix("<");
        palette.DropDownHeight = Length.Cells(4);
        palette.RowHeight = Length.Cells(2);
        palette.ItemTemplate = template;
        palette.FieldBorder = palette.FieldBorder;
        palette.FieldShadow = palette.FieldShadow;

        published.ShouldBeEmpty();
        palette.ItemTemplate.ShouldBeSameAs(template);
        palette.RowHeight.ShouldBe(Length.Cells(2));

        ItemTemplate replacement = static item => new ControlText($"* {item}");
        palette.Placeholder = "Search";
        palette.StartAffix = null;
        palette.EndAffix = null;
        palette.DropDownHeight = Length.Percent(50);
        palette.ItemTemplate = replacement;

        published.ShouldBe([
            nameof(CommandPalette.Placeholder),
            nameof(CommandPalette.StartAffix),
            nameof(CommandPalette.EndAffix),
            nameof(CommandPalette.DropDownHeight),
            nameof(CommandPalette.ItemTemplate)
        ]);
        palette.ItemTemplate.ShouldBeSameAs(replacement);
    }

    /// <summary>Verifies ResetFieldShadow with no local editor style is a silent no-op.</summary>
    [Fact]
    public void ResetFieldShadow_WhenNeverAssigned_IsNoOp()
    {
        var palette = new CommandPalette();
        var editor = OwnedTree.Find<TextInput>(palette).ShouldNotBeNull();
        var published = 0;
        palette.PropertyChanged += (_, _) => published++;

        palette.ResetFieldShadow();

        editor.Style.ShouldBeNull();
        published.ShouldBe(0);
    }

    /// <summary>Verifies reassigning the same resolver instance neither publishes nor resolves
    /// again, while a different instance does both.</summary>
    [Fact]
    public void Resolver_WhenReassignedSameReference_DoesNotResolveAgain()
    {
        var queries = 0;

        ValueTask<IReadOnlyList<object?>> Resolve(string terms, CancellationToken token)
        {
            _ = terms;
            _ = token;
            queries++;
            return ValueTask.FromResult<IReadOnlyList<object?>>(["One"]);
        }

        CommandPaletteResolver resolver = Resolve;
        var palette = new CommandPalette { Resolver = resolver };
        queries.ShouldBe(1);
        var published = 0;
        palette.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(CommandPalette.Resolver))
            {
                published++;
            }
        };

        palette.Resolver = resolver;

        queries.ShouldBe(1);
        published.ShouldBe(0);

        palette.Resolver = (_, _) =>
        {
            queries++;
            return ValueTask.FromResult<IReadOnlyList<object?>>(["Two"]);
        };

        queries.ShouldBe(2);
        published.ShouldBe(1);
        palette.Items.ShouldBe(["Two"]);
    }

    /// <summary>Verifies assigning the current text does not start another resolution.</summary>
    [Fact]
    public void Text_WhenAssignedSameValue_DoesNotResolveAgain()
    {
        var queries = new List<string>();
        var palette = new CommandPalette
        {
            Resolver = (terms, _) =>
            {
                queries.Add(terms);
                return ValueTask.FromResult<IReadOnlyList<object?>>([terms]);
            },
            Text = "same"
        };
        queries.Clear();

        palette.Text = "same";

        queries.ShouldBeEmpty();
        palette.Items.ShouldBe(["same"]);
    }

    /// <summary>Verifies disposal clears every public event and rejects later mutation with
    /// ObjectDisposedException.</summary>
    [Fact]
    public void Dispose_WhenCalled_ClearsEventsAndRejectsMutation()
    {
        var palette = NewPalette(static _ => ["One"]);
        var raised = 0;
        palette.Opened += (_, _) => raised++;
        palette.Closed += (_, _) => raised++;
        palette.ResultsChanged += (_, _) => raised++;
        palette.ResolutionFailed += (_, _) => raised++;
        palette.ItemInvoked += (_, _) => raised++;
        palette.IsOpen = true;
        raised.ShouldBeGreaterThan(0);
        raised = 0;

        palette.Dispose();
        palette.Dispose();

        raised.ShouldBe(0);
        palette.IsDisposed.ShouldBeTrue();
        _ = Should.Throw<ObjectDisposedException>(() => palette.IsOpen = true);
        _ = Should.Throw<ObjectDisposedException>(palette.Refresh);
        _ = Should.Throw<ObjectDisposedException>(() => palette.Resolver = null);
    }

    #endregion

    private static CommandPalette NewPalette(Func<string, object?[]> resolve) => new()
    {
        Width = Length.Cells(18),
        Resolver = (terms, _) => ValueTask.FromResult<IReadOnlyList<object?>>(resolve(terms))
    };

    private static Task<ComponentSurface> MountAsync(ControlBase control, Size size) =>
        ComponentSurface.MountAsync(control, size, TestContext.Current.CancellationToken);

    private static void Observe(CommandPalette palette, List<string> events)
    {
        palette.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(CommandPalette.IsOpen))
            {
                events.Add("PropertyChanged:IsOpen");
            }
        };
        palette.ResultsChanged += (_, _) => events.Add("ResultsChanged");
        palette.Opened += (_, _) => events.Add("Opened");
        palette.Closed += (_, _) => events.Add("Closed");
        palette.ItemInvoked += (_, _) => events.Add("ItemInvoked");
    }
}
