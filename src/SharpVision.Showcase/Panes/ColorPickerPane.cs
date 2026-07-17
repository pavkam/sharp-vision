// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents adaptive true-color and indexed ColorPicker interaction.</summary>
internal sealed class ColorPickerPane: CompositeControl
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "ColorPicker";

    /// <summary>Initializes the retained adaptive ColorPicker documentation page.</summary>
    internal ColorPickerPane()
    {
        Picker = new ColorPicker
        {
            Width = Length.Cells(40),
            Height = Length.Cells(18),
            Value = Color.Rgb(255, 72, 128),
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

    private Dock CreateContent() => Doc.Page(
        Title,
        "Selects the nearest color the active terminal can reproduce, from 24-bit RGB through indexed and monochrome tiers.",
        Doc.Section(
            "🎨",
            "Adaptive color depth",
            "The retained component reads application capabilities automatically and changes presentation without rebuilding its layout tree.",
            Doc.Example(
                "Live capability-aware picker",
                "True-color terminals show the full editor; indexed terminals receive the Canvas-inspired palette grid.",
                Doc.Column(Picker, Status),
                "var picker = new ColorPicker { Value = Color.Rgb(255, 72, 128) };")),
        Doc.Section(
            "🌈",
            "True color",
            "A saturation/value plane, hue ramp, exact RGB sliders, preview, and hexadecimal readout cover the full 24-bit output model."),
        Doc.Section(
            "▦",
            "Indexed palettes",
            "Indexed-256 uses a responsive 16 by 16 grid; Basic-16 uses four by four; monochrome exposes only the terminal default."),
        Doc.Section(
            "⌨️",
            "Keyboard and pointer",
            "Click or drag across the plane and palettes. Tab reaches retained parts; arrows edit surfaces and sliders; Home and End reach palette or range endpoints."));

    private static string Format(Color value)
    {
        var rgb = Palette.Resolve(value);
        return rgb.Kind == ColorKind.Rgb ? $"#{rgb.Red:X2}{rgb.Green:X2}{rgb.Blue:X2}" : "DEFAULT";
    }
}
