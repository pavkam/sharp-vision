// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the Stack control with orientation, spacing, and reverse-order specimens.</summary>
internal sealed class StackPane: CompositeControlBase
{
    internal StackPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Stack";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        // Fixed, percentage, and star widths share one horizontal axis.
        Stack horizontal = new() { Orientation = Orientation.Horizontal, Spacing = 2, Width = Length.Cells(40) };
        var fixedCard = Card("Fixed 10", BorderGlyphStyle.Light);
        fixedCard.Width = Length.Cells(10);
        horizontal.Children.Add(fixedCard);
        var percentCard = Card("35%", BorderGlyphStyle.Heavy);
        percentCard.Width = Length.Percent(35);
        horizontal.Children.Add(percentCard);
        var starCard = Card("1*", BorderGlyphStyle.Paired);
        starCard.Width = Length.Star(1);
        horizontal.Children.Add(starCard);

        // Reverse changes presentation order without reparenting children.
        Stack reversed = new() { Orientation = Orientation.Horizontal, Spacing = 2, Reverse = true };
        reversed.Children.Add(Card("First", BorderGlyphStyle.Light));
        reversed.Children.Add(Card("Second", BorderGlyphStyle.Heavy));
        reversed.Children.Add(Card("Third", BorderGlyphStyle.Paired));

        // Vertical stack with explicit region heights.
        Stack vertical = new() { Spacing = 1, Width = Length.Cells(50) };
        var vHeader = new Dock
        {
            Height = Length.Cells(3),
            Padding = new Thickness(1, 0),
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Rounded,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Children = { new Text("Header region (3 rows)") }
        };
        var vContent = new Dock
        {
            Height = Length.Cells(5),
            Padding = new Thickness(1, 0),
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Light,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Children =
            {
                new Text("Content region (5 rows)\nSpacing = 1 between children") { Overflow = Overflow.Wrap }
            }
        };
        var vFooter = new Dock
        {
            Height = Length.Cells(3),
            Padding = new Thickness(1, 0),
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Heavy,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Children = { new Text("Footer region (3 rows)") }
        };
        vertical.Children.Add(vHeader);
        vertical.Children.Add(vContent);
        vertical.Children.Add(vFooter);

