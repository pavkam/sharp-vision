// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents Prism directions, caller-driven animation, style preservation, and FIGlet composition.</summary>
internal sealed class PrismPane: CompositeControlBase
{
    /// <summary>The exact catalog and page name.</summary>
    internal const string Title = "Prism";

    /// <summary>Initializes the retained Prism documentation page exactly once.</summary>
    internal PrismPane() => InitializeContent(CreateContent());

    private static DocPage CreateContent()
    {
        var horizontal = new Prism
        {
            Direction = PrismDirection.Horizontal,
            CycleLength = 12,
            Content = new Text("LEFT → RIGHT")
        };
        var vertical = new Prism
        {
            Direction = PrismDirection.Vertical,
            CycleLength = 6,
            Content = new Text("TOP\n ↓\nBOTTOM")
        };
        var diagonal = new Prism
        {
            Direction = PrismDirection.Diagonal,
            CycleLength = 12,
            Content = new Text("DIAGONAL\n  SPECTRUM")
        };

        var live = new Prism
        {
            Direction = PrismDirection.Diagonal,
            CycleLength = 18,
            Content = new FigletText(FigletCatalog.Default.Load("small")) { Content = "PRISM" }
        };
        var status = new Text("Phase 0 / 60");
        var advance = new Button { Text = "&Advance phase" };
        var frame = 0;
        advance.Click += (_, _) =>
        {
            frame = (frame + 1) % 60;
            live.Phase = frame / 60d;
            status.Content = FormattableString.Invariant($"Phase {frame} / 60");
        };

        var styled = new Prism
        {
            Direction = PrismDirection.Horizontal,
            CycleLength = 18,
            Content = new Text(
                "<bg=brightblack><b><u=curly><uc=warning>" +
                "<link=https://github.com/pavkam>Only this foreground cycles</link>" +
                "</u></b></bg>")
        };

        var title = new Prism
        {
            Direction = PrismDirection.Diagonal,
            CycleLength = 18,
            Content = new FigletText(FigletCatalog.Default.Load("small")) { Content = "SPECTRUM" }
        };

        return new DocPage(
            Title,
            "<info>Prism</info> applies a deterministic foreground spectrum to retained content without owning an animation timer.",
            new DocSection(
                "🌈",
                "Directions",
                "Choose the content-relative axis that advances the hue: columns, rows, or their diagonal sum.",
                new DocExample(
                    "Horizontal, vertical, and diagonal axes",
                    "Each specimen uses ordinary Text content; the labels identify the coordinate that changes its foreground.",
                    new DocRow(
                        new DocCard(new DocColumn(new Text("Horizontal"), horizontal)),
                        new DocCard(new DocColumn(new Text("Vertical"), vertical)),
                        new DocCard(new DocColumn(new Text("Diagonal"), diagonal))),
                    "var spectrum = new Prism\n{\n    Direction = PrismDirection.Horizontal,\n    CycleLength = 12,\n    Content = new Text(\"LEFT → RIGHT\"),\n};")),
            new DocSection(
                "⏱️",
                "Caller-driven animation",
                "Prism owns no timer. Advance its normalized Phase from the dispatcher cadence your application already controls.",
                new DocExample(
                    "One deterministic frame",
                    "Activate the button to advance exactly one sixtieth of a cycle. Text and layout stay fixed while foreground colors move.",
                    new DocColumn(live, status, advance),
                    "var frame = 0;\nadvance.Click += (_, _) =>\n{\n    frame = (frame + 1) % 60;\n    live.Phase = frame / 60d;\n};")),
            new DocSection(
                "🎨",
                "Style preservation",
                "Only foreground changes; backgrounds, attributes, underline shape and color, hyperlinks, glyphs, and wide-cell ownership survive.",
                new DocExample(
                    "Rich marked Text",
                    "The dark background, bold weight, curly warning underline, and link metadata remain attached to the same stored cells.",
                    new DocCard(styled),
                    "var rich = new Prism\n{\n    Content = new Text(\n        \"<bg=brightblack><b><u=curly><uc=warning>\" +\n        \"<link=https://github.com/pavkam>Only this foreground cycles</link>\" +\n        \"</u></b></bg>\"),\n};")),
            new DocSection(
                "🔤",
                "FIGlet title",
                "Compose Prism around any retained control, including audited FIGlet output, without changing that child's measurement or text.",
                new DocExample(
                    "Large diagonal spectrum",
                    "FigletText remains responsible for glyph generation; Prism contributes only the foreground pass.",
                    title,
                    "var title = new Prism\n{\n    Direction = PrismDirection.Diagonal,\n    CycleLength = 18,\n    Content = new FigletText(FigletCatalog.Default.Load(\"small\"))\n    {\n        Content = \"SPECTRUM\",\n    },\n};")));
    }
}
