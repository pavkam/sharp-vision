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
        queries.Clear();

        // Act
        await surface.Application.Dispatcher.InvokeAsync(
            () =>
            {
                foreach (var rune in "café 🔎".EnumerateRunes())
                {
                    _ = Router.Route(
                        editor,
                        Events.Text,
                        new TextEventArgs(new TerminalText(rune)));
                }
            },
            TestContext.Current.CancellationToken);
        await surface.Application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        // Assert
        palette.Text.ShouldBe("café 🔎");
        queries.ShouldBe(["c", "ca", "caf", "cafe", "café", "café ", "café 🔎"]);
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
        await surface.SendAsync("\u001b[1;1:2B"u8.ToArray(), "repeat Down");

        // Assert
        list.SelectedIndex.ShouldBe(1);
        list.ActiveIndex.ShouldBe(1);
    }

    /// <summary>Verifies popup-focused input is intercepted by the owner once, so the ListView's
    /// own bubble handler cannot apply the same navigation stroke a second time.</summary>
    [Fact]
    public async Task Navigation_WhenPopupListHasFocus_MovesSelectionExactlyOnceAsync()
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
        await surface.UpdateAsync(
            () => surface.Application.Focus.Focus(list).ShouldBeTrue(),
            "focus command palette result list");

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        list.SelectedIndex.ShouldBe(1);
        list.ActiveIndex.ShouldBe(1);
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
