// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;


/// <summary>Documents the Canvas control with fixed, percentage, and edge-anchored placement specimens.</summary>
internal sealed class CanvasPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Canvas";

    /// <inheritdoc/>
    protected override Control Build()
    {
        var fixedStage = Stage();
        var fixedCard = Card("fixed 2,1", Glyphs.Light);
        Canvas.SetLeft(fixedCard, Length.Cells(2));
        Canvas.SetTop(fixedCard, Length.Cells(1));
        fixedStage.Children.Add(fixedCard);

        var percentStage = Stage();
        var percentCard = Card("50%,50%", Glyphs.Heavy);
        Canvas.SetLeft(percentCard, Length.Percent(50));
        Canvas.SetTop(percentCard, Length.Percent(50));
        percentStage.Children.Add(percentCard);

        var edgeStage = Stage();
        var edgeCard = Card("Right 2 / Bottom 1", Glyphs.Paired);
        Canvas.SetRight(edgeCard, Length.Cells(2));
        Canvas.SetBottom(edgeCard, Length.Cells(1));
        edgeStage.Children.Add(edgeCard);
        var widthCard = Card("40% wide", Glyphs.Rounded);
        widthCard.Width = Length.Percent(40);
        Canvas.SetLeft(widthCard, Length.Cells(1));
        Canvas.SetTop(widthCard, Length.Cells(1));
        edgeStage.Children.Add(widthCard);

        var layerStage = Stage();
        var back = Card("Back", Glyphs.Light);
        Canvas.SetLeft(back, Length.Cells(2));
        Canvas.SetTop(back, Length.Cells(1));
        layerStage.Children.Add(back);
        var front = Card("Front", Glyphs.Heavy);
        Canvas.SetLeft(front, Length.Cells(6));
        Canvas.SetTop(front, Length.Cells(2));
        layerStage.Children.Add(front);
        var clipped = Card("clipped", Glyphs.Ascii);
        Canvas.SetLeft(clipped, Length.Cells(29));
        Canvas.SetTop(clipped, Length.Cells(5));
        layerStage.Children.Add(clipped);

        return Doc.Page(
            Title,
            "Positions children with fixed-cell or percentage offsets attached to any combination of physical edges, clipping descendants to its committed content box by default.",
            Doc.Example(
                "Fixed placement",
                "Cell offsets place this bordered child a constant two cells from the left edge and one cell from the top, regardless of how large the Canvas box becomes.",
                Frame(fixedStage)),
            Doc.Example(
                "Percentage placement",
                "Percentage offsets resolve against the final committed Canvas box, so this specimen re-centers itself whenever the available width or height changes.",
                Frame(percentStage)),
            Doc.Example(
                "Edge constraints",
                "Right and Bottom offsets anchor a child to the trailing edges instead of the leading ones, while a sibling requests a percentage width the Canvas resolves against its own committed box.",
                Frame(edgeStage)),
            Doc.Example(
                "Layering and clipping",
                "Children paint in insertion order, so later additions cover earlier ones; the final child deliberately crosses the right edge and is clipped away by the Canvas's default ClipToBounds box.",
                Frame(layerStage)),
            Doc.Example(
                "Drawing primitives",
                "Beyond attached-property child placement, Canvas exposes low-level drawing APIs for boxes, lines, shade fills, and quadrant glyphs without allocating a control per terminal cell.",
                new CanvasSample()));
    }

    private static Canvas Stage() => new()
    {
        Width = Length.Cells(36),
        Height = Length.Cells(7),
        ClipToBounds = true,
    };

    private static Dock Frame(Control child) => new()
    {
        Children = { child },
        BorderThickness = new Thickness(1),
        BorderGlyphs = Glyphs.Light,
    };

    private static Dock Card(string content, Glyphs glyphs) => new()
    {
        Children = { new Text(content) },
        BorderThickness = new Thickness(1),
        BorderGlyphs = glyphs,
        Padding = new Thickness(1, 0),
    };
}
