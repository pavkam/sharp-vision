// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents ChaseIndicator movement, glyph families, track length, and playback.</summary>
internal sealed class ChaseIndicatorPane: CompositeControlBase
{
    /// <summary>The exact catalog and page name.</summary>
    internal const string Title = "ChaseIndicator";

    /// <summary>Initializes the retained ChaseIndicator documentation page.</summary>
    internal ChaseIndicatorPane() => InitializeContent(CreateContent());

    private static DocPage CreateContent()
    {
        var patterns = new DocColumn(
            Pattern("Circle", ChaseIndicatorStyle.Circle),
            Pattern("Diamond", ChaseIndicatorStyle.Diamond),
            Pattern("Square", ChaseIndicatorStyle.Square),
            Pattern("Up", ChaseIndicatorStyle.Up),
            Pattern("Down", ChaseIndicatorStyle.Down),
            Pattern("Left", ChaseIndicatorStyle.Left),
            Pattern("Right", ChaseIndicatorStyle.Right));
        var movements = new DocColumn(
            Movement("Bounce", ChaseMovement.Bounce),
            Movement("Wrap", ChaseMovement.Wrap),
            Movement("Spread", ChaseMovement.Spread));
        var longTrack = new ChaseIndicator { Style = ChaseIndicatorStyle.Diamond, Length = 15, TrailLength = 5 };
        var vertical = new ChaseIndicator
        {
            Movement = ChaseMovement.Spread,
            Orientation = Orientation.Vertical,
            Length = 9,
            TrailLength = 3
        };
        var spaced = new ChaseIndicator { Style = ChaseIndicatorStyle.Diamond, Spacing = 1 };
        var slowFade = new ChaseIndicator
        {
            Style = ChaseIndicatorStyle.Right,
            Length = 15,
            TrailLength = 5,
            FadeDuration = TimeSpan.FromMilliseconds(800)
        };
        var running = new ChaseIndicator { Style = ChaseIndicatorStyle.Right };
        var paused = new ChaseIndicator { Style = ChaseIndicatorStyle.Square, IsPlaying = false };
        var playbackStatus = new Text("Playback: running");
        var togglePlayback = new Button { Text = "&Pause" };
        togglePlayback.Click += (_, _) =>
        {
            running.IsPlaying = !running.IsPlaying;
            togglePlayback.Text = running.IsPlaying ? "&Pause" : "&Resume";
            playbackStatus.Content = running.IsPlaying ? "Playback: running" : "Playback: paused";
        };
        return new DocPage(
            Title,
            "<info>ChaseIndicator</info> bounces, wraps, or spreads highlighted glyphs and a gradual fading trail through a horizontal or vertical status track.",
            new DocSection(
                "↔️",
                "Movement",
                "Bounce reverses at each end, Wrap restarts after the final position, and Spread moves mirrored heads from the center to both ends and back.",
                new DocExample(
                    "Long movement tracks",
                    "All three live specimens use fifteen positions and a five-frame gradual tail.",
                    movements,
                    "var chase = new ChaseIndicator { Movement = ChaseMovement.Wrap, Length = 15, TrailLength = 5 };"),
                new DocExample(
                    "Long center spread",
                    "Twenty-one positions make the mirrored outward-and-return movement and its fading arms easy to inspect.",
                    Movement("21 cells", ChaseMovement.Spread, 21),
                    "var chase = new ChaseIndicator { Movement = ChaseMovement.Spread, Length = 21 };")),
            new DocSection(
                "◆",
                "Glyph families",
                "Seven built-in filled and hollow pairs cover circles, diamonds, squares, and directional triangles.",
                new DocExample(
                    "Built-in patterns",
                    "Every row is a live five-cell indicator using the same bounce algorithm.",
                    patterns,
                    "var chase = new ChaseIndicator { Style = ChaseIndicatorStyle.Diamond };")),
            new DocSection(
                "↔️",
                "Track length",
                "Length counts glyph positions; orientation selects the axis and spacing inserts blank cells.",
                new DocExample(
                    "Fifteen-cell track",
                    "The active diamond traverses all fifteen positions before reversing once at the endpoint.",
                    longTrack,
                    "var chase = new ChaseIndicator { Length = 15 };"),
                new DocExample(
                    "Long vertical spread and spacing",
                    "Vertical changes the axis for a nine-position spread; spacing inserts blank cells between horizontal glyph positions.",
                    new DocRow(
                        new DocColumn(new Text("Vertical"), vertical),
                        new DocColumn(new Text("Spaced"), spaced)),
                    "var chase = new ChaseIndicator { Orientation = Orientation.Vertical };"),
                new DocExample(
                    "Slow fade",
                    "Each retained glyph fades independently; this specimen uses a slower 800-millisecond fade.",
                    new DocRow(new DocColumn(new Text("Slow fade"), slowFade)),
                    "var chase = new ChaseIndicator { FadeDuration = TimeSpan.FromMilliseconds(800) };")),
            new DocSection(
                "🎨",
                "Trail and presentation",
                "<info>TrailLength</info> controls how many previously visited positions fade behind the head. The complete style customizes colors, glyphs, and intrinsic chrome.",
                new DocExample(
                    "No trail and long trail",
                    "The first indicator shows only the head; the second shows a five-frame fading trail.",
                    new DocColumn(
                        new DocRow(new Text("No trail "), new ChaseIndicator { TrailLength = 0, Length = 15 }),
                        new DocRow(new Text("Trail: 5 "), new ChaseIndicator { TrailLength = 5, Length = 15 })),
                    "chase.TrailLength = 5;"),
                new DocExample(
                    "Custom colors",
                    "Each part uses a distinct concrete color while sharing the same bounce algorithm.",
                    new ChaseIndicator
                    {
                        Length = 15,
                        Padding = new Thickness(1, 0),
                        Style = ChaseIndicatorStyle.Default with
                        {
                            Border = new Border(
                                BorderSide.All,
                                BorderGlyphStyle.Rounded,
                                SemanticColor.ControlBorder,
                                Color.Transparent,
                                SemanticDecoration.Border),
                            HeadColor = Color.Rgb(0xff, 0x40, 0x40),
                            TrailColor = Color.Rgb(0x80, 0x20, 0x20),
                            TrackColor = Color.Rgb(0x30, 0x30, 0x30),
                            Glyphs = new ChaseIndicatorGlyphs(new Rune('*'), new Rune('·'))
                        }
                    },
                    "chase.Padding = new Thickness(1, 0);\nchase.Style = ChaseIndicatorStyle.Default with\n{\n    Border = new Border(\n        BorderSide.All,\n        BorderGlyphStyle.Rounded,\n        SemanticColor.ControlBorder,\n        Color.Transparent,\n        SemanticDecoration.Border),\n    HeadColor = Color.Rgb(0xFF, 0x40, 0x40),\n    TrailColor = Color.Rgb(0x80, 0x20, 0x20),\n    TrackColor = Color.Rgb(0x30, 0x30, 0x30),\n    Glyphs = new ChaseIndicatorGlyphs(new Rune('*'), new Rune('·')),\n};")),
            new DocSection(
                "⏱️",
                "Playback",
                "<info>Interval</info> defaults to 200 milliseconds and fading to 400 milliseconds; <info>IsPlaying</info> pauses both clocks.",
                new DocExample(
                    "Live pause and retained phase",
                    "Pause and Resume retain the running track's current phase; the second track remains independently paused.",
                    new DocColumn(
                        new DocRow(new Text("Running"), running),
                        new DocRow(togglePlayback, playbackStatus),
                        new DocRow(new Text("Paused "), paused)),
                    "toggle.Click += (_, _) => chase.IsPlaying = !chase.IsPlaying;")));
    }

    private static DocRow Pattern(string label, ChaseIndicatorStyle style) =>
        new(new Text(label) { Width = Length.Cells(8) }, new ChaseIndicator { Style = style });

    private static DocRow Movement(string label, ChaseMovement movement, int length = 15) =>
        new(
            new Text(label) { Width = Length.Cells(10) },
            new ChaseIndicator
            {
                Movement = movement,
                Length = length,
                TrailLength = 5,
                Interval = TimeSpan.FromMilliseconds(150),
                FadeDuration = TimeSpan.FromMilliseconds(900)
            });
}
