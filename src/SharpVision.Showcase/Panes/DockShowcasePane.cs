// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;


/// <summary>Documents and demonstrates the Dock control.</summary>
internal sealed class DockShowcasePane: ShowcasePane
{
    internal const string Title = "Dock";
    private const string _catalogSummary =
        "Consumes remaining physical edges in child order and optionally gives the final child all remaining space.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Layout", "Attach each child to a Side", "Children consume the remaining rectangle in insertion order."),
        new InteractionDescription("Focus", "Move focus with Tab or Shift+Tab", "Focus follows stable child order rather than changing with docked edges."),
        new InteractionDescription("Resize", "Change the available bounds", "Edge sizes recompute and the filling child receives the remainder."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Children", "Children", "empty", "Owns controls whose attached Side values consume the remaining rectangle in order."),
        new PropertyDescription("LastChildFills", "bool", "true", "Lets the final child fill the remaining content box regardless of its attached side."),
        new PropertyDescription("Spacing", "int", "0", "Adds non-negative terminal cells after each consuming edge without overflowing tiny layouts."),
        new PropertyDescription("Side", "Side", "Left", "Attaches Left, Top, Right, or Bottom placement to each child."),
    ];

    /// <summary>Initializes the Dock showcase page and composes its specimens.</summary>
    internal DockShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        ControlDock dock = new()
        {
            Width = Length.Cells(38),
            Height = Length.Cells(9),
            LastChildFills = true,
            Spacing = 1,
        };
        ControlBorder left = PaneSupport.Card(new ControlText("Left"), Glyphs.Light);
        left.Width = Length.Cells(7);
        ControlDock.SetSide(left, Side.Left);
        dock.Children.Add(left);
        ControlBorder top = PaneSupport.Card(new ControlText("Top"), Glyphs.Heavy);
        top.Height = Length.Cells(2);
        ControlDock.SetSide(top, Side.Top);
        dock.Children.Add(top);
        ControlBorder right = PaneSupport.Card(new ControlText("Right"), Glyphs.Paired);
        right.Width = Length.Cells(8);
        ControlDock.SetSide(right, Side.Right);
        dock.Children.Add(right);
        ControlBorder bottom = PaneSupport.Card(new ControlText("Bottom"), Glyphs.Ascii);
        bottom.Height = Length.Cells(2);
        ControlDock.SetSide(bottom, Side.Bottom);
        dock.Children.Add(bottom);
        dock.Children.Add(PaneSupport.Card(new ControlText("Fill"), Glyphs.Rounded));
        examples.Children.Add(dock);
    }
}
