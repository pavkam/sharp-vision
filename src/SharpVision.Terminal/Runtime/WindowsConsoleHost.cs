// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using Capabilities;

using InstantHandle = JetBrains.Annotations.InstantHandleAttribute;
using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;

/// <summary>Opens an interactive console on Windows using VT input and VT processing.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsConsoleHost
{
    /// <summary>Enters VT console mode and opens standard streams and a polling resize source.</summary>
    /// <param name="options">The validated host policy.</param>
    /// <returns>A connection whose disposal restores the saved console modes.</returns>
    /// <exception cref="IOException">The console mode cannot be configured.</exception>
    [MustDisposeResource]
    public static ConsoleConnection Open(ConsoleHostOptions options) =>
        Open(
            options,
            // The standard Windows console does not report pixel dimensions, so resize is
            // cell-only polling and needs nothing from the already-open transport.
            _ => new ConsoleResizeSource(options.ResizeInterval));

    /// <summary>
    /// Enters VT console mode and opens standard streams, a resize source built by the supplied
    /// factory, and a restore lease.
    /// </summary>
    /// <remarks>
    /// The factory exists for the same test obligation as <see cref="UnixConsoleHost"/>'s
    /// equivalent overload. It can fail after the input, output, and transport streams already
    /// exist, which is the only ordering in which a partially constructed console must unwind,
    /// and it exposes the already-wired transport so a test can assert that transport is
    /// genuinely disposed at the end of the failure path.
    /// </remarks>
    /// <param name="options">The validated host policy.</param>
    /// <param name="createResize">Builds the resize source from the already-open transport.</param>
    /// <returns>A connection whose disposal restores the saved console modes.</returns>
    /// <exception cref="IOException">The console mode cannot be configured.</exception>
    [MustDisposeResource]
    internal static ConsoleConnection Open(
        ConsoleHostOptions options,
        [InstantHandle] Func<StreamTransport, IResizeSource> createResize)
    {
        Debug.Assert(createResize is not null, "The console host always supplies a resize factory.");
        var mode = WindowsConsoleMode.Enter(options.CaptureControlKeys);
        WindowsConsoleInputStream? input = null;
        Stream? output = null;
        StreamTransport? transport = null;
        IResizeSource? resize = null;

        try
        {
            input = new WindowsConsoleInputStream(
                Console.OpenStandardInput(bufferSize: 1),
                RuntimeInterop.GetStandardHandle(RuntimeInterop.StdInputHandle));
            output = Console.OpenStandardOutput();
            transport = new StreamTransport(input, output, leaveOpen: true);
            resize = createResize(transport);

            return new ConsoleConnection(
                transport,
                resize,
                mode,
                DescriptionPlatform.Windows,
                outputFileDescriptor: RuntimeInterop.StandardOutputFileDescriptor,
                windowsVirtualTerminal: true);
        }
        catch
        {
            // Unwind in exact reverse construction order, mirroring UnixConsoleHost.Open: every
            // resource actually constructed before the failure is released, and the console-mode
            // lease is restored last because it was entered first.
            ConsoleHostResourceRelease.ReleaseResource(resize);
            ConsoleHostResourceRelease.ReleaseResource(transport);
            ConsoleHostResourceRelease.ReleaseResource(output);
            ConsoleHostResourceRelease.ReleaseResource(input);
            ConsoleHostResourceRelease.ReleaseMode(mode);

            throw;
        }
    }
}
