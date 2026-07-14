// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Runtime;

/// <summary>
/// Verifies console host options validation and defaults.
/// </summary>
public sealed class ConsoleHostOptionsTests
{
    /// <summary>
    /// Verifies default values match the documented policy.
    /// </summary>
    [Fact]
    public void Defaults_WhenConstructed_MatchDocumentedPolicy()
    {
        ConsoleHostOptions options = new();

        options.ResizeInterval.ShouldBe(TimeSpan.FromMilliseconds(100));
        options.CaptureControlKeys.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies that non-positive resize interval values throw.
    /// </summary>
    [Fact]
    public void ResizeInterval_WhenNotPositive_Throws()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => new ConsoleHostOptions { ResizeInterval = TimeSpan.Zero });
    }
}
