using System.Runtime.Versioning;

using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Runtime;
using SharpVision.Terminal.Tests.Support;
using SharpVision.Terminal.Transport;

using Shouldly;

namespace SharpVision.Terminal.Tests.Transport;

/// <summary>
/// Verifies real Unix pseudoterminal transport, EOF, and resize behavior.
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class PseudoterminalTests
{
    /// <summary>Verifies exact bytes cross a real PTY in both directions.</summary>
    [Fact]
    public async Task ReadWriteAsync_WhenUsingUnixPseudoterminal_TransfersExactBytesAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "Unix pseudoterminals require Linux or macOS.");
        await using var terminal = UnixPseudoterminal.Open();
        await using var transport = new StreamTransport(
            terminal.Slave,
            terminal.Slave,
            leaveOpen: true);
        var destination = new byte[5];

        await terminal.Master.WriteAsync(
            "input"u8.ToArray(),
            TestContext.Current.CancellationToken);
        await terminal.Master.FlushAsync(TestContext.Current.CancellationToken);
        var read = await transport.ReadAsync(
            destination,
            TestContext.Current.CancellationToken);
        await transport.WriteAsync(
            "output"u8.ToArray(),
            TestContext.Current.CancellationToken);
        await transport.FlushAsync(TestContext.Current.CancellationToken);
        var output = new byte[6];
        var written = await terminal.Master.ReadAsync(
            output,
            TestContext.Current.CancellationToken);

        read.ShouldBe(destination.Length);
        destination.ShouldBe("input"u8.ToArray());
        written.ShouldBe(output.Length);
        output.ShouldBe("output"u8.ToArray());
    }

    /// <summary>Verifies closing the PTY master becomes transport EOF.</summary>
    [Fact]
    public async Task ReadAsync_WhenPseudoterminalMasterCloses_ReturnsEndOfFileAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "Unix pseudoterminals require Linux or macOS.");
        await using var terminal = UnixPseudoterminal.Open();
        await using var transport = new StreamTransport(
            terminal.Slave,
            terminal.Slave,
            leaveOpen: true);
        await terminal.CloseMasterAsync();

        var read = await transport.ReadAsync(
            new byte[1],
            TestContext.Current.CancellationToken);

        read.ShouldBe(0);
    }

    /// <summary>Verifies SIGWINCH makes the newest cell and pixel size observable.</summary>
    [Fact]
    public async Task ReadAsync_WhenPseudoterminalResizes_ReturnsNewestDimensionsAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "Unix pseudoterminals require Linux or macOS.");
        var initial = new Dimensions(new Size(80, 24), new Size(800, 480));
        var expected = new Dimensions(new Size(132, 43), initial.Pixels);
        await using var terminal = UnixPseudoterminal.Open(initial);
        await using var source = new UnixResizeSource(terminal.SlaveDescriptor);

        var first = await source.ReadAsync(TestContext.Current.CancellationToken);
        terminal.SetWindowSize(expected);
        terminal.SignalWindowChange();
        var resized = await source.ReadAsync(TestContext.Current.CancellationToken);

        first.ShouldBe(initial);
        resized.ShouldBe(expected);
    }

    /// <summary>Verifies the native size boundary without signal delivery.</summary>
    [Fact]
    public async Task GetDimensions_WhenPseudoterminalHasSize_ReturnsExactDimensionsAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "Unix pseudoterminals require Linux or macOS.");
        var expected = new Dimensions(new Size(91, 31), new Size(1092, 620));
        await using var terminal = UnixPseudoterminal.Open(expected);

        var actual = Native.GetDimensions(terminal.SlaveDescriptor);

        actual.ShouldBe(expected);
    }

    /// <summary>Verifies a real SIGWINCH resize crosses the serialized runtime.</summary>
    [Fact]
    public async Task RunAsync_WhenPseudoterminalResizes_DeliversNewestDimensionsAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "Unix pseudoterminals require Linux or macOS.");
        var initial = new Dimensions(new Size(80, 24), new Size(800, 480));
        var expected = new Dimensions(new Size(144, 48), initial.Pixels);
        await using var terminal = UnixPseudoterminal.Open(initial);
        await using var transport = new StreamTransport(
            terminal.Slave,
            terminal.Slave,
            leaveOpen: true);
        await using var source = new UnixResizeSource(terminal.SlaveDescriptor);
        var sink = new RuntimeSink(expected);
        await using var session = new Session(
            transport,
            source,
            sink,
            Terminal.Runtime.Options.Minimal);
        var running = session.RunAsync(TestContext.Current.CancellationToken).AsTask();

        terminal.SetWindowSize(expected);
        terminal.SignalWindowChange();
        var resized = await sink.Expected.Task.WaitAsync(TestContext.Current.CancellationToken);
        await terminal.CloseMasterAsync();
        await running;

        resized.ShouldBe(expected);
        sink.Faults.ShouldBeEmpty();
    }

    private sealed class RuntimeSink(Dimensions expected): ISink
    {
        internal TaskCompletionSource<Dimensions> Expected { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal List<Exception> Faults { get; } = [];

        public void Input(in Stroke value)
        {
        }

        public void Input(in Text value)
        {
        }

        public void Input(in Pointer value)
        {
        }

        public void Input(Paste value)
        {
        }

        public void Input(in Focus value)
        {
        }

        public void Input(in Diagnostic value)
        {
        }

        public void Resize(in Dimensions value)
        {
            if (value == expected)
            {
                _ = Expected.TrySetResult(value);
            }
        }

        public void Closed()
        {
        }

        public void Fault(Exception exception) => Faults.Add(exception);
    }
}
