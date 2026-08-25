// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves command-palette focus, keyboard selection, activation, and popup cells when mounted.</summary>
public sealed class CommandPaletteSurfaceTests
{
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
        await surface.Keyboard.PressAsync(Code.Down);
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
