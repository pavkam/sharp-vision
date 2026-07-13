// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;


/// <summary>Documents and demonstrates the Canvas control.</summary>
internal sealed class CanvasShowcasePane: ShowcasePane
{
    internal const string Title = "Canvas";
    private const string _catalogSummary =
        "Positions children with fixed or percentage offsets from physical edges and optional clipping.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Position", "Set fixed or percentage edge attachments", "Children resolve against the committed Canvas content rectangle."),
        new InteractionDescription("Pointer", "Target a positioned child", "Hit testing follows the child's final geometry and z-order."),
        new InteractionDescription("Resize", "Change the Canvas bounds", "Percentage positions and deferred sizes recompute from the new edges."),
        new InteractionDescription("Clipping", "Set ClipToBounds", "Rendering and hit testing either remain inside or may escape the Canvas box."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Children", "Children", "empty", "Owns positioned controls in stable insertion order for layout, rendering, and hit testing."),
        new PropertyDescription("ClipToBounds", "bool", "true", "Clips descendant rendering and hit testing to the committed Canvas content box."),
        new PropertyDescription("Left / Top", "Length?", "null", "Attach fixed-cell or percentage offsets from the leading physical edges."),
        new PropertyDescription("Right / Bottom", "Length?", "null", "Attach fixed-cell or percentage offsets from trailing edges and resolve deferred sizes."),
        new PropertyDescription("Width / Height", "Length", "Auto", "Accept fixed, percentage, automatic, or proportional border-box size requests."),
    ];

    /// <summary>Initializes the Canvas showcase page and composes its specimens.</summary>
    internal CanvasShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }


    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        ControlCanvas fixedPlacement = PaneSupport.CanvasStage();
        ControlBorder fixedLabel = PaneSupport.DemoCard("fixed 2,1", Glyphs.Light);
        ControlCanvas.SetLeft(fixedLabel, Length.Cells(2));
        ControlCanvas.SetTop(fixedLabel, Length.Cells(1));
        fixedPlacement.Children.Add(fixedLabel);
        examples.Children.Add(PaneSupport.CanvasSection(
            "Fixed placement",
            "Cell offsets place this bordered child two cells from the left and one from the top.",
            fixedPlacement));

        ControlCanvas percentagePlacement = PaneSupport.CanvasStage();
        ControlBorder percentLabel = PaneSupport.DemoCard("50%,50%", Glyphs.Heavy);
        ControlCanvas.SetLeft(percentLabel, Length.Percent(50));
        ControlCanvas.SetTop(percentLabel, Length.Percent(50));
        percentagePlacement.Children.Add(percentLabel);
        examples.Children.Add(PaneSupport.CanvasSection(
            "Percentage placement",
            "Percentage offsets resolve against the final Canvas box, so this specimen moves when the available width changes.",
            percentagePlacement));

        ControlCanvas constrained = PaneSupport.CanvasStage();
        ControlBorder edgeLabel = PaneSupport.DemoCard("Right 2 / Bottom 1", Glyphs.Paired);
        ControlCanvas.SetRight(edgeLabel, Length.Cells(2));
        ControlCanvas.SetBottom(edgeLabel, Length.Cells(1));
        constrained.Children.Add(edgeLabel);
        ControlBorder sizedLabel = PaneSupport.DemoCard("40% wide", Glyphs.Rounded);
        sizedLabel.Width = Length.Percent(40);
        ControlCanvas.SetLeft(sizedLabel, Length.Cells(1));
        ControlCanvas.SetTop(sizedLabel, Length.Cells(1));
        constrained.Children.Add(sizedLabel);
        examples.Children.Add(PaneSupport.CanvasSection(
            "Edge constraints",
            "Right and bottom offsets anchor one child, while a second child requests a percentage width from the same canvas.",
            constrained));

        ControlCanvas layered = PaneSupport.CanvasStage();
        ControlBorder back = PaneSupport.DemoCard("Back", Glyphs.Light);
        ControlCanvas.SetLeft(back, Length.Cells(2));
        ControlCanvas.SetTop(back, Length.Cells(1));
        layered.Children.Add(back);
        ControlBorder front = PaneSupport.DemoCard("Front", Glyphs.Heavy);
        ControlCanvas.SetLeft(front, Length.Cells(6));
        ControlCanvas.SetTop(front, Length.Cells(2));
        layered.Children.Add(front);
        ControlBorder clipped = PaneSupport.DemoCard("clipped", Glyphs.Ascii);
        ControlCanvas.SetLeft(clipped, Length.Cells(29));
        ControlCanvas.SetTop(clipped, Length.Cells(5));
        layered.Children.Add(clipped);
        examples.Children.Add(PaneSupport.CanvasSection(
            "Layering and clipping",
            "Later children paint above earlier ones; the final child deliberately crosses the edge and is clipped by the canvas.",
            layered));

        examples.Children.Add(PaneSupport.CanvasSection(
            "Drawing primitives",
            "Canvas drawing APIs add box, line, shade, and quadrant glyphs without creating a control per terminal cell.",
            new CanvasSample()));
    }
}
