// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Runtime;
using SharpVision.Terminal.Tests.Support;

/// <summary>
/// Verifies console connection initialization and asynchronous disposal behavior.
/// </summary>
public sealed class ConsoleConnectionTests
{
    private sealed class TrackingRestore: IDisposable
    {
        public int Disposals { get; private set; }

        public void Dispose() => Disposals++;
    }

    /// <summary>
    /// Verifies that DisposeAsync restores the lease exactly once when called multiple times.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenCalledTwice_RestoresExactlyOnce()
    {
        TrackingRestore restore = new();
        ConsoleConnection connection = new(new FakeTransport(), new FakeResizeSource(), restore);

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
}
