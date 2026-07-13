// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;

/// <summary>Verifies the WPF-named layout override seams are the extension points.</summary>
public sealed class OverrideSeamTests
{
    /// <summary>Verifies a control's MeasureOverride result flows into DesiredSize.</summary>
    [Fact]
    public void MeasureOverride_WhenControlReportsContent_DrivesDesiredSize()
    {
        FixedContent control = new();

        control.Measure(new Constraint(20, 6));

        control.DesiredSize.ShouldBe(new Size(7, 3));
    }

    private sealed class FixedContent: Control
    {
        protected override Size MeasureOverride(Constraint constraint) => new(7, 3);
    }
}
