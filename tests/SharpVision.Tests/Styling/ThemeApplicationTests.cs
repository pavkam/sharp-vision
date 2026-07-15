// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

/// <summary>Verifies application-wide theme ownership and propagation.</summary>
public sealed class ThemeApplicationTests
{
    /// <summary>Verifies mutating an installed theme republishes and renders a semantic color.</summary>
    [Fact]
    public async Task SetColor_WhenApplicationUsesMutableTheme_RepublishesResolvedColorAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var root = new ProbeControl() { Content = "x".AsMemory() };
        await using var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        var theme = new Theme();
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, ThemeColors.Accent);
        theme.SetStyle(style);
        theme.SetColor(ColorRole.Accent, Color.Indexed(2));

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Theme = theme;
            },
            TestContext.Current.CancellationToken);
        await application.StartAsync(TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                ThemeTestSupport.Resolve(root, Control.ForegroundProperty, State.Normal)
                    .ShouldBe(Color.Indexed(2));
            },
            TestContext.Current.CancellationToken);

        var previousWrites = terminal.Writes.Count;
        var rendered = NextFrame(application);

        await application.Dispatcher.InvokeAsync(
            () => theme.SetColor(ColorRole.Accent, Color.Indexed(9)),
            TestContext.Current.CancellationToken);
        await rendered.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                using var frame = new Frame(application.Size);
                root.Render(frame.Canvas);
                frame.GetCell(default).Style.Foreground.ShouldBe(Color.Indexed(9));
            },
            TestContext.Current.CancellationToken);
        terminal.Writes.Count.ShouldBeGreaterThan(previousWrites);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a started application publishes the default dark theme.</summary>
    [Fact]
    public async Task StartAsync_WhenNoThemeIsAssigned_UsesDarkThemeAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using var application = new Application(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);

        await application.StartAsync(TestContext.Current.CancellationToken);

        ReferenceEquals(application.Theme, Themes.Dark).ShouldBeTrue();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies assigning a new theme republishes resolved values on the tree.</summary>
    [Fact]
    public async Task Theme_WhenSwitchedToWhite_RepublishesResolvedForegroundAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var root = new ProbeControl();
        await using var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                application.Theme = Themes.White;
            },
            TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                ThemeTestSupport.Resolve(root, Control.ForegroundProperty, State.Normal)
                    .ShouldBe(Color.Indexed(0));
            },
            TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies off-dispatcher theme assignment posts to the dispatcher.</summary>
    [Fact]
    public async Task Theme_WhenAssignedOffDispatcher_PostsChangeAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using var application = new Application(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        application.Theme = Themes.White;

        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (ReferenceEquals(application.Theme, Themes.White))
            {
                break;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        ReferenceEquals(application.Theme, Themes.White).ShouldBeTrue();
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static Task NextFrame(Application application)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += Complete;
        return completion.Task;

        void Complete(object? sender, FrameRenderedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            application.FrameRendered -= Complete;
            _ = completion.TrySetResult();
        }
    }
}
