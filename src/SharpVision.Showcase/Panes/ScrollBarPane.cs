// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the ScrollBar control with full-rail, vertical, and thin-chrome specimens.</summary>
internal sealed class ScrollBarPane: CompositeControl
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "ScrollBar";

    /// <summary>Initializes the retained ScrollBar documentation page.</summary>
    internal ScrollBarPane() => InitializeContent(CreateContent());

    private static Stack CreateContent()
    {
        var horizontal = new ScrollBar()
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
        var status = new Text($"Thumb value: {horizontal.Value}");
        horizontal.ValueChanged += (_, eventArgs) => status.Content = $"Thumb value: {eventArgs.Value}";
        var full = new Stack() { Spacing = 1 };
        full.Children.Add(horizontal);
        full.Children.Add(status);

        var vertical = new ScrollBar()
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

        var thin = new ScrollBar()
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
