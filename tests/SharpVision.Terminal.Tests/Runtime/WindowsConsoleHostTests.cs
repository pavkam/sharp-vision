// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>
/// Verifies that the Windows console host releases every resource it opens on a partially
/// constructed failure path, mirroring <see cref="UnixConsoleHostTests"/> for the Unix host.
/// </summary>
/// <remarks>
/// These tests need a real interactive console because <see cref="WindowsConsoleMode.Enter"/>
/// reads and writes the process's actual standard-handle console modes with no injection seam.
/// They skip when the process has none, which is the case for a redirected build agent, matching
/// <see cref="WindowsConsoleModeTests"/>'s convention.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsConsoleHostTests
{
    /// <summary>
    /// Verifies a failure raised after the input, output, and transport streams already exist
    /// disposes the transport, restores the saved console modes, and still surfaces the original
    /// exception to the caller.
    /// </summary>
    [Fact]
    public async Task Open_WhenResizeSourceCreationFails_DisposesTransportAndRestoresModesAsync()
    {
        SkipWithoutWindowsConsole();
        var options = new ConsoleHostOptions();
        var inputHandle = RuntimeInterop.GetStandardHandle(RuntimeInterop.StdInputHandle);
        var outputHandle = RuntimeInterop.GetStandardHandle(RuntimeInterop.StdOutputHandle);
        _ = RuntimeInterop.TryGetConsoleMode(inputHandle, out var savedInput);
        _ = RuntimeInterop.TryGetConsoleMode(outputHandle, out var savedOutput);
        var failure = new IOException("resize source unavailable");
        StreamTransport? capturedTransport = null;

        var thrown = Should.Throw<IOException>(() => WindowsConsoleHost.Open(
            options,
            transport =>
            {
                capturedTransport = transport;
                throw failure;
            }));

        thrown.ShouldBeSameAs(failure);
        var transport = capturedTransport.ShouldNotBeNull();

        // The transport was constructed with leaveOpen: true, so its own DisposeAsync never
        // touches the input/output streams - proving it ran at all requires observing its own
        // disposed state directly, which a subsequent write surfaces as ObjectDisposedException.
        _ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
            await transport.WriteAsync(
                ReadOnlyMemory<byte>.Empty,
                TestContext.Current.CancellationToken));

        // The console-mode lease was entered first and released last; if the catch block never
        // reached it, the VT modes this test observed on entry would still be applied here.
        _ = RuntimeInterop.TryGetConsoleMode(inputHandle, out var restoredInput);
        _ = RuntimeInterop.TryGetConsoleMode(outputHandle, out var restoredOutput);
        restoredInput.ShouldBe(savedInput);
        restoredOutput.ShouldBe(savedOutput);
    }

    private static void SkipWithoutWindowsConsole()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The Windows console host requires Windows.");
        Assert.SkipUnless(
            RuntimeInterop.TryGetConsoleMode(RuntimeInterop.GetStandardHandle(RuntimeInterop.StdInputHandle), out _),
            "The Windows console host requires a real console.");
    }
}
