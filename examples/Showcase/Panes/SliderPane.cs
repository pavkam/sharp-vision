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

        // The rail itself only has nine discrete cell positions across a 0-100 range, so a single
        // SmallChange=1 arrow press frequently lands on the same rounded position as the previous
        // value and moves no glyph at all - a label is the only way to keep every keystroke
        // observably confirmed, matching the horizontal specimen's LiveStatus pattern above.
        var verticalStatus = new Text($"Selected value: {vertical.Value}");
        vertical.ValueChanged += (_, eventArgs) => verticalStatus.Content = $"Selected value: {eventArgs.Value}";
        var signed = new Slider
        {
            Width = Length.Cells(32),
            Minimum = -50,
            Maximum = 50,
            Value = -15,
            SmallChange = 5,
            LargeChange = 20
        };
        var reversed = new Slider
        {
            Width = Length.Cells(32),
            Maximum = 100,
            Value = 35,
            SmallChange = 5,
            IsDirectionReversed = true
        };
        var reversedStatus = new Text($"Selected value: {reversed.Value}");
        reversed.ValueChanged += (_, eventArgs) => reversedStatus.Content = $"Selected value: {eventArgs.Value}";
        var reversedVertical = new Slider
        {
            Orientation = Orientation.Vertical,
            Height = Length.Cells(9),
            Maximum = 100,
            Value = 35,
            SmallChange = 5,
            IsDirectionReversed = true
        };
        var reversedVerticalStatus = new Text($"Selected value: {reversedVertical.Value}");
        reversedVertical.ValueChanged += (_, eventArgs) =>
            reversedVerticalStatus.Content = $"Selected value: {eventArgs.Value}";
        var customGlyphs = new SliderGlyphs(
            new Rune('·'),
            new Rune('='),
            new Rune('·'),
            new Rune('='),
            new Rune('●'));
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
                    new DocColumn(vertical, verticalStatus))),
            new DocSection(
                "⇄",
                "Direction",
                "Set <info>IsDirectionReversed</info> when the visual minimum belongs on the opposite edge. Pointer mapping and directional arrows reverse together; <reverse>Home</reverse> and <reverse>End</reverse> remain semantic minimum and maximum commands.",
                new DocExample(
                    "Reversed horizontal and vertical rails",
                    "The minimum is on the right or top, so Left and Down increase while Right and Up decrease these live five-step sliders.",
                    new DocColumn(
                        new DocRow(new Text("Horizontal"), reversed),
                        reversedStatus,
                        new DocRow(new Text("Vertical  "), reversedVertical),
                        reversedVerticalStatus),
                    "var horizontal = new Slider { IsDirectionReversed = true };\nvar vertical = new Slider\n{\n    Orientation = Orientation.Vertical,\n    IsDirectionReversed = true,\n};")),
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
                "Presentation customization",
                "<info>Style</info> assigns a complete <info>SliderStyle</info>; its colors and <info>SliderGlyphs</info> independently define the fill, track, and thumb.",
                new DocExample(
                    "Custom rail colors and glyphs",
                    "The fill, track, and thumb use distinct local colors with an equals, middle-dot, and circle glyph family.",
                    new Slider
                    {
                        Width = Length.Cells(32),
                        Maximum = 100,
                        Value = 60,
                        Style = SliderStyle.Default with
                        {
                            FillColor = Color.Rgb(0x40, 0xc0, 0x40),
                            TrackColor = Color.Rgb(0x60, 0x60, 0x60),
                            ThumbColor = Color.Rgb(0xff, 0xff, 0xff),
                            Glyphs = customGlyphs
                        }
                    },
                    "slider.Style = SliderStyle.Default with\n{\n    FillColor = Color.Rgb(0x40, 0xC0, 0x40),\n    TrackColor = Color.Rgb(0x60, 0x60, 0x60),\n    ThumbColor = Color.Rgb(0xFF, 0xFF, 0xFF),\n    Glyphs = new SliderGlyphs(new Rune('·'), new Rune('='), new Rune('·'), new Rune('='), new Rune('●')),\n};")),
            new DocSection(
                "⌨️",
                "Keyboard and pointer",
                "Arrow keys use SmallChange, Page Up and Page Down use LargeChange, wheel gestures bubble at endpoints, and drag cancellation never invents a value."));
    }
}
