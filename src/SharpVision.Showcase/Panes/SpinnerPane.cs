// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents Spinner patterns, compatibility, cadence, and pause behavior.</summary>
internal sealed class SpinnerPane: CompositeControl
{
    /// <summary>The exact catalog and page name.</summary>
    internal const string Title = "Spinner";

    /// <summary>Initializes the retained Spinner documentation page.</summary>
    internal SpinnerPane() => InitializeContent(CreateContent());

    private static Dock CreateContent()
    {
        var braille = new Spinner();
        var dense = new Spinner { Pattern = SpinnerPattern.DenseBraille };
        var ascii = new Spinner { Pattern = SpinnerPattern.Ascii };
        var fast = new Spinner { Interval = TimeSpan.FromMilliseconds(100) };
        var paused = new Spinner
        {
            Pattern = SpinnerPattern.DenseBraille,
            IsPlaying = false,
        };
        return Doc.Page(
            Title,
            "Displays one non-interactive automatically advancing glyph from a fixed built-in sequence.",
            Doc.Section(
                "🌀",
                "Braille",
                "The default ten-frame Braille orbit is compact, neutral-width, and suited to ordinary status activity.",
                Doc.Example(
                    "Default orbit",
                    "Playback starts automatically at the documented 200 millisecond interval.",
                    Doc.Row(new Text("Working "), braille),
                    "var spinner = new Spinner();")),
            Doc.Section(
                "⣿",
                "Dense rotation",
                "DenseBraille uses eight filled Braille patterns for a heavier visual pulse.",
                Doc.Example(
                    "Dense status",
                    "Use the dense sequence when the indicator should carry more visual weight.",
                    Doc.Row(new Text("Loading "), dense))),
            Doc.Section(
                "|",
                "ASCII fallback",
                "Ascii rotates through vertical, slash, horizontal, and backslash for maximum font compatibility.",
                Doc.Example(
                    "Portable status",
                    "Select this pattern when a terminal font does not cover Braille patterns.",
                    Doc.Row(new Text("Connecting "), ascii))),
            Doc.Section(
                "⏱️",
                "Playback",
                "Interval changes cadence, while IsPlaying pauses and resumes without discarding the current frame.",
                Doc.Example(
                    "Fast and paused",
                    "The first spinner advances every 100 milliseconds; the second remains paused.",
                    Doc.Column(
                        Doc.Row(new Text("Fast   "), fast),
                        Doc.Row(new Text("Paused "), paused)),
                    "var paused = new Spinner { IsPlaying = false };")));
    }
}
