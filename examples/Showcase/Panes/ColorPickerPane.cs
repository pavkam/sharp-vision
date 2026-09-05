// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents RGB ColorPicker interaction and terminal-safe degradation.</summary>
internal sealed class ColorPickerPane: CompositeControlBase
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "ColorPicker";

    /// <summary>Initializes the retained adaptive ColorPicker documentation page.</summary>
    internal ColorPickerPane()
    {
        Picker = new ColorPicker
        {
            Width = Length.Cells(40),
            Height = Length.Cells(18)
        };
        Status = new Text($"Selected: {Format(Picker.Value)}");
        Picker.ValueChanged += (_, eventArgs) =>
            Status.Content = $"Selected: {Format(eventArgs.Value)}";
        InitializeContent(CreateContent());
    }

    /// <summary>Gets the live adaptive picker specimen.</summary>
    internal ColorPicker Picker { get; }

    /// <summary>Gets the live selected-color label.</summary>
    internal Text Status { get; }

    private DocPage CreateContent()
    {
        var preset = new ColorPicker
        {
            Width = Length.Cells(34),
            Height = Length.Cells(14),
            Value = Color.Rgb(220, 120, 50)
        };
        var presetStatus = new Text($"Initial: {Format(preset.Value)}");
        preset.ValueChanged += (_, eventArgs) =>
            presetStatus.Content = $"Selected: {Format(eventArgs.Value)}";

        var styled = new ColorPicker
        {
            Width = Length.Cells(34),
            Height = Length.Cells(14),
            Value = Color.Rgb(140, 90, 220),
            Style = new ColorPickerStyle(
                ControlStyle.Default.Face,
                ControlStyle.Default.Border,
                ControlStyle.Default.Shadow,
                new SliderStyle(
                    SliderStyle.Default.Face,
                    SliderStyle.Default.Border,
                    SliderStyle.Default.Shadow,
                    Color.Rgb(0x8c, 0x5a, 0xdc),
                    SliderStyle.Default.TrackColor,
                    SliderStyle.Default.ThumbColor,
                    SliderStyle.Default.Glyphs),
                null,
                new Rune('◇'))
        };
        var styledStatus = new Text($"Selected: {Format(styled.Value)}");
        styled.ValueChanged += (_, eventArgs) =>
            styledStatus.Content = $"Selected: {Format(eventArgs.Value)}";

        return new DocPage(
            Title,
            "<info>ColorPicker</info> authors stable <i>24-bit RGB</i> values while terminal output degrades privately to the active color depth.",
            new DocSection(
                "🎨",
                "Adaptive color depth",
                "The retained component keeps one RGB editor for every color-capable terminal and switches to a default-only surface for monochrome. A saturation/value plane, hue ramp, RGB sliders, preview, and directly editable value field cover the full 24-bit model.",
                new DocExample(
                    "Live capability-aware picker",
                    "The authored RGB value survives capability changes; only emitted terminal bytes lose fidelity.",
                    new DocColumn(Picker, Status),
                    "var picker = new ColorPicker();")),
            new DocSection(
                "⌨️",
                "Keyboard, pointer, and presets",
                "Click or drag across the plane. <reverse>Tab</reverse> reaches all six retained parts; arrows edit the surface and sliders; the final field accepts #RRGGBB, rgb(r, g, b), decimal components, or DEFAULT directly; an unparsable value (for example \"#GG0000\" or a stray character) turns the field the active theme's error background/foreground until the text becomes valid again.",
                new DocExample(
                    "Preset initial color",
                    "Set <info>Value</info> before display to open at a known color, then Tab to the final field for direct entry.",
                    new DocColumn(preset, presetStatus),
                    "picker.Value = Color.Rgb(220, 120, 50);")),
            new DocSection(
                "🖌️",
                "Style customization",
                "<info>Style</info> assigns a complete <info>ColorPickerStyle</info>. Its <info>SliderStyle</info> forwards one presentation to all four retained RGB/hue Sliders, and <info>SelectedMarker</info> replaces the default filled diamond that highlights the currently selected plane coordinate.",
                new DocExample(
                    "Outline marker with a matching accent",
                    "An outline diamond stands in for the default filled marker, and every retained slider shares the same purple fill color.",
                    new DocColumn(styled, styledStatus),
                    "picker.Style = new ColorPickerStyle(\n    face, border, shadow,\n    new SliderStyle(face, border, shadow, Color.Rgb(0x8C, 0x5A, 0xDC), track, thumb, glyphs),\n    statusFace: null,\n    selectedMarker: new Rune('◇'));")));
    }

    private static string Format(Color value) =>
        value.IsRgb ? $"#{value.Red:X2}{value.Green:X2}{value.Blue:X2}" : "DEFAULT";
}
