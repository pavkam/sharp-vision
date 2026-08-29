// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the Dock control with side-attached and fill layout specimens.</summary>
internal sealed class DockPane: CompositeControlBase
{
    internal DockPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Dock";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        // Application shell: four edge regions plus a fill child.
        Dock allSides = new()
        {
            Width = Length.Cells(46),
            Height = Length.Cells(13),
            LastChildFills = true,
            Spacing = 1
        };

        var left = Card("Explorer", BorderGlyphStyle.Light);
        left.Width = Length.Cells(12);
        Dock.SetSide(left, DockSide.Left);
        allSides.Children.Add(left);

        var top = Card("Application header", BorderGlyphStyle.Heavy);
        top.Height = Length.Cells(3);
        Dock.SetSide(top, DockSide.Top);
        allSides.Children.Add(top);

        var right = Card("Inspector", BorderGlyphStyle.Paired);
        right.Width = Length.Cells(13);
        Dock.SetSide(right, DockSide.Right);
        allSides.Children.Add(right);

        var bottom = Card("Status bar", BorderGlyphStyle.Ascii);
        bottom.Height = Length.Cells(3);
        Dock.SetSide(bottom, DockSide.Bottom);
        allSides.Children.Add(bottom);
        allSides.Children.Add(Card("Editor workspace", BorderGlyphStyle.Rounded));

        // Repeated left sides consume the remainder in insertion order.
        Dock order = new() { Width = Length.Cells(42), Height = Length.Cells(7), LastChildFills = true, Spacing = 1 };

        var first = Card("1: Left", BorderGlyphStyle.Light);
        first.Width = Length.Cells(12);
        Dock.SetSide(first, DockSide.Left);
        order.Children.Add(first);

        var second = Card("2: Left", BorderGlyphStyle.Light);
        second.Width = Length.Cells(12);
        Dock.SetSide(second, DockSide.Left);
        order.Children.Add(second);
        order.Children.Add(Card("3: Fill", BorderGlyphStyle.Rounded));

        // LastChildFills false leaves unused remainder visible.
        Dock noFill = new() { Width = Length.Cells(34), Height = Length.Cells(4), LastChildFills = false };

        var onlyChild = Card("No remainder claimed", BorderGlyphStyle.Heavy);
        onlyChild.Width = Length.Cells(24);
        Dock.SetSide(onlyChild, DockSide.Top);
        noFill.Children.Add(onlyChild);

        // Percentage edge sizes resolve against the rectangle available at each iteration.
        var remaining = new Dock { Width = Length.Cells(46), Height = Length.Cells(5), Spacing = 1 };

        var firstPercent = Card("23 cells", BorderGlyphStyle.Light);
        firstPercent.Width = Length.Percent(50);
        Dock.SetSide(firstPercent, DockSide.Left);
        remaining.Children.Add(firstPercent);

        var secondPercent = Card("11 cells", BorderGlyphStyle.Heavy);
        secondPercent.Width = Length.Percent(50);
        Dock.SetSide(secondPercent, DockSide.Left);
        remaining.Children.Add(secondPercent);
        remaining.Children.Add(Card("Fill", BorderGlyphStyle.Rounded));

        // Collapsed edge regions release geometry and spacing to the fill child.
        var shell = new Dock { Width = Length.Cells(38), Height = Length.Cells(6) };

        var collapsibleSidebar = Card("Sidebar", BorderGlyphStyle.Light);
        collapsibleSidebar.Width = Length.Cells(12);
        Dock.SetSide(collapsibleSidebar, DockSide.Left);
        shell.Children.Add(collapsibleSidebar);
        shell.Children.Add(Card("Main content", BorderGlyphStyle.Rounded));

        var shellStatus = new Text("Sidebar: visible");

        var toggleSidebar = new Button { Text = "&Toggle sidebar" };
        toggleSidebar.Click += (_, _) =>
        {
            collapsibleSidebar.Visibility = collapsibleSidebar.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
            shellStatus.Content = collapsibleSidebar.Visibility == Visibility.Collapsed
                ? "Sidebar: collapsed; main reclaimed the remainder"
                : "Sidebar: visible";
        };

        // Over-consumption saturates at zero instead of producing negative child bounds.
        var constrained = new Dock { Width = Length.Cells(14), Height = Length.Cells(4), Spacing = 2 };
        var tooWide = Card("12 cells", BorderGlyphStyle.Light);
        tooWide.Width = Length.Cells(12);
        Dock.SetSide(tooWide, DockSide.Left);
        constrained.Children.Add(tooWide);
        var trailing = Card("Safe", BorderGlyphStyle.Heavy);
        trailing.Width = Length.Cells(12);
        Dock.SetSide(trailing, DockSide.Right);
        constrained.Children.Add(trailing);
        constrained.Children.Add(Card("Fill", BorderGlyphStyle.Rounded));

