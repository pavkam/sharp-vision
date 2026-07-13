// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Integration;

using SharpVision.Runtime;
using SharpVision.Terminal.Runtime;
using SharpVision.Terminal.Unicode;


using ComboBoxControl = ComboBox;
using RichTextControl = RichText;
using RunInline = Run;
using TerminalOptions = Terminal.Runtime.Options;
using TextControl = SharpVision.Controls.Text;
using TextInputControl = TextInput;
using TextWrapping = SharpVision.Text.Wrapping;

/// <summary>Verifies end-to-end Unicode geometry across controls, frames, and pointer routing.</summary>
public sealed class UnicodeGeometryTests
{
    /// <summary>Verifies ambiguous-width geometry is consistent across representative text consumers.</summary>
    [Fact]
    public async Task Layout_WhenAmbiguousWidthIsWide_AgreesAcrossTextConsumersAsync()
    {
        // Arrange
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            Policy policy = new(Ambiguous.Wide);
            TextControl text = new() { Content = "·" };
            RichTextControl rich = new() { Wrapping = TextWrapping.None };
            rich.Inlines.Add(new RunInline("·"));
            TextInputControl input = new() { Text = "·" };
            Table table = new();
            table.Columns.Add(TableColumn.Fixed("Value", 4));
            table.Rows.Add(new TableRow([new TextControl("·")]));
            ComboBoxControl combo = new() { Items = ["·"] };
            Stack stack = new();
            stack.Children.Add(text);
            stack.Children.Add(rich);
            stack.Children.Add(input);
            stack.Children.Add(table);
            stack.Children.Add(combo);
            stack.Attach(dispatcher, policy);
            new Engine().Layout(stack, new Size(20, 12));

            // Assert
            text.DesiredSize.Width.ShouldBe(2);
            rich.DesiredSize.Width.ShouldBe(2);
            input.DesiredSize.Width.ShouldBe(2);
            table.DesiredSize.Width.ShouldBeGreaterThanOrEqualTo(2);
            combo.DesiredSize.Width.ShouldBeGreaterThanOrEqualTo(2);
            text.CellPolicy.AmbiguousWidth.ShouldBe(Ambiguous.Wide);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies editable source text survives while frames store safe orphan presentation.</summary>
    [Fact]
    public async Task Render_WhenOrphanMarkIsEdited_PreservesSourceAndStoresReplacementAsync()
    {
        // Arrange
        const string source = "\u0301";
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            TextInputControl input = new() { Text = source, Width = Length.Cells(2) };
            input.Attach(dispatcher, Policy.Default);
            new Engine().Layout(input, new Size(2, 1));
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
        ProbePressable root = new()
        {
            Width = Length.Cells(10),
            Height = Length.Cells(4),
        };
        _ = root.AddHandler(Events.Pointer, (_, eventArgs) =>
        {
            if (eventArgs is PointerEventArgs pointer)
            {
                hits.Add(pointer.LocalCells);
            }
        });
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel });
        TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
        await WaitForPointerHitAsync(hits);

        // Assert
        hits.ShouldContain(new Point(9, 3));
        hits.ShouldNotContain(new Point(0, 0));
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task WaitForPointerHitAsync(List<Point?> hits)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (hits.Count > 0)
            {
                return;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("Pointer routing did not deliver a mapped cell.");
    }
}
