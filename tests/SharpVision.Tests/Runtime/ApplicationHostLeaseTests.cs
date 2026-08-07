// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies the application owns and disposes an injected console host restore lease.</summary>
public sealed class ApplicationHostLeaseTests
{
    /// <summary>Verifies disposal before start still disposes the owned host lease exactly once.</summary>
    [Fact]
    public async Task DisposeAsync_WhenNeverStarted_DisposesHostLeaseOnceAsync()
    {
        await using FakeTerminal terminal = new();
        var lease = new TrackingLease();
        var application = new Application(
            new ProbeControl(),
            terminal,
            terminal,
            options: null,
            hostLease: lease);

        await application.DisposeAsync();

        lease.Disposals.ShouldBe(1);
    }
}