        // Star weights split the leftover axis space proportionally; 2* receives twice 1*'s share.
        Dock starWeights = new() { Width = Length.Cells(45), Height = Length.Cells(4), LastChildFills = false };

        var single = Card("1* = 15 cells", BorderGlyphStyle.Light);
        single.Width = Length.Star(1);
        Dock.SetSide(single, DockSide.Left);
        starWeights.Children.Add(single);

        var doubled = Card("2* = 30 cells", BorderGlyphStyle.Heavy);
        doubled.Width = Length.Star(2);
        Dock.SetSide(doubled, DockSide.Left);
        starWeights.Children.Add(doubled);

        // A MinWidth reservation comes out of the shared pool before the weighted split runs, so
        // the reserving Star ends up wider than its unconstrained sibling instead of losing cells.
        Dock starMinimum = new() { Width = Length.Cells(40), Height = Length.Cells(4), LastChildFills = false };

        var reserving = Card("24 min + 8 share", BorderGlyphStyle.Light);
        reserving.Width = Length.Star(1);
        reserving.MinWidth = Length.Cells(24);
        Dock.SetSide(reserving, DockSide.Left);
        starMinimum.Children.Add(reserving);

        var unconstrained = Card("8 cells", BorderGlyphStyle.Heavy);
        unconstrained.Width = Length.Star(1);
        Dock.SetSide(unconstrained, DockSide.Left);
        starMinimum.Children.Add(unconstrained);

        return new DocPage(
            Title,
            "<info>Dock</info> consumes physical edges in child order and optionally gives the final child all remaining space through <info>LastChildFills</info>.",
            new DocSection(
                "⚓",
                "Application shell",
                "Compose familiar application regions by consuming physical edges and leaving the center to the final child.",
                new DocExample(
                    "Explorer, header, inspector, status, and editor",
                    "Each named region uses Dock.SetSide; Editor workspace receives the rectangle left after the four edge regions.",
                    allSides,
                    "var shell = new Dock { LastChildFills = true };\nDock.SetSide(sidebar, DockSide.Left);\nshell.Children.Add(sidebar);\nshell.Children.Add(main);")),
            new DocSection(
                "⚓",
                "Order and spacing",
                "Children consume the current remainder in insertion order, with spacing after each non-final participant.",
                new DocExample(
                    "Repeated left sides",
                    "The first left child takes the outer strip; the second starts after it and its gap.",
                    order)),
            new DocSection(
                "⚓",
                "Sizing from the remainder",
                "Percentage edge sizes resolve against the rectangle available at that iteration, not the original panel.",
                new DocExample(
                    "Successive percentages",
                    "The first receives 23 of 46 cells; the second receives 11 of the remainder; Fill receives the rest.",
                    remaining)),
            new DocSection(
                "⚓",
                "Collapse and fill",
                "Collapsed edge regions consume neither geometry nor spacing, so the fill child reclaims their cells.",
                new DocExample(
                    "Optional sidebar",
                    "Toggle the sidebar and watch Main content reclaim or release the exact left strip.",
                    new DocColumn(toggleSidebar, shellStatus, shell))),
            new DocSection(
                "⚓",
                "Constrained space",
                "Over-consumption saturates the remaining rectangle at zero instead of producing negative child bounds.",
                new DocExample(
                    "Requests larger than the host",
                    "Both edge requests exceed the available width; every committed rectangle remains contained and non-negative.",
                    constrained)),
            new DocSection(
                "⚓",
                "Fill policy",
                "Disable <info>LastChildFills</info> when the final child should honor its own side and leave unused remainder visible.",
                new DocExample(
                    "Unclaimed remainder",
                    "The final top child takes only its requested extent; the rest of the panel stays empty.",
                    noFill)),
            new DocSection(
                "⚓",
                "Proportional sharing",
                "Star-length children share the axis space left after every other side has claimed its edge, splitting the remainder by weight instead of the first child claiming it all.",
                new DocExample(
                    "Weighted Star siblings",
                    "The 2* card receives twice as many cells as the 1* card from the shared 45-cell width.",
                    starWeights,
                    "left.Width = Length.Star(1);\nright.Width = Length.Star(2);\nDock.SetSide(left, DockSide.Left);\nDock.SetSide(right, DockSide.Left);"),
                new DocExample(
                    "MinWidth reserved first",
                    "The left card reserves its 24-cell minimum before the leftover 16 cells split evenly between both Star weights, ending up wider than its unconstrained sibling.",
                    starMinimum)));
    }

    private static Dock Card(string label, BorderGlyphStyle glyphs) =>
        ShowcasePaneHelpers.Card(label, glyphs, new Thickness(1, 0));
}
