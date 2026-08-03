// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents Spinner patterns, compatibility, cadence, and pause behavior.</summary>
internal sealed class SpinnerPane: CompositeControlBase
{
    /// <summary>The exact catalog and page name.</summary>
    internal const string Title = "Spinner";

    /// <summary>Initializes the retained Spinner documentation page.</summary>
    internal SpinnerPane() => InitializeContent(CreateContent());

    private static DocPage CreateContent()
    {
        var braille = new Spinner();
        var dense = new Spinner { Style = SpinnerStyle.DenseBraille };
        var ascii = new Spinner { Style = SpinnerStyle.Ascii };
        var fast = new Spinner { Interval = TimeSpan.FromMilliseconds(100) };
        var paused = new Spinner { Style = SpinnerStyle.DenseBraille, IsPlaying = false };
        return new DocPage(
            Title,
            "<info>Spinner</info> displays one non-interactive automatically advancing glyph from a fixed built-in sequence.",
            new DocSection(
                "🌀",
                "Braille",
                "The default ten-frame Braille orbit is compact, neutral-width, and suited to ordinary status activity.",
                new DocExample(
                    "Default orbit",
                    "Playback starts automatically at the documented 200 millisecond interval.",
                    new DocRow(new Text("Working "), braille),
                    "var spinner = new Spinner();")),
            new DocSection(
                "⣿",
                "Dense rotation",
                "DenseBraille uses eight filled Braille patterns for a heavier visual pulse.",
                new DocExample(
                    "Dense status",
                    "Use the dense sequence when the indicator should carry more visual weight.",
                    new DocRow(new Text("Loading "), dense))),
            new DocSection(
                "|",
                "ASCII fallback",
                "Ascii rotates through vertical, slash, horizontal, and backslash for maximum font compatibility.",
                new DocExample(
                    "Portable status",
                    "Select this pattern when a terminal font does not cover Braille patterns.",
                    new DocRow(new Text("Connecting "), ascii))),
            new DocSection(
                "⏱️",
                "Playback",
                "<info>Interval</info> changes cadence, while <info>IsPlaying</info> pauses and resumes without discarding the current frame.",
                new DocExample(
                    "Fast and paused",
                    "The first spinner advances every 100 milliseconds; the second remains paused.",
                    new DocColumn(
                        new DocRow(new Text("Fast   "), fast),
                        new DocRow(new Text("Paused "), paused)),
                    "var paused = new Spinner { IsPlaying = false };")));
    }
}
