// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Documents the frame-owned TerminalCanvas drawing API used by custom controls.</summary>
internal sealed class CanvasPane: CompositeControlBase
{
    internal CanvasPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Canvas";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        return new DocPage(
            Title,
            "<info>TerminalCanvas</info> is the frame-owned drawing API used by custom controls in <info>OnRenderContent</info>. It draws cells; it does not own or position child controls.",
            new DocSection(
                "🎨",
                "Drawing fundamentals",
                "Inside a custom control, TerminalCanvas draws clipped semantic lines, boxes, fills, clears, text, and styles.",
                new DocExample(
                    "Line and fill matrix",
                    "Light, heavy, paired, rounded, and ASCII topology merge deterministically; fill and clear remain clipped.",
                    new CanvasSample(),
                    "protected override void OnRenderContent(TerminalCanvas canvas)\n{\n    canvas.DrawBox(Bounds, LineStyle.Rounded);\n    canvas.DrawHorizontalLine(origin, length, LineStyle.Heavy);\n}"),
                new DocExample(
                    "Arbitrary cell geometry",
                    "A deterministic diagonal, circle, and wide ellipse use public integer rasterization. The ellipse crosses the right edge so clipping is visible without corrupting a partial glyph.",
                    new CanvasGeometrySample(),
                    "canvas.DrawLine(start, end, new Rune('/'));\ncanvas.DrawCircle(center, 3, new Rune('o'));\ncanvas.DrawEllipse(bounds, new Rune('e'));")),
            new DocSection(
                "🎨",
                "Shade and quadrants",
                "Standard shade glyphs and quadrant masks provide dense cell-native topology without one child control per cell.",
                new DocExample(
                    "Shade palette and merged blocks",
                    "Light, medium, and dark shades sit beside two merged quadrant combinations.",
                    new CanvasShadeSample(),
                    "canvas.FillShade(region, Shade.Medium);\ncanvas.DrawQuadrants(point, Quadrants.UpperLeft | Quadrants.LowerRight);")),
            new DocSection(
                "🎨",
                "Unicode drawing",
                "TerminalCanvas segments complete grapheme clusters before measuring, clipping, and storing wide-cell ownership.",
                new DocExample(
                    "Combining, CJK, emoji, and clip edge",
                    "The final developer emoji begins at the right edge and is clipped as a complete owner rather than leaving half a glyph.",
                    new CanvasUnicodeSample(),
                    "_ = canvas.Draw(\"é 你好 👩‍💻\".AsSpan(), origin, style, Edge.Clip);")),
            new DocSection(
                "🎨",
                "Useful custom drawing",
                "A focused control can turn deterministic application data into a responsive semantic chart with no ANSI knowledge.",
                new DocExample(
                    "CPU sample chart",
                    "Bars use solid shade fills above a merged baseline; resize containment is handled by the control's committed Bounds.",
                    new CanvasChartSample(),
                    "canvas.FillShade(barBounds, Shade.Solid, style);")),
            new DocSection(
                "🎨",
                "Pointer-aware drawing",
                "Custom rendering composes with the same routed pointer events, capture, cell coordinates, and optional pixel coordinates as controls.",
                new DocExample(
                    "Live coordinate marker",
                    "Move or click inside the box. The control redraws a marker and reports the exact coordinates supplied by the terminal input path.",
                    new CanvasPointerSample())),
            new DocSection(
                "📈",
                "Sparkline chart",
                "Sub-cell vertical resolution using Unicode block elements ▁▂▃▄▅▆▇█. Each column encodes value in eighths of a cell, doubling effective vertical precision.",
                new DocExample(
                    "Trend data with eighth-cell bars",
                    "Twenty-eight deterministic samples rendered with eight granularity levels per cell row.",
                    new CanvasSparklineSample(),
                    "var blocks = [\" \", \"▁\", \"▂\", \"▃\", \"▄\", \"▅\", \"▆\", \"▇\", \"█\"];\nvar eighths = (int)(ratio * height * 8);\ncanvas.Draw(blocks[eighths % 8], point, style);")),
            new DocSection(
                "📊",
                "Dashboard composition",
                "A single custom control combines lines, fills, shades, labels, and topology into a compact information panel.",
                new DocExample(
                    "System monitor panel",
                    "Three gauges using solid and light shade fills, a vertical divider, and stat labels—all deterministic cell drawing inside one OnRender.",
                    new CanvasDashboardSample(),
                    "canvas.FillShade(barBounds, Shade.Solid, style);\ncanvas.FillShade(emptyBounds, Shade.Light, style);\ncanvas.DrawVerticalLine(divider, length, LineStyle.Light, style);")),
            new DocSection(
                "🧩",
                "Topology merging",
                "Lines auto-join at shared cells: horizontal and vertical segments produce ┼ ├ ┤ ┬ ┴ and corner glyphs without per-junction code.",
                new DocExample(
                    "Maze with merged junctions",
                    "Overlapping DrawHorizontalLine and DrawVerticalLine calls produce a maze where every intersection merges deterministically.",
                    new CanvasMazeSample(),
                    "canvas.DrawBox(outer, LineStyle.Light, style);\ncanvas.DrawVerticalLine(wall, height, LineStyle.Light, style);\ncanvas.DrawHorizontalLine(floor, width, LineStyle.Light, style);")));
    }
}
