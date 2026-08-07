// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents Text geometry, markup, overflow, links, and live content mutation.</summary>
internal sealed class TextPane: CompositeControlBase
{
    internal TextPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Text";

    /// <inheritdoc/>
    private static DocPage CreateContent()
    {
        var dynamicValue = "2 < 3";
        var safe = new Text(
            $"Dynamic: {Text.Escape(dynamicValue)}\n" +
            "Malformed: <unknown=bad>")
        { Overflow = Overflow.Wrap };
        var geometry = new Text("é vs é · orphan ́ · ambiguous · · 你好 · 👩‍💻 · 🇺🇸");

        var introductory = new Text(
            "<b>Marked </b><i>terminal text</i>\n" +
            "Unicode: café · 你好 · 👩‍💻 · " +
            "<u><link=https://github.com/pavkam>project source</link></u>")
        { Overflow = Overflow.Wrap };

        var attributes = new Text(
            "<b>Bold: increased intensity</b>\n" +
            "<d>Dim: reduced intensity</d>\n" +
            "<i>Italic: slanted presentation</i>\n" +
            "<u>Underline: single underline</u>\n" +
            "<blink>Blink: terminal policy may suppress it</blink>\n" +
            "<rapidblink>Rapid blink: rapid blink requested</rapidblink>\n" +
            "<reverse>Reverse: foreground and background exchanged</reverse>\n" +
            "<s>Strike: strikethrough presentation</s>\n" +
            "<overline>Overline: line above the text</overline>\n" +
            "Curly underline: <u=curly><uc=brightyellow>diagnostic emphasis</uc></u>\n" +
            "Hidden: <hidden>concealed sample</hidden> (intentional)\n" +
            "Combined: <b><u><i>bold + underline + italic</i></u></b>")
        { Overflow = Overflow.Wrap };

        var wrapped = new Text(
            "Resize this narrow reading column. Text wraps between words while keeping Unicode graphemes intact. " +
            "<link=https://invisible-island.net/xterm/ctlseqs/ctlseqs.html>Read the protocol guide</link>")
        {
            Width = Length.Cells(30),
            Overflow = Overflow.Wrap
        };
        var activity = new Text("Activity log: waiting for a markup mutation.");
        var append = new Button
        {
            Text = "&Append markup",
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 1, 1)
        };
        var mutation = 0;
        (string Name, string Markup)[] mutationStyles =
        [
            (Name: "underline", Markup: "<u><success>underlined text</success></u>"),
            (Name: "strikethrough", Markup: "<s><warning>struck text</warning></s>"),
            (Name: "reverse", Markup: "<reverse><accent>reversed text</accent></reverse>"),
            (Name: "bold + italic", Markup: "<b><i>bold italic text</i></b>")
        ];
        append.Click += (_, eventArgs) =>
        {
            var (name, markup) = mutationStyles[mutation % mutationStyles.Length];
            mutation++;
            wrapped.Content += $"\nMutation {mutation}: {markup}";
            activity.Content =
                $"Activity log: {eventArgs.Cause} appended {Text.Escape(name)} markup {mutation}.";
        };

        var centered = new Text("<b>Centered status</b>")
        {
            Width = Length.Cells(28),
            TextAlignment = Alignment.Center
        };

        var trimmed = new Text("This deliberately long one-line label trims safely")
        {
            Width = Length.Cells(28),
            Overflow = Overflow.Ellipsis
        };

        const string overflowSource = "Alpha café 你好 👩‍💻 omega words that exceed eighteen cells";
        var visible = new Text(overflowSource) { Width = Length.Cells(18), Overflow = Overflow.Visible };
        var wrap = new Text(overflowSource) { Width = Length.Cells(18), Overflow = Overflow.Wrap };
        var anywhere = new Text(overflowSource) { Width = Length.Cells(18), Overflow = Overflow.WrapAnywhere };
        var clip = new Text(overflowSource) { Width = Length.Cells(18), Overflow = Overflow.Clip };
        var ellipsis = new Text(overflowSource) { Width = Length.Cells(18), Overflow = Overflow.Ellipsis };

        var endAligned = new Text("End aligned") { Width = Length.Cells(28), TextAlignment = Alignment.End };
        var lineMetrics = new Text("Lines: resize to recompute wrapped offsets and cell widths.")
        {
            Width = Length.Cells(28),
            Overflow = Overflow.Wrap
        };
        var tabs = new Text("Name\tState\nRenderer\tStable\r\nInput\tPreview") { Overflow = Overflow.Visible };

