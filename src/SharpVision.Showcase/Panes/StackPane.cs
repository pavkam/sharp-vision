// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the Stack control with orientation, spacing, and reverse-order specimens.</summary>
internal sealed class StackPane: CompositeControl
{

    internal StackPane() => InitializeContent(CreateContent());
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Stack";

    /// <inheritdoc/>
    private static Dock CreateContent()
    {
        Stack horizontal = new() { Orientation = Orientation.Horizontal, Spacing = 2, Width = Length.Cells(40) };
        var fixedCard = Card("Fixed 10", Glyphs.Light);
        fixedCard.Width = Length.Cells(10);
        horizontal.Children.Add(fixedCard);
        var percentCard = Card("35%", Glyphs.Heavy);
        percentCard.Width = Length.Percent(35);
        horizontal.Children.Add(percentCard);
        var starCard = Card("1*", Glyphs.Paired);
        starCard.Width = Length.Star(1);
        horizontal.Children.Add(starCard);

        Stack reversed = new() { Orientation = Orientation.Horizontal, Spacing = 2, Reverse = true };
        reversed.Children.Add(Card("First", Glyphs.Light));
        reversed.Children.Add(Card("Second", Glyphs.Heavy));
        reversed.Children.Add(Card("Third", Glyphs.Paired));

        Stack vertical = new() { Spacing = 1 };
        vertical.Children.Add(Card("Top", Glyphs.Rounded));
        vertical.Children.Add(Card("Spacing = 1", Glyphs.Light));
        vertical.Children.Add(Card("Bottom", Glyphs.Heavy));

        var horizontalOrientation = new Stack { Orientation = Orientation.Horizontal, Spacing = 1 };
        horizontalOrientation.Children.Add(Card("Left", Glyphs.Light));
        horizontalOrientation.Children.Add(Card("Center", Glyphs.Rounded));
        horizontalOrientation.Children.Add(Card("Right", Glyphs.Heavy));

        var margins = new Stack { Orientation = Orientation.Horizontal, Spacing = 1 };
        var marginCard = Card("Margin 2", Glyphs.Light);
        marginCard.Margin = new Thickness(2, 0);
        margins.Children.Add(marginCard);
        margins.Children.Add(Card("Spacing 1", Glyphs.Heavy));

        var hidden = new Text("Hidden keeps its track") { Visibility = Visibility.Hidden };
        var collapsed = new Text("Collapsed releases its track") { Visibility = Visibility.Collapsed };
        var visibility = new Stack { Spacing = 1 };
        visibility.Children.Add(Card("Before", Glyphs.Light));
        visibility.Children.Add(hidden);
        visibility.Children.Add(collapsed);
        visibility.Children.Add(Card("After", Glyphs.Heavy));

        var constrained = new Stack
        {
            Orientation = Orientation.Horizontal,
            Width = Length.Cells(16),
            Spacing = 2,
        };
        var constrainedFixed = Card("Fixed", Glyphs.Light);
        constrainedFixed.Width = Length.Cells(12);
        var constrainedStar = Card("Star", Glyphs.Paired);
        constrainedStar.Width = Length.Star(1);
        constrained.Children.Add(constrainedFixed);
        constrained.Children.Add(constrainedStar);

        var actionBar = new Stack { Orientation = Orientation.Horizontal, Spacing = 1, Width = Length.Cells(40) };
        actionBar.Children.Add(new Button { Content = new Text("Cancel") });
        var spacer = new Dock { Width = Length.Star(1) };
        actionBar.Children.Add(spacer);
        actionBar.Children.Add(new Button { Content = new Text("Save") });

        return Doc.Page(
            Title,
            "Arranges children sequentially with fixed, automatic, percentage, or proportional lengths and stable spacing.",
            Doc.Section(
                "📚",
                "Orientation",
                "Choose the sequential axis; the child collection remains the stable ownership and navigation source.",
                Doc.Example(
                    "Vertical and horizontal",
                    "Vertical is the default. Horizontal places the same kind of children left to right.",
                    Doc.Column(vertical, horizontalOrientation),
                    "var actions = new Stack\n{\n    Orientation = Orientation.Horizontal,\n    Spacing = 1,\n};")),
            Doc.Section(
                "📚",
                "Mixed sizing",
                "Fixed, percentage, automatic, and proportional lengths share one deterministic axis allocation.",
                Doc.Example(
                    "Fixed, percent, and star",
                    "The fixed card reserves ten cells, percentage resolves once against the inner width, and star receives the remainder.",
                    horizontal)),
            Doc.Section(
                "📚",
                "Spacing and margins",
                "Stack spacing belongs between participating tracks; margins belong outside individual children.",
                Doc.Example(
                    "Two different gaps",
                    "Compare the two-cell external margin around the first card with the one-cell inter-child spacing.",
                    margins)),
            Doc.Section(
                "📚",
                "Reverse",
                "Reverse changes geometry, rendering, and default focus traversal without reparenting children.",
                Doc.Example(
                    "Stable source; reversed presentation",
                    "The source order remains First, Second, Third while the visible and keyboard order runs in reverse.",
                    reversed)),
            Doc.Section(
                "📚",
                "Visibility",
                "Hidden children retain a track while Collapsed children consume neither a track nor adjacent spacing.",
                Doc.Example(
                    "Hidden versus collapsed",
                    "Only the surrounding cards render, but their gap reveals that Hidden still participates while Collapsed does not.",
                    visibility)),
            Doc.Section(
                "📚",
                "Constrained space",
                "Containment wins when requests and spacing cannot fit; later flexible tracks may shrink safely to zero.",
                Doc.Example(
                    "Saturated allocation",
                    "A twelve-cell fixed card and spacing leave only the safe remainder for the proportional card.",
                    constrained)),
            Doc.Section(
                "📚",
                "Action-bar recipe",
                "A proportional spacer pushes the primary command to the trailing edge without absolute positioning.",
                Doc.Example(
                    "Secondary and primary actions",
                    "Resize the page and the spacer absorbs the changing remainder between Cancel and Save.",
                    actionBar)));
    }

    private static Dock Card(string text, Glyphs glyphs) => new()
    {
        BorderThickness = new Thickness(1),
        BorderGlyphs = glyphs,
        Padding = new Thickness(1, 0),
        Children = { new Text(text) },
    };
}
