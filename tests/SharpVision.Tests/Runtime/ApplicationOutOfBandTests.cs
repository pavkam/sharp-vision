// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies out-of-band protocol bytes share the render write gate.</summary>
public sealed class ApplicationOutOfBandTests
{
    /// <summary>Verifies posted bytes reach the transport while the application is running.</summary>
    [Fact]
    public async Task PostOutOfBand_WhenApplicationRunning_WritesBytesToTransportAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var bell = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += memory =>
        {
            if (memory.Span.IndexOf((byte) 0x07) >= 0)
            {
                _ = bell.TrySetResult();
            }
        };
        await using Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.PostOutOfBand(new byte[] { 0x07 });
        await bell.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a render deferred behind an in-flight frame flush still lands once a
    /// successful out-of-band write completes, without waiting for a separate dispatcher idle
    /// tick. CompleteRender prefers draining the out-of-band queue over the deferred render
    /// whenever both are pending, and PumpAfterWrite already re-checks that same deferred render
    /// once the out-of-band write's own completion runs - this pins that existing chain so a
    /// future change cannot silently reintroduce a stranding gap in the common, non-faulting
    /// path.</summary>
    [Fact]
    public async Task FrameRendered_WhenOutOfBandWriteCompletesWhileRenderIsPending_RunsWithoutIdleTickAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var probe = new ProbeControl { Content = "a".AsMemory() };
        await using Application application = new(probe, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        terminal.PauseFlush();

        // Content actually changes each time - an invalidate alone can produce a zero-byte diff
        // that the renderer never hands to the transport at all, which would never engage the
        // paused flush this test relies on.
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                probe.Content = "b".AsMemory();
                probe.InvalidateKernel(InvalidationImpact.Render);
            },
            TestContext.Current.CancellationToken);
        await WaitForAsync(() => application.IsRendering, TimeSpan.FromSeconds(5));

        // Only observed from here on: while the first frame's write holds the dispatcher pending,
        // no idle transition can occur at all, so anything caught earlier would be the ordinary
        // idle tick that started this very frame - not evidence of the recovery under test.
        var idleFired = false;
        void IdleHandler(object? sender, EventArgs eventArgs) => idleFired = true;
        application.Dispatcher.Idle += IdleHandler;

        var secondFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var frames = 0;
        application.FrameRendered += (_, _) =>
        {
            frames++;

            if (frames == 2)
            {
                // Unsubscribed from inside the very handler that observes the target frame, still
                // on the dispatcher thread and before this frame's own Dispatcher.Hold releases -
                // the legitimate idle tick that follows full quiescence must not be mistaken for a
                // stranding symptom.
                application.Dispatcher.Idle -= IdleHandler;
                _ = secondFrame.TrySetResult();
            }
        };

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                probe.Content = "c".AsMemory();
                probe.InvalidateKernel(InvalidationImpact.Render);
            },
            TestContext.Current.CancellationToken);
        application.PostOutOfBand(new byte[] { 0x07 });
        terminal.ReleaseFlush();

        await secondFrame.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        frames.ShouldBe(2);
        idleFired.ShouldBeFalse();

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a render request pending behind an out-of-band write still lands once
    /// that write faults and the failure is handled, without waiting for a separate dispatcher
    /// idle tick. Before the fix, CompleteOutOfBand returned as soon as Report ran, skipping
    /// PumpAfterWrite entirely on this path and leaving the deferred render stranded until an
    /// unrelated invalidation or idle tick happened to notice it.</summary>
    [Fact]
    public async Task FrameRendered_WhenOutOfBandWriteFaultsAndFailureIsHandled_RunsWithoutIdleTickAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var probe = new ProbeControl();
        await using Application application = new(probe, terminal, terminal, TerminalOptions.Minimal);
        application.UnhandledException += (_, eventArgs) => eventArgs.Handled = true;
        await application.StartAsync(TestContext.Current.CancellationToken);

        // Targets the very next write - the out-of-band write triggered below is the only thing
        // that writes anything between here and the assertion.
        terminal.WriteFailure = new InvalidOperationException("Injected out-of-band write failure.");
        terminal.FailWriteNumber = terminal.Writes.Count + 1;

        var idleFired = false;
        void IdleHandler(object? sender, EventArgs eventArgs) => idleFired = true;

        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += (_, _) =>
        {
            // Unsubscribed from inside this very handler, still on the dispatcher thread and
            // before this frame's own Dispatcher.Hold releases - the legitimate idle tick that
            // follows full quiescence must not be mistaken for a stranding symptom.
            application.Dispatcher.Idle -= IdleHandler;
            _ = recovered.TrySetResult();
        };

        // Subscribing to Idle inside the same dispatcher callback that requests the render and
        // posts the out-of-band bytes leaves no gap in which an unrelated idle tick - e.g. the
        // one that would ordinarily follow the first frame settling - could be mistaken for
        // evidence one way or the other.
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Dispatcher.Idle += IdleHandler;

                // A content change (rather than a no-op invalidate) gives the deferred render an
                // actual diff to transmit, so its recovery is also visible on the wire and not
                // just through the FrameRendered event.
                probe.Content = "recovered".AsMemory();
                probe.InvalidateKernel(InvalidationImpact.Render);
                application.PostOutOfBand(new byte[] { 0x07 });
            },
            TestContext.Current.CancellationToken);

        await recovered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        idleFired.ShouldBeFalse();

        // The handled failure is still the application's recorded Failure - by design, it
        // resurfaces from StopAsync once the run completes, even though the run itself kept going
        // and this deferred render still landed. That resurfacing is not what this test is about.
        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StopAsync(TestContext.Current.CancellationToken));
    }

    // IsRendering has no change notification of its own, so a condition built on it cannot be
    // awaited through an event or a completion source. Polling the flag is the only available
    // signal; the wait stays real wall-clock by necessity, not oversight.
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