        // Horizontal orientation with fixed sidebar and star main area.
        var horizontalOrientation =
            new Stack { Orientation = Orientation.Horizontal, Spacing = 1, Width = Length.Cells(50) };
        var hNav = new Dock
        {
            Width = Length.Cells(12),
            Height = Length.Cells(6),
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Rounded,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Padding = new Thickness(1, 0),
            Children = { new Text("Sidebar\n12 cells") { Overflow = Overflow.Wrap } }
        };
        var hMain = new Dock
        {
            Width = Length.Star(1),
            Height = Length.Cells(6),
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Light,
                ThemeColor.ControlBorder,
                Color.Transparent,
                ThemeDecoration.Border),
            Padding = new Thickness(1, 0),
            Children = { new Text("Main area\nFills remaining width") { Overflow = Overflow.Wrap } }
        };
        horizontalOrientation.Children.Add(hNav);
        horizontalOrientation.Children.Add(hMain);

        // Margin belongs outside a child; spacing belongs between participating tracks.
        var margins = new Stack { Orientation = Orientation.Horizontal, Spacing = 1 };
        var marginCard = Card("Margin 2", BorderGlyphStyle.Light);
        marginCard.Margin = new Thickness(2, 0);
        margins.Children.Add(marginCard);
        margins.Children.Add(Card("Spacing 1", BorderGlyphStyle.Heavy));

        // Hidden keeps its track; Collapsed releases it and adjacent spacing.
        var hidden = new Text("Hidden keeps its track") { Visibility = Visibility.Hidden };
        var collapsed = new Text("Collapsed releases its track") { Visibility = Visibility.Collapsed };
        var visibility = new Stack { Spacing = 1 };
        visibility.Children.Add(Card("Before", BorderGlyphStyle.Light));
        visibility.Children.Add(hidden);
        visibility.Children.Add(collapsed);
        visibility.Children.Add(Card("After", BorderGlyphStyle.Heavy));

        // Over-constrained requests saturate safely instead of producing negative geometry.
        var constrained = new Stack { Orientation = Orientation.Horizontal, Width = Length.Cells(16), Spacing = 2 };
        var constrainedFixed = Card("Fixed", BorderGlyphStyle.Light);
        constrainedFixed.Width = Length.Cells(12);
        var constrainedStar = Card("Star", BorderGlyphStyle.Paired);
        constrainedStar.Width = Length.Star(1);
        constrained.Children.Add(constrainedFixed);
        constrained.Children.Add(constrainedStar);

        // Star spacer pushes the primary action to the trailing edge.
        var actionBar = new Stack { Orientation = Orientation.Horizontal, Spacing = 1, Width = Length.Cells(40) };
        actionBar.Children.Add(new Button { Content = new Text("&Cancel") });
        var spacer = new Dock { Width = Length.Star(1) };
        actionBar.Children.Add(spacer);
        actionBar.Children.Add(new Button { Content = new Text("&Save") });

        return new DocPage(
            Title,
            "<info>Stack</info> arranges children sequentially with fixed, automatic, percentage, or proportional lengths and stable <info>Spacing</info>.",
            new DocSection(
                "📚",
                "Orientation",
                "Choose the sequential axis; the child collection remains the authoritative source for child identity and keyboard navigation.",
                new DocExample(
                    "Vertical and horizontal",
                    "Vertical is the default. Horizontal places the same kind of children left to right.",
                    new DocColumn(vertical, horizontalOrientation),
                    "var actions = new Stack\n{\n    Orientation = Orientation.Horizontal,\n    Spacing = 1,\n};")),
            new DocSection(
                "📐",
                "Mixed sizing",
                "Fixed, percentage, automatic, and proportional lengths share one deterministic axis allocation.",
                new DocExample(
                    "Fixed, percent, and star",
                    "The fixed card reserves ten cells, percentage resolves once against the inner width, and star receives the remainder.",
                    horizontal)),
            new DocSection(
                "↔️",
                "Spacing and margins",
                "Stack spacing belongs between participating tracks; margins belong outside individual children.",
                new DocExample(
                    "Two different gaps",
                    "Compare the two-cell external margin around the first card with the one-cell inter-child spacing.",
                    margins)),
            new DocSection(
                "🔄",
                "Reverse",
                "Reverse changes geometry, rendering, and default focus traversal without reparenting children.",
                new DocExample(
                    "Stable source; reversed presentation",
                    "The source order remains First, Second, Third while the visible and keyboard order runs in reverse.",
                    reversed)),
            new DocSection(
                "👁️",
                "Visibility",
                "Hidden children retain a track while Collapsed children consume neither a track nor adjacent spacing.",
                new DocExample(
                    "Hidden versus collapsed",
                    "Only the surrounding cards render, but their gap reveals that Hidden still participates while Collapsed does not.",
                    visibility)),
            new DocSection(
                "📏",
                "Constrained space",
                "The container's size limit prevails when requests and spacing cannot fit; later flexible tracks may shrink safely to zero.",
                new DocExample(
                    "Saturated allocation",
                    "A twelve-cell fixed card and spacing leave only the safe remainder for the proportional card.",
                    constrained)),
            new DocSection(
                "🧩",
                "Action-bar recipe",
                "A proportional spacer pushes the primary command to the trailing edge without absolute positioning.",
                new DocExample(
                    "Secondary and primary actions",
                    "Resize the page and the spacer absorbs the changing remainder between Cancel and Save.",
                    actionBar)));
    }

    private static Dock Card(string text, BorderGlyphStyle glyphs) =>
        ShowcasePaneHelpers.Card(text, glyphs);
}
