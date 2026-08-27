// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>Verifies the real platform host enforces its interactive-stream precondition before
/// any platform mode or stream boundary can run.</summary>
public sealed class SystemConsoleHostTests
{
    /// <summary>Verifies redirected standard streams are rejected by the lower-level public host
    /// path, rather than entering raw mode and sending terminal output into a redirect.</summary>
    [Fact]
    public void Open_WhenStandardStreamIsRedirected_RejectsBeforePlatformOpen()
    {
        var platformOpenCalled = false;
        var host = new SystemConsoleHost(
            isInteractive: static () => false,
            openPlatform: _ =>
            {
                platformOpenCalled = true;
                throw new InvalidOperationException("The platform boundary must not run.");
            });

        var thrown = Should.Throw<InvalidOperationException>(() => host.Open(new ConsoleHostOptions()));

        thrown.Message.ShouldContain("interactive");
        platformOpenCalled.ShouldBeFalse();
    }
}
