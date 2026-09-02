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

    /// <summary>Verifies a second concurrent <see cref="SystemConsoleHost.Open"/> call is rejected
    /// before the platform boundary runs while the first connection is still live, so the second
    /// call never reaches <c>Enter()</c> and snapshots the first connection's already-modified
    /// state as its own restore target.</summary>
    [Fact]
    public async Task Open_WhenAConnectionIsAlreadyOpen_RejectsBeforePlatformOpenAsync()
    {
        var platformOpenCount = 0;
        var host = new SystemConsoleHost(
            isInteractive: static () => true,
            openPlatform: _ =>
            {
                platformOpenCount++;
                return new ConsoleConnection(new FakeTransport(), new FakeResizeSource(), new TrackingRestore());
            });

        await using var first = host.Open(new ConsoleHostOptions());

        var thrown = Should.Throw<InvalidOperationException>(() => host.Open(new ConsoleHostOptions()));

        thrown.Message.ShouldContain("already open");
        platformOpenCount.ShouldBe(1);
    }

    /// <summary>Verifies disposing the first connection releases the guard, so a legitimate
    /// sequential open -> close -> open still succeeds instead of permanently locking the host
    /// out of ever opening a connection again.</summary>
    [Fact]
    public async Task Open_WhenTheFirstConnectionIsDisposed_AllowsASubsequentOpenAsync()
    {
        var platformOpenCount = 0;
        var host = new SystemConsoleHost(
            isInteractive: static () => true,
            openPlatform: _ =>
            {
                platformOpenCount++;
                return new ConsoleConnection(new FakeTransport(), new FakeResizeSource(), new TrackingRestore());
            });

        var first = host.Open(new ConsoleHostOptions());
        await first.DisposeAsync();

        await using var second = host.Open(new ConsoleHostOptions());

        platformOpenCount.ShouldBe(2);
    }

    /// <summary>Verifies the ordinary single-open happy path is unaffected by the guard: opening
    /// once still reaches the platform boundary and returns its connection.</summary>
    [Fact]
    public async Task Open_WhenNoConnectionIsOpen_ReturnsThePlatformConnectionAsync()
    {
        var restore = new TrackingRestore();
        var host = new SystemConsoleHost(
            isInteractive: static () => true,
            openPlatform: _ => new ConsoleConnection(new FakeTransport(), new FakeResizeSource(), restore));

        await using var connection = host.Open(new ConsoleHostOptions());

        _ = connection.ShouldNotBeNull();
    }
}
