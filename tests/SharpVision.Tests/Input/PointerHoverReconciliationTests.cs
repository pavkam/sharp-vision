// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies hover is re-hit-tested when geometry moves under a stationary pointer instead
/// of only ever following physical pointer motion.</summary>
public sealed class PointerHoverReconciliationTests
{
    /// <summary>Verifies a wheel scroll that moves content under a pointer left stationary at the
    /// wheeled cell repaints hover onto the control now at that cell, not the one that used to be
    /// there.</summary>
    [Fact]
    public async Task PointerOverFace_WhenWheelScrollMovesContentUnderTheCursor_RepaintsTheNewlyHoveredControlAsync()
    {
        // Arrange
        var stack = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var buttons = new Button[4];

        for (var index = 0; index < buttons.Length; index++)
        {
            buttons[index] = new Button(index.ToString(CultureInfo.InvariantCulture))
            {
                Height = Length.Cells(1),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            stack.Children.Add(buttons[index]);
        }

        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(4, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(stack, new Point(0, 0));
        surface.ShouldHaveState(buttons[0], VisualState.PointerOver);

        // Act - wheel down at the same cell the pointer already occupies
        await surface.Pointer.WheelAsync(stack, new Point(0, 0), wheelY: -1);

        // Assert - the button now under the stationary cell is hovered, not the stale one
        stack.VerticalOffset.ShouldBe(1);
        surface.ShouldHaveState(buttons[0], VisualState.Normal);
        surface.ShouldHaveState(buttons[1], VisualState.PointerOver);
    }

    /// <summary>Verifies a terminal resize that removes the row at the pointer's cell clears hover
    /// instead of leaving it pointing at a control no longer under the cursor.</summary>
    [Fact]
    public async Task Hovered_WhenTerminalResizesUnderThePointer_ClearsWhenNoControlRemainsAtTheCellAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(4, 2), new Size(40, 20)));
        var spacer = new ProbeControl { Height = Length.Cells(1) };
        var button = new Button("Go") { Height = Length.Cells(1), Width = Length.Cells(4) };
        var root = new Stack
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children = { spacer, button }
        };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        terminal.QueueInput(Encoding.ASCII.GetBytes("[<35;1;2M"));
        await WaitUntilAsync(
            () => ReferenceEquals(application.Capture.Hovered, button),
            application,
            "initial hover",
            TestContext.Current.CancellationToken);

        // Act - shrink the terminal so the row the pointer sits on no longer exists
        terminal.QueueResize(new Dimensions(new Size(4, 1), new Size(40, 10)));
        await WaitUntilAsync(
            () => application.Capture.Hovered is null,
            application,
            "hover clears after resize",
            TestContext.Current.CancellationToken);

        // Assert
        await application.Dispatcher.InvokeAsync(
            () => application.Capture.Hovered.ShouldBeNull(),
            TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        Application application,
        string operation,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10_000; attempt++)
        {
            if (await application.Dispatcher.InvokeAsync(predicate, cancellationToken))
            {
                return;
            }

            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }

        (await application.Dispatcher.InvokeAsync(predicate, cancellationToken))
            .ShouldBeTrue($"Timed out waiting for {operation}.");
    }
}
