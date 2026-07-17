// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies the dedicated Slider showcase page and live value specimen.</summary>
public sealed class SliderPaneTests
{
    /// <summary>Verifies the page contains horizontal, vertical, signed, and keyboard guidance.</summary>
    [Fact]
    public void Content_WhenBuilt_ContainsRequiredSliderGuidance()
    {
        using var pane = new SliderPane();
        var content = ControlTree.Text(pane);

        content.ShouldContain("Range and value");
        content.ShouldContain("Orientation");
        content.ShouldContain("Signed ranges");
        content.ShouldContain("Keyboard and pointer");
        ControlTree.FindAll<Slider>(pane).Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    /// <summary>Verifies a committed Slider value updates the retained live label.</summary>
    [Fact]
    public void Value_WhenLiveSliderChanges_UpdatesStatus()
    {
        using var pane = new SliderPane();

        pane.LiveSlider.Value = 73;

        pane.LiveStatus.Content.ShouldBe("Selected value: 73");
    }
}
