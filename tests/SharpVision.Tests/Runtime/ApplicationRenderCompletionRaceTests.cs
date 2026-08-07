// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies a render whose completion cannot be posted back to the owning dispatcher
/// (e.g. because the dispatcher was disposed while the transport write was still pending) still
/// retires the in-flight flag instead of leaving it permanently set, which would otherwise wedge
/// the shutdown drain loop in an unbounded spin.</summary>
public sealed class ApplicationRenderCompletionRaceTests
{
    /// <summary>Verifies IsRendering returns to false after the dispatcher is disposed mid-render
    /// and the paused transport flush later completes, instead of staying latched true forever.</summary>
    [Fact]
    public async Task IsRendering_WhenDispatcherIsDisposedWhileFlushIsPending_RetiresAfterFlushCompletesAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.PauseFlush();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var application = new Application(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);

        try
        {
            var starting = application.StartAsync(TestContext.Current.CancellationToken).AsTask();

            // Swallow the fault StartAsync will surface once the dispatcher beneath it disposes;
            // the race under test is entirely about IsRendering's own bookkeeping, not startup
            // completion.
            _ = starting.ContinueWith(
                static task => _ = task.Exception,
                TestContext.Current.CancellationToken,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);

            await WaitForAsync(() => application.IsRendering, TimeSpan.FromSeconds(5));
            application.IsRendering.ShouldBeTrue();

            await application.Dispatcher.DisposeAsync();
            terminal.ReleaseFlush();

            await WaitForAsync(() => !application.IsRendering, TimeSpan.FromSeconds(5));

            application.IsRendering.ShouldBeFalse();
        }
        finally
        {
            // The dispatcher was already disposed directly above to construct the race; a second,
            // orderly Application.DisposeAsync would itself throw ObjectDisposedException trying to
            // hop back onto it, which is expected and irrelevant to the assertion above.
            try
            {
                await application.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The awaited condition never became true.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }
    }
}
