// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies command-palette resolution, forwarding, validation, and stale-result handling.</summary>
public sealed class CommandPaletteTests
{
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
