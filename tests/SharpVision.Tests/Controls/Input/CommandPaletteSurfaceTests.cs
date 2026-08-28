// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves command-palette focus, keyboard selection, activation, and popup cells when mounted.</summary>
public sealed class CommandPaletteSurfaceTests
{
    /// <summary>Verifies the owner-managed results popup does not close an unrelated
    /// owner-managed popup elsewhere in the same tree.</summary>
    [Fact]
    public async Task Open_WhenCalled_DoesNotCloseUnrelatedOwnerManagedPopupAsync()
    {
        // Arrange
        var pinned = new Popup
        {
            Content = new ControlText("Pinned"),
            ModalBehavior = PopupModalBehavior.None,
            FocusOnOpen = false
        };
        var palette = new CommandPalette
        {
            Resolver = static (_, _) => ValueTask.FromResult<IReadOnlyList<object?>>(["One", "Two"])
        };
        var root = new Overlay { Children = { pinned, palette } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(30, 12),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => pinned.IsOpen = true, "open unrelated popup");

        // Act
        await surface.UpdateAsync(() => palette.Open(), "open command palette results");

        // Assert
        palette.IsOpen.ShouldBeTrue();
        pinned.IsOpen.ShouldBeTrue();
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

    /// <summary>Verifies Enter cannot activate the previous query's selected row while a newer
    /// asynchronous resolution is still pending.</summary>
    [Fact]
    public async Task Enter_WhenNewerResolutionIsPending_DoesNotInvokeStaleResultAsync()
    {
        // Arrange
        var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ItemInvokedEventArgs? invoked = null;
        var palette = new CommandPalette
        {
            Width = Length.Cells(18),
            Resolver = (searchTerms, _) => searchTerms == "o"
                ? ValueTask.FromResult<IReadOnlyList<object?>>(["Open file"])
                : new ValueTask<IReadOnlyList<object?>>(completion.Task)
        };
        palette.ItemInvoked += (_, eventArgs) => invoked = eventArgs;
        await using var surface = await ComponentSurface.MountAsync(
            palette,
            new Size(24, 8),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => palette.Text = "o", "resolve current results");
        await surface.UpdateAsync(() => palette.Text = "op", "start newer resolution");

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        invoked.ShouldBeNull();
        palette.IsResolving.ShouldBeTrue();

        // Cleanup
        completion.SetResult([]);
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
