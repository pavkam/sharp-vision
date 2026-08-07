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

        return new DocPage(
            Title,
            "<info>ColorPicker</info> authors stable <i>24-bit RGB</i> values while terminal output degrades privately to the active color depth.",
            new DocSection(
                "🎨",
                "Adaptive color depth",
                "The retained component keeps one RGB editor for every color-capable terminal and switches to a default-only surface for monochrome. A saturation/value plane, hue ramp, RGB sliders, preview, and hexadecimal readout cover the full 24-bit model.",
                new DocExample(
                    "Live capability-aware picker",
                    "The authored RGB value survives capability changes; only emitted terminal bytes lose fidelity.",
                    new DocColumn(Picker, Status),
                    "var picker = new ColorPicker();")),
            new DocSection(
                "⌨️",
                "Keyboard, pointer, and presets",
                "Click or drag across the plane. <reverse>Tab</reverse> reaches retained parts; arrows edit the surface and sliders; <reverse>Home</reverse> and <reverse>End</reverse> reach range endpoints.",
                new DocExample(
                    "Preset initial color",
                    "Set <info>Value</info> before display to open the picker at a known starting color.",
                    new DocColumn(preset, presetStatus),
                    "picker.Value = Color.Rgb(220, 120, 50);")));
    }

    private static string Format(Color value) =>
        value.IsRgb ? $"#{value.Red:X2}{value.Green:X2}{value.Blue:X2}" : "DEFAULT";
}
