// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies the dedicated adaptive ColorPicker showcase page.</summary>
public sealed class ColorPickerPaneTests
{
    /// <summary>Verifies the page explains true-color, palette, pointer, and keyboard behavior.</summary>
    [Fact]
    public void Content_WhenBuilt_ContainsRequiredColorPickerGuidance()
    {
        using var pane = new ColorPickerPane();
        var content = ControlTree.Text(pane);

        content.ShouldContain("Adaptive color depth");
        content.ShouldContain("True color");
        content.ShouldContain("Indexed palettes");
        content.ShouldContain("Keyboard and pointer");
        _ = ControlTree.FindAll<ColorPicker>(pane).ShouldHaveSingleItem();
    }

    /// <summary>Verifies a committed picker value updates the retained value label.</summary>
    [Fact]
    public void Value_WhenPickerChanges_UpdatesStatus()
    {
        using var pane = new ColorPickerPane();

        pane.Picker.Value = Color.Rgb(12, 34, 56);

        pane.Status.Content.ShouldBe("Selected: #0C2238");
    }
}
