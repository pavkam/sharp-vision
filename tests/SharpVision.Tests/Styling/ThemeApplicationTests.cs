namespace SharpVision.Tests.Styling;

using SharpVision.Controls;
using SharpVision.Runtime;
using SharpVision.Styling;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Runtime;
using SharpVision.Tests.Support;

using Shouldly;

using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Verifies application-wide theme ownership and propagation.</summary>
public sealed class ThemeApplicationTests
{
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
}
