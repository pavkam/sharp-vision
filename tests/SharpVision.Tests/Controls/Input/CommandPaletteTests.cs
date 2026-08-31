// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies command-palette resolution, forwarding, validation, and stale-result handling.</summary>
public sealed class CommandPaletteTests
{
    /// <summary>Verifies command-modified list navigation bubbles without changing provisional state.</summary>
    [Fact]
    public void Navigation_WhenCommandModified_LeavesCurrentSelectionUnchangedAndUnhandled()
    {
        var palette = new CommandPalette
        {
            IsOpen = true,
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["First", "Second"])
        };
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        var editor = OwnedTree.Find<TextInput>(palette).ShouldNotBeNull();
        list.SelectedIndex = 0;

        var routed = Router.Route(editor, Events.Key, Key(Code.Down, KeyAction.Press, Modifiers.Control));

        routed.IsHandled.ShouldBeFalse();
        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(0);
    }

    /// <summary>Verifies every initial and repeated ListView navigation key is delegated through
    /// the canonical selection transaction while the retained editor owns the route.</summary>
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
    public void Navigation_WhenPopupIsOpen_DelegatesEveryListKey(
        Code code,
        KeyAction action,
        int startingIndex,
        int expectedIndex)
    {
        // Arrange
        var palette = new CommandPalette
        {
            DropDownHeight = Length.Cells(3),
            IsOpen = true,
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(
                Enumerable.Range(0, 10).Select(index => (object?) $"Item {index}").ToArray())
        };
        new LayoutEngine().Layout(palette, new Size(20, 8));
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        var editor = OwnedTree.Find<TextInput>(palette).ShouldNotBeNull();
        list.SelectedIndex = startingIndex;

        // Act
        var routed = Router.Route(editor, Events.Key, Key(code, action));

        // Assert
        routed.IsHandled.ShouldBeTrue();
        list.SelectedIndex.ShouldBe(expectedIndex);
        list.ActiveIndex.ShouldBe(expectedIndex);
    }

    /// <summary>Verifies a non-cooperative completion from an attachment that ended cannot mutate
    /// detached state or publish callbacks from its continuation thread.</summary>
    [Fact]
    public async Task Resolver_WhenDetachedBeforeIgnoredCancellationCompletes_DiscardsCompletionAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>();
        var palette = new CommandPalette
        {
            Resolver = (searchTerms, _) => searchTerms == "late"
                ? new ValueTask<IReadOnlyList<object?>>(completion.Task)
                : ValueTask.FromResult<IReadOnlyList<object?>>(["initial"])
        };
        await using var dispatcher = Dispatcher.Start();
        var callbacks = new List<int>();
        palette.ResultsChanged += (_, _) => callbacks.Add(Environment.CurrentManagedThreadId);
        await dispatcher.InvokeAsync(() =>
        {
            palette.Attach(dispatcher);
            palette.Text = "late";
            palette.Detach();
            callbacks.Clear();
        }, TestContext.Current.CancellationToken);

        // Act
        await Task.Run(() => completion.SetResult(["stale"]), TestContext.Current.CancellationToken);

        // Assert
        palette.Items.ShouldBe(["initial"]);
        palette.IsResolving.ShouldBeFalse();
        callbacks.ShouldBeEmpty();
    }

    /// <summary>Verifies a resolution that began while detached still commits its result once the
    /// palette attaches before the pending resolver completes, instead of silently discarding it.</summary>
    [Fact]
    public async Task Resolver_WhenAttachedAfterResolutionStartedDetached_CommitsCompletionAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var palette = new CommandPalette
        {
            Resolver = (searchTerms, _) => searchTerms == "late"
                ? new ValueTask<IReadOnlyList<object?>>(completion.Task)
                : ValueTask.FromResult<IReadOnlyList<object?>>(["initial"])
        };
        palette.Text = "late";
        palette.IsResolving.ShouldBeTrue();
        var resultsChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        palette.ResultsChanged += (_, _) => resultsChanged.TrySetResult();
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() => palette.Attach(dispatcher), TestContext.Current.CancellationToken);

        // Act
        await Task.Run(() => completion.SetResult(["resolved"]), TestContext.Current.CancellationToken);
        await resultsChanged.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert
        palette.Items.ShouldBe(["resolved"]);
        palette.IsResolving.ShouldBeFalse();
    }

    /// <summary>Verifies transient local availability changes do not cancel the live request for
    /// the palette's still-current text.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resolver_WhenTemporarilyUnavailableMidResolution_StillCommitsAsync(bool hide)
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var palette = new CommandPalette
        {
            Resolver = (searchTerms, _) => searchTerms == "conf"
                ? new ValueTask<IReadOnlyList<object?>>(completion.Task)
                : ValueTask.FromResult<IReadOnlyList<object?>>(["initial"])
        };
        palette.ResultsChanged += (_, _) =>
        {
            if (palette.Items.Count == 1 && Equals(palette.Items[0], "Confirm"))
            {
                _ = committed.TrySetResult();
            }
        };
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            palette.Attach(dispatcher);
            palette.Text = "conf";

            if (hide)
            {
                palette.Visibility = Visibility.Hidden;
                palette.Visibility = Visibility.Visible;
            }
            else
            {
                palette.IsEnabled = false;
                palette.IsEnabled = true;
            }
        }, TestContext.Current.CancellationToken);

        // Act
        completion.SetResult(["Confirm"]);
        await committed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert
        palette.Items.ShouldHaveSingleItem().ShouldBe("Confirm");
        palette.IsResolving.ShouldBeFalse();
    }

    /// <summary>Verifies a success callback that starts an empty current query prevents the stale
    /// outer success from reopening the popup.</summary>
    [Fact]
    public void ResultsChanged_WhenHandlerStartsEmptyResolution_KeepsNewerPopupState()
    {
        // Arrange
        var palette = new CommandPalette
        {
            Resolver = (searchTerms, _) => ValueTask.FromResult<IReadOnlyList<object?>>(
                searchTerms == "first" ? ["old"] : [])
        };
        palette.ResultsChanged += (_, _) =>
        {
            if (palette.Text == "first")
            {
                palette.Text = "second";
            }
        };

        // Act
        palette.Text = "first";

        // Assert
        palette.Text.ShouldBe("second");
        palette.Items.ShouldBeEmpty();
        palette.IsOpen.ShouldBeFalse();
        palette.IsResolving.ShouldBeFalse();
    }

    /// <summary>Verifies a failure callback that starts a successful current query prevents stale
    /// close and failure publication.</summary>
    [Fact]
    public void ResultsChanged_WhenFailureHandlerStartsSuccessfulResolution_KeepsNewerSuccess()
    {
        // Arrange
        var failures = new List<CommandPaletteResolutionFailedEventArgs>();
        var palette = new CommandPalette
        {
            Resolver = (searchTerms, _) => searchTerms == "first"
                ? throw new InvalidOperationException("old failure")
                : ValueTask.FromResult<IReadOnlyList<object?>>(["new"])
        };
        palette.ResultsChanged += (_, _) =>
        {
            if (palette.Text == "first")
            {
                palette.Text = "second";
            }
        };
        palette.ResolutionFailed += (_, eventArgs) => failures.Add(eventArgs);

        // Act
        palette.Text = "first";

        // Assert
        palette.Text.ShouldBe("second");
        palette.Items.ShouldBe(["new"]);
        palette.IsOpen.ShouldBeTrue();
        failures.ShouldBeEmpty();
    }

    /// <summary>Verifies disposal during result publication ends the stale completion without
    /// touching retained popup state.</summary>
    [Fact]
    public void ResultsChanged_WhenHandlerDisposesPalette_StopsCompletion()
    {
        // Arrange
        var palette = new CommandPalette
        {
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["result"])
        };
        palette.ResultsChanged += (_, _) => palette.Dispose();

        // Act and assert
        Should.NotThrow(palette.Refresh);
        palette.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies assigning the current row-height request is an observable no-op.</summary>
    [Fact]
    public void RowHeight_WhenAssignedSameValue_DoesNotPublishPropertyChange()
    {
        // Arrange
        var palette = new CommandPalette
        {
            RowHeight = Length.Percent(25)
        };
        var changes = 0;
        palette.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(CommandPalette.RowHeight))
            {
                changes++;
            }
        };

        // Act
        palette.RowHeight = Length.Percent(25);

        // Assert
        changes.ShouldBe(0);
    }

    /// <summary>Verifies a throwing Text observer cannot prevent the committed search from being
    /// admitted to the resolver.</summary>
    [Fact]
    public void Text_WhenPropertyObserverThrows_StillResolvesCommittedTextBeforeRethrowing()
    {
        // Arrange
        var queries = new List<string>();
        var palette = new CommandPalette
        {
            Resolver = (searchTerms, _) =>
            {
                queries.Add(searchTerms);
                return ValueTask.FromResult<IReadOnlyList<object?>>([searchTerms]);
            }
        };
        queries.Clear();
        palette.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(CommandPalette.Text))
            {
                throw new InvalidOperationException("observer");
            }
        };

        // Act and assert
        _ = Should.Throw<InvalidOperationException>(() => palette.Text = "query");
        queries.ShouldBe(["query"]);
        palette.Items.ShouldBe(["query"]);
        palette.IsResolving.ShouldBeFalse();
    }

    /// <summary>Verifies a throwing busy-state observer cannot strand the request before the
    /// resolver is invoked and synchronously completed.</summary>
    [Fact]
    public void Resolver_WhenResolvingObserverThrows_CompletesRequestBeforeRethrowing()
    {
        // Arrange
        var calls = 0;
        var palette = new CommandPalette();
        palette.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(CommandPalette.IsResolving) && palette.IsResolving)
            {
                throw new InvalidOperationException("observer");
            }
        };

        // Act and assert
        _ = Should.Throw<InvalidOperationException>(() => palette.Resolver = (_, _) =>
        {
            calls++;
            return ValueTask.FromResult<IReadOnlyList<object?>>(["result"]);
        });
        calls.ShouldBe(1);
        palette.Items.ShouldBe(["result"]);
        palette.IsResolving.ShouldBeFalse();
    }

    /// <summary>Verifies freely assigned text resolves immediately and publishes the resulting items.</summary>
    [Fact]
    public void Text_WhenChanged_ResolvesItemsAndOpensTheDropDown()
    {
        // Arrange
        var queries = new List<string>();
        CommandPalette palette = new()
        {
            Resolver = (searchTerms, _) =>
            {
                queries.Add(searchTerms);
                return ValueTask.FromResult<IReadOnlyList<object?>>(["Open file", "Open folder"]);
            }
        };
        queries.Clear();

        // Act
        palette.Text = "open";

        // Assert
        queries.ShouldBe(["open"]);
        palette.Items.ShouldBe(["Open file", "Open folder"]);
        palette.IsResolving.ShouldBeFalse();
        palette.IsOpen.ShouldBeTrue();
    }

    /// <summary>Verifies a later query cancels and supersedes an older asynchronous completion.</summary>
    [Fact]
    public async Task Text_WhenEarlierResolutionCompletesLast_DiscardsTheStaleItemsAsync()
    {
        // Arrange
        var first = new TaskCompletionSource<IReadOnlyList<object?>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<IReadOnlyList<object?>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resultsChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var palette = new CommandPalette
        {
            Resolver = (searchTerms, _) => new ValueTask<IReadOnlyList<object?>>(
                searchTerms == "first" ? first.Task : second.Task)
        };
        palette.ResultsChanged += (_, _) =>
        {
            if (palette.Items.Count == 1 && Equals(palette.Items[0], "second result"))
            {
                _ = resultsChanged.TrySetResult();
            }
        };

        // Act
        palette.Text = "first";
        palette.Text = "second";
        second.SetResult(["second result"]);
        await resultsChanged.Task.WaitAsync(TestContext.Current.CancellationToken);
        first.SetResult(["stale result"]);
        await Task.Yield();

        // Assert
        palette.Items.ShouldBe(["second result"]);
    }

    /// <summary>Verifies text-field presentation properties are forwarded without leaking the retained editor.</summary>
    [Fact]
    public void Presentation_WhenCustomized_ForwardsPlaceholderAffixesAndFieldBorder()
    {
        // Arrange
        var border = new Border(
            BorderSide.None,
            BorderGlyphStyle.Ascii,
            Color.Default,
            Color.Transparent,
            TerminalAttributes.None);
        var palette = new CommandPalette
        {
            Placeholder = "Search commands",
            StartAffix = new Affix(">"),
            EndAffix = new Affix("/"),
            FieldBorder = border
        };

        // Act

        // Assert
        palette.Placeholder.ShouldBe("Search commands");
        palette.StartAffix.ShouldBe(new Affix(">"));
        palette.EndAffix.ShouldBe(new Affix("/"));
        palette.FieldBorder.ShouldBe(border);
    }

    /// <summary>Verifies resetting one field facet returns only that facet to the theme-owned
    /// appearance and leaves a separately assigned facet untouched.</summary>
    [Fact]
    public void ResetFieldBorder_WhenFieldShadowIsAlsoSet_RevertsBorderAndPreservesShadow()
    {
        // Arrange
        var border = new Border(
            BorderSide.None,
            BorderGlyphStyle.Ascii,
            Color.Default,
            Color.Transparent,
            TerminalAttributes.None);
        var shadow = new Shadow(
            isVisible: true,
            ShadowMode.Composite,
            new Point(1, 1),
            new Rune('#'),
            Color.Rgb(1, 2, 3),
            Color.Transparent,
            TerminalAttributes.None);
        var palette = new CommandPalette
        {
            FieldBorder = border,
            FieldShadow = shadow
        };

        // Act
        palette.ResetFieldBorder();

        // Assert
        palette.FieldBorder.ShouldNotBe(border);
        palette.FieldShadow.ShouldBe(shadow);
    }

    /// <summary>Verifies resetting one field facet, mirrored: shadow reverts and a separately
    /// assigned border survives.</summary>
    [Fact]
    public void ResetFieldShadow_WhenFieldBorderIsAlsoSet_RevertsShadowAndPreservesBorder()
    {
        // Arrange
        var border = new Border(
            BorderSide.None,
            BorderGlyphStyle.Ascii,
            Color.Default,
            Color.Transparent,
            TerminalAttributes.None);
        var shadow = new Shadow(
            isVisible: true,
            ShadowMode.Composite,
            new Point(1, 1),
            new Rune('#'),
            Color.Rgb(1, 2, 3),
            Color.Transparent,
            TerminalAttributes.None);
        var palette = new CommandPalette
        {
            FieldBorder = border,
            FieldShadow = shadow
        };

        // Act
        palette.ResetFieldShadow();

        // Assert
        palette.FieldShadow.ShouldNotBe(shadow);
        palette.FieldBorder.ShouldBe(border);
    }

    /// <summary>Verifies resetting both field facets in turn fully collapses the local style back
    /// to theme ownership, so it stays live across a subsequent theme swap.</summary>
    [Fact]
    public void ResetFieldBorderAndShadow_WhenBothWereSet_CollapsesToTheme()
    {
        // Arrange
        var border = new Border(
            BorderSide.None,
            BorderGlyphStyle.Ascii,
            Color.Default,
            Color.Transparent,
            TerminalAttributes.None);
        var shadow = new Shadow(
            isVisible: true,
            ShadowMode.Composite,
            new Point(1, 1),
            new Rune('#'),
            Color.Rgb(1, 2, 3),
            Color.Transparent,
            TerminalAttributes.None);
        var palette = new CommandPalette
        {
            FieldBorder = border,
            FieldShadow = shadow
        };

        // Act
        palette.ResetFieldBorder();
        palette.ResetFieldShadow();

        // Assert
        palette.FieldBorder.ShouldNotBe(border);
        palette.FieldShadow.ShouldNotBe(shadow);
    }

    /// <summary>Verifies resetting a facet that was never assigned is a no-op.</summary>
    [Fact]
    public void ResetFieldBorder_WhenNeverAssigned_IsNoOp()
    {
        // Arrange
        var palette = new CommandPalette();
        var before = palette.FieldBorder;

        // Act
        palette.ResetFieldBorder();

        // Assert
        palette.FieldBorder.ShouldBe(before);
    }

    /// <summary>Verifies invalid drop-down sizing is rejected before the prior value changes.</summary>
    [Fact]
    public void DropDownHeight_WhenNonPositive_ThrowsBeforeMutation()
    {
        // Arrange
        var palette = new CommandPalette { DropDownHeight = Length.Percent(50) };

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => palette.DropDownHeight = Length.Star(1));
        palette.DropDownHeight.ShouldBe(Length.Percent(50));
    }

    /// <summary>Verifies a resolver contract violation is observable without publishing invalid results.</summary>
    [Fact]
    public void Resolver_WhenItReturnsNull_RaisesResolutionFailedAndClearsResults()
    {
        // Arrange
        CommandPaletteResolutionFailedEventArgs? failure = null;
        var palette = new CommandPalette();
        palette.ResolutionFailed += (_, eventArgs) => failure = eventArgs;

        // Act
        palette.Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(null!);

        // Assert
        var actual = failure.ShouldNotBeNull();
        actual.SearchTerms.ShouldBeEmpty();
        _ = actual.Exception.ShouldBeOfType<InvalidOperationException>();
        palette.Items.ShouldBeEmpty();
        palette.IsResolving.ShouldBeFalse();
        palette.IsOpen.ShouldBeFalse();
    }

    private static KeyEventArgs Key(
        Code code,
        KeyAction action,
        Modifiers modifiers = Modifiers.None) => new(new Stroke(
        code,
        default,
        nativeCode: 0,
        modifiers,
        action));
}