        return new DocPage(
            Title,
            "<info>Text</info> formats Unicode by <i>grapheme cluster</i> and applies compact inline markup for semantic terminal styling.",
            new DocSection(
                "✍️",
                "Unicode",
                "Segmentation and width apply to complete grapheme clusters before wrapping, clipping, pointer mapping, and drawing.",
                new DocExample(
                    "Cell geometry specimen",
                    "Composed and decomposed text share width; orphan combining marks render conservatively without changing source text.",
                    geometry),
                new DocExample(
                    "Uneven pixel pointer grid",
                    "Pixel coordinates stay exact and map to cells only when terminal metrics make the mapping reliable.",
                    new PointerProbe())),
            new DocSection(
                "✍️",
                "Safe content",
                "Use <info>Text.Escape</info> on dynamic visible text before interpolation; malformed markup remains literal instead of throwing.",
                new DocExample(
                    "Dynamic and malformed input",
                    "The comparison operator is escaped as visible text, while the unknown tag fragment is preserved exactly for deterministic recovery.",
                    safe,
                    "var user = \"2 < 3\";\nvar text = new Text($\"Dynamic: {Text.Escape(user)}\");")),
            new DocSection(
                "✍️",
                "Markup",
                "Compact tags compose named or RGB colors, attributes, typed underlines, and OSC 8 link metadata.",
                new DocExample(
                    "Inline markup and link",
                    "The visible link owns hyperlink metadata but never opens a URL automatically.",
                    new DocCard(introductory)),
                new DocExample(
                    "Terminal attributes",
                    "Every supported attribute and underline form is visible; unsupported terminal presentation degrades below the cell model.",
                    new DocCard(attributes))),
            new DocSection(
                "✍️",
                "Overflow",
                "Choose whether a finite width preserves full lines, wraps at words or graphemes, clips, or reserves an ellipsis.",
                new DocExample(
                    "Five policies over identical Unicode",
                    "Compare Visible, Wrap, WrapAnywhere, Clip, and Ellipsis. None splits the CJK or emoji grapheme ownership.",
                    new DocRow(
                        new DocColumn(new Text("<b>Visible</b>"), OverflowCard(visible)),
                        new DocColumn(new Text("<b>Wrap</b>"), OverflowCard(wrap)),
                        new DocColumn(new Text("<b>Anywhere</b>"), OverflowCard(anywhere)),
                        new DocColumn(new Text("<b>Clip</b>"), OverflowCard(clip)),
                        new DocColumn(new Text("<b>Ellipsis</b>"), OverflowCard(ellipsis))))),
            new DocSection(
                "✍️",
                "Alignment and lines",
                "Alignment places each formatted line inside the arranged width; Lines exposes committed visible metrics until the next layout.",
                new DocExample(
                    "Start-independent alignment",
                    "<info>Center</info> and <info>End</info> alignment work independently from markup and overflow policy.",
                    new DocColumn(centered, endAligned, lineMetrics)),
                new DocExample(
                    "Single-line truncation",
                    "Ellipsis preserves complete grapheme clusters when a label must remain one line.",
                    trimmed)),
            new DocSection(
                "✍️",
                "Tabs and logical lines",
                "Tabs advance to four-cell stops and <info>CR</info>, <info>LF</info>, and <info>CRLF</info> create stable logical lines.",
                new DocExample(
                    "Aligned tabular text",
                    "The same tab-stop and newline rules feed line metrics and semantic cell drawing.",
                    tabs)),
            new DocSection(
                "✍️",
                "Live mutation",
                "Changing <info>Content</info> reparses markup and remeasures only the affected <info>Text</info> control.",
                new DocExample(
                    "Responsive marked reading column",
                    "Append a new marked line and resize the narrow reading column to watch safe reflow.",
                    new DocCard(new DocColumn(wrapped, append, activity)))));
    }

    private static Dock OverflowCard(Text text) => new()
    {
        Border = new Border(
            BorderSide.All,
            BorderGlyphStyle.Light,
            SemanticColor.ControlBorder,
            Color.Transparent,
            SemanticDecoration.Border),
        Width = Length.Cells(20),
        Children = { text }
    };
}
