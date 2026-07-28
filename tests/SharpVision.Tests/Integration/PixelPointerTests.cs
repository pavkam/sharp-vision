// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Integration;

/// <summary>Verifies pixel-only terminal input cannot fabricate control cells.</summary>
public sealed class PixelPointerTests
{
    /// <summary>Verifies captured pixel-only release cancels without activation.</summary>
    [Fact]
    public async Task Input_WhenCapturedReleaseCannotMap_CancelsPressWithoutActivationAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(
            new Size(10, 4),
            new Size(80, 64)));
        var root = new ProbePressable { Width = Length.Cells(10), Height = Length.Cells(4) };
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel });
        var primary = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondary = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        application.ResponseReceived += (_, eventArgs) =>
        {
            if (eventArgs.Response.Kind == ResponseKind.PrimaryAttributes)
            {
                _ = primary.TrySetResult();
            }
            else if (eventArgs.Response.Kind == ResponseKind.SecondaryAttributes)
            {
                _ = secondary.TrySetResult();
            }
        };
        await application.StartAsync(TestContext.Current.CancellationToken);

        // Act / Assert
        terminal.QueueInput("\u001b[<0;1;1M\u001b[?1;2c"u8);
        await primary.Task.WaitAsync(TestContext.Current.CancellationToken);
        root.IsPressed.ShouldBeTrue();
        application.Capture.Captured.ShouldBeSameAs(root);

        terminal.QueueInput("\u001b[<0;81;1m\u001b[>1;2c"u8);
        await secondary.Task.WaitAsync(TestContext.Current.CancellationToken);
        root.IsPressed.ShouldBeFalse();
        root.Activations.ShouldBeEmpty();
        application.Capture.Captured.ShouldBeNull();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an out-of-grid pixel press does not hit the root's top-left cell.</summary>
    [Fact]
    public async Task Input_WhenPixelCannotMap_DoesNotRouteAsTopLeftCellAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(
            new Size(10, 4),
            new Size(80, 64)));
        var root = new ProbePressable { Width = Length.Cells(10), Height = Length.Cells(4) };
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel });
        var pointers = 0;
        _ = root.AddHandler(Events.Pointer, (_, _) => pointers++);
        var observed = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        application.ResponseReceived += (_, eventArgs) =>
        {
            if (eventArgs.Response.Kind == ResponseKind.PrimaryAttributes)
            {
                _ = observed.TrySetResult();
            }
        };
        await application.StartAsync(TestContext.Current.CancellationToken);

        // Act
        terminal.QueueInput("\u001b[<0;81;1M\u001b[?1;2c"u8);
        await observed.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Assert
        pointers.ShouldBe(0);
        root.IsPressed.ShouldBeFalse();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }
}
