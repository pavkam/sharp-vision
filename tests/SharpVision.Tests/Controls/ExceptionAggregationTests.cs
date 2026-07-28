// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using System.Runtime.ExceptionServices;

/// <summary>Verifies the shared exception-capture helper.</summary>
public sealed class ExceptionAggregationTests
{
    /// <summary>Verifies the first thrown exception is captured into the failure slot.</summary>
    [Fact]
    public void CaptureFailure_WhenActionThrows_CapturesFirstException()
    {
        ExceptionDispatchInfo? failure = null;
        var expected = new InvalidOperationException("boom");

        ExceptionAggregation.Capture(() => throw expected, ref failure);

        _ = failure.ShouldNotBeNull();
        failure.SourceException.ShouldBe(expected);
    }

    /// <summary>Verifies a pre-existing failure is not overwritten by a later exception.</summary>
    [Fact]
    public void CaptureFailure_WhenFailureAlreadySet_IgnoresSubsequentException()
    {
        var first = new InvalidOperationException("first");
        var failure = ExceptionDispatchInfo.Capture(first);

        ExceptionAggregation.Capture(
            () => throw new InvalidOperationException("second"),
            ref failure);

        failure!.SourceException.ShouldBe(first);
    }

    /// <summary>Verifies a successful action leaves the failure slot null.</summary>
    [Fact]
    public void CaptureFailure_WhenActionSucceeds_LeavesFailureNull()
    {
        ExceptionDispatchInfo? failure = null;

        ExceptionAggregation.Capture(() => { }, ref failure);

        failure.ShouldBeNull();
    }
}
