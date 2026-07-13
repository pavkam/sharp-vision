namespace SharpVision.Tests.Runtime;

using SharpVision.Input;
using SharpVision.Runtime;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Runtime;
using SharpVision.Tests.Support;

using Shouldly;

using KeyAction = Terminal.Input.Action;
using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Verifies resize coalescing, input targeting, and application idleness.</summary>
public sealed class OrderingTests
{
    /// <summary>Verifies a blocked dispatcher observes only the newest resize in a storm.</summary>
    [Fact]
    public async Task Resize_WhenSeveralArriveBeforeDrain_CoalescesNewestAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using var application = new Application(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.Dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var sizes = new List<Size>();
        application.Resize += (_, eventArgs) => sizes.Add(eventArgs.Dimensions.Cells);

        terminal.QueueResize(new Dimensions(new Size(20, 5)));
        terminal.QueueResize(new Dimensions(new Size(30, 6)));
        terminal.QueueResize(new Dimensions(new Size(40, 7)));
        await Task.Delay(20, TestContext.Current.CancellationToken);
        release.Set();
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        sizes.ShouldBe([new Size(40, 7)]);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies key input routes to the manager's current focus target.</summary>
    [Fact]
    public async Task Input_WhenFocusExists_RoutesTypedKeyToFocusedControlAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var root = new ProbeContainer();
        var child = new ProbeControl { CanFocus = true };
        root.Children.Add(child);
        await using var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var phases = new List<Phase>();
        await application.Dispatcher.InvokeAsync(() =>
        {
            application.Focus.Focus(child).ShouldBeTrue();
            _ = child.AddHandler(Events.Key, (_, eventArgs) =>
                phases.Add(eventArgs.Phase));
        }, TestContext.Current.CancellationToken);
        var stroke = new Stroke(
            Code.Enter,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press);

        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        phases.ShouldBe([Phase.Preview, Phase.Bubble]);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies input received before initial resize is retained until attachment.</summary>
    [Fact]
    public async Task Input_WhenReceivedBeforeResize_DeliversAfterTreeAttachmentAsync()
    {
        await using var terminal = new FakeTerminal();
        var root = new ProbeContainer();
        var calls = 0;
        _ = root.AddHandler(Events.Focus, (_, _) => calls++);
        await using var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        var focus = new Focus(gained: true);
        application.Input(in focus);
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        calls.ShouldBe(2);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies resize-handler invalidation is laid out before frame production.</summary>
    [Fact]
    public async Task Resize_WhenHandlerInvalidatesLayout_ReflowsBeforeFrameAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var root = new ProbeControl();
        await using var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        application.Resize += (_, _) => root.Width = SharpVision.Layout.Length.Cells(5);
        root.Rendering = _ => root.Bounds.Width.ShouldBe(5);

        await application.StartAsync(TestContext.Current.CancellationToken);

        root.Bounds.Width.ShouldBe(5);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a terminal fault is primary and forces stopped completion.</summary>
    [Fact]
    public async Task Fault_WhenSessionReportsFailure_StopsWithOriginalExceptionAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using var application = new Application(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var failure = new IOException("terminal");

        application.Fault(failure);
        var thrown = await Should.ThrowAsync<IOException>(application.Completion);

        thrown.ShouldBeSameAs(failure);
        application.Failure.ShouldBeSameAs(failure);
    }

    /// <summary>Verifies idle-posted work drains before the next idle transition.</summary>
    [Fact]
    public async Task Idle_WhenHandlerPostsWork_DrainsBeforeSecondIdleAsync()
    {
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using var application = new Application(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        var order = new List<string>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.Idle += (_, _) =>
        {
            order.Add("idle");

            if (order.Count == 1)
            {
                application.Dispatcher.Post(() => order.Add("work"));
            }
            else
            {
                completed.SetResult();
            }
        };

        await application.StartAsync(TestContext.Current.CancellationToken);
        await completed.Task.WaitAsync(TestContext.Current.CancellationToken);

        order.ShouldBe(["idle", "work", "idle"]);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }
}
