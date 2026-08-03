// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the ScrollBar control with full-rail, vertical, and thin-chrome specimens.</summary>
internal sealed class ScrollBarPane: CompositeControlBase
{
    internal ScrollBarPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "ScrollBar";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        var horizontal = new ScrollBar
        {
            Width = Length.Cells(28),
            Orientation = Orientation.Horizontal,
            Style = ScrollBarStyle.FullBlock,
            Maximum = 100,
            ViewportSize = 25,
            Value = 35
        };
        var status = new Text($"Thumb value: {horizontal.Value}");
        var causeStatus = new Text("Input cause: waiting");
        horizontal.ValueChanged += (_, eventArgs) =>
        {
            status.Content = $"Thumb value: {eventArgs.Value}";
            causeStatus.Content = $"Input cause: {eventArgs.Cause}";
        };
        var full = new Stack { Spacing = 1 };
        full.Children.Add(horizontal);
        full.Children.Add(status);
        full.Children.Add(causeStatus);

        var vertical = new ScrollBar
        {
            Height = Length.Cells(8),
            Style = ScrollBarStyle.FullBlock,
            Maximum = 40,
            ViewportSize = 10,
            Value = 12
        };

        var thin = new ScrollBar
        {
            Width = Length.Cells(28),
            Orientation = Orientation.Horizontal,
            Style = ScrollBarStyle.ThinLine,
            Maximum = 100,
            Value = 62,
            ViewportSize = 30
        };

        var rangeStatus = new Text("Range: 0..100, value 20, viewport 20");
        var liveRange = new ScrollBar
        {
            Width = Length.Cells(28),
            Orientation = Orientation.Horizontal,
            Maximum = 100,
            Value = 20,
            ViewportSize = 20
        };
        var increaseViewport = new Button { Content = new Text("&Increase viewport") };
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
            Style = ScrollBarStyle.Default.With(
                glyphs: new ScrollBarGlyphs(
                    new Rune('^'), new Rune('v'), new Rune('<'), new Rune('>'),
                    new Rune('.'), new Rune('#'), new Rune('-'), new Rune('='),
                    new Rune('|'), new Rune('#')))
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
            ViewportSize = 4
        };

        return new DocPage(
            Title,
            "<info>ScrollBar</info> edits an integer viewport range through buttons, track paging, keyboard commands, and thumb dragging.",
            new DocSection(
                "📏",
                "Range anatomy",
                "<info>Minimum</info>, <info>Maximum</info>, <info>Value</info>, and <info>ViewportSize</info> together determine the stable thumb length and position.",
                new DocExample(
                    "Full horizontal rail",
                    "Drag the thumb, click the track, or use the arrow buttons. The label reports committed value and typed cause.",
                    new DocCard(full),
                    "var rail = new ScrollBar\n{\n    Maximum = 100,\n    Value = 35,\n    ViewportSize = 25,\n};")),
            new DocSection(
                "🔄",
                "Input parity",
                "<info>Orientation</info> changes geometry, not range semantics or keyboard, wheel, track, and drag behavior.",
                new DocExample(
                    "Vertical rail",
                    "Focus the vertical rail and compare arrow, <reverse>Page Up</reverse>/<reverse>Page Down</reverse>, <reverse>Home</reverse>/<reverse>End</reverse>, wheel, track, and thumb input.",
                    new DocCard(vertical))),
            new DocSection(
                "🎨",
                "Chrome",
                "Full, thin, and caller glyph variants share the same range mapping.",
                new DocExample(
                    "Thin and custom rails",
                    "Thin chrome removes buttons; custom printable one-cell glyphs change presentation without changing behavior.",
                    new DocColumn(new DocCard(thin), new DocCard(custom)))),
            new DocSection(
                "📐",
                "Live range",
                "Mutating viewport or range updates thumb geometry in place and keeps the current value validated.",
                new DocExample(
                    "Viewport growth",
                    "Increase the viewport and watch the thumb grow while the same ScrollBar instance and value remain committed.",
                    new DocColumn(liveRange, increaseViewport, rangeStatus))),
            new DocSection(
                "📌",
                "Tiny rails",
                "One-, two-, and three-cell rails use deterministic fallback roles and never draw outside their bounds.",
                new DocExample(
                    "Minimum geometry",
                    "One cell shows only the thumb, two show buttons, and three admit the smallest complete rail.",
                    new DocColumn(one, two, three))),
            new DocSection(
                "🔗",
                "Nested behavior",
                "A wheel event is handled only when <info>Value</info> changes, leaving endpoint gestures available to an enclosing viewport.",
                new DocExample(
                    "Endpoint bubbling",
                    "This rail already sits at maximum. Wheel farther and the unchanged event may bubble to the documentation page.",
                    endpoint)));
    }
}
