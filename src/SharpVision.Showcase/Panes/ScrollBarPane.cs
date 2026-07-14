// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the ScrollBar control with full-rail, vertical, and thin-chrome specimens.</summary>
internal sealed class ScrollBarPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "ScrollBar";

    /// <inheritdoc/>
    protected override Control Build()
    {
        ScrollBar horizontal = new()
        {
            Width = Length.Cells(28),
            Orientation = Orientation.Horizontal,
            Maximum = 100,
            ViewportSize = 25,
            Value = 35,
            DecrementGlyph = new Rune('◀'),
            IncrementGlyph = new Rune('▶'),
            TrackGlyph = new Rune('─'),
            ThumbGlyph = new Rune('█'),
        };
        Text status = new($"Thumb value: {horizontal.Value}");
        horizontal.ValueChanged += (_, eventArgs) => status.Content = $"Thumb value: {eventArgs.Value}";
        Stack full = new() { Spacing = 1 };
        full.Children.Add(horizontal);
        full.Children.Add(status);

        ScrollBar vertical = new()
        {
            Height = Length.Cells(8),
            Maximum = 40,
            ViewportSize = 10,
            Value = 12,
            DecrementGlyph = new Rune('▲'),
            IncrementGlyph = new Rune('▼'),
            TrackGlyph = new Rune('│'),
            ThumbGlyph = new Rune('█'),
        };

        ScrollBar thin = new()
        {
            Width = Length.Cells(28),
            Orientation = Orientation.Horizontal,
            Chrome = ScrollBarChrome.Thin,
            Fill = ScrollBarFill.Line,
            Maximum = 100,
            Value = 62,
            ViewportSize = 30,
        };

        return Doc.Page(
            Title,
            "Edits an integer viewport range through buttons, track paging, keyboard commands, and thumb dragging.",
            Doc.Example(
                "Full horizontal rail",
                "Drag the solid thumb, click the shaded track for page movement, or use the arrow buttons for line movement. Value changes report through the live label below.",
                Doc.Card(full)),
            Doc.Example(
                "Vertical rail",
                "The same canonical ScrollBar changes orientation while retaining keyboard, wheel, track, and live drag behavior.",
                Doc.Card(vertical)),
            Doc.Example(
                "Thin line chrome",
                "Thin rails omit buttons to conserve cells; a heavy line thumb remains distinct from the passive track.",
                Doc.Card(thin)));
    }
}
