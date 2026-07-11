using System.Text;

using SharpVision.Controls;
using SharpVision.Input;
using SharpVision.Runtime;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;
using SharpVision.Terminal.Runtime;
using SharpVision.Tests.Support;

using Shouldly;

using TerminalOptions = SharpVision.Terminal.Runtime.Options;

namespace SharpVision.Tests.Integration;

/// <summary>Proves real terminal text, paste, keys, focus, frames, and bytes through TextInput.</summary>
public sealed class InteractiveControlTests
{
    /// <summary>Verifies raw input mutates grapheme state and incremental output without stale source cells.</summary>
    [Fact]
    public async Task Input_WhenTextPasteAndLegacyKeysArrive_CommitsExactEditorFrameAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(12, 2)));
        var input = new TextInput();
        await using var application = new Application(input, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(() =>
            application.Focus.Focus(input).ShouldBeTrue(),
            TestContext.Current.CancellationToken);

        terminal.QueueInput(Encoding.UTF8.GetBytes("界\u001b[200~e\u0301\u001b[201~"));
        await WaitUntilAsync(
            () => input.Text == "界e\u0301",
            application,
            "text and paste",
            TestContext.Current.CancellationToken);
        terminal.QueueInput(Encoding.ASCII.GetBytes("\u001b[D"));
        await WaitUntilAsync(
            () => input.CaretIndex == 1,
            application,
            "legacy Left",
            TestContext.Current.CancellationToken);
        terminal.QueueInput([0x7F]);
        await WaitUntilAsync(
            () => input.Text == "e\u0301",
            application,
            "legacy Backspace",
            TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(() =>
        {
            input.Text.ShouldBe("e\u0301");
            input.CaretIndex.ShouldBe(0);
            using var frame = new Frame(application.Size);
            input.Render(frame.Canvas);
            FrameOracle.Get(frame, default).ShouldBe("e\u0301");
            frame.GetCell(new Point(1, 0)).IsContinuation.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
        terminal.Writes.SelectMany(static bytes => bytes)
            .Contains((byte) 'e').ShouldBeTrue();

        var focusLost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await application.Dispatcher.InvokeAsync(() =>
        {
            _ = input.AddHandler(Events.Focus, (_, eventArgs) =>
            {
                if (eventArgs.Phase == Phase.Bubble && !eventArgs.Focus.Gained)
                {
                    _ = focusLost.TrySetResult();
                }
            });
        }, TestContext.Current.CancellationToken);
        terminal.QueueInput(Encoding.ASCII.GetBytes("\u001b[O"));
        await focusLost.Task.WaitAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () => input.IsFocused.ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        application.Capture.Captured.ShouldBeNull();
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
