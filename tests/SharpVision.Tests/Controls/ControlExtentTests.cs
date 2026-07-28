// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the natural content extent captured during measure.</summary>
public sealed class ControlExtentTests
{
    /// <summary>Verifies the extent keeps the natural size when desired size is clamped smaller.</summary>
    [Fact]
    public void ContentExtent_WhenConstraintClampsDesired_KeepsNaturalSize()
    {
        var probe = new ProbeControl(new Size(20, 40));

        probe.Measure(new Constraint(10, 12));

        probe.DesiredSize.ShouldBe(new Size(10, 12));
        probe.ExposedContentExtent.ShouldBe(new Size(20, 40));
    }
}
