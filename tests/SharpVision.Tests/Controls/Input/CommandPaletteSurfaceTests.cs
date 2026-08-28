// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves command-palette focus, keyboard selection, activation, and popup cells when mounted.</summary>
public sealed class CommandPaletteSurfaceTests
{
    /// <summary>Verifies Unicode text continues through the retained editor while an open popup
    /// delegates navigation to its result list.</summary>
    [Fact]
    public async Task Input_WhenUnicodeTextIsTyped_RetainsEditorFocusAndFiltersAsync()
    {
        // Arrange
        var queries = new List<string>();
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = (searchTerms, _) =>
            {
                queries.Add(searchTerms);
                return ValueTask.FromResult<IReadOnlyList<object?>>(
                    [searchTerms.Length == 0 ? "initial" : searchTerms]);
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            palette,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => palette.Open(), "open command palette results");
        var editor = OwnedTree.Find<TextInput>(palette).ShouldNotBeNull();
        var focusLosses = new List<FocusChangedEventArgs>();
        surface.Application.Focus.Lost += (_, eventArgs) => focusLosses.Add(eventArgs);
        surface.ShouldHaveFocus(editor);
        palette.IsOpen.ShouldBeTrue();
        palette.Items.ShouldBe(["initial"]);
        queries.Clear();

        // Act
        await surface.Keyboard.TypeAsync("café 🔎");
        await surface.UpdateAsync(static () => { }, "settle Unicode filtering");

        // Assert
        palette.Text.ShouldBe("café 🔎");
        queries.ShouldBe(["c", "ca", "caf", "cafe", "café", "café ", "café 🔎"]);
        palette.IsOpen.ShouldBeTrue();
        editor.IsFocused.ShouldBeTrue();
        focusLosses.ShouldBeEmpty();
        surface.ShouldHaveFocus(editor);
    }

