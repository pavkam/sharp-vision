// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Controls;
using SharpVision.Layout;

/// <summary>Documents and demonstrates the Stack control.</summary>
internal sealed class StackShowcasePane: ShowcasePane
{
    internal const string Title = "Stack";
    private const string _catalogSummary =
        "Arranges children sequentially with fixed, automatic, percentage, or proportional lengths and stable spacing.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Layout", "Set Orientation, lengths, and Spacing", "Children receive deterministic sequential tracks along the selected axis."),
        new InteractionDescription("Resize", "Change the available bounds", "Automatic and proportional children recompute without exceeding the stack."),
        new InteractionDescription("Reverse", "Set Reverse to true", "Geometry, rendering, hit testing, and default focus traversal reverse together."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Children", "Children", "empty", "Owns the sequential controls whose box requests participate in track allocation."),
        new PropertyDescription("Orientation", "Orientation", "Vertical", "Chooses top-to-bottom or left-to-right sequential layout."),
        new PropertyDescription("Spacing", "int", "0", "Adds non-negative terminal cells between participating children."),
        new PropertyDescription("Reverse", "bool", "false", "Reverses geometry, rendering, hit testing, and default focus traversal consistently."),
        new PropertyDescription("Width / Height", "Length", "Auto", "Supports fixed, percentage, automatic, and proportional requests on child border boxes."),
    ];

    /// <summary>Initializes the Stack showcase page and composes its specimens.</summary>
    internal StackShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        ControlStack horizontal = PaneSupport.Horizontal();
        ControlBorder fixedCard = PaneSupport.Card(new ControlText("Fixed 10"), Glyphs.Light);
        fixedCard.Width = Length.Cells(10);
        horizontal.Children.Add(fixedCard);
        ControlBorder percentCard = PaneSupport.Card(new ControlText("35%"), Glyphs.Heavy);
        percentCard.Width = Length.Percent(35);
        horizontal.Children.Add(percentCard);
        ControlBorder starCard = PaneSupport.Card(new ControlText("1*"), Glyphs.Paired);
        starCard.Width = Length.Star(1);
        horizontal.Children.Add(starCard);
        horizontal.Width = Length.Cells(40);
        examples.Children.Add(PaneSupport.SampleSection(
            "Mixed horizontal tracks",
            "Fixed cells, percentage sizing, and proportional remainder can coexist in one horizontal ControlStack.",
            horizontal));
        ControlStack reversed = PaneSupport.Horizontal();
        reversed.Reverse = true;
        reversed.Children.Add(PaneSupport.Card(new ControlText("First"), Glyphs.Light));
        reversed.Children.Add(PaneSupport.Card(new ControlText("Second"), Glyphs.Heavy));
        reversed.Children.Add(PaneSupport.Card(new ControlText("Third"), Glyphs.Paired));
        examples.Children.Add(PaneSupport.SampleSection(
            "Reverse order",
            "Reverse changes visual and keyboard-navigation order without changing the source child collection.",
            reversed));

        ControlStack vertical = PaneSupport.Vertical();
        vertical.Children.Add(PaneSupport.Card(new ControlText("Top"), Glyphs.Rounded));
        vertical.Children.Add(PaneSupport.Card(new ControlText("Spacing = 1"), Glyphs.Light));
        vertical.Children.Add(PaneSupport.Card(new ControlText("Bottom"), Glyphs.Heavy));
        examples.Children.Add(PaneSupport.SampleSection(
            "Vertical spacing",
            "Vertical is the default orientation; explicit spacing is applied only between participating children.",
            vertical));
    }
}
