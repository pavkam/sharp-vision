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
        var playback = new Spinner { Style = SpinnerStyle.DenseBraille };
        var playbackStatus = new Text("Playback: running");
        var togglePlayback = new Button { Text = "&Pause" };
        togglePlayback.Click += (_, _) =>
        {
            playback.IsPlaying = !playback.IsPlaying;
            togglePlayback.Text = playback.IsPlaying ? "&Pause" : "&Resume";
            playbackStatus.Content = playback.IsPlaying ? "Playback: running" : "Playback: paused";
        };
        var custom = new Spinner
        {
            Padding = new Thickness(1, 0),
            Style = SpinnerStyle.Default with
            {
                Border = new Border(
                    BorderSide.All,
                    BorderGlyphStyle.Rounded,
                    SemanticColor.ControlBorder,
                    Color.Transparent,
                    SemanticDecoration.Border),
                Frames = [new Rune('◐'), new Rune('◓'), new Rune('◑'), new Rune('◒')]
            }
        };
        return new DocPage(
            Title,
            "<info>Spinner</info> displays one non-interactive automatically advancing glyph from a built-in or developer-authored sequence.",
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
                "🎨",
                "Custom frames and chrome",
                "Assign a complete <info>SpinnerStyle</info> to supply one through 256 printable one-cell frames plus intrinsic face, border, and shadow presentation.",
                new DocExample(
                    "Framed quarter-circle sequence",
                    "The live custom sequence stays centered inside one-cell horizontal padding and a rounded border.",
                    new DocRow(new Text("Polling "), custom),
                    "spinner.Padding = new Thickness(1, 0);\nspinner.Style = SpinnerStyle.Default with\n{\n    Border = new Border(\n        BorderSide.All,\n        BorderGlyphStyle.Rounded,\n        SemanticColor.ControlBorder,\n        Color.Transparent,\n        SemanticDecoration.Border),\n    Frames = [new Rune('◐'), new Rune('◓'), new Rune('◑'), new Rune('◒')],\n};")),
            new DocSection(
                "⏱️",
                "Playback",
                "<info>Interval</info> changes cadence, while <info>IsPlaying</info> pauses and resumes without discarding the current frame.",
                new DocExample(
                    "Cadence and live playback",
                    "The first spinner advances every 100 milliseconds; Pause and Resume retain the second spinner's current frame.",
                    new DocColumn(
                        new DocRow(new Text("Fast   "), fast),
                        new DocRow(new Text("Live   "), playback),
                        new DocRow(togglePlayback, playbackStatus)),
                    "toggle.Click += (_, _) => spinner.IsPlaying = !spinner.IsPlaying;")));
    }
}
