// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;




/// <summary>Verifies application startup, frame completion, suspension, and shutdown.</summary>
public sealed class ApplicationTests
{
    /// <summary>Verifies starting precedes modes and started follows layout, resize, and frame.</summary>
    [Fact]
    public async Task StartAsync_WhenFirstResizeArrives_UsesDocumentedOrderingAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        List<string> order = [];
        var root = new ProbeControl()
        {
            Measuring = _ => order.Add("layout"),
            Rendering = _ => order.Add("control-frame"),
        };
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal with { AlternateScreen = true });
        application.Starting += (_, _) => order.Add("starting");
        terminal.Written += _ => order.Add("write");
        application.Resize += (_, eventArgs) =>
        {
            root.Bounds.Width.ShouldBe(eventArgs.Dimensions.Cells.Width);
            root.Bounds.Height.ShouldBe(eventArgs.Dimensions.Cells.Height);
            order.Add("resize");
        };
        application.FrameRendered += (_, _) => order.Add("frame");
        application.Started += (_, _) => order.Add("started");

        await application.StartAsync(TestContext.Current.CancellationToken);

        order.IndexOf("starting").ShouldBeLessThan(order.IndexOf("write"));
        order.IndexOf("layout").ShouldBeLessThan(order.IndexOf("resize"));
        order.IndexOf("resize").ShouldBeLessThan(order.IndexOf("frame"));
        order.IndexOf("frame").ShouldBeLessThan(order.IndexOf("started"));
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies frame callbacks and startup wait for transport flush completion.</summary>
    [Fact]
    public async Task StartAsync_WhenFlushIsPaused_DoesNotCommitFrameEarlyAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.PauseFlush();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        var rendered = false;
        application.FrameRendered += (_, _) => rendered = true;

        var starting = application.StartAsync(TestContext.Current.CancellationToken).AsTask();
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);
        rendered.ShouldBeFalse();
        starting.IsCompleted.ShouldBeFalse();
        terminal.ReleaseFlush();
        await starting;

        rendered.ShouldBeTrue();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies zero-cell startup commits suspended layout without a frame.</summary>
    [Fact]
    public async Task StartAsync_WhenSizeIsSuspended_StartsWithoutRenderingFrameAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(0, 0)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        var frames = 0;
        application.FrameRendered += (_, _) => frames++;

        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Size.ShouldBe(new Size(0, 0));
        frames.ShouldBe(0);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies repeated stop raises lifecycle events once and restores modes.</summary>
    [Fact]
    public async Task StopAsync_WhenCalledRepeatedly_StopsAndCleansOnceAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal with { AlternateScreen = true });
        var stopping = 0;
        var stopped = 0;
        application.Stopping += (_, _) => stopping++;
        application.Stopped += (_, _) => stopped++;
        await application.StartAsync(TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);

        stopping.ShouldBe(1);
        stopped.ShouldBe(1);
        terminal.Writes.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    /// <summary>Verifies an explicit stopping preview may cancel one request.</summary>
    [Fact]
    public async Task StopAsync_WhenPreviewCancels_LeavesApplicationRunningAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        application.Stopping += Cancel;

        await application.StopAsync(TestContext.Current.CancellationToken);

        application.Completion.IsCompleted.ShouldBeFalse();
        application.Stopping -= Cancel;
        await application.StopAsync(TestContext.Current.CancellationToken);
        application.Completion.IsCompletedSuccessfully.ShouldBeTrue();

        static void Cancel(object? sender, StoppingEventArgs eventArgs)
        {
            _ = sender;
            eventArgs.Cancel = true;
        }
    }

    /// <summary>Verifies callback failure identity survives terminal cleanup.</summary>
    [Fact]
    public async Task StartAsync_WhenResizeHandlerThrows_PreservesPrimaryExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var cleanup = new IOException("cleanup");
        terminal.FailWriteNumber = 2;
        terminal.WriteFailure = cleanup;
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal with { AlternateScreen = true });
        var failure = new InvalidOperationException("resize-handler");
        application.Resize += (_, _) => throw failure;

        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await application.StartAsync(TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(failure);
        application.Failure.ShouldBeSameAs(failure);
        application.LastCleanupException.ShouldBeSameAs(cleanup);
        terminal.Writes.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    /// <summary>Verifies disposal before start still releases the owned root.</summary>
    [Fact]
    public async Task DisposeAsync_WhenNeverStarted_ReleasesOwnedResourcesAsync()
    {
        await using FakeTerminal terminal = new();
        var root = new ProbeControl();
        var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);

        await application.DisposeAsync();

        root.IsDisposed.ShouldBeTrue();
        application.Completion.IsCompletedSuccessfully.ShouldBeTrue();
    }

    /// <summary>Verifies hosted clipboard shortcuts copy, cut, paste, and retain normal edit history.</summary>
    [Fact]
    public async Task Input_WhenClipboardShortcutsTargetTextInputs_SharesApplicationBufferAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var first = new TextInput { Text = "cafe\u0301" };
        var second = new TextInput();
        var root = new Stack { Children = { first, second } };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(first).ShouldBeTrue();
            first.Select(0, first.Text.Length);
        }, TestContext.Current.CancellationToken);

        await ShortcutAsync(application, 'c');
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(second).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        await ShortcutAsync(application, 'v');

        await application.Dispatcher.InvokeAsync(() =>
        {
            second.Text.ShouldBe("cafe\u0301");
            second.CanUndo.ShouldBeTrue();
            second.Select(0, second.Text.Length);
        }, TestContext.Current.CancellationToken);
        await ShortcutAsync(application, 'x');

        await application.Dispatcher.InvokeAsync(() =>
        {
            second.Text.ShouldBeEmpty();
            second.CaretIndex.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Focus.Focus(first).ShouldBeTrue();
                first.CaretIndex = first.Text.Length;
            },
            TestContext.Current.CancellationToken);
        await ShortcutAsync(application, 'v');

        await application.Dispatcher.InvokeAsync(
            () => first.Text.ShouldBe("cafe\u0301cafe\u0301"),
            TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies password-suppressed copy and an empty initial buffer never disclose or mutate text.</summary>
    [Fact]
    public async Task Input_WhenClipboardHasNoPublishableText_PreservesBufferAndDocumentAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 4)));
        var source = new TextInput { Text = "safe" };
        var password = new TextInput { Text = "secret", PasswordCharacter = new Rune('*') };
        var target = new TextInput();
        var root = new Stack { Children = { source, password, target } };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(target).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        await ShortcutAsync(application, 'v');
        await application.Dispatcher.InvokeAsync(() =>
        {
            target.Text.ShouldBeEmpty();
            application.Focus.Focus(source).ShouldBeTrue();
            source.Select(0, source.Text.Length);
        }, TestContext.Current.CancellationToken);
        await ShortcutAsync(application, 'c');
        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(password).ShouldBeTrue();
            password.Select(0, password.Text.Length);
        }, TestContext.Current.CancellationToken);
        await ShortcutAsync(application, 'c');
        await application.Dispatcher.InvokeAsync(
            () => application.Focus.Focus(target).ShouldBeTrue(),
            TestContext.Current.CancellationToken);
        await ShortcutAsync(application, 'v');

        await application.Dispatcher.InvokeAsync(() =>
        {
            target.Text.ShouldBe("safe");
            password.Text.ShouldBe("secret");
        }, TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task ShortcutAsync(Application application, char character)
    {
        var stroke = new Stroke(
            Code.Character,
            new Rune(character),
            nativeCode: 0,
            Modifiers.Control,
            KeyAction.Press);
        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);
    }
}
