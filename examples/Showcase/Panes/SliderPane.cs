// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents Slider range, orientation, signed values, and direct interaction.</summary>
internal sealed class SliderPane: CompositeControlBase
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Slider";

    /// <summary>Initializes the retained Slider documentation page.</summary>
    internal SliderPane()
    {
        LiveSlider = new Slider { Width = Length.Cells(32), Maximum = 100, Value = 35 };
        LiveStatus = new Text($"Selected value: {LiveSlider.Value}");
        LiveSlider.ValueChanged += (_, eventArgs) =>
            LiveStatus.Content = $"Selected value: {eventArgs.Value}";
        InitializeContent(CreateContent());
    }

    /// <summary>Gets the live primary specimen.</summary>
    internal Slider LiveSlider { get; }

    /// <summary>Gets the live primary specimen's value label.</summary>
    internal Text LiveStatus { get; }

    private DocPage CreateContent()
    {
        var vertical = new Slider
        {
            Orientation = Orientation.Vertical,
            Height = Length.Cells(9),
            Maximum = 100,
            Value = 65
        };
        var signed = new Slider
        {
            Width = Length.Cells(32),
            Minimum = -50,
            Maximum = 50,
            Value = -15,
            SmallChange = 5,
            LargeChange = 20
        };
        return new DocPage(
            Title,
            "<info>Slider</info> selects a signed integer through a direct, draggable, keyboard-accessible rail.",
            new DocSection(
                "🎚️",
                "Range and value",
                "Press anywhere on the rail to select directly, drag with capture, or use wheel and keys.",
                new DocExample(
                    "Live horizontal slider",
                    "The label observes the committed value after pointer, wheel, keyboard, or API changes.",
                    new DocColumn(LiveSlider, LiveStatus),
                    "var slider = new Slider { Minimum = 0, Maximum = 100, Value = 35 };")),
            new DocSection(
                "↕️",
                "Orientation",
                "Horizontal ranges increase left-to-right; vertical ranges increase bottom-to-top.",
                new DocExample(
                    "Vertical value rail",
                    "<reverse>Up</reverse> increases and <reverse>Down</reverse> decreases while <reverse>Home</reverse> and <reverse>End</reverse> reach exact endpoints.",
                    vertical)),
            new DocSection(
                "±",
                "Signed ranges",
                "<info>Minimum</info> and <info>Maximum</info> accept the complete signed integer domain with overflow-safe mapping.",
                new DocExample(
                    "Negative through positive",
                    "This range moves in steps of five and pages in steps of twenty.",
                    signed)),
            new DocSection(
                "🎨",
                "Color customization",
                "<info>Style</info> assigns a complete <info>SliderStyle</info>, whose FillColor, TrackColor, and ThumbColor control the appearance of each rail part independently.",
                new DocExample(
                    "Custom rail colors",
                    "Each part uses a distinct local color override.",
                    new Slider
                    {
                        Width = Length.Cells(32),
                        Maximum = 100,
                        Value = 60,
                        Style = new SliderStyle(
                            SliderStyle.Default.Face,
                            SliderStyle.Default.Border,
                            SliderStyle.Default.Shadow,
                            Color.Rgb(0x40, 0xc0, 0x40),
                            Color.Rgb(0x60, 0x60, 0x60),
                            Color.Rgb(0xff, 0xff, 0xff),
                            SliderStyle.Default.Glyphs)
                    },
                    "slider.Style = new SliderStyle(\n    face, border, shadow,\n    Color.Rgb(0x40, 0xC0, 0x40),\n    Color.Rgb(0x60, 0x60, 0x60),\n    Color.Rgb(0xFF, 0xFF, 0xFF),\n    SliderStyle.Default.Glyphs);")),
            new DocSection(
                "⌨️",
                "Keyboard and pointer",
                "Arrow keys use SmallChange, Page Up and Page Down use LargeChange, wheel gestures bubble at endpoints, and drag cancellation never invents a value."));
    }
}
