using System.Text;

using SharpVision.Controls;
using SharpVision.Input;
using SharpVision.Runtime;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Runtime;
using SharpVision.Tests.Support;

using Shouldly;

using KeyAction = SharpVision.Terminal.Input.Action;
using TerminalOptions = SharpVision.Terminal.Runtime.Options;

namespace SharpVision.Tests.Integration;

/// <summary>Proves real terminal bytes through routing, rendering, and output transport.</summary>
public sealed class TerminalInputTests
{
    /// <summary>Verifies UTF-8 text mutates a focused control and reaches encoded frame bytes.</summary>
    [Fact]
    public async Task Input_WhenUtf8TextArrives_ChangesFocusedControlAndFinalOutputAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(10, 3)));
        var root = new ProbeContainer();
        var child = new ProbeControl
        {
            Bounds = new Rect(0, 0, 4, 1),
            CanFocus = true,
        };
        root.Children.Add(child);
        await using var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var handled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = Encoding.UTF8.GetBytes("λ");
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += value =>
        {
            if (value.Span.IndexOf(expected) >= 0)
            {
                _ = written.TrySetResult();
            }
        };
        application.FrameRendered += (_, _) =>
        {
            if (written.Task.IsCompleted)
            {
                _ = rendered.TrySetResult();
            }
        };
        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(child).ShouldBeTrue();
            _ = child.AddHandler(Events.Text, (_, eventArgs) =>
            {
                if (eventArgs.Phase != Phase.Bubble)
                {
                    return;
                }

                child.Content = eventArgs.Text.Value.ToString().AsMemory();
                child.Invalidate(Invalidation.Render);
                _ = handled.TrySetResult();
            });
        }, TestContext.Current.CancellationToken);

        terminal.QueueInput(Encoding.UTF8.GetBytes("λ"));
        await handled.Task.WaitAsync(TestContext.Current.CancellationToken);
        await rendered.Task.WaitAsync(TestContext.Current.CancellationToken);

        child.Content.ToString().ShouldBe("λ");
        terminal.Writes.Any(value => value.AsSpan().IndexOf(expected) >= 0).ShouldBeTrue();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies focus, pixel mouse, paste, and Kitty bytes retain typed payloads.</summary>
    [Fact]
    public async Task Input_WhenExtendedSequencesArrive_PreservesTypedPayloadsAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(10, 4), new Size(80, 64)));
        var root = new ProbeContainer();
        var child = new ProbeControl
        {
            Bounds = new Rect(0, 0, 5, 4),
            CanFocus = true,
        };
        root.Children.Add(child);
        await using var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel });
        await application.StartAsync(TestContext.Current.CancellationToken);
        Pointer? pointer = null;
        Paste? paste = null;
        Focus? focus = null;
        Stroke? stroke = null;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(child).ShouldBeTrue();
            _ = child.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == Phase.Bubble)
                {
                    pointer = eventArgs.Pointer;
                    CompleteIfReady();
                }
            });
            _ = child.AddHandler(Events.Paste, (_, eventArgs) =>
            {
                if (eventArgs.Phase == Phase.Bubble)
                {
                    paste = eventArgs.Paste;
                    CompleteIfReady();
                }
            });
            _ = child.AddHandler(Events.Focus, (_, eventArgs) =>
            {
                if (eventArgs.Phase == Phase.Bubble)
                {
                    focus = eventArgs.Focus;
                    CompleteIfReady();
                }
            });
            _ = child.AddHandler(Events.Key, (_, eventArgs) =>
            {
                if (eventArgs.Phase == Phase.Bubble && eventArgs.Stroke.Action == KeyAction.Repeat)
                {
                    stroke = eventArgs.Stroke;
                    CompleteIfReady();
                }
            });
        }, TestContext.Current.CancellationToken);
        var bytes = Encoding.UTF8.GetBytes(
            "\u001b[I\u001b[<0;17;33M\u001b[200~ok\u001b[201~\u001b[97;256:2u");

        terminal.QueueInput(bytes);
        await completed.Task.WaitAsync(TestContext.Current.CancellationToken);

        focus.ShouldBe(new Focus(Gained: true));
        pointer!.Value.Pixels.ShouldBe(new Point(16, 32));
        pointer.Value.Cells.ShouldBe(new Point(2, 2));
        pointer.Value.IsCellPositionInferred.ShouldBeTrue();
        Encoding.UTF8.GetString(paste!.Utf8.Span).ShouldBe("ok");
        stroke!.Value.Code.ShouldBe(Code.Character);
        stroke.Value.Character.ShouldBe(new Rune('a'));
        stroke.Value.Action.ShouldBe(KeyAction.Repeat);
        await application.StopAsync(TestContext.Current.CancellationToken);

        void CompleteIfReady()
        {
            if (pointer.HasValue && paste is not null && focus.HasValue && stroke.HasValue)
            {
                _ = completed.TrySetResult();
            }
        }
    }
}
