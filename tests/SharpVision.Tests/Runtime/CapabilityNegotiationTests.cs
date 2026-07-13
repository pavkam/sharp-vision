namespace SharpVision.Tests.Runtime;

using System.Text;

using SharpVision.Runtime;
using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Runtime;
using SharpVision.Tests.Support;

using Shouldly;

using Ambiguous = Terminal.Unicode.Ambiguous;
using CapabilityOrigin = Terminal.Capabilities.Origin;
using CapabilitySupport = Terminal.Capabilities.Support;
using TerminalCapabilities = Terminal.Capabilities.Capabilities;
using TerminalOptions = Terminal.Runtime.Options;

/// <summary>Verifies negotiated profiles become active before layout and rendering.</summary>
public sealed class CapabilityNegotiationTests
{
    /// <summary>Verifies the first frame uses the profile published before its resize.</summary>
    [Fact]
    public async Task StartAsync_WhenNegotiationCompletes_AppliesProfileBeforeFirstFrameAsync()
    {
        // Arrange
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        var root = new ProbeControl { Content = "x".AsMemory() };
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(new Dictionary<string, string?>()),
        };
        await using var application = new Application(root, terminal, terminal, options);
        var order = new List<string>();
        var queryWritten = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CapabilitiesChangedEventArgs? changed = null;
        terminal.Written += value =>
        {
            if (Encoding.ASCII.GetString(value.Span).StartsWith("\u001b[?u", StringComparison.Ordinal))
            {
                _ = queryWritten.TrySetResult();
            }
        };
        application.CapabilitiesChanged += (_, eventArgs) =>
        {
            application.Dispatcher.CheckAccess().ShouldBeTrue();
            changed = eventArgs;
            order.Add("capabilities");
        };
        application.Resize += (_, _) => order.Add("resize");
        application.FrameRendered += (_, _) => order.Add("frame");

        // Act
        var starting = application.StartAsync(TestContext.Current.CancellationToken).AsTask();
        await queryWritten.Task.WaitAsync(TestContext.Current.CancellationToken);
        starting.IsCompleted.ShouldBeFalse();
        terminal.Writes.Count.ShouldBe(1);
        terminal.QueueInput(Encoding.ASCII.GetBytes(
            "\u001b[?1016;1$y\u001b[?1006;1$y\u001b[?2004;1$y" +
            "\u001b[?1004;1$y\u001b[?2026;1$y\u001b[?3u\u001b[?1;2c"));
        await starting;

        // Assert
        var eventArgs = changed.ShouldNotBeNull();
        eventArgs.Previous.ShouldBeSameAs(TerminalCapabilities.Conservative);
        eventArgs.Current.ShouldBeSameAs(application.Capabilities);
        application.Capabilities.SynchronizedOutput.IsSupported.ShouldBeTrue();
        order.ShouldBe(["capabilities", "resize", "frame"]);
        var frame = Encoding.UTF8.GetString(terminal.Writes[^1]);
        frame.ShouldStartWith("\u001b[?2026h");
        frame.ShouldEndWith("\u001b[?2026l");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a profile arriving during output affects only the next frame.</summary>
    [Fact]
    public async Task Profile_WhenRenderIsPaused_CoalescesIntoNextFrameAsync()
    {
        // Arrange
        await using var terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        var root = new ProbeControl { Content = "·".AsMemory() };
        await using var application = new Application(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        var changes = new List<CapabilitiesChangedEventArgs>();
        application.CapabilitiesChanged += (_, eventArgs) => changes.Add(eventArgs);
        await application.StartAsync(TestContext.Current.CancellationToken);
        terminal.PauseFlush();
        var synchronizedWrite = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thirdFrame = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var frames = 1;
        terminal.Written += value =>
        {
            if (value.Span.StartsWith("\u001b[?2026h"u8))
            {
                _ = synchronizedWrite.TrySetResult();
            }
        };
        application.FrameRendered += (_, _) =>
        {
            frames++;

            if (frames == 3)
            {
                _ = thirdFrame.TrySetResult();
            }
        };
        var supported = new Feature(CapabilitySupport.Supported, CapabilityOrigin.Override);
        var unsupported = new Feature(CapabilitySupport.Unsupported, CapabilityOrigin.Override);
        var first = TerminalCapabilities.Conservative with
        {
            SynchronizedOutput = supported,
        };
        var second = TerminalCapabilities.Conservative with
        {
            SynchronizedOutput = unsupported,
            AmbiguousWidth = Ambiguous.Wide,
        };

        // Act
        application.Profile(first);
        await synchronizedWrite.Task.WaitAsync(TestContext.Current.CancellationToken);
        application.Profile(second);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);
        terminal.ReleaseFlush();
        await thirdFrame.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        // Assert
        application.Capabilities.ShouldBeSameAs(second);
        changes.Count.ShouldBe(2);
        changes[0].Previous.ShouldBeSameAs(TerminalCapabilities.Conservative);
        changes[0].Current.ShouldBeSameAs(first);
        changes[1].Previous.ShouldBeSameAs(first);
        changes[1].Current.ShouldBeSameAs(second);
        Encoding.UTF8.GetString(terminal.Writes[^2]).ShouldStartWith("\u001b[?2026h");
        Encoding.UTF8.GetString(terminal.Writes[^1]).ShouldNotStartWith("\u001b[?2026h");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }
}
