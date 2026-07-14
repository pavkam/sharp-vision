// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the shrink-wrap arrange seam overrides stretch.</summary>
public sealed class ControlShrinkWrapTests
{
    /// <summary>Verifies a shrink-wrapping control ignores stretch and sizes to content.</summary>
    [Fact]
    public void Arrange_WhenShrinkWrapsWidth_SizesToContentDespiteStretch()
    {
        ShrinkProbe probe = new(new Size(6, 2)) { HorizontalAlignment = HorizontalAlignment.Stretch };

        probe.Measure(new Constraint(20, 20));
        probe.Arrange(new Rect(0, 0, 20, 20));

        probe.Bounds.Width.ShouldBe(6);
    }
}
