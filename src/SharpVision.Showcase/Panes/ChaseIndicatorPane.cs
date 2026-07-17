// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents ChaseIndicator glyph families, track length, and playback.</summary>
internal sealed class ChaseIndicatorPane: CompositeControl
{
    /// <summary>The exact catalog and page name.</summary>
    internal const string Title = "ChaseIndicator";

    /// <summary>Initializes the retained ChaseIndicator documentation page.</summary>
    internal ChaseIndicatorPane() => InitializeContent(CreateContent());

    private static Dock CreateContent()
    {
        var patterns = Doc.Column(
            Pattern("Circle ", ChasePattern.Circle),
            Pattern("Diamond", ChasePattern.Diamond),
            Pattern("Square  ", ChasePattern.Square),
            Pattern("Up      ", ChasePattern.Up),
            Pattern("Down    ", ChasePattern.Down),
            Pattern("Left    ", ChasePattern.Left),
            Pattern("Right   ", ChasePattern.Right));
        var longTrack = new ChaseIndicator
        {
            Pattern = ChasePattern.Diamond,
            Length = 7,
        };
        var running = new ChaseIndicator { Pattern = ChasePattern.Right };
        var paused = new ChaseIndicator
        {
            Pattern = ChasePattern.Square,
            IsPlaying = false,
        };
        return Doc.Page(
            Title,
            "Moves one active glyph forward and backward through a fixed-length horizontal status track.",
            Doc.Section(
                "◆",
                "Glyph families",
                "Seven built-in filled and hollow pairs cover circles, diamonds, squares, and directional triangles.",
                Doc.Example(
                    "Built-in patterns",
                    "Every row is a live five-cell indicator using the same bounce algorithm.",
                    patterns,
                    "var chase = new ChaseIndicator { Pattern = ChasePattern.Diamond };")),
            Doc.Section(
                "↔️",
                "Track length",
                "Length is the exact number of terminal cells and must be at least two.",
                Doc.Example(
                    "Seven-cell track",
                    "The active diamond traverses all seven positions before reversing once at the endpoint.",
                    longTrack,
                    "var chase = new ChaseIndicator { Length = 7 };")),
            Doc.Section(
                "⏱️",
                "Playback",
                "The default interval is 200 milliseconds; IsPlaying pauses at the current position.",
                Doc.Example(
                    "Running and paused",
                    "Running playback advances automatically while the paused track retains its phase.",
                    Doc.Column(
                        Doc.Row(new Text("Running"), running),
                        Doc.Row(new Text("Paused "), paused)),
                    "chase.IsPlaying = false;")));
    }

    private static Stack Pattern(string label, ChasePattern pattern) =>
        Doc.Row(new Text(label), new ChaseIndicator { Pattern = pattern });
}
