// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Integration;

/// <summary>Proves real terminal bytes through routing, rendering, and output transport.</summary>
public sealed class TerminalInputTests
{
    /// <summary>Verifies Tab moves focus from one editor to the next eligible sibling.</summary>
    [Fact]
    public async Task Input_WhenTabIsPressedOnFocusedEditor_FocusMovesToNextEditorAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var root = new ProbeContainer();
        var first = new TextInput();
        var second = new TextInput();
        root.Children.Add(first);
        root.Children.Add(second);
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var focused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Gained += (_, eventArgs) =>
            {
                if (ReferenceEquals(eventArgs.Current, second))
                {
                    _ = focused.TrySetResult();
                }
            };
            application.Focus.Focus(first).ShouldBeTrue();
        }, TestContext.Current.CancellationToken);

        terminal.QueueInput("\t"u8);
        await focused.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focused.ShouldBeSameAs(second);
            first.Focused.ShouldBeFalse();
            second.Focused.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies UTF-8 text mutates a focused control and reaches encoded frame bytes.</summary>
    [Fact]
    public async Task Input_WhenUtf8TextArrives_ChangesFocusedControlAndFinalOutputAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 3)));
        var root = new ProbeContainer();
        var child = new ProbeControl { Bounds = new Rect(0, 0, 4, 1), Focusable = true };
        root.Children.Add(child);
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var handled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = "λ"u8.ToArray();
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
                if (eventArgs.Phase != RoutingPhase.Bubble)
                {
                    return;
                }

                child.Content = eventArgs.Text.Value.ToString().AsMemory();
                child.Invalidate(Invalidation.Render);
                _ = handled.TrySetResult();
            });
        }, TestContext.Current.CancellationToken);

        terminal.QueueInput("λ"u8);
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
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4), new Size(80, 64)));
        var root = new ProbeContainer();
        var child = new ProbeControl { Bounds = new Rect(0, 0, 5, 4), Focusable = true };
        root.Children.Add(child);
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel });
        await application.StartAsync(TestContext.Current.CancellationToken);
        Pointer? pointer = null;
        Paste? paste = null;
        TerminalFocus? focus = null;
        Stroke? stroke = null;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(child).ShouldBeTrue();
            _ = child.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    pointer = eventArgs.Pointer;
                    CompleteIfReady();
                }
            });
            _ = child.AddHandler(Events.Paste, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    paste = eventArgs.Paste;
                    CompleteIfReady();
                }
            });
            _ = child.AddHandler(Events.TerminalFocusChanged, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    focus = eventArgs.Focus;
                    CompleteIfReady();
                }
            });
            _ = child.AddHandler(Events.Key, (_, eventArgs) =>
            {
                if (eventArgs is { Phase: RoutingPhase.Bubble, Stroke.Action: KeyAction.Repeat })
                {
                    stroke = eventArgs.Stroke;
                    CompleteIfReady();
                }
            });
        }, TestContext.Current.CancellationToken);
        var bytes = "\u001b[I\u001b[<0;17;33M\u001b[200~ok\u001b[201~\u001b[97;256:2u"u8.ToArray();

        terminal.QueueInput(bytes);
        await completed.Task.WaitAsync(TestContext.Current.CancellationToken);

        focus.ShouldBe(new TerminalFocus(gained: true));
        pointer!.Value.Pixels.ShouldBe(new Point(16, 32));
        pointer.Value.Cells.ShouldBe(new Point(2, 2));
        pointer.Value.CellPositionInferred.ShouldBeTrue();
        Encoding.UTF8.GetString(paste!.Value.Utf8.Span).ShouldBe("ok");
        stroke!.Value.Code.ShouldBe(Code.Character);
        stroke.Value.Character.ShouldBe(new Rune('a'));
        stroke.Value.Action.ShouldBe(KeyAction.Repeat);
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void CompleteIfReady()
        {
            if (pointer.HasValue && paste.HasValue && focus.HasValue && stroke.HasValue)
            {
                _ = completed.TrySetResult();
            }
        }
    }
}