    /// <summary>Verifies resolved results start with one unified selected and current row while
    /// keyboard focus remains in the editor.</summary>
    [Fact]
    public async Task Results_WhenOpened_SelectsFirstRowAsTheOnlyCurrentRowAsync()
    {
        // Arrange
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["One", "Two", "Three"])
        };
        await using var surface = await ComponentSurface.MountAsync(
            palette,
            new Size(24, 8),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => palette.Open(), "open command palette results");
        var editor = OwnedTree.Find<TextInput>(palette).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();

        // Assert
        surface.ShouldHaveFocus(editor);
        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(0);
    }

    /// <summary>Verifies reopening results resets selection and current state to the first row
    /// instead of retaining the row browsed before close.</summary>
    [Fact]
    public async Task Open_WhenReopened_ResetsSelectionAndCurrentRowAsync()
    {
        // Arrange
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["One", "Two", "Three"])
        };
        await using var surface = await ComponentSurface.MountAsync(
            palette,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => palette.Open(), "open command palette results");
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        await surface.Keyboard.PressAsync(Code.Down);
        list.SelectedIndex.ShouldBe(1);
        await surface.UpdateAsync(palette.Close, "close browsed command palette results");

        // Act
        await surface.UpdateAsync(() => palette.Open(), "reopen command palette results");

        // Assert
        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(0);
    }

    /// <summary>Verifies opening results chooses the first eligible row when the first template
    /// result is unavailable.</summary>
    [Fact]
    public async Task Results_WhenFirstRowIsUnavailable_SelectsFirstEligibleRowAsync()
    {
        // Arrange
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            ItemTemplate = item => new ControlText((string) item!)
            {
                IsEnabled = !Equals(item, "Unavailable")
            },
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(
                ["Unavailable", "Available", "Later"])
        };
        await using var surface = await ComponentSurface.MountAsync(
            palette,
            new Size(24, 8),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => palette.Open(), "open command palette with unavailable first result");
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();

        // Assert
        list.SelectedIndex.ShouldBe(1);
        list.ActiveIndex.ShouldBe(1);
    }

    /// <summary>Verifies keyboard navigation scrolls the result viewport minimally and keeps the
    /// selected row identical to the list's current row.</summary>
    [Fact]
    public async Task Navigation_WhenSelectionLeavesViewport_ScrollsAndKeepsRowStateUnifiedAsync()
    {
        // Arrange
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            DropDownHeight = 3,
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(
                Enumerable.Range(0, 10).Select(index => (object?) $"Item {index}").ToArray())
        };
        await using var surface = await ComponentSurface.MountAsync(
            palette,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => palette.Open(), "open overflowing command palette results");
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();

        // Act
        for (var press = 0; press < 5; press++)
        {
            await surface.Keyboard.PressAsync(Code.Down);
        }

        // Assert
        list.SelectedIndex.ShouldBe(5);
        list.ActiveIndex.ShouldBe(5);
        list.VerticalOffset.ShouldBe(3);
    }

    /// <summary>Verifies terminal key-repeat actions advance the current selection just like the
    /// initial directional press.</summary>
    [Fact]
    public async Task Navigation_WhenDownRepeats_ContinuesMovingSelectionAsync()
    {
        // Arrange
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["One", "Two", "Three"])
        };
        await using var surface = await ComponentSurface.MountAsync(
            palette,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => palette.Open(), "open repeatable command palette results");
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();

        // Act
        await surface.Keyboard.RepeatAsync(Code.Down);

        // Assert
        list.SelectedIndex.ShouldBe(1);
        list.ActiveIndex.ShouldBe(1);
    }

    /// <summary>Verifies every initial and repeated ListView navigation key sent to the retained
    /// editor is delegated exactly once without moving focus into the popup.</summary>
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
    public async Task Navigation_WhenEditorHasFocus_MovesSelectionExactlyOnceAsync(
        Code code,
        KeyAction action,
        int startingIndex,
        int expectedIndex)
    {
        // Arrange
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            DropDownHeight = 3,
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(
                Enumerable.Range(0, 10).Select(index => (object?) $"Item {index}").ToArray())
        };
        await using var surface = await ComponentSurface.MountAsync(
            palette,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => palette.Open(), "open command palette results");
        var editor = OwnedTree.Find<TextInput>(palette).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        surface.ShouldHaveFocus(editor);

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
        list.SelectedIndex.ShouldBe(expectedIndex);
        list.ActiveIndex.ShouldBe(expectedIndex);
    }

    /// <summary>Verifies Escape, direct close, and light dismissal all cancel browsing and restore
    /// the selection and current item captured when the session opened.</summary>
    [Theory]
    [InlineData("escape")]
    [InlineData("direct")]
    [InlineData("property")]
    [InlineData("light-dismiss")]
    public async Task Close_WhenBrowsingIsCancelled_RestoresOpeningListStateAsync(string closePath)
    {
        // Arrange
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["One", "Two", "Three"])
        };
        var background = new ControlText("outside");
        Overlay.SetTop(background, Length.Cells(7));
        Overlay.SetLeft(background, Length.Cells(20));
        var root = new Overlay { Children = { palette, background } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 9),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => palette.Open(), "open command palette results");
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        await surface.Keyboard.PressAsync(Code.Down);
        list.SelectedIndex.ShouldBe(1);

        // Act
        if (closePath == "escape")
        {
            await surface.Keyboard.PressAsync(Code.Escape);
        }
        else if (closePath == "light-dismiss")
        {
            await surface.Pointer.ClickAsync(background);
        }
        else if (closePath == "property")
        {
            await surface.UpdateAsync(() => palette.IsOpen = false, "close command palette by property");
        }
        else
        {
            await surface.UpdateAsync(palette.Close, "close command palette directly");
        }

        // Assert
        palette.IsOpen.ShouldBeFalse();
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldBe(-1);
    }

    /// <summary>Verifies owner unavailability cancels an open result-navigation session and
    /// restores the selection and current row captured before opening.</summary>
    [Fact]
    public async Task Availability_WhenOwnerBecomesUnavailable_RestoresOpeningListStateAsync()
    {
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["One", "Two", "Three"])
        };
        await using var surface = await ComponentSurface.MountAsync(
            palette,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => palette.Open(), "open command palette results");
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        await surface.Keyboard.PressAsync(Code.Down);
        list.SelectedIndex.ShouldBe(1);

        await surface.UpdateAsync(() => palette.IsEnabled = false, "make command palette unavailable");

        palette.IsOpen.ShouldBeFalse();
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldBe(-1);
        surface.Application.Modality.Active.ShouldBeNull();
    }

    /// <summary>Verifies cancellation restores a nonzero selection accepted by the prior session,
    /// not merely the empty initial state.</summary>
    [Fact]
    public async Task Close_WhenPriorInvocationWasAccepted_RestoresItsOpeningSelectionAsync()
    {
        // Arrange
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["Zero", "One", "Two"])
        };
        await using var surface = await ComponentSurface.MountAsync(
            palette,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => palette.Open(), "open command palette results");
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Enter);
        palette.IsOpen.ShouldBeFalse();
        list.SelectedIndex.ShouldBe(1);
        await surface.UpdateAsync(() => palette.Open(), "reopen command palette results");
        list.SelectedIndex.ShouldBe(0);

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        palette.IsOpen.ShouldBeFalse();
        list.SelectedIndex.ShouldBe(1);
        list.ActiveIndex.ShouldBe(1);
    }

    /// <summary>Verifies refreshed results that invalidate the opening indexes cancel to a stable
    /// unselected state instead of throwing from rollback.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Close_WhenRefreshedResultsInvalidateOpeningIndexes_CancelsWithoutThrowingAsync(
        int filteredResultCount)
    {
        // Arrange
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = (searchTerms, _) => ValueTask.FromResult<IReadOnlyList<object?>>(
                searchTerms.Length == 0
                    ? ["Zero", "One", "Two"]
                    : Enumerable.Range(0, filteredResultCount).Select(index => (object?) $"Filtered {index}").ToArray())
        };
        await using var surface = await ComponentSurface.MountAsync(
            palette,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => palette.Open(), "open command palette results");
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Enter);
        list.SelectedIndex.ShouldBe(1);
        await surface.UpdateAsync(() => palette.Open(), "reopen accepted command palette results");

        // Act
        await surface.Application.Dispatcher.InvokeAsync(
            () =>
            {
                palette.Text = "filter";
            },
            TestContext.Current.CancellationToken);
        await surface.Application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        if (filteredResultCount > 0)
        {
            await surface.Application.Dispatcher.InvokeAsync(
                palette.Close,
                TestContext.Current.CancellationToken);
            await surface.Application.Dispatcher.InvokeAsync(
                static () => { },
                TestContext.Current.CancellationToken);
        }

        // Assert
        palette.IsOpen.ShouldBeFalse();
        palette.Items.Count.ShouldBe(filteredResultCount);
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldBe(-1);
    }

    /// <summary>Verifies a first-result selection deferred while detached is reconciled after
    /// attachment so immediate Enter accepts the refreshed first item.</summary>
    [Fact]
    public async Task Refresh_WhenOpenAndDetached_SelectsFirstResultAfterAttachmentAsync()
    {
        // Arrange
        ItemInvokedEventArgs? invoked = null;
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = (searchTerms, _) => ValueTask.FromResult<IReadOnlyList<object?>>(
                searchTerms.Length == 0 ? ["Initial"] : ["Fresh first", "Fresh second"])
        };
        palette.ItemInvoked += (_, eventArgs) => invoked = eventArgs;
        palette.IsOpen = true;
        palette.Text = "fresh";
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        list.SelectedIndex.ShouldBe(-1);

        // Act
        var screen = new HostedControlScreen(palette);
        await using var surface = await ComponentSurface.MountScreenAsync(
            screen,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        var actual = invoked.ShouldNotBeNull();
        actual.Index.ShouldBe(0);
        actual.Item.ShouldBe("Fresh first");
        actual.Cause.ShouldBe(ActivationCause.Keyboard);
        palette.IsOpen.ShouldBeFalse();
        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(0);
    }

    /// <summary>Verifies an activation from an ended session cannot invoke a command or close a
    /// newer session reopened at the same item index.</summary>
    [Theory]
    [InlineData("keyboard")]
    [InlineData("pointer")]
    public async Task Invocation_WhenActivationReopensSameIndex_IgnoresStaleSessionAsync(string activationPath)
    {
        // Arrange
        var invoked = new List<ItemInvokedEventArgs>();
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["One", "Two"])
        };
        palette.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs);
        await using var surface = await ComponentSurface.MountAsync(
            palette,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => palette.Open(), "open command palette results");
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();
        var reentered = false;
        list.ItemActivationStarting += (_, _) =>
        {
            if (reentered)
            {
                return;
            }

            reentered = true;
            palette.Close();
            _ = palette.Open();
        };

        // Act
        if (activationPath == "pointer")
        {
            await surface.Pointer.ClickAsync(list, new Point(0, 0));
        }
        else
        {
            await surface.Keyboard.PressAsync(Code.Enter);
        }

        // Assert
        reentered.ShouldBeTrue();
        invoked.ShouldBeEmpty();
        palette.IsOpen.ShouldBeTrue();
        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(0);
    }

    /// <summary>Verifies opening an embedded palette focuses its editor and Enter invokes the keyboard-selected result.</summary>
    [Fact]
    public async Task Open_WhenMounted_FocusesEditorAndInvokesSelectedResultAsync()
    {
        // Arrange
        ItemInvokedEventArgs? invoked = null;
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["Open file", "Open folder"])
        };
        palette.ItemInvoked += (_, eventArgs) => invoked = eventArgs;
        var root = new Overlay { Children = { palette } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(() => palette.Open(), "open and focus command palette");
        var editor = OwnedTree.Find<TextInput>(palette).ShouldNotBeNull();
        surface.ShouldHaveFocus(editor);
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        var actual = invoked.ShouldNotBeNull();
        actual.Index.ShouldBe(0);
        actual.Item.ShouldBe("Open file");
        actual.Cause.ShouldBe(ActivationCause.Keyboard);
        palette.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies resolved rows render through the owned Popup and remain pointer-activatable.</summary>
    [Fact]
    public async Task Results_WhenResolved_RenderInPopupAndSupportPointerInvocationAsync()
    {
        // Arrange
        ItemInvokedEventArgs? invoked = null;
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Text = "open",
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["Open file", "Open folder"])
        };
        palette.ItemInvoked += (_, eventArgs) => invoked = eventArgs;
        var root = new Overlay { Children = { palette } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => palette.Open(), "open resolved command palette results");
        var list = OwnedTree.Find<UiListView>(palette).ShouldNotBeNull();

        // Assert rendered result
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y)).Text.ShouldBe("O");

        // Act
        await surface.Pointer.ClickAsync(list, new Point(1, 1));

        // Assert invoked result
        var actual = invoked.ShouldNotBeNull();
        actual.Index.ShouldBe(1);
        actual.Item.ShouldBe("Open folder");
        actual.Cause.ShouldBe(ActivationCause.Pointer);
        palette.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies an unavailable palette cannot open a modal result surface or acquire focus.</summary>
    [Fact]
    public async Task Open_WhenDisabled_DoesNotFocusOrOpenResultsAsync()
    {
        // Arrange
        var palette = new CommandPalette
        {
            IsEnabled = false,
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["Open file"])
        };
        await using var surface = await ComponentSurface.MountAsync(
            palette,
            new Size(18, 5),
            TestContext.Current.CancellationToken);

        // Act
        var focused = false;
        await surface.UpdateAsync(() => focused = palette.Open(), "try to open a disabled command palette");

        // Assert
        focused.ShouldBeFalse();
        palette.IsOpen.ShouldBeFalse();
        surface.Application.Modality.Active.ShouldBeNull();
    }
}
