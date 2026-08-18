// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>
/// Verifies that the Unix console host releases every descriptor it opens, on the success path and
/// on a partially constructed failure path.
/// </summary>
/// <remarks>
/// These tests require a controlling terminal because the host opens <c>/dev/tty</c> and enters raw
/// mode on it. They skip when the process has none, which is the case for a redirected build agent.
/// </remarks>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class UnixConsoleHostTests
{
    /// <summary>
    /// Verifies repeated complete host lifecycles close every <c>/dev/tty</c> handle they opened.
    /// </summary>
    [Fact]
    public async Task Open_WhenConsoleLifecycleCompletes_ClosesOwnedTtyDescriptorAsync()
    {
        SkipWithoutControllingTerminal();
        var options = new ConsoleHostOptions();
        List<SafeFileHandle> handles = [];

        for (var iteration = 0; iteration < 4; iteration++)
        {
            var connection = UnixConsoleHost.Open(options, handle =>
            {
                handles.Add(handle);
                return new UnixResizeSource((int) handle.DangerousGetHandle());
            });
            handles[^1].IsClosed.ShouldBeFalse();

            // Exactly the documented shutdown chain: the session disposes resize and transport,
            // then the application disposes the host lease.
            await connection.Resize.DisposeAsync();
            await connection.Transport.DisposeAsync();
            await connection.DisposeAsync();
        }

        handles.Count.ShouldBe(4);
        handles.ShouldAllBe(handle => handle.IsClosed);
    }

    /// <summary>
    /// Verifies a failure raised after the tty stream and transport already exist unwinds both and
    /// still surfaces the original exception to the caller.
    /// </summary>
    [Fact]
    public void Open_WhenResizeSourceCreationFails_DisposesInputStreamAndPreservesPrimaryException()
    {
        SkipWithoutControllingTerminal();
        var options = new ConsoleHostOptions();
        var failure = new IOException("resize source unavailable");
        SafeFileHandle? handle = null;

        var thrown = Should.Throw<IOException>(() => UnixConsoleHost.Open(
            options,
            captured =>
            {
                handle = captured;
                throw failure;
            }));

        thrown.ShouldBeSameAs(failure);
        _ = handle.ShouldNotBeNull();
        handle.IsClosed.ShouldBeTrue();
    }

    private static void SkipWithoutControllingTerminal()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "The Unix console host requires Linux or macOS.");
        Assert.SkipUnless(
            HasControllingTerminal(),
            "The Unix console host requires a controlling terminal.");
    }

    private static bool HasControllingTerminal()
    {
        try
        {
            using var probe = new FileStream("/dev/tty", FileMode.Open, FileAccess.Read);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
