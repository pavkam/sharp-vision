// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Transport;

using System.Runtime.Versioning;

using SharpVision.Terminal.Runtime;
using SharpVision.Terminal.Transport;


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
        await using UnixPseudoterminal terminal = UnixPseudoterminal.Open();
        await using StreamTransport transport = new(
            terminal.Slave,
            terminal.Slave,
            leaveOpen: true);
        byte[] destination = new byte[5];

        await terminal.Master.WriteAsync(
            "input"u8.ToArray(),
            TestContext.Current.CancellationToken);
        await terminal.Master.FlushAsync(TestContext.Current.CancellationToken);
        int read = await transport.ReadAsync(
            destination,
            TestContext.Current.CancellationToken);
        await transport.WriteAsync(
            "output"u8.ToArray(),
            TestContext.Current.CancellationToken);
        await transport.FlushAsync(TestContext.Current.CancellationToken);
        byte[] output = new byte[6];
        int written = await terminal.Master.ReadAsync(
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
        await using UnixPseudoterminal terminal = UnixPseudoterminal.Open();
        await using StreamTransport transport = new(
            terminal.Slave,
            terminal.Slave,
            leaveOpen: true);
        await terminal.CloseMasterAsync();

        int read = await transport.ReadAsync(
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
        Dimensions initial = new(new Size(80, 24), new Size(800, 480));
        Dimensions expected = new(new Size(132, 43), initial.Pixels);
        await using UnixPseudoterminal terminal = UnixPseudoterminal.Open(initial);
        await using UnixResizeSource source = new(terminal.SlaveDescriptor);

        Dimensions first = await source.ReadAsync(TestContext.Current.CancellationToken);
        terminal.SetWindowSize(expected);
        terminal.SignalWindowChange();
        Dimensions resized = await source.ReadAsync(TestContext.Current.CancellationToken);

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
        Dimensions expected = new(new Size(91, 31), new Size(1092, 620));
        await using UnixPseudoterminal terminal = UnixPseudoterminal.Open(expected);

        Dimensions actual = Native.GetDimensions(terminal.SlaveDescriptor);

        actual.ShouldBe(expected);
    }

    /// <summary>Verifies a real SIGWINCH resize crosses the serialized runtime.</summary>
    [Fact]
    public async Task RunAsync_WhenPseudoterminalResizes_DeliversNewestDimensionsAsync()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "Unix pseudoterminals require Linux or macOS.");
        Dimensions initial = new(new Size(80, 24), new Size(800, 480));
        Dimensions expected = new(new Size(144, 48), initial.Pixels);
        await using UnixPseudoterminal terminal = UnixPseudoterminal.Open(initial);
        await using StreamTransport transport = new(
            terminal.Slave,
            terminal.Slave,
            leaveOpen: true);
        await using UnixResizeSource source = new(terminal.SlaveDescriptor);
        RuntimeSink sink = new(expected);
        await using Session session = new(
            transport,
            source,
            sink,
            Options.Minimal);
        Task running = session.RunAsync(TestContext.Current.CancellationToken).AsTask();

        terminal.SetWindowSize(expected);
        terminal.SignalWindowChange();
        Dimensions resized = await sink.Expected.Task.WaitAsync(TestContext.Current.CancellationToken);
        await terminal.CloseMasterAsync();
        await running;

        resized.ShouldBe(expected);
        sink.Faults.ShouldBeEmpty();
    }

}
