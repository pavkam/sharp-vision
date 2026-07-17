// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the Dock control with side-attached and fill layout specimens.</summary>
internal sealed class DockPane: CompositeControl
{

    internal DockPane() => InitializeContent(CreateContent());
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Dock";

    /// <inheritdoc/>
    private static Dock CreateContent()
    {
        Dock allSides = new()
        {
            Width = Length.Cells(56),
            Height = Length.Cells(13),
            LastChildFills = true,
            Spacing = 1,
        };
        var left = Card("Explorer", Glyphs.Light);
        left.Width = Length.Cells(12);
        Dock.SetSide(left, Side.Left);
        allSides.Children.Add(left);
        var top = Card("Application header", Glyphs.Heavy);
        top.Height = Length.Cells(3);
        Dock.SetSide(top, Side.Top);
        allSides.Children.Add(top);
        var right = Card("Inspector", Glyphs.Paired);
        right.Width = Length.Cells(13);
        Dock.SetSide(right, Side.Right);
        allSides.Children.Add(right);
        var bottom = Card("Status bar", Glyphs.Ascii);
        bottom.Height = Length.Cells(3);
        Dock.SetSide(bottom, Side.Bottom);
        allSides.Children.Add(bottom);
        allSides.Children.Add(Card("Editor workspace", Glyphs.Rounded));

        Dock order = new()
        {
            Width = Length.Cells(42),
            Height = Length.Cells(7),
            LastChildFills = true,
        };
        var first = Card("1: Left", Glyphs.Light);
        first.Width = Length.Cells(12);
        Dock.SetSide(first, Side.Left);
        order.Children.Add(first);
        var second = Card("2: Left", Glyphs.Light);
        second.Width = Length.Cells(12);
        Dock.SetSide(second, Side.Left);
        order.Children.Add(second);
        order.Children.Add(Card("3: Fill", Glyphs.Rounded));

        Dock noFill = new()
        {
            Width = Length.Cells(34),
            Height = Length.Cells(4),
            LastChildFills = false,
        };
        var onlyChild = Card("No remainder claimed", Glyphs.Heavy);
        onlyChild.Width = Length.Cells(24);
        Dock.SetSide(onlyChild, Side.Top);
        noFill.Children.Add(onlyChild);

        var remaining = new Dock
        {
            Width = Length.Cells(48),
            Height = Length.Cells(5),
            Spacing = 1,
        };
        var firstPercent = Card("24 cells", Glyphs.Light);
        firstPercent.Width = Length.Percent(50);
        Dock.SetSide(firstPercent, Side.Left);
        remaining.Children.Add(firstPercent);
        var secondPercent = Card("12 cells", Glyphs.Heavy);
        secondPercent.Width = Length.Percent(50);
        Dock.SetSide(secondPercent, Side.Left);
        remaining.Children.Add(secondPercent);
        remaining.Children.Add(Card("Fill", Glyphs.Rounded));

        var shell = new Dock { Width = Length.Cells(38), Height = Length.Cells(6) };
        var collapsibleSidebar = Card("Sidebar", Glyphs.Light);
        collapsibleSidebar.Width = Length.Cells(12);
        Dock.SetSide(collapsibleSidebar, Side.Left);
        shell.Children.Add(collapsibleSidebar);
        shell.Children.Add(Card("Main content", Glyphs.Rounded));
        var shellStatus = new Text("Sidebar: visible");
        var toggleSidebar = new Button { Content = new Text("Toggle sidebar") };
        toggleSidebar.Click += (_, _) =>
        {
            collapsibleSidebar.Visibility = collapsibleSidebar.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
            shellStatus.Content = collapsibleSidebar.Visibility == Visibility.Collapsed
                ? "Sidebar: collapsed; main reclaimed the remainder"
                : "Sidebar: visible";
        };

        var constrained = new Dock { Width = Length.Cells(14), Height = Length.Cells(4), Spacing = 2 };
        var tooWide = Card("12 cells", Glyphs.Light);
        tooWide.Width = Length.Cells(12);
        Dock.SetSide(tooWide, Side.Left);
        constrained.Children.Add(tooWide);
        var trailing = Card("Safe", Glyphs.Heavy);
        trailing.Width = Length.Cells(12);
        Dock.SetSide(trailing, Side.Right);
        constrained.Children.Add(trailing);
        constrained.Children.Add(Card("Fill", Glyphs.Rounded));

        return Doc.Page(
            Title,
            "Consumes remaining physical edges in child order and optionally gives the final child all remaining space.",
            Doc.Section(
                "⚓",
                "Application shell",
                "Compose familiar application regions by consuming physical edges and leaving the center to the final child.",
                Doc.Example(
                    "Explorer, header, inspector, status, and editor",
                    "Each named region uses Dock.SetSide; Editor workspace receives the rectangle left after the four edge regions.",
                    allSides,
                    "var shell = new Dock { LastChildFills = true };\nDock.SetSide(sidebar, Side.Left);\nshell.Children.Add(sidebar);\nshell.Children.Add(main);")),
            Doc.Section(
                "⚓",
                "Order and spacing",
                "Children consume the current remainder in insertion order, with spacing after each non-final participant.",
                Doc.Example(
                    "Repeated left sides",
                    "The first left child takes the outer strip; the second starts after it and its gap.",
                    order)),
            Doc.Section(
                "⚓",
                "Sizing from the remainder",
                "Percentage edge sizes resolve against the rectangle available at that iteration, not the original panel.",
                Doc.Example(
                    "Successive percentages",
                    "The first receives 24 of 48 cells; the second receives 12 of the remainder; Fill receives the rest.",
                    remaining)),
            Doc.Section(
                "⚓",
                "Collapse and fill",
                "Collapsed edge regions consume neither geometry nor spacing, so the fill child reclaims their cells.",
                Doc.Example(
                    "Optional sidebar",
                    "Toggle the sidebar and watch Main content reclaim or release the exact left strip.",
                    Doc.Column(toggleSidebar, shellStatus, shell))),
            Doc.Section(
                "⚓",
                "Constrained space",
                "Over-consumption saturates the remaining rectangle at zero instead of producing negative child bounds.",
                Doc.Example(
                    "Requests larger than the host",
                    "Both edge requests exceed the available width; every committed rectangle remains contained and non-negative.",
                    constrained)),
            Doc.Section(
                "⚓",
                "Fill policy",
                "Disable LastChildFills when the final child should honor its own side and leave unused remainder visible.",
                Doc.Example(
                    "Unclaimed remainder",
                    "The final top child takes only its requested extent; the rest of the panel stays empty.",
                    noFill)));
    }

    private static Dock Card(string label, Glyphs glyphs) => new()
    {
        BorderThickness = new Thickness(1),
        BorderGlyphs = glyphs,
        Padding = new Thickness(1, 0),
        Children = { new Text(label) },
    };
}
