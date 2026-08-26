// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies command-palette resolution, forwarding, validation, and stale-result handling.</summary>
public sealed class CommandPaletteTests
{
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

    /// <summary>Verifies invalid drop-down sizing is rejected before the prior value changes.</summary>
    [Fact]
    public void DropDownHeight_WhenNonPositive_ThrowsBeforeMutation()
    {
        // Arrange
        var palette = new CommandPalette { DropDownHeight = 5 };

        // Act and assert
        _ = Should.Throw<ArgumentOutOfRangeException>(() => palette.DropDownHeight = 0);
        palette.DropDownHeight.ShouldBe(5);
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
}
