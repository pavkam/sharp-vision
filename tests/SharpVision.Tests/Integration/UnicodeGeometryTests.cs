// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Integration;

using ComboBoxControl = ComboBox;
using TextInputControl = TextInput;

/// <summary>Verifies end-to-end Unicode geometry across controls, frames, and pointer routing.</summary>
public sealed class UnicodeGeometryTests
{
    /// <summary>Verifies ambiguous-width geometry is consistent across representative text consumers.</summary>
    [Fact]
    public async Task Layout_WhenAmbiguousWidthIsWide_AgreesAcrossTextConsumersAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var policy = new UnicodePolicy(Ambiguous.Wide);
            var text = new ControlText { Content = "·" };
            var marked = new ControlText("<b>·</b>");
            var input = new TextInputControl { Text = "·" };
            var table = new Table();
            table.Columns.Add(TableColumn.Auto("·"));
            var combo = new ComboBoxControl { Items = ["·"], SelectedIndex = 0 };
            var treeItem = new TreeViewItem { Header = "·" };
            var navItem = new NavigationViewItem { Text = "Home", Glyph = "·" };
            var stack = new Stack();
            stack.Children.Add(text);
            stack.Children.Add(marked);
            stack.Children.Add(input);
            stack.Children.Add(table);
            stack.Children.Add(combo);
            stack.Children.Add(treeItem);
            stack.Children.Add(navItem);
            stack.SetTheme(TestThemes.BorderlessInput);
            stack.Attach(dispatcher, policy);
            new LayoutEngine().Layout(stack, new Size(40, 14));

            // Assert
            text.DesiredSize.Width.ShouldBe(2);
            marked.DesiredSize.Width.ShouldBe(2);

            // One more than text's width: the editor reserves an extra cell beyond the
            // ambiguous-wide "·" for the end-of-text caret.
            input.DesiredSize.Width.ShouldBe(3);
            text.CellPolicy.AmbiguousWidth.ShouldBe(Ambiguous.Wide);

            // TablePresenter's Auto column, ComboBox's field, TreeViewItem's header, and
            // NavigationViewItem's glyph each reserve exactly one more cell than the equivalent
            // Narrow-policy layout below, since "·" alone drives their measured width and is
            // ambiguous-width (1 cell Narrow, 2 cells Wide). Compared as a delta rather than an
            // absolute width, since the absolute figure also depends on border/padding/glyph-
            // reservation constants not worth hardcoding here. Before the fix this delta was 0 for
            // all four: every affected site always measured under Narrow regardless of the
            // ambient policy, so switching the tree to Wide never moved their desired size at all.
            var narrowTable = new Table();
            narrowTable.Columns.Add(TableColumn.Auto("·"));
            var narrowCombo = new ComboBoxControl { Items = ["·"], SelectedIndex = 0 };
            var narrowTreeItem = new TreeViewItem { Header = "·" };
            var narrowNavItem = new NavigationViewItem { Text = "Home", Glyph = "·" };
            var narrowStack = new Stack();
            narrowStack.Children.Add(narrowTable);
            narrowStack.Children.Add(narrowCombo);
            narrowStack.Children.Add(narrowTreeItem);
            narrowStack.Children.Add(narrowNavItem);
            narrowStack.SetTheme(TestThemes.BorderlessInput);
            narrowStack.Attach(dispatcher, UnicodePolicy.Default);
            new LayoutEngine().Layout(narrowStack, new Size(40, 14));

            (table.DesiredSize.Width - narrowTable.DesiredSize.Width).ShouldBe(1);
            (combo.DesiredSize.Width - narrowCombo.DesiredSize.Width).ShouldBe(1);
            (treeItem.DesiredSize.Width - narrowTreeItem.DesiredSize.Width).ShouldBe(1);
            (navItem.DesiredSize.Width - narrowNavItem.DesiredSize.Width).ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies editable source text survives while frames store safe orphan presentation.</summary>
    [Fact]
    public async Task Render_WhenOrphanMarkIsEdited_PreservesSourceAndStoresReplacementAsync()
    {
        // Arrange
        const string source = "\u0301";
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var input = new TextInputControl { Text = source, Width = Length.Cells(2) };
            input.SetTheme(TestThemes.BorderlessInput);
            input.Attach(dispatcher, UnicodePolicy.Default);
            new LayoutEngine().Layout(input, new Size(2, 1));
            using Frame frame = new(new Size(2, 1));

            // Act
            input.Render(frame.Canvas);

            // Assert
            input.Text.ShouldBe(source);
            FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("�");
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies uneven pixel metrics map exactly and never fabricate top-left cells.</summary>
    [Fact]
    public async Task Input_WhenUnevenPixelGridMaps_RoutesExactCellsWithoutFabricationAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4), new Size(101, 31)));
        List<Point?> hits = [];
        var pointerHit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var root = new ProbePressable { Width = Length.Cells(10), Height = Length.Cells(4) };
        _ = root.AddHandler(Events.Pointer, (_, eventArgs) =>
        {
            if (eventArgs is PointerEventArgs pointer)
            {
                hits.Add(pointer.LocalCells);
                _ = pointerHit.TrySetResult();
            }
        });
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel });
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.ResponseReceived += (_, eventArgs) =>
        {
            if (eventArgs.Response.Kind == ResponseKind.PrimaryAttributes)
            {
                _ = ready.TrySetResult();
            }
        };
        await application.StartAsync(TestContext.Current.CancellationToken);
        terminal.QueueInput("\u001b[?1;2c"u8);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Act
        terminal.QueueInput("\u001b[<0;100;30M"u8);
        await pointerHit.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert
        hits.ShouldContain(new Point(9, 3));
        hits.ShouldNotContain(new Point(0, 0));
        await application.StopAsync(TestContext.Current.CancellationToken);
    }
}
