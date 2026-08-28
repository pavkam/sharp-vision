// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies opaque latest-wins operation ownership and cancellation.</summary>
public sealed class LatestControlOperationTests
{
    /// <summary>Verifies replacement revokes the prior lease without retiring the new one.</summary>
    [Fact]
    public void Begin_WhenReplacingCurrent_CancelsOnlyPreviousLease()
    {
        var operation = new LatestControlOperation();
        var first = operation.Begin();

        var second = operation.Begin();

        first.CancellationToken.IsCancellationRequested.ShouldBeTrue();
        operation.IsCurrent(first).ShouldBeFalse();
        operation.IsCurrent(second).ShouldBeTrue();
        second.CancellationToken.IsCancellationRequested.ShouldBeFalse();
    }

    /// <summary>Verifies stale retirement cannot clear a newer current lease.</summary>
    [Fact]
    public void TryComplete_WhenLeaseIsStale_PreservesCurrentLease()
    {
        var operation = new LatestControlOperation();
        var stale = operation.Begin();
        var current = operation.Begin();

        operation.TryComplete(stale).ShouldBeFalse();

        operation.IsCurrent(current).ShouldBeTrue();
        operation.TryComplete(current).ShouldBeTrue();
        operation.IsCurrent(current).ShouldBeFalse();
    }

    /// <summary>Verifies cancellation authority is gone before reentrant callbacks execute.</summary>
    [Fact]
    public void Cancel_WhenCallbackReenters_LeavesReentrantLeaseCurrent()
    {
        var operation = new LatestControlOperation();
        var cancelled = operation.Begin();
        LatestControlOperationLease? reentrant = null;
        using var registration = cancelled.CancellationToken.Register(() =>
        {
            operation.IsCurrent(cancelled).ShouldBeFalse();
            reentrant = operation.Begin();
        });

        operation.Cancel();

        var current = reentrant.ShouldNotBeNull();
        operation.IsCurrent(current).ShouldBeTrue();
    }

    /// <summary>Verifies a throwing cancellation callback does not leave revoked authority live.</summary>
    [Fact]
    public void Cancel_WhenCallbackThrows_ClearsAuthorityBeforeRethrow()
    {
        var operation = new LatestControlOperation();
        var lease = operation.Begin();
        using var registration = lease.CancellationToken.Register(() =>
            throw new InvalidOperationException("cancel failed"));

        _ = Should.Throw<AggregateException>(operation.Cancel);

        operation.IsCurrent(lease).ShouldBeFalse();
        lease.CancellationToken.IsCancellationRequested.ShouldBeTrue();
    }

    /// <summary>Verifies startup abort affects only the exact failed lease.</summary>
    [Fact]
    public void TryAbort_WhenNewerLeaseExists_DoesNotRetireNewerLease()
    {
        var operation = new LatestControlOperation();
        var failed = operation.Begin();
        var current = operation.Begin();

        operation.TryAbort(failed).ShouldBeFalse();

        operation.IsCurrent(current).ShouldBeTrue();
    }

    /// <summary>Verifies throwing replacement cancellation cannot strand an unreturned lease.</summary>
    [Fact]
    public void Begin_WhenPreviousCancellationThrows_AbortsUnreturnedLease()
    {
        var operation = new LatestControlOperation();
        var previous = operation.Begin();
        using var registration = previous.CancellationToken.Register(() =>
            throw new InvalidOperationException("cancel failed"));

        _ = Should.Throw<AggregateException>(operation.Begin);

        operation.HasCurrent.ShouldBeFalse();
    }
}
