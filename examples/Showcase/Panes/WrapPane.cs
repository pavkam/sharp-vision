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
        var horizontalWidth = 44;
        var horizontal = new Wrap
        {
            Width = Length.Cells(horizontalWidth),
            Spacing = 1,
            LineSpacing = 1
        };
        horizontal.Children.Add(Card("Build", BorderGlyphStyle.Light, 10));
        horizontal.Children.Add(Card("Test", BorderGlyphStyle.Heavy, 10));
        horizontal.Children.Add(Card("Publish", BorderGlyphStyle.Paired, 12));
        horizontal.Children.Add(Card("Inspect", BorderGlyphStyle.Rounded, 12));
        var horizontalStatus = new Text(
            "Width: 44 cells\nSource order: Build → Test → Publish → Inspect")
        {
            Overflow = Overflow.Wrap
        };
        var narrow = new Button { Text = "&Narrow rows" };
        var widen = new Button { Text = "&Widen rows" };
        narrow.Click += (_, _) => ResizeHorizontal(-20);
        widen.Click += (_, _) => ResizeHorizontal(20);
        UpdateHorizontalActions();

        var verticalHeight = 8;
        var vertical = new Wrap
        {
            Orientation = Orientation.Vertical,
            Width = Length.Cells(44),
            Height = Length.Cells(verticalHeight),
            Spacing = 1,
            LineSpacing = 2
        };
        vertical.Children.Add(Card("One", BorderGlyphStyle.Light, 9));
        vertical.Children.Add(Card("Two", BorderGlyphStyle.Heavy, 9));
        vertical.Children.Add(Card("Three", BorderGlyphStyle.Paired, 9));
        vertical.Children.Add(Card("Four", BorderGlyphStyle.Rounded, 9));
        var verticalStatus = new Text(
            "Height: 8 cells\nSource order: One → Two → Three → Four")
        {
            Overflow = Overflow.Wrap
        };
        var shorten = new Button { Text = "&Shorten columns" };
        var lengthen = new Button { Text = "&Lengthen columns" };
        shorten.Click += (_, _) => ResizeVertical(-2);
        lengthen.Click += (_, _) => ResizeVertical(2);
        UpdateVerticalActions();

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
                    "Horizontal reflow controls",
                    "Narrow or widen the explicit lane. Cards move between rows while the status keeps the unchanged source order visible.",
                    new DocColumn(
                        horizontal,
                        new DocRow(narrow, widen),
                        horizontalStatus),
                    "var wrap = new Wrap\n{\n    Width = Length.Cells(44),\n    Spacing = 1,\n    LineSpacing = 1\n};")),
            new DocSection(
                "↕️",
                "Vertical columns",
                "Vertical orientation makes height the wrapping lane and places each newly wrapped line in the next column.",
                new DocExample(
                    "Vertical reflow controls",
                    "Shorten or lengthen the explicit lane. Cards fill downward, then continue into the next column without changing source order.",
                    new DocColumn(
                        vertical,
                        new DocRow(shorten, lengthen),
                        verticalStatus))),
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

        void ResizeHorizontal(int delta)
        {
            horizontalWidth = Math.Clamp(horizontalWidth + delta, 24, 44);
            horizontal.Width = Length.Cells(horizontalWidth);
            horizontalStatus.Content =
                $"Width: {horizontalWidth} cells\nSource order: Build → Test → Publish → Inspect";
            UpdateHorizontalActions();
        }

        void ResizeVertical(int delta)
        {
            verticalHeight = Math.Clamp(verticalHeight + delta, 4, 10);
            vertical.Height = Length.Cells(verticalHeight);
            verticalStatus.Content =
                $"Height: {verticalHeight} cells\nSource order: One → Two → Three → Four";
            UpdateVerticalActions();
        }

        void UpdateHorizontalActions()
        {
            narrow.IsEnabled = horizontalWidth > 24;
            widen.IsEnabled = horizontalWidth < 44;
        }

        void UpdateVerticalActions()
        {
            shorten.IsEnabled = verticalHeight > 4;
            lengthen.IsEnabled = verticalHeight < 10;
        }
    }

    private static Dock Card(string text, BorderGlyphStyle glyphs, int width)
    {
        var card = ShowcasePaneHelpers.Card(text, glyphs, new Thickness(1, 0), Overflow.Wrap);
        card.Width = Length.Cells(width);
        return card;
    }
}
