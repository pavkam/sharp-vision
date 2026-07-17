// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents Slider range, orientation, signed values, and direct interaction.</summary>
internal sealed class SliderPane: CompositeControl
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Slider";

    /// <summary>Initializes the retained Slider documentation page.</summary>
    internal SliderPane()
    {
        LiveSlider = new Slider
        {
            Width = Length.Cells(32),
            Maximum = 100,
            Value = 35,
        };
        LiveStatus = new Text($"Selected value: {LiveSlider.Value}");
        LiveSlider.ValueChanged += (_, eventArgs) =>
            LiveStatus.Content = $"Selected value: {eventArgs.Value}";
        InitializeContent(CreateContent());
    }

    /// <summary>Gets the live primary specimen.</summary>
    internal Slider LiveSlider { get; }

    /// <summary>Gets the live primary specimen's value label.</summary>
    internal Text LiveStatus { get; }

    private Dock CreateContent()
    {
        var vertical = new Slider
        {
            Orientation = Orientation.Vertical,
            Height = Length.Cells(9),
            Maximum = 100,
            Value = 65,
        };
        var signed = new Slider
        {
            Width = Length.Cells(32),
            Minimum = -50,
            Maximum = 50,
            Value = -15,
            SmallChange = 5,
            LargeChange = 20,
        };
        return Doc.Page(
            Title,
            "Selects a signed integer through a direct, draggable, keyboard-accessible rail.",
            Doc.Section(
                "🎚️",
                "Range and value",
                "Press anywhere on the rail to select directly, drag with capture, or use wheel and keys.",
                Doc.Example(
                    "Live horizontal slider",
                    "The label observes the committed value after pointer, wheel, keyboard, or API changes.",
                    Doc.Column(LiveSlider, LiveStatus),
                    "var slider = new Slider { Minimum = 0, Maximum = 100, Value = 35 };")),
            Doc.Section(
                "↕️",
                "Orientation",
                "Horizontal ranges increase left-to-right; vertical ranges increase bottom-to-top.",
                Doc.Example(
                    "Vertical value rail",
                    "Up increases and Down decreases while Home and End reach exact endpoints.",
                    vertical)),
            Doc.Section(
                "±",
                "Signed ranges",
                "Minimum and Maximum accept the complete signed integer domain with overflow-safe mapping.",
                Doc.Example(
                    "Negative through positive",
                    "This range moves in steps of five and pages in steps of twenty.",
                    signed)),
            Doc.Section(
                "⌨️",
                "Keyboard and pointer",
                "Arrow keys use SmallChange, Page Up and Page Down use LargeChange, wheel gestures bubble at endpoints, and drag cancellation never invents a value."));
    }
}
