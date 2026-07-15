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
            var (selectedName, selectedMarkup) = mutationStyles[mutation % mutationStyles.Length];
            mutation++;
            wrapped.Content += $"\nMutation {mutation}: {selectedMarkup}";
            activity.Content =
                $"Activity log: {eventArgs.Cause} appended {Text.Escape(selectedName)} markup {mutation}.";
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

        return Doc.Page(
            Title,
            "Formats Unicode text by grapheme cluster and applies compact inline markup for semantic terminal styling.",
            Doc.Example(
                "Cell geometry specimen",
                "Composed and decomposed text share width. Orphan combining marks render as replacement cells without changing source text.",
                geometry),
            Doc.Example(
                "Uneven pixel pointer grid",
                "Pixel coordinates stay exact. Mapped cells appear only when exact grid metrics are available.",
                new PointerProbe()),
            Doc.Example(
                "Inline markup and OSC 8 link",
                "Named tags compose colors, attributes, typed underlines, and semantic links without exposing a mutable run object model.",
                Doc.Card(introductory)),
            Doc.Example(
                "Terminal text attributes",
                "Every supported attribute and underline form is represented by markup. Unsupported terminal presentation degrades below the cell model.",
                Doc.Card(attributes)),
            Doc.Example(
                "Responsive marked reading column",
                "Use Wrap for prose. Activate the button to replace Content with a longer marked string and watch layout reflow.",
                Doc.Card(Doc.Column(wrapped, append, activity))),
            Doc.Example(
                "Centered label",
                "Center alignment is independent from inline style markup.",
                centered),
            Doc.Example(
                "Single-line truncation",
                "Ellipsis preserves complete grapheme clusters when a one-line label must fit.",
                trimmed));
    }
}
