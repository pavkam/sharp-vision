// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Controls;
using SharpVision.Layout;

/// <summary>Documents and demonstrates the ScrollBar control.</summary>
internal sealed class ScrollBarShowcasePane: ShowcasePane
{
    internal const string Title = "ScrollBar";
    private const string _catalogSummary =
        "Edits an integer viewport range through buttons, track paging, keyboard commands, and thumb dragging.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Arrows", "Press an arrow button or key", "Value changes by SmallChange and remains clamped to the range."),
        new InteractionDescription("Page keys", "Press Page Up or Page Down", "Value changes by LargeChange while preserving the viewport relationship."),
        new InteractionDescription("Home or End", "Jump to a range endpoint", "Value becomes Minimum or Maximum through the normal ValueChanged path."),
        new InteractionDescription("Pointer drag", "Drag the thumb using cell or pixel coordinates", "The thumb tracks the pointer and capture releases cleanly on completion or cancellation."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Minimum / Maximum", "int", "0 / 100", "Define validated non-negative inclusive range endpoints containing the current value."),
        new PropertyDescription("Value", "int", "0", "Stores the current clamped position and raises ValueChanged only after a real change."),
        new PropertyDescription("ViewportSize", "int", "0", "Sizes the thumb relative to the visible extent represented by the control."),
        new PropertyDescription("Orientation", "Orientation", "Vertical", "Chooses a top-to-bottom or left-to-right range and glyph direction."),
        new PropertyDescription("SmallChange / LargeChange", "int", "1 / 10", "Control line-button and page-track movement amounts for keyboard and pointer input."),
    ];

    /// <summary>Initializes the ScrollBar showcase page and composes its specimens.</summary>
    internal ScrollBarShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        ControlScrollBar horizontal = new ControlScrollBar
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
        ControlText status = new ControlText($"Thumb value: {horizontal.Value}")
        {
            Foreground = Palette.Muted,
        };
        horizontal.ValueChanged += (_, eventArgs) =>
            status.Content = $"Thumb value: {eventArgs.Value}";
        ControlStack full = PaneSupport.Vertical();
        full.Children.Add(horizontal);
        full.Children.Add(status);
        examples.Children.Add(PaneSupport.SampleSection(
            "Full horizontal rail",
            "Drag the solid thumb, click the shaded track for page movement, or use the arrow buttons for line movement.",
            PaneSupport.Card(full, Glyphs.Rounded)));

        ControlScrollBar vertical = new ControlScrollBar
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
        examples.Children.Add(PaneSupport.SampleSection(
            "Vertical rail",
            "The same canonical ScrollBar changes orientation while retaining keyboard, wheel, track, and live drag behavior.",
            PaneSupport.Card(vertical, Glyphs.Light)));

        examples.Children.Add(PaneSupport.SampleSection(
            "Thin line chrome",
            "Thin rails omit buttons to conserve cells; a heavy line thumb remains distinct from the passive track.",
            PaneSupport.Card(new ControlScrollBar
            {
                Width = Length.Cells(28),
                Orientation = Orientation.Horizontal,
                Chrome = ScrollBarStyle.Thin,
                Fill = ScrollBarFill.Line,
                Maximum = 100,
                Value = 62,
                ViewportSize = 30,
            }, Glyphs.Paired)));
    }
}
