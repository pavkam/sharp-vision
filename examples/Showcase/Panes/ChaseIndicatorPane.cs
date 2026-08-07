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

    private static Dock CreateContent()
    {
        var patterns = Doc.Column(
            Pattern("Circle", ChaseIndicatorStyle.Circle),
            Pattern("Diamond", ChaseIndicatorStyle.Diamond),
            Pattern("Square", ChaseIndicatorStyle.Square),
            Pattern("Up", ChaseIndicatorStyle.Up),
            Pattern("Down", ChaseIndicatorStyle.Down),
            Pattern("Left", ChaseIndicatorStyle.Left),
            Pattern("Right", ChaseIndicatorStyle.Right));
        var movements = Doc.Column(
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
        var paused = new ChaseIndicator { Style = ChaseIndicatorStyle.Square, Playing = false };
        return Doc.Page(
            Title,
            "<info>ChaseIndicator</info> bounces, wraps, or spreads highlighted glyphs and a gradual fading trail through a horizontal or vertical status track.",
            Doc.Section(
                "↔️",
                "Movement",
                "Bounce reverses at each end, Wrap restarts after the final position, and Spread moves mirrored heads from the center to both ends and back.",
                Doc.Example(
                    "Long movement tracks",
                    "All three live specimens use fifteen positions and a five-frame gradual tail.",
                    movements,
                    "var chase = new ChaseIndicator { Movement = ChaseMovement.Wrap, Length = 15, TrailLength = 5 };"),
                Doc.Example(
                    "Long center spread",
                    "Twenty-one positions make the mirrored outward-and-return movement and its fading arms easy to inspect.",
                    Movement("21 cells", ChaseMovement.Spread, 21),
                    "var chase = new ChaseIndicator { Movement = ChaseMovement.Spread, Length = 21 };")),
            Doc.Section(
                "◆",
                "Glyph families",
                "Seven built-in filled and hollow pairs cover circles, diamonds, squares, and directional triangles.",
                Doc.Example(
                    "Built-in patterns",
                    "Every row is a live five-cell indicator using the same bounce algorithm.",
                    patterns,
                    "var chase = new ChaseIndicator { Style = ChaseIndicatorStyle.Diamond };")),
            Doc.Section(
                "↔️",
                "Track length",
                "Length counts glyph positions; orientation selects the axis and spacing inserts blank cells.",
                Doc.Example(
                    "Fifteen-cell track",
                    "The active diamond traverses all fifteen positions before reversing once at the endpoint.",
                    longTrack,
                    "var chase = new ChaseIndicator { Length = 15 };"),
                Doc.Example(
                    "Long vertical spread and spacing",
                    "Vertical changes the axis for a nine-position spread; spacing inserts blank cells between horizontal glyph positions.",
                    Doc.Row(
                        Doc.Column(new Text("Vertical"), vertical),
                        Doc.Column(new Text("Spaced"), spaced)),
                    "var chase = new ChaseIndicator { Orientation = Orientation.Vertical };"),
                Doc.Example(
                    "Slow fade",
                    "Each retained glyph fades independently; this specimen uses a slower 800-millisecond fade.",
                    Doc.Row(Doc.Column(new Text("Slow fade"), slowFade)),
                    "var chase = new ChaseIndicator { FadeDuration = TimeSpan.FromMilliseconds(800) };")),
            Doc.Section(
                "🎨",
                "Trail and color",
                "<info>TrailLength</info> controls how many previously visited positions fade behind the head. <info>HeadColor</info>, <info>TrailColor</info>, and <info>TrackColor</info> override each part independently.",
                Doc.Example(
                    "No trail and long trail",
                    "The first indicator shows only the head; the second shows a five-frame fading trail.",
                    Doc.Column(
                        Doc.Row(new Text("No trail "), new ChaseIndicator { TrailLength = 0, Length = 15 }),
                        Doc.Row(new Text("Trail: 5 "), new ChaseIndicator { TrailLength = 5, Length = 15 })),
                    "chase.TrailLength = 5;"),
                Doc.Example(
                    "Custom colors",
                    "Each part uses a distinct concrete color while sharing the same bounce algorithm.",
                    new ChaseIndicator
                    {
                        Length = 15,
                        Style = new ChaseIndicatorStyle(
                            ChaseIndicatorStyle.Default.Face,
                            ChaseIndicatorStyle.Default.Border,
                            ChaseIndicatorStyle.Default.Shadow,
                            ChaseIndicatorStyle.Default.Active,
                            ChaseIndicatorStyle.Default.Inactive,
                            Color.Rgb(0xff, 0x40, 0x40),
                            Color.Rgb(0x80, 0x20, 0x20),
                            Color.Rgb(0x30, 0x30, 0x30))
                    },
                    "chase.Style = chase.Style.Value with { HeadColor = Color.Rgb(0xFF, 0x40, 0x40) };")),
            Doc.Section(
                "⏱️",
                "Playback",
                "Movement defaults to 200 milliseconds, fading to 400 milliseconds; <info>Playing</info> pauses both clocks.",
                Doc.Example(
                    "Running and paused",
                    "Running playback advances automatically while the paused track retains its phase.",
                    Doc.Column(
                        Doc.Row(new Text("Running"), running),
                        Doc.Row(new Text("Paused "), paused)),
                    "chase.Playing = false;")));
    }

    private static Stack Pattern(string label, ChaseIndicatorStyle style) =>
        Doc.Row(new Text(label) { Width = Length.Cells(8) }, new ChaseIndicator { Style = style });

    private static Stack Movement(string label, ChaseMovement movement, int length = 15) =>
        Doc.Row(
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
