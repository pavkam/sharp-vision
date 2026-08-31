// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents Wrap with source-order rows, columns, gaps, reflow, and participation states.</summary>
internal sealed class WrapPane: CompositeControlBase
{
    /// <summary>Initializes the retained Wrap showcase content.</summary>
    internal WrapPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Wrap";

    private static DocPage CreateContent()
    {
        var horizontal = new Wrap { Width = Length.Cells(44), Spacing = 1, LineSpacing = 1 };
        horizontal.Children.Add(Card("Build", BorderGlyphStyle.Light, 10));
        horizontal.Children.Add(Card("Test", BorderGlyphStyle.Heavy, 10));
        horizontal.Children.Add(Card("Publish", BorderGlyphStyle.Paired, 12));
        horizontal.Children.Add(Card("Inspect", BorderGlyphStyle.Rounded, 12));

        var vertical = new Wrap
        {
            Orientation = Orientation.Vertical,
            Width = Length.Cells(44),
            Height = Length.Cells(8),
            Spacing = 1,
            LineSpacing = 2
        };
        vertical.Children.Add(Card("One", BorderGlyphStyle.Light, 10));
        vertical.Children.Add(Card("Two", BorderGlyphStyle.Heavy, 10));
        vertical.Children.Add(Card("Three", BorderGlyphStyle.Paired, 10));
        vertical.Children.Add(Card("Four", BorderGlyphStyle.Rounded, 10));

        var reflow = new Wrap { Width = Length.Percent(100), Spacing = 1, LineSpacing = 1 };
        reflow.Children.Add(Card("Tag", BorderGlyphStyle.Light, 8));
        var description = ShowcasePaneHelpers.Card(
            "This full-lane child wraps its text as the showcase reading column changes width.",
            BorderGlyphStyle.Heavy,
            new Thickness(1, 0),
            Overflow.Wrap);
        description.Width = Length.Percent(100);
        reflow.Children.Add(description);
        reflow.Children.Add(Card("Done", BorderGlyphStyle.Paired, 9));

        var participant = Card("Optional", BorderGlyphStyle.Light, 12);
        var participation = new Wrap { Width = Length.Cells(44), Spacing = 1, LineSpacing = 1 };
        participation.Children.Add(participant);
        participation.Children.Add(Card("Always present", BorderGlyphStyle.Heavy, 15));
        participation.Children.Add(Card("Next item", BorderGlyphStyle.Paired, 12));
        var status = new Text("Optional: visible and participating.");
        var cycle = new Button { Text = "&Cycle visibility" };
        cycle.Click += (_, _) =>
        {
            participant.Visibility = participant.Visibility == Visibility.Visible
                ? Visibility.Hidden
                : participant.Visibility == Visibility.Hidden
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            status.Content = participant.Visibility == Visibility.Hidden
                ? "Optional: hidden; its slot and gap remain."
                : participant.Visibility == Visibility.Collapsed
                    ? "Optional: collapsed; its slot and gaps are released."
                    : "Optional: visible and participating.";
        };

        return new DocPage(
            Title,
            "<info>Wrap</info> packs direct children in source order into rows or columns, starting a new line when the next child cannot fit.",
            new DocSection(
                "↔️",
                "Horizontal rows",
                "Spacing separates children in a row; LineSpacing separates the rows created by the finite width.",
                new DocExample(
                    "Commands in source order",
                    "Shrink the reading column to watch the next command begin a fresh row without changing ownership order.",
                    horizontal,
                    "var wrap = new Wrap { Spacing = 1, LineSpacing = 1 };")),
            new DocSection(
                "↕️",
                "Vertical columns",
                "Vertical orientation makes height the wrapping lane and places each newly wrapped line in the next column.",
                new DocExample(
                    "Short vertical lane",
                    "The four cards fill downward, then continue into the next column with two cells between columns.",
                    vertical)),
            new DocSection(
                "📏",
                "Full-lane reflow",
                "A percentage child uses the complete primary lane, so it takes its own row and its wrapped text remeasures after resize.",
                new DocExample(
                    "Responsive description",
                    "Tag and Done remain ordinary source-order items around the full-lane description.",
                    reflow)),
            new DocSection(
                "👁️",
                "Hidden and collapsed",
                "Hidden keeps its reserved rectangle; Collapsed removes its rectangle and adjacent spacing from the pack.",
                new DocExample(
                    "Optional command",
                    "Cycle through visible, hidden, and collapsed to compare retained and released participation.",
                    new DocColumn(cycle, status, participation))));
    }

    private static Dock Card(string text, BorderGlyphStyle glyphs, int width)
    {
        var card = ShowcasePaneHelpers.Card(text, glyphs, new Thickness(1, 0), Overflow.Wrap);
        card.Width = Length.Cells(width);
        return card;
    }
}
