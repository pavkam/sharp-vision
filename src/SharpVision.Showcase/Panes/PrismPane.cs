// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents Prism directions, caller-driven animation, style preservation, and FIGlet composition.</summary>
public sealed class PrismPane: CompositeControl
{
    /// <summary>The exact catalog and page name.</summary>
    internal const string Title = "Prism";

    /// <summary>Initializes the retained Prism documentation page exactly once.</summary>
    public PrismPane() => InitializeContent(CreateContent());

    private static Dock CreateContent()
    {
        var horizontal = new Prism
        {
            Direction = PrismDirection.Horizontal,
            CycleLength = 12,
            Content = new Text("LEFT → RIGHT"),
        };
        var vertical = new Prism
        {
            Direction = PrismDirection.Vertical,
            CycleLength = 6,
            Content = new Text("TOP\n ↓\nBOTTOM"),
        };
        var diagonal = new Prism
        {
            Direction = PrismDirection.Diagonal,
            CycleLength = 12,
            Content = new Text("DIAGONAL\n  SPECTRUM"),
        };

        var live = new Prism
        {
            Direction = PrismDirection.Diagonal,
            CycleLength = 18,
            Content = new FigletText(FigletCatalog.Default.Load("Small"))
            {
                Content = "PRISM",
            },
        };
        var status = new Text("Phase 0 / 60");
        var advance = new Button { Content = new Text("Advance phase") };
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
                "</u></b></bg>"),
        };

        var title = new Prism
        {
            Direction = PrismDirection.Diagonal,
            CycleLength = 18,
            Content = new FigletText(FigletCatalog.Default.Load("Small"))
            {
                Content = "SPECTRUM",
            },
        };

        return Doc.Page(
            Title,
            "Applies a deterministic foreground spectrum to retained content without owning an animation timer.",
            Doc.Section(
                "🌈",
                "Directions",
                "Choose the content-relative axis that advances the hue: columns, rows, or their diagonal sum.",
                Doc.Example(
                    "Horizontal, vertical, and diagonal axes",
                    "Each specimen uses ordinary Text content; the labels identify the coordinate that changes its foreground.",
                    Doc.Row(
                        Doc.Card(Doc.Column(new Text("Horizontal"), horizontal)),
                        Doc.Card(Doc.Column(new Text("Vertical"), vertical)),
                        Doc.Card(Doc.Column(new Text("Diagonal"), diagonal))),
                    "var spectrum = new Prism\n{\n    Direction = PrismDirection.Horizontal,\n    CycleLength = 12,\n    Content = new Text(\"LEFT → RIGHT\"),\n};")),
            Doc.Section(
                "⏱️",
                "Caller-driven animation",
                "Prism owns no timer. Advance its normalized Phase from the dispatcher cadence your application already controls.",
                Doc.Example(
                    "One deterministic frame",
                    "Activate the button to advance exactly one sixtieth of a cycle. Text and layout stay fixed while foreground colors move.",
                    Doc.Column(live, status, advance),
                    "var frame = 0;\nadvance.Click += (_, _) =>\n{\n    frame = (frame + 1) % 60;\n    live.Phase = frame / 60d;\n};")),
            Doc.Section(
                "🎨",
                "Style preservation",
                "Only foreground changes; backgrounds, attributes, underline shape and color, hyperlinks, glyphs, and wide-cell ownership survive.",
                Doc.Example(
                    "Rich marked Text",
                    "The dark background, bold weight, curly warning underline, and link metadata remain attached to the same stored cells.",
                    Doc.Card(styled),
                    "var rich = new Prism\n{\n    Content = new Text(\n        \"<bg=brightblack><b><u=curly><uc=warning>\" +\n        \"<link=https://github.com/pavkam>Only this foreground cycles</link>\" +\n        \"</u></b></bg>\"),\n};")),
            Doc.Section(
                "🔤",
                "FIGlet title",
                "Compose Prism around any retained control, including audited FIGlet output, without changing that child's measurement or text.",
                Doc.Example(
                    "Large diagonal spectrum",
                    "FigletText remains responsible for glyph generation; Prism contributes only the foreground pass.",
                    title,
                    "var title = new Prism\n{\n    Direction = PrismDirection.Diagonal,\n    CycleLength = 18,\n    Content = new FigletText(FigletCatalog.Default.Load(\"Small\"))\n    {\n        Content = \"SPECTRUM\",\n    },\n};")));
    }
}
