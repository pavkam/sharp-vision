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
        var causeStatus = new Text("Input cause: waiting");
        horizontal.ValueChanged += (_, eventArgs) =>
        {
            status.Content = $"Thumb value: {eventArgs.Value}";
            causeStatus.Content = $"Input cause: {eventArgs.Cause}";
        };
        var full = new Stack() { Spacing = 1 };
        full.Children.Add(horizontal);
        full.Children.Add(status);
        full.Children.Add(causeStatus);

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

        var rangeStatus = new Text("Range: 0..100, value 20, viewport 20");
        var liveRange = new ScrollBar
        {
            Width = Length.Cells(28),
            Orientation = Orientation.Horizontal,
            Maximum = 100,
            Value = 20,
            ViewportSize = 20,
        };
        var increaseViewport = new Button { Content = new Text("Increase viewport") };
        increaseViewport.Click += (_, _) =>
        {
            liveRange.ViewportSize = Math.Min(100, liveRange.ViewportSize + 10);
            rangeStatus.Content =
                $"Range: 0..{liveRange.Maximum}, value {liveRange.Value}, viewport {liveRange.ViewportSize}";
        };

        var custom = new ScrollBar
        {
            Width = Length.Cells(28),
            Orientation = Orientation.Horizontal,
            Maximum = 50,
            ViewportSize = 10,
            DecrementGlyph = new Rune('<'),
            IncrementGlyph = new Rune('>'),
            TrackGlyph = new Rune('.'),
            ThumbGlyph = new Rune('#'),
        };

        var one = new ScrollBar { Width = Length.Cells(1), Orientation = Orientation.Horizontal, Maximum = 10 };
        var two = new ScrollBar { Width = Length.Cells(2), Orientation = Orientation.Horizontal, Maximum = 10 };
        var three = new ScrollBar { Width = Length.Cells(3), Orientation = Orientation.Horizontal, Maximum = 10 };

        var endpoint = new ScrollBar
        {
            Width = Length.Cells(20),
            Orientation = Orientation.Horizontal,
            Maximum = 10,
            Value = 10,
            ViewportSize = 4,
        };

        return Doc.Page(
            Title,
            "Edits an integer viewport range through buttons, track paging, keyboard commands, and thumb dragging.",
            Doc.Section(
                "Range anatomy",
                "Minimum, maximum, value, and viewport together determine the stable thumb length and position.",
                Doc.Example(
                    "Full horizontal rail",
                    "Drag the thumb, click the track, or use the arrow buttons. The label reports committed value and typed cause.",
                    Doc.Card(full),
                    "var rail = new ScrollBar\n{\n    Maximum = 100,\n    Value = 35,\n    ViewportSize = 25,\n};")),
            Doc.Section(
                "Input parity",
                "Orientation changes geometry, not range semantics or keyboard, wheel, track, and drag behavior.",
                Doc.Example(
                    "Vertical rail",
                    "Focus the vertical rail and compare arrow, Page, Home/End, wheel, track, and thumb input.",
                    Doc.Card(vertical))),
            Doc.Section(
                "Chrome",
                "Full, thin, and caller glyph variants share the same range mapping.",
                Doc.Example(
                    "Thin and custom rails",
                    "Thin chrome removes buttons; custom printable one-cell glyphs change presentation without changing behavior.",
                    Doc.Column(Doc.Card(thin), Doc.Card(custom)))),
            Doc.Section(
                "Live range",
                "Mutating viewport or range updates thumb geometry in place and keeps the current value validated.",
                Doc.Example(
                    "Viewport growth",
                    "Increase the viewport and watch the thumb grow while the same ScrollBar instance and value remain committed.",
                    Doc.Column(liveRange, increaseViewport, rangeStatus))),
            Doc.Section(
                "Tiny rails",
                "One-, two-, and three-cell rails use deterministic fallback roles and never draw outside their bounds.",
                Doc.Example(
                    "Minimum geometry",
                    "One cell shows only the thumb, two show buttons, and three admit the smallest complete rail.",
                    Doc.Column(one, two, three))),
            Doc.Section(
                "Nested behavior",
                "A wheel event is handled only when Value changes, leaving endpoint gestures available to an enclosing viewport.",
                Doc.Example(
                    "Endpoint bubbling",
                    "This rail already sits at maximum. Wheel farther and the unchanged event may bubble to the documentation page.",
                    endpoint)));
    }
}
