// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Dialogs;

/// <summary>Proves the public file-picker presentation through a mounted application surface.</summary>
public sealed class FilePickerDialogSurfaceTests
{
    /// <summary>Verifies Escape commits the cancellation sentinel for a directly mounted,
    /// modeless file picker.</summary>
    [Fact]
    public async Task Escape_WhenDialogIsModeless_PublishesCancelledResultAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-modeless-escape"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(76, 33),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        var input = OwnedTree.Find<TextInput>(dialog).ShouldNotBeNull();
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(input).ShouldBeTrue(), "focus file picker");

        // Act
        await surface.Keyboard.PressAsync(Code.Escape);

        // Assert
        dialog.HasSelectedResult.ShouldBeTrue();
        dialog.SelectedResult.ShouldBeSameAs(FilePickerResult.Cancelled);
    }

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
        var path = OwnedTree.Find<TextInput>(dialog).ShouldNotBeNull();

        // A directly-mounted dialog never receives PresentAsync's initial focus of
        // GetModalFocusTarget(); apply it explicitly so this test reproduces the
        // same initial-focus sequence a real caller's PresentAsync would produce.
        await surface.UpdateAsync(() => surface.Application.Focus.Focus(path).ShouldBeTrue(), "focus path input");
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        var window = OwnedTree.Find<Window>(dialog).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var listSurface = list.Parent.ShouldBeOfType<Dock>();
        var root = listSurface.Parent.ShouldBeOfType<Grid>();
        var filter = OwnedTree.Find<ComboBox>(dialog).ShouldNotBeNull();
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();
        var status = FindAll<ControlText>(dialog).Single(static text => text.Content == "0 folders · 1 file");
        var separator = OwnedTree.Find<Separator>(dialog).ShouldNotBeNull();
        var up = FindAll<Button>(dialog).Single(static button =>
            button.Text == "↑");
        var open = FindAll<Button>(dialog).Single(static button =>
            button.Text == "&Open");
        var cancel = FindAll<Button>(dialog).Single(static button =>
            button.Text == "&Cancel");

        // Assert
        window.ShouldBeSameAs(dialog);
        _ = window.Parent.ShouldBeOfType<Overlay>();
        window.CanMove.ShouldBeTrue();
        window.Bounds.Height.ShouldBe(26);
        list.ContentBounds.Height.ShouldBe(7);
        path.ScrollBars.ShouldBe(ScrollBars.None);
        up.ActualBorder.Sides.ShouldBe(BorderSide.All);
        up.ActualShadow.IsVisible.ShouldBeFalse();
        up.Bounds.Width.ShouldBe(5);
        up.Bounds.Height.ShouldBe(3);
        var upGlyph = up.TextControl.ShouldNotBeNull();
        upGlyph.Bounds.ShouldBe(new Rect(up.Bounds.X + 2, up.Bounds.Y + 1, 1, 1));
        surface.Cell(new Point(upGlyph.Bounds.X, upGlyph.Bounds.Y)).Text.ShouldBe("↑");
        FindAll<ControlText>(dialog).ShouldNotContain(static text =>
            text.Content == "Filter" || text.Content.Contains("Refresh", StringComparison.Ordinal));
        up.Bounds.Width.ShouldBe(5);
        ReadRow(surface, path.ContentBounds).ShouldContain("picker-polish");
        path.Bounds.Right.ShouldBe(listSurface.Bounds.Right);
        listSurface.Bounds.X.ShouldBe(root.ContentBounds.X);
        listSurface.Bounds.Right.ShouldBe(root.ContentBounds.Right);
        status.Bounds.Right.ShouldBe(listSurface.Bounds.Right);
        status.Bounds.Y.ShouldBe(filter.Bounds.Y + 1);
        status.Bounds.Height.ShouldBe(1);
        ReadRow(surface, status.Bounds).ShouldContain("0 folders · 1 file");
        hidden.Bounds.X.ShouldBe(listSurface.Bounds.X);
        hidden.Bounds.Y.ShouldBeGreaterThan(filter.Bounds.Bottom);
        filter.ActualBorder.Sides.ShouldBe(BorderSide.All);
        filter.Bounds.Height.ShouldBe(3);
        filter.Bounds.X.ShouldBe(listSurface.Bounds.X);
        filter.Bounds.Y.ShouldBe(listSurface.Bounds.Bottom);
        Math.Abs((filter.Bounds.Width * 2) - (listSurface.Bounds.Width - 1))
            .ShouldBeLessThanOrEqualTo(1);
        separator.Bounds.X.ShouldBe(listSurface.Bounds.X);
        separator.Bounds.Right.ShouldBe(listSurface.Bounds.Right);
        separator.Bounds.Y.ShouldBeGreaterThan(hidden.Bounds.Bottom);
        open.Bounds.Y.ShouldBe(separator.Bounds.Bottom);
        cancel.Bounds.Y.ShouldBe(separator.Bounds.Bottom);
        open.Bounds.Y.ShouldBe(cancel.Bounds.Y);
        open.Bounds.Height.ShouldBe(cancel.Bounds.Height);
        open.Bounds.Right.ShouldBe(cancel.Bounds.X - 1);
        cancel.Bounds.Right.ShouldBe(listSurface.Bounds.Right);
        cancel.Bounds.Bottom.ShouldBe(window.ContentBounds.Bottom);
        open.Style.ShouldBeNull();
        cancel.Style.ShouldBeNull();
        open.ActualShadow.IsVisible.ShouldBeFalse();
        cancel.ActualShadow.IsVisible.ShouldBeFalse();
        ReadRow(surface, open.Bounds).ShouldContain("Open");
        ReadRow(surface, cancel.Bounds).ShouldContain("Cancel");
    }

    /// <summary>Verifies action shadow clearance follows retained Button style changes.</summary>
    [Fact]
    public async Task ButtonStyle_WhenShadowVisibilityChanges_UpdatesFooterClearanceAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-action-shadow"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new FilePickerDialog(
            new FilePickerOptions
            {
                InitialDirectory = directory,
                OpenButtonStyle = ButtonStyle.Filled,
                CancelButtonStyle = ButtonStyle.Filled
            },
            source);
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(76, 33),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        var window = OwnedTree.Find<Window>(dialog).ShouldNotBeNull();
        var open = OwnedTree.FindAll<Button>(dialog).Single(static button => button.Text == "&Open");
        var cancel = OwnedTree.FindAll<Button>(dialog).Single(static button => button.Text == "&Cancel");

        // Assert shadow clearance
        open.ActualShadow.IsVisible.ShouldBeTrue();
        cancel.ActualShadow.IsVisible.ShouldBeTrue();
        open.Bounds.Bottom.ShouldBe(window.ContentBounds.Bottom - 1);
        cancel.Bounds.Bottom.ShouldBe(window.ContentBounds.Bottom - 1);

        // Act
        await surface.UpdateAsync(
            () =>
            {
                dialog.OpenButtonStyle = ButtonStyle.Standard;
                dialog.CancelButtonStyle = ButtonStyle.Standard;
            },
            "remove action shadows");

        // Assert flush actions
        open.ActualShadow.IsVisible.ShouldBeFalse();
        cancel.ActualShadow.IsVisible.ShouldBeFalse();
        open.Bounds.Bottom.ShouldBe(window.ContentBounds.Bottom);
        cancel.Bounds.Bottom.ShouldBe(window.ContentBounds.Bottom);
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
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
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
    [InlineData(7, 26, 9)]
    [InlineData(20, 39, 22)]
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
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        var window = OwnedTree.Find<Window>(dialog).ShouldNotBeNull();
        var list = OwnedTree.Find<UiListView>(dialog).ShouldNotBeNull();
        var listSurface = list.Parent.ShouldBeOfType<Dock>();
        var filter = OwnedTree.Find<ComboBox>(dialog).ShouldNotBeNull();
        var hidden = OwnedTree.Find<CheckBox>(dialog).ShouldNotBeNull();

        // Assert capped height
        window.Bounds.Height.ShouldBe(maximumWindowHeight);
        listSurface.Bounds.Height.ShouldBe(maximumListHeight);
        list.ContentBounds.Height.ShouldBe(maximumRows);
        filter.Bounds.Y.ShouldBe(listSurface.Bounds.Bottom);
        hidden.Bounds.Y.ShouldBe(filter.Bounds.Bottom + 1);

        // Act and assert available height
        await surface.ResizeAsync(new Size(100, 29));
        window.Bounds.Height.ShouldBe(23);
        list.ContentBounds.Height.ShouldBe(4);

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
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
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
    [Fact]
    public async Task ShowAsync_WhenFileIsAccepted_ReturnsPathAndRemovesModalDialogAsync()
    {
        // Arrange
        var directory = CreateTemporaryDirectory();

        try
        {
            var file = Path.Combine(directory, "Program.cs");
            await File.WriteAllTextAsync(file, "class Program;", TestContext.Current.CancellationToken);
            var opener = new Button { Text = "Open picker" };
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
            await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
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
            result.IsAccepted.ShouldBeTrue();
            result.Paths.ShouldHaveSingleItem().ShouldBe(file);
            host.Children.Count.ShouldBe(1);
            opener.IsFocused.ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies direct and ancestor-inherited disablement switch the mounted
    /// FilePickerDialog to its disabled appearance, re-enabling restores Normal, and a disabled
    /// instance moved to a genuinely different size arranges identically to an
    /// independently-mounted enabled instance at that same size.</summary>
    [Fact]
    public async Task Enabled_WhenDisabledDirectlyOrByAncestor_ChangesAppearanceAndRecoversAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-disabled-contract"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        var stack = new Stack { Children = { dialog } };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(90, 30),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        surface.ShouldHaveState(dialog, VisualState.Normal);

        // Act and assert direct disable
        await surface.UpdateAsync(() => dialog.IsEnabled = false, "disable FilePickerDialog directly");
        surface.ShouldHaveState(dialog, VisualState.Disabled);

        // Act and assert re-enable recovery
        await surface.UpdateAsync(() => dialog.IsEnabled = true, "re-enable FilePickerDialog");
        surface.ShouldHaveState(dialog, VisualState.Normal);

        // Act and assert ancestor-inherited disable
        await surface.UpdateAsync(() => stack.IsEnabled = false, "disable ancestor Stack");
        surface.ShouldHaveState(dialog, VisualState.Disabled);
        dialog.IsEnabled.ShouldBeTrue();
        dialog.EffectiveIsEnabled.ShouldBeFalse();
        await surface.UpdateAsync(() => stack.IsEnabled = true, "re-enable ancestor Stack");
        surface.ShouldHaveState(dialog, VisualState.Normal);

        // Arrange geometry comparison: a disabled instance moved to a genuinely different size -
        // same-size arrange is a no-op - must match an independently-mounted enabled instance
        // arranged directly at that same size.
        var disabledSource = new FakeFilePickerFileSystem();
        disabledSource.AddDirectory(directory);
        var disabled = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, disabledSource);
        await using var disabledSurface = await ComponentSurface.MountAsync(
            disabled,
            new Size(90, 30),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(disabledSurface, disabled, () => !disabled.IsLoading);
        await disabledSurface.UpdateAsync(() => disabled.IsEnabled = false, "disable independent FilePickerDialog");

        // Act
        await disabledSurface.ResizeAsync(new Size(70, 24));

        var enabledSource = new FakeFilePickerFileSystem();
        enabledSource.AddDirectory(directory);
        var enabled = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, enabledSource);
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabled,
            new Size(70, 24),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(enabledSurface, enabled, () => !enabled.IsLoading);

        // Assert
        disabled.Bounds.ShouldBe(enabled.Bounds);
        disabled.DesiredSize.ShouldBe(enabled.DesiredSize);
    }

    /// <summary>Verifies a disabled FilePickerDialog's close glyph renders with the theme's
    /// disabled-text color and ignores pointer hover/press instead of closing the dialog.</summary>
    [Fact]
    public async Task CloseGlyph_WhenFilePickerDialogIsDisabled_ShowsDisabledColorAndIgnoresPointerAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-disabled-close"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source);
        var closed = 0;
        dialog.Closed += (_, _) => closed++;
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(76, 33),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        var theme = dialog.Theme.ShouldNotBeNull();
        var disabledForeground = TerminalPalette.Project(
            ThemeColorHelper.DisabledForeground(theme),
            ColorDepth.Basic16);
        var glyphPoint = new Point(dialog.Bounds.X + 4, dialog.Bounds.Y);

        // Act
        await surface.UpdateAsync(() => dialog.IsEnabled = false, "disable FilePickerDialog");

        // Assert disabled close-glyph color
        surface.Cell(glyphPoint).Style.Foreground.ShouldBe(disabledForeground);

        // Act - aim a pointer press at the close glyph while disabled
        await surface.Pointer.MoveToAsync(dialog, new Point(4, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();

        // Assert the press neither closed the dialog nor changed the glyph's rendered color
        closed.ShouldBe(0);
        dialog.IsDisposed.ShouldBeFalse();
        surface.Cell(glyphPoint).Style.Foreground.ShouldBe(disabledForeground);
    }

    /// <summary>Verifies a local Style override's close-mark color reaches the rendered close
    /// glyph, proving the close chrome consults FilePickerDialog's own resolved style instead of
    /// always reading the generic "window" theme section.</summary>
    [Fact]
    public async Task CloseGlyph_WhenLocalStyleOverridesCloseMarkColor_UsesTheOverrideColorAsync()
    {
        // Arrange
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "picker-local-close-style"));
        var source = new FakeFilePickerFileSystem();
        source.AddDirectory(directory);
        var dialog = new FilePickerDialog(new FilePickerOptions { InitialDirectory = directory }, source)
        {
            Style = FilePickerDialogStyle.Default with { CloseMarkColor = SemanticColor.Error }
        };
        await using var surface = await ComponentSurface.MountAsync(
            dialog,
            new Size(76, 33),
            TestContext.Current.CancellationToken);
        await DialogWait.UntilAsync(surface, dialog, () => !dialog.IsLoading);
        var theme = dialog.Theme.ShouldNotBeNull();
        var glyphPoint = new Point(dialog.Bounds.X + 4, dialog.Bounds.Y);

        // Assert
        surface.Cell(glyphPoint).Style.Foreground.ShouldBe(
            TerminalPalette.Project(theme.Error, ColorDepth.Basic16));
    }

    /// <summary>Verifies Escape returns the cancelled result without activating background content.</summary>
    [Fact]
    public async Task ShowAsync_WhenEscapeIsPressed_CancelsAndRestoresFocusAsync()
    {
        // Arrange
        var directory = CreateTemporaryDirectory();

        try
        {
            var opener = new Button { Text = "Open picker" };
            var background = new Button { Text = "Background" };
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
            result.IsAccepted.ShouldBeFalse();
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
            var opener = new Button { Text = "Open picker" };
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
            var opener = new Button { Text = "Open picker" };
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
                    // Model an asynchronous directory-load commit that reached the dispatcher
                    // before this callback. Queue-pressure setup must not assume it owns all
                    // capacity slots.
                    surface.Application.Dispatcher.Post(static () => { });

                    while (true)
                    {
                        try
                        {
                            surface.Application.Dispatcher.Post(static () => { });
                        }
                        catch (InvalidOperationException)
                        {
                            break;
                        }
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
            var opener = new Button { Text = "Nested opener" };
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
            (await pending!).IsAccepted.ShouldBeFalse();
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
