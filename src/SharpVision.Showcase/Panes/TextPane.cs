// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Text;

using Text = SharpVision.Controls.Text;

/// <summary>Documents Text geometry, markup, overflow, links, and live content mutation.</summary>
internal sealed class TextPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Text";

    /// <inheritdoc/>
    protected override Control Build()
    {
        var dynamicValue = "2 < 3";
        var safe = new Text(
            $"Dynamic: {Text.Escape(dynamicValue)}\n" +
            "Malformed: <unknown=bad>")
        {
            Overflow = Overflow.Wrap,
        };
        var geometry = new Text("é vs é · orphan ́ · ambiguous · · 你好 · 👩‍💻 · 🇺🇸");

        var introductory = new Text(
            "<b>Marked </b><i>terminal text</i>\n" +
            "Unicode: café · 你好 · 👩‍💻 · " +
            "<u><link=https://github.com/pavkam>project source</link></u>")
        {
            Overflow = Overflow.Wrap,
        };

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
            "Curly underline: <u=curly><uc=11>diagnostic emphasis</uc></u>\n" +
            "Hidden: <hidden>concealed sample</hidden> (intentional)\n" +
            "Combined: <b><u><i>bold + underline + italic</i></u></b>")
        {
            Overflow = Overflow.Wrap,
        };

        var wrapped = new Text(
            "Resize this narrow reading column. Text wraps between words while keeping Unicode graphemes intact. " +
            "<link=https://invisible-island.net/xterm/ctlseqs/ctlseqs.html>Read the protocol guide</link>")
        {
            Width = Length.Cells(30),
            Overflow = Overflow.Wrap,
        };
        var activity = new Text("Activity log: waiting for a markup mutation.");
        var append = new Button()
        {
            Content = new Text("Append markup"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 1, 1),
        };
        var mutation = 0;
        (string Name, string Markup)[] mutationStyles =
        [
            (Name: "underline", Markup: "<u><success>underlined text</success></u>"),
            (Name: "strikethrough", Markup: "<s><warning>struck text</warning></s>"),
            (Name: "reverse", Markup: "<reverse><accent>reversed text</accent></reverse>"),
            (Name: "bold + italic", Markup: "<b><i>bold italic text</i></b>"),
        ];
        append.Click += (_, eventArgs) =>
        {
            var selected = mutationStyles[mutation % mutationStyles.Length];
            mutation++;
            wrapped.Content += $"\nMutation {mutation}: {selected.Markup}";
            activity.Content =
                $"Activity log: {eventArgs.Cause} appended {Text.Escape(selected.Name)} markup {mutation}.";
        };

        var centered = new Text("<b>Centered status</b>")
        {
            Width = Length.Cells(28),
            TextAlignment = Alignment.Center,
        };

        var trimmed = new Text("This deliberately long one-line label trims safely")
        {
            Width = Length.Cells(28),
            Overflow = Overflow.Ellipsis,
        };

        const string overflowSource = "Alpha café 你好 👩‍💻 omega words that exceed eighteen cells";
        var visible = new Text(overflowSource) { Width = Length.Cells(18), Overflow = Overflow.Visible };
        var wrap = new Text(overflowSource) { Width = Length.Cells(18), Overflow = Overflow.Wrap };
        var anywhere = new Text(overflowSource) { Width = Length.Cells(18), Overflow = Overflow.WrapAnywhere };
        var clip = new Text(overflowSource) { Width = Length.Cells(18), Overflow = Overflow.Clip };
        var ellipsis = new Text(overflowSource) { Width = Length.Cells(18), Overflow = Overflow.Ellipsis };

        var endAligned = new Text("End aligned")
        {
            Width = Length.Cells(28),
            TextAlignment = Alignment.End,
        };
        var lineMetrics = new Text("Lines: resize to recompute wrapped offsets and cell widths.")
        {
            Width = Length.Cells(28),
            Overflow = Overflow.Wrap,
        };
        var tabs = new Text("Name\tState\nRenderer\tStable\r\nInput\tPreview")
        {
            Overflow = Overflow.Visible,
        };

        return Doc.Page(
            Title,
            "Formats Unicode text by grapheme cluster and applies compact inline markup for semantic terminal styling.",
            Doc.Section(
                "✍️",
                "Unicode",
                "Segmentation and width apply to complete grapheme clusters before wrapping, clipping, pointer mapping, and drawing.",
                Doc.Example(
                    "Cell geometry specimen",
                    "Composed and decomposed text share width; orphan combining marks render conservatively without changing source text.",
                    geometry),
                Doc.Example(
                    "Uneven pixel pointer grid",
                    "Pixel coordinates stay exact and map to cells only when terminal metrics make the mapping reliable.",
                    new PointerProbe())),
            Doc.Section(
                "✍️",
                "Safe content",
                "Escape dynamic visible text before interpolating it into marked content; malformed markup remains literal instead of throwing.",
                Doc.Example(
                    "Dynamic and malformed input",
                    "The comparison operator is escaped as visible text, while the unknown tag fragment is preserved exactly for deterministic recovery.",
                    safe,
                    "var user = \"2 < 3\";\nvar text = new Text($\"Dynamic: {Text.Escape(user)}\");")),
            Doc.Section(
                "✍️",
                "Markup",
                "Compact tags compose semantic colors, attributes, typed underlines, and OSC 8 link metadata.",
                Doc.Example(
                    "Inline markup and link",
                    "The visible link owns hyperlink metadata but never opens a URL automatically.",
                    Doc.Card(introductory)),
                Doc.Example(
                    "Terminal attributes",
                    "Every supported attribute and underline form is visible; unsupported terminal presentation degrades below the cell model.",
                    Doc.Card(attributes))),
            Doc.Section(
                "✍️",
                "Overflow",
                "Choose whether a finite width preserves full lines, wraps at words or graphemes, clips, or reserves an ellipsis.",
                Doc.Example(
                    "Five policies over identical Unicode",
                    "Compare Visible, Wrap, WrapAnywhere, Clip, and Ellipsis. None splits the CJK or emoji grapheme ownership.",
                    Doc.Column(
                        Doc.Row(new Text("Visible"), visible),
                        Doc.Row(new Text("Wrap"), wrap),
                        Doc.Row(new Text("Anywhere"), anywhere),
                        Doc.Row(new Text("Clip"), clip),
                        Doc.Row(new Text("Ellipsis"), ellipsis)))),
            Doc.Section(
                "✍️",
                "Alignment and lines",
                "Alignment places each formatted line inside the arranged width; Lines exposes committed visible metrics until the next layout.",
                Doc.Example(
                    "Start-independent alignment",
                    "Center and End alignment work independently from markup and overflow policy.",
                    Doc.Column(centered, endAligned, lineMetrics)),
                Doc.Example(
                    "Single-line truncation",
                    "Ellipsis preserves complete grapheme clusters when a label must remain one line.",
                    trimmed)),
            Doc.Section(
                "✍️",
                "Tabs and logical lines",
                "Tabs advance to four-cell stops and CR, LF, and CRLF create stable logical lines.",
                Doc.Example(
                    "Aligned tabular text",
                    "The same tab-stop and newline rules feed line metrics and semantic cell drawing.",
                    tabs)),
            Doc.Section(
                "✍️",
                "Live mutation",
                "Changing Content reparses markup and remeasures only the affected Text control.",
                Doc.Example(
                    "Responsive marked reading column",
                    "Append a new marked line and resize the narrow reading column to watch safe reflow.",
                    Doc.Card(Doc.Column(wrapped, append, activity)))));
    }
}
