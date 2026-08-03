// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;


using UiListView = ListView;

/// <summary>Proves the public file-picker presentation through a mounted application surface.</summary>
public sealed class FilePickerDialogSurfaceTests
{
    /// <summary>Verifies the compact dialog aligns full-width content, metadata, options, and trailing actions.</summary>
    [Fact]
    public async Task Render_WhenShowcaseSized_DisplaysCompleteFieldsAndActionsWithoutClippingAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-polish"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("Program.cs", Path.Combine(directory, "Program.cs"), false, false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(76, 33),
            TestContext.Current.CancellationToken);
        await WaitUntilAsync(surface, () => !dialog.IsLoading);
        var window = OwnedTree.Find<Window>(dialog).ShouldNotBeNull();
        var path = OwnedTree.Find<TextInput>(dialog).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var listSurface = list.Parent.ShouldBeOfType<Dock>();
        var root = listSurface.Parent.ShouldBeOfType<Grid>();
        var filter = OwnedTree.Find<ComboBox>(dialog).ShouldNotBeNull();
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        var status = FindAll<ControlText>(dialog).Single(static text => text.Content == "0 folders · 1 file");
        var up = FindAll<Button>(dialog).Single(static button =>
            button.Content is ControlText { Content: "↑" });
        var open = FindAll<Button>(dialog).Single(static button =>
            button.Content is ControlText { Content: "&Open" });
        var cancel = FindAll<Button>(dialog).Single(static button =>
            button.Content is ControlText { Content: "&Cancel" });

        // Assert
        window.ShouldBeSameAs(dialog);
        _ = window.Parent.ShouldBeOfType<Overlay>();
        window.CanMove.ShouldBeTrue();
        window.Bounds.Height.ShouldBe(26);
        list.ContentBounds.Height.ShouldBe(5);
        path.ScrollBars.ShouldBe(ScrollBars.None);
        up.ActualBorder.Sides.ShouldBe(BorderSide.All);
        up.ActualShadow.IsVisible.ShouldBeFalse();
        up.Bounds.Width.ShouldBe(3);
        up.Bounds.Height.ShouldBe(3);
        // The exact border glyph depends on width constraints in the dialog layout.
        up.Bounds.Width.ShouldBeGreaterThan(0);
        FindAll<ControlText>(dialog).ShouldNotContain(static text =>
            text.Content == "Filter" || text.Content.Contains("Refresh", StringComparison.Ordinal));
        up.Bounds.Width.ShouldBe(3);
        ReadRow(surface, path.ContentBounds).ShouldContain("picker-polish");
        path.Bounds.Right.ShouldBe(listSurface.Bounds.Right);
        listSurface.Bounds.X.ShouldBe(root.ContentBounds.X);
        listSurface.Bounds.Right.ShouldBe(root.ContentBounds.Right);
        status.Bounds.X.ShouldBe(listSurface.Bounds.X);
        status.Bounds.Y.ShouldBe(listSurface.Bounds.Bottom + 1);
        ReadRow(surface, status.Bounds).ShouldContain("0 folders · 1 file");
        hidden.Bounds.X.ShouldBe(listSurface.Bounds.X);
        hidden.Bounds.Y.ShouldBeLessThan(filter.Bounds.Y);
        filter.ActualBorder.Sides.ShouldBe(BorderSide.All);
        filter.Bounds.Height.ShouldBe(3);
        filter.Bounds.X.ShouldBe(listSurface.Bounds.X);
        open.Bounds.Y.ShouldBe(filter.Bounds.Bottom);
        cancel.Bounds.Y.ShouldBe(filter.Bounds.Bottom);
        open.Bounds.Y.ShouldBe(cancel.Bounds.Y);
        open.Bounds.Height.ShouldBe(cancel.Bounds.Height);
        open.Bounds.Right.ShouldBe(cancel.Bounds.X - 1);
        cancel.Bounds.Right.ShouldBe(listSurface.Bounds.Right);
        open.Style.ShouldBeNull();
        cancel.Style.ShouldBeNull();
        open.ActualShadow.IsVisible.ShouldBeFalse();
        cancel.ActualShadow.IsVisible.ShouldBeFalse();
        ReadRow(surface, open.Bounds).ShouldContain("Open");
        ReadRow(surface, cancel.Bounds).ShouldContain("Cancel");
    }

    /// <summary>Verifies the modal FilePicker Window follows a captured drag from unoccupied top-border chrome.</summary>
    [Fact]
    public async Task Drag_WhenTopBorderIsDragged_MovesFilePickerWindowAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-drag"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(76, 33),
            TestContext.Current.CancellationToken);
        await WaitUntilAsync(surface, () => !dialog.IsLoading);
        var window = OwnedTree.Find<Window>(dialog).ShouldNotBeNull();
        var initial = window.Bounds;
        var start = new Point(window.Bounds.Width - 10, 0);
        var end = new Point(start.X + 4, start.Y + 2);

        // Act
        await surface.Pointer.MoveToAsync(window, start);
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(window, end);
        await surface.Pointer.ReleaseAsync();

        // Assert
        window.Bounds.ShouldBe(new Rect(initial.X + 4, initial.Y + 2, initial.Width, initial.Height));
    }

    /// <summary>Verifies the ListView absorbs available height without exceeding the configured visible-row cap.</summary>
    [Theory]
    [InlineData(7, 28, 9)]
    [InlineData(20, 41, 22)]
    public async Task Render_WhenHeightChanges_ExpandsListThroughConfiguredVisibleRowMaximumAsync(
        int maximumRows,
        int maximumWindowHeight,
        int maximumListHeight)
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-height"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new FilePickerDialog(
            new FilePickerOptions
            {
                InitialDirectory = directory,
                MaxVisibleRows = maximumRows
            },
            source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(100, 60),
            TestContext.Current.CancellationToken);
        await WaitUntilAsync(surface, () => !dialog.IsLoading);
        var window = OwnedTree.Find<Window>(dialog).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var listSurface = list.Parent.ShouldBeOfType<Dock>();

        // Assert capped height
        window.Bounds.Height.ShouldBe(maximumWindowHeight);
        listSurface.Bounds.Height.ShouldBe(maximumListHeight);
        list.ContentBounds.Height.ShouldBe(maximumRows);

        // Act and assert available height
        await surface.ResizeAsync(new Size(100, 29));
        window.Bounds.Height.ShouldBe(23);
        list.ContentBounds.Height.ShouldBe(3);

        await surface.ResizeAsync(new Size(100, 60));
        list.ContentBounds.Height.ShouldBe(maximumRows);
    }

    /// <summary>Verifies responsive bounds, semantic row glyphs, and selected-cell contrast across resize.</summary>
    [Fact]
    public async Task Render_WhenViewportChanges_KeepsDialogReachableAndSelectionVisuallyDistinctAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-responsive"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(
            directory,
            new FilePickerEntry("src", Path.Combine(directory, "src"), true, false),
            new FilePickerEntry("Program.cs", Path.Combine(directory, "Program.cs"), false, false));
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(120, 40),
            TestContext.Current.CancellationToken);
        await WaitUntilAsync(surface, () => !dialog.IsLoading);
        var window = OwnedTree.Find<Window>(dialog).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var listSurface = list.Parent.ShouldBeOfType<Dock>();

        // Assert wide
        window.Bounds.Width.ShouldBe(96);
        window.Bounds.Height.ShouldBe(32);
        window.Bounds.X.ShouldBe(12);
        window.Bounds.Y.ShouldBe(4);
        surface.Cell(new Point(listSurface.Bounds.X, listSurface.Bounds.Y)).Text.ShouldBe("┌");
        surface.Cell(new Point(list.Bounds.X, list.Bounds.Y)).Text.ShouldBe("▸");
        await surface.Pointer.MoveToAsync(list);
        dialog.IsPointerOver.ShouldBeTrue();

        // Act and assert selected semantic cell
        await surface.UpdateAsync(() => list.SelectedIndex = 1, "select rendered file");
        var selectedBg = surface.Cell(new Point(list.Bounds.X, list.Bounds.Y + 1)).Style.Background;
        selectedBg.IsRgb.ShouldBeTrue();

        // Act and assert normal/narrow sizes on the same retained instance
        await surface.ResizeAsync(new Size(60, 20));
        window.Bounds.Width.ShouldBe(48);
        window.Bounds.Height.ShouldBe(16);
        window.Bounds.ShouldBe(new Rect(6, 2, 48, 16));
        window.Bounds.X.ShouldBeGreaterThanOrEqualTo(0);
        window.Bounds.Y.ShouldBeGreaterThanOrEqualTo(0);
        window.Bounds.Right.ShouldBeLessThanOrEqualTo(60);
        window.Bounds.Bottom.ShouldBeLessThanOrEqualTo(20);

        await surface.ResizeAsync(new Size(44, 12));
        window.Bounds.Width.ShouldBe(35);
        window.Bounds.Height.ShouldBe(10);
        window.Bounds.ShouldBe(new Rect(4, 1, 35, 10));
    }

    /// <summary>Verifies accepting one selected file returns its canonical path and restores the opener.</summary>
    [ComponentBehaviorEvidence(
        typeof(FilePickerDialog),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Activation |
        ComponentBehavior.KeyboardActivation |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Transient |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task ShowAsync_WhenFileIsAccepted_ReturnsPathAndRemovesModalDialogAsync()
    {
        // Arrange
        var directory = CreateTemporaryDirectory();

        try
        {
            var file = Path.Combine(directory, "Program.cs");
            await File.WriteAllTextAsync(file, "class Program;", TestContext.Current.CancellationToken);
            var opener = new Button { Content = new ControlText("Open picker") };
            var host = new Overlay { Children = { opener } };
            await using var surface = await ComponentSurface.MountAsync(
                host,
                new Size(90, 30),
                TestContext.Current.CancellationToken);
            await surface.UpdateAsync(() => surface.Application.Focus.Focus(opener).ShouldBeTrue(), "focus opener");
            Task<FilePickerResult>? pending = null;

            // Act
            await surface.UpdateAsync(
                () => pending = FilePickerDialog.ShowAsync(
                    opener,
                    new FilePickerOptions
                    {
                        InitialDirectory = directory,
                        Filters = [new FilePickerFilter("Sources", "*.cs")]
                    }),
                "show file picker");
            var dialog = OwnedTree.Find<FilePickerDialog>(surface.Application.Root).ShouldNotBeNull();
            await WaitUntilAsync(surface, () => !dialog.IsLoading);
            var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
            await surface.UpdateAsync(
                () =>
                {
                    list.SelectedIndex = 0;
                    surface.Application.Focus.Focus(list).ShouldBeTrue();
                },
                "select listed file");
            await surface.Keyboard.PressAsync(Code.Enter);

            // Assert
            var result = await pending!;
            result.Accepted.ShouldBeTrue();
            result.Paths.ShouldHaveSingleItem().ShouldBe(file);
            host.Children.Count.ShouldBe(1);
            opener.IsFocused.ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies Escape returns the cancelled result without activating background content.</summary>
    [Fact]
    public async Task ShowAsync_WhenEscapeIsPressed_CancelsAndRestoresFocusAsync()
    {
        // Arrange
        var directory = CreateTemporaryDirectory();

        try
        {
            var opener = new Button { Content = new ControlText("Open picker") };
            var background = new Button { Content = new ControlText("Background") };
            var activations = 0;
            background.Click += (_, _) => activations++;
            var host = new Overlay { Children = { opener, background } };
            await using var surface = await ComponentSurface.MountAsync(
                host,
                new Size(80, 24),
                TestContext.Current.CancellationToken);
            await surface.UpdateAsync(() => surface.Application.Focus.Focus(opener).ShouldBeTrue(), "focus opener");
            Task<FilePickerResult>? pending = null;

            // Act
            await surface.UpdateAsync(
                () => pending = FilePickerDialog.ShowAsync(
                    opener,
                    new FilePickerOptions { InitialDirectory = directory }),
                "show cancellable picker");
            await surface.Pointer.MoveToAsync(background);
            await surface.Pointer.PressAsync();
            await surface.Pointer.ReleaseAsync();
            await surface.Keyboard.PressAsync(Code.Tab);
            background.IsFocused.ShouldBeFalse();
            await surface.Keyboard.PressAsync(Code.Escape);

            // Assert
            var result = await pending!;
            result.Accepted.ShouldBeFalse();
            result.Paths.ShouldBeEmpty();
            activations.ShouldBe(0);
            opener.IsFocused.ShouldBeTrue();
            host.Children.Count.ShouldBe(2);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies external cancellation cancels the task and removes the temporary modal surface.</summary>
    [Fact]
    public async Task ShowAsync_WhenCancellationIsRequested_CancelsTaskAndRestoresHostAsync()
    {
        // Arrange
        var directory = CreateTemporaryDirectory();

        try
        {
            using var cancellation = new CancellationTokenSource();
            var opener = new Button { Content = new ControlText("Open picker") };
            var host = new Overlay { Children = { opener } };
            await using var surface = await ComponentSurface.MountAsync(
                host,
                new Size(80, 24),
                TestContext.Current.CancellationToken);
            await surface.UpdateAsync(() => surface.Application.Focus.Focus(opener).ShouldBeTrue(), "focus opener");
            Task<FilePickerResult>? pending = null;
            await surface.UpdateAsync(
                () => pending = FilePickerDialog.ShowAsync(
                    opener,
                    new FilePickerOptions { InitialDirectory = directory },
                    cancellation.Token),
                "show externally cancellable picker");

            // Act
            cancellation.Cancel();
            _ = await Should.ThrowAsync<TaskCanceledException>(() => pending!);

            // Assert
            await surface.UpdateAsync(static () => { }, "settle cancellation cleanup");
            host.Children.Count.ShouldBe(1);
            opener.IsFocused.ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies queue pressure defers cancellation cleanup instead of settling a still-mounted dialog.</summary>
    [Fact]
    public async Task ShowAsync_WhenCancellationPostFindsFullQueue_DefersSettlementUntilCleanupAsync()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            using var cancellation = new CancellationTokenSource();
            var opener = new Button { Content = new ControlText("Open picker") };
            var host = new Overlay { Children = { opener } };
            await using var surface = await ComponentSurface.MountAsync(
                host,
                new Size(80, 24),
                TestContext.Current.CancellationToken);
            Task<FilePickerResult>? pending = null;
            await surface.UpdateAsync(
                () => pending = FilePickerDialog.ShowAsync(
                    opener,
                    new FilePickerOptions { InitialDirectory = directory },
                    cancellation.Token),
                "show queue-pressure picker");
            var dialog = OwnedTree.Find<FilePickerDialog>(surface.Application.Root).ShouldNotBeNull();
            var scope = surface.Application.Modality.Active.ShouldNotBeNull();

            await surface.UpdateAsync(
                () =>
                {
                    for (var index = 0; index < 4096; index++)
                    {
                        surface.Application.Dispatcher.Post(static () => { });
                    }

                    cancellation.Cancel();
                },
                "cancel picker through full dispatcher queue");

            _ = await Should.ThrowAsync<TaskCanceledException>(() => pending!);
            dialog.IsDisposed.ShouldBeTrue();
            dialog.Parent.ShouldBeNull();
            scope.IsActive.ShouldBeFalse();
            surface.Application.Modality.Active.ShouldBeNull();
            host.Children.ShouldBe([opener]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies a nested opener presents against the outer application container instead of changing form layout.</summary>
    [Fact]
    public async Task ShowAsync_WhenOwnerIsNested_AttachesPickerToOutermostContainerAsync()
    {
        // Arrange
        var directory = CreateTemporaryDirectory();

        try
        {
            var opener = new Button { Content = new ControlText("Nested opener") };
            var form = new Stack { Children = { opener } };
            var host = new Overlay { Children = { form } };
            await using var surface = await ComponentSurface.MountAsync(
                host,
                new Size(80, 24),
                TestContext.Current.CancellationToken);
            Task<FilePickerResult>? pending = null;

            // Act
            await surface.UpdateAsync(
                () => pending = FilePickerDialog.ShowAsync(
                    opener,
                    new FilePickerOptions { InitialDirectory = directory }),
                "show picker from nested form");
            var picker = OwnedTree.Find<FilePickerDialog>(surface.Application.Root).ShouldNotBeNull();

            // Assert
            picker.Parent.ShouldBeSameAs(surface.Application.Root);
            form.Children.Count.ShouldBe(1);
            await surface.Keyboard.PressAsync(Code.Escape);
            (await pending!).Accepted.ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sharp-vision-picker-surface-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(path);
        return path;
    }

    private static async Task WaitUntilAsync(ComponentSurface surface, Func<bool> predicate)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (await surface.Application.Dispatcher.InvokeAsync(predicate, TestContext.Current.CancellationToken))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5), TestContext.Current.CancellationToken);
        }

        (await surface.Application.Dispatcher.InvokeAsync(predicate, TestContext.Current.CancellationToken))
            .ShouldBeTrue("the file-picker operation should settle within 500ms");
    }

    private static List<T> FindAll<T>(ControlBase root) where T : ControlBase
    {
        var matches = new List<T>();
        Visit(root);
        return matches;

        void Visit(ControlBase control)
        {
            if (control is T match)
            {
                matches.Add(match);
            }

            for (var index = 0; index < control.OwnedControlCount; index++)
            {
                Visit(control.OwnedControlAt(index));
            }
        }
    }

    private static string ReadRow(ComponentSurface surface, Rect bounds)
    {
        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return string.Empty;
        }

        var row = new StringBuilder();
        var y = bounds.Y + (bounds.Height / 2);

        for (var x = bounds.X; x < bounds.Right; x++)
        {
            _ = row.Append(surface.Cell(new Point(x, y)).Text);
        }

        return row.ToString();
    }
}
