// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Runtime;


/// <summary>
/// Verifies console connection initialization and asynchronous disposal behavior.
/// </summary>
public sealed class ConsoleConnectionTests
{
    /// <summary>
    /// Verifies that DisposeAsync restores the lease exactly once when called multiple times.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenCalledTwice_RestoresExactlyOnceAsync()
    {
        var restore = new TrackingRestore();
        var connection = new ConsoleConnection(new FakeTransport(), new FakeResizeSource(), restore);

        await connection.DisposeAsync();
        await connection.DisposeAsync();

        restore.Disposals.ShouldBe(1);
    }

    /// <summary>
    /// Verifies that a null restore lease throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void Constructor_WhenRestoreNull_Throws()
    {
        _ = Should.Throw<ArgumentNullException>(
            () => new ConsoleConnection(new FakeTransport(), new FakeResizeSource(), restore: null!));
    }

    /// <summary>
    /// Verifies that DisposeAsync restores the lease without disposing the transport, which the
    /// caller (e.g. the owning <c>Application</c>) is responsible for disposing.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenCalled_DoesNotDisposeTransportOrResizeAsync()
    {
        var transport = new FakeTransport();
        var connection = new ConsoleConnection(transport, new FakeResizeSource(), new TrackingRestore());

        await connection.DisposeAsync();

        transport.DisposeCount.ShouldBe(0);
    }
}
