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

        var constraintStage = Stage();
        var stretched = Card("Stretched", Glyphs.Rounded);
        Canvas.SetLeft(stretched, Length.Cells(2));
        Canvas.SetRight(stretched, Length.Cells(2));
        Canvas.SetTop(stretched, Length.Cells(1));
        Canvas.SetBottom(stretched, Length.Cells(1));
        constraintStage.Children.Add(stretched);

        var explicitStage = Stage();
        var explicitSize = Card("Explicit 12 cells", Glyphs.Heavy);
        explicitSize.Width = Length.Cells(12);
        Canvas.SetLeft(explicitSize, Length.Cells(2));
        Canvas.SetRight(explicitSize, Length.Cells(2));
        Canvas.SetTop(explicitSize, Length.Cells(2));
        explicitStage.Children.Add(explicitSize);

        var intrinsicStage = new Canvas { ClipToBounds = true };
        var intrinsicLeft = Card("Union starts here", Glyphs.Light);
        Canvas.SetLeft(intrinsicLeft, Length.Cells(1));
        Canvas.SetTop(intrinsicLeft, Length.Cells(1));
        intrinsicStage.Children.Add(intrinsicLeft);
        var intrinsicRight = Card("and ends here", Glyphs.Paired);
        Canvas.SetLeft(intrinsicRight, Length.Cells(22));
        Canvas.SetTop(intrinsicRight, Length.Cells(3));
        intrinsicStage.Children.Add(intrinsicRight);

        var negativeStage = new Canvas
        {
            Width = Length.Cells(24),
            Height = Length.Cells(6),
            ClipToBounds = true,
        };
        var oversized = Card("Larger than host", Glyphs.Ascii);
        oversized.Width = Length.Cells(30);
        Canvas.SetRight(oversized, Length.Cells(2));
        Canvas.SetTop(oversized, Length.Cells(1));
        negativeStage.Children.Add(oversized);

        var pointerStatus = new Text("Hit log: waiting");
        var underlying = new Button { Content = new Text("Underlying button") };
        underlying.Click += (_, eventArgs) => pointerStatus.Content = $"Hit log: button ({eventArgs.Cause})";
        Canvas.SetLeft(underlying, Length.Cells(2));
        Canvas.SetTop(underlying, Length.Cells(1));
        var decoration = new Text("transparent decoration")
        {
            IsHitTestVisible = false,
            Attributes = TerminalAttributes.Dim,
        };
        Canvas.SetLeft(decoration, Length.Cells(3));
        Canvas.SetTop(decoration, Length.Cells(2));
        var inputStage = Stage();
        inputStage.Children.Add(underlying);
        inputStage.Children.Add(decoration);

        return Doc.Page(
            Title,
            "Canvas positions child controls. Custom controls receive TerminalCanvas in OnRender to draw semantic cells. Neither API emits terminal escape sequences.",
            Doc.Section(
                "🎨",
                "Canvas layout",
                "Use attached offsets for diagrams, badges, overlays, and other deliberate positioning—not general responsive flow.",
                Doc.Example(
                    "Fixed placement",
                    "Cell offsets keep the child two cells from the left and one from the top as the host changes size.",
                    Frame(fixedStage),
                    "var canvas = new Canvas();\nCanvas.SetLeft(card, Length.Cells(2));\nCanvas.SetTop(card, Length.Cells(1));\ncanvas.Children.Add(card);"),
                Doc.Example(
                    "Percentage placement",
                    "Percentage offsets resolve against the final committed Canvas box and move when the host resizes.",
                    Frame(percentStage)),
                Doc.Example(
                    "Edge constraints",
                    "Right and Bottom anchor to trailing edges while a sibling resolves percentage width against the same host.",
                    Frame(edgeStage)),
                Doc.Example(
                    "Layering and clipping",
                    "Later children cover earlier ones, and the final child deliberately crosses the right edge where ClipToBounds removes it.",
                    Frame(layerStage))),
            Doc.Section(
                "🎨",
                "Constraints",
                "Opposing offsets stretch an automatic child; an explicit size instead keeps its extent and gives left/top precedence.",
                Doc.Example(
                    "Automatic opposing-edge stretch",
                    "Left 2, Right 2, Top 1, and Bottom 1 define the complete automatic child slot.",
                    Frame(constraintStage),
                    "Canvas.SetLeft(child, Length.Cells(2));\nCanvas.SetRight(child, Length.Cells(2));"),
                Doc.Example(
                    "Explicit size precedence",
                    "This twelve-cell child keeps its width even though both horizontal edges are supplied; Left chooses its origin.",
                    Frame(explicitStage))),
            Doc.Section(
                "🎨",
                "Intrinsic and constrained size",
                "Fixed positioned children contribute to intrinsic union; oversized trailing placement may produce a negative origin that clips safely.",
                Doc.Example(
                    "Intrinsic fixed-child union",
                    "With no explicit Canvas size, the desired box spans the finite union of both positioned cards.",
                    Frame(intrinsicStage)),
                Doc.Example(
                    "Negative origin and clipping",
                    "The child is wider than its host and anchored from the right, so its origin falls left of the host without escaping the clip.",
                    Frame(negativeStage))),
            Doc.Section(
                "🎨",
                "Layering and input",
                "Collection order controls painting and hit testing; pointer-transparent decoration can remain visually above an action.",
                Doc.Example(
                    "Pointer-transparent top layer",
                    "Click the visual overlap. The decoration renders later but IsHitTestVisible false lets the underlying Button receive input.",
                    Doc.Column(Frame(inputStage), pointerStatus))),
            Doc.Section(
                "🎨",
                "Drawing fundamentals",
                "Inside a custom control, TerminalCanvas draws clipped semantic lines, boxes, fills, clears, text, and styles.",
                Doc.Example(
                    "Line and fill matrix",
                    "Light, heavy, paired, rounded, and ASCII topology merge deterministically; fill and clear remain clipped.",
                    new CanvasSample(),
                    "protected override void OnRender(TerminalCanvas canvas)\n{\n    canvas.DrawBox(Bounds, LineStyle.Rounded);\n    canvas.DrawHorizontalLine(origin, length, LineStyle.Heavy);\n}"),
                Doc.Example(
                    "Arbitrary cell geometry",
                    "A deterministic diagonal, circle, and wide ellipse use public integer rasterization. The ellipse crosses the right edge so clipping is visible without corrupting a partial glyph.",
                    new CanvasGeometrySample(),
                    "canvas.DrawLine(start, end, new Rune('/'));\ncanvas.DrawCircle(center, 3, new Rune('o'));\ncanvas.DrawEllipse(bounds, new Rune('e'));")),
            Doc.Section(
                "🎨",
                "Shade and quadrants",
                "Standard shade glyphs and quadrant masks provide dense cell-native topology without one child control per cell.",
                Doc.Example(
                    "Shade palette and merged blocks",
                    "Light, medium, and dark shades sit beside two merged quadrant combinations.",
                    new CanvasShadeSample(),
                    "canvas.FillShade(region, Shade.Medium);\ncanvas.DrawQuadrants(point, Quadrants.UpperLeft | Quadrants.LowerRight);")),
            Doc.Section(
                "🎨",
                "Unicode drawing",
                "TerminalCanvas segments complete grapheme clusters before measuring, clipping, and storing wide-cell ownership.",
                Doc.Example(
                    "Combining, CJK, emoji, and clip edge",
                    "The final developer emoji begins at the right edge and is clipped as a complete owner rather than leaving half a glyph.",
                    new CanvasUnicodeSample(),
                    "_ = canvas.Draw(\"é 你好 👩‍💻\".AsSpan(), origin, style, Edge.Clip);")),
            Doc.Section(
                "🎨",
                "Useful custom drawing",
                "A focused control can turn deterministic application data into a responsive semantic chart with no ANSI knowledge.",
                Doc.Example(
                    "CPU sample chart",
                    "Bars use solid shade fills above a merged baseline; resize containment is handled by the control's committed Bounds.",
                    new CanvasChartSample(),
                    "canvas.FillShade(barBounds, Shade.Solid, style);")),
            Doc.Section(
                "🎨",
                "Pointer-aware drawing",
                "Custom rendering composes with the same routed pointer events, capture, cell coordinates, and optional pixel coordinates as controls.",
                Doc.Example(
                    "Live coordinate marker",
                    "Move or click inside the box. The control redraws a marker and reports the exact coordinates supplied by the terminal input path.",
                    new CanvasPointerSample())));
    }

    private static Canvas Stage() => new()
    {
        Width = Length.Cells(36),
        Height = Length.Cells(7),
        ClipToBounds = true,
    };

    private static Dock Frame(Control child) => new()
    {
        BorderThickness = new Thickness(1),
        BorderGlyphs = Glyphs.Light,
        Children = { child },
    };

    private static Dock Card(string content, Glyphs glyphs) => new()
    {
        BorderThickness = new Thickness(1),
        BorderGlyphs = glyphs,
        Padding = new Thickness(1, 0),
        Children = { new Text(content) },
    };
}
