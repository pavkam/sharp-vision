// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;

/// <summary>Verifies the application owns and disposes an injected console host restore lease.</summary>
public sealed class ApplicationHostLeaseTests
{
    private sealed class TrackingLease: IAsyncDisposable
    {
        internal int Disposals { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposals++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Verifies disposal before start still disposes the owned host lease exactly once.</summary>
    [Fact]
    public async Task DisposeAsync_WhenNeverStarted_DisposesHostLeaseOnceAsync()
    {
        await using FakeTerminal terminal = new();
        TrackingLease lease = new();
        Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            options: null,
            hostLease: lease);

        await application.DisposeAsync();

        lease.Disposals.ShouldBe(1);
    }
}
