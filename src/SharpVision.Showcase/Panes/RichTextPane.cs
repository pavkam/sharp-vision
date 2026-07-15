// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Terminal.Protocols;
using SharpVision.Text;

using TerminalAttributes = Terminal.Rendering.Attributes;
using Text = SharpVision.Controls.Text;

/// <summary>Documents the RichText control with styled runs, hyperlinks, and an interactive mutation specimen.</summary>
internal sealed class RichTextPane: View
{
    private static readonly Color _bright = Color.Indexed(15);
    private static readonly Color _muted = Color.Indexed(8);
    private static readonly Color _accent = Color.Indexed(14);
    private static readonly Color _success = Color.Indexed(10);
    private static readonly Color _warning = Color.Indexed(11);

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "RichText";

    /// <inheritdoc/>
    protected override Control Build()
    {
        RichText introductory = new() { Wrapping = Wrapping.Word };
        introductory.Inlines.Add(new Run("Rich ") { Attributes = TerminalAttributes.Bold });
        introductory.Inlines.Add(new Run("terminal text") { Attributes = TerminalAttributes.Italic });
        introductory.Inlines.Add(new LineBreak());
        introductory.Inlines.Add(new Run("Unicode: café · 你好 · 👩‍💻 · "));
        introductory.Inlines.Add(new Hyperlink("project source", "https://github.com/pavkam")
        {
            Attributes = TerminalAttributes.Underline,
        });

        RichText attributes = new() { Wrapping = Wrapping.Word };
        AddAttributeLine(attributes, "Bold", "increased intensity", TerminalAttributes.Bold, _bright);
        AddAttributeLine(attributes, "Dim", "reduced intensity", TerminalAttributes.Dim, _muted);
        AddAttributeLine(attributes, "Italic", "slanted presentation", TerminalAttributes.Italic, _accent);
        AddAttributeLine(attributes, "Underline", "single underline", TerminalAttributes.Underline, _success);
        AddAttributeLine(attributes, "Blink", "blink requested; terminal policy may suppress it", TerminalAttributes.Blink, _warning);
        AddAttributeLine(attributes, "Rapid blink", "rapid blink requested", TerminalAttributes.RapidBlink, _warning);
        AddAttributeLine(attributes, "Reverse", "foreground and background exchanged", TerminalAttributes.Reverse, _accent);
        AddAttributeLine(attributes, "Strike", "strikethrough presentation", TerminalAttributes.Strike, _warning);
        AddAttributeLine(attributes, "Overline", "line above the text", TerminalAttributes.Overline, _accent);
        attributes.Inlines.Add(new LineBreak());
        attributes.Inlines.Add(new Run("Curly underline: ") { Attributes = TerminalAttributes.Dim });
        attributes.Inlines.Add(new Run("diagnostic emphasis")
        {
            Foreground = _bright,
            Underline = Terminal.Protocols.Underline.Curly,
            UnderlineColor = _warning,
        });
        AddAttributeLine(attributes, "Hidden", "concealed run follows", TerminalAttributes.Hidden, _muted);
        attributes.Inlines.Add(new Run(" (the concealed sample is intentional)") { Attributes = TerminalAttributes.Dim });
        AddAttributeLine(
            attributes,
            "Combined",
            "bold + underline + italic",
            TerminalAttributes.Bold | TerminalAttributes.Underline | TerminalAttributes.Italic,
            _success);

        RichText wrapped = new() { Width = Length.Cells(30), Wrapping = Wrapping.Word };
        wrapped.Inlines.Add(new Run("Resize this narrow reading column. RichText wraps between words while keeping Unicode graphemes intact. "));
        wrapped.Inlines.Add(new Hyperlink("Read the protocol guide", "https://invisible-island.net/xterm/ctlseqs/ctlseqs.html"));

        Text activity = new("Activity log: waiting for an inline mutation.");
        Button append = new()
        {
            Content = new Text("Append a Run"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 1, 1),
        };
        int mutation = 0;
        (string Name, TerminalAttributes Value, Color Color)[] mutationStyles =
        [
            (Name: "underline", Value: TerminalAttributes.Underline, Color: _success),
            (Name: "strikethrough", Value: TerminalAttributes.Strike, Color: _warning),
            (Name: "reverse", Value: TerminalAttributes.Reverse, Color: _accent),
            (Name: "bold + italic", Value: TerminalAttributes.Bold | TerminalAttributes.Italic, Color: _bright),
        ];
        append.Click += (_, eventArgs) =>
        {
            (string Name, TerminalAttributes Value, Color Color) selectedStyle = mutationStyles[mutation % mutationStyles.Length];
            mutation++;
            wrapped.Inlines.Add(new LineBreak());
            wrapped.Inlines.Add(new Run(
                $"Mutation {mutation}: {selectedStyle.Name} Run appended through the {eventArgs.Cause} path.")
            {
                Attributes = selectedStyle.Value,
                Foreground = selectedStyle.Color,
            });
            activity.Content = $"Activity log: {eventArgs.Cause} appended {selectedStyle.Name} Run {mutation}.";
        };

        return Doc.Page(
            Title,
            "Displays an owned document of styled runs, explicit line breaks, and semantic hyperlinks.",
            Doc.Example(
                "Styled document and OSC 8 link",
                "Runs carry independent foreground, attributes, and hyperlink metadata. The link is explicitly underlined as well as semantic; compatible terminals expose it on hover or open it with their configured gesture.",
                Card(introductory, Glyphs.Rounded)),
            Doc.Example(
                "Terminal text attributes",
                "Every row below is a real RichText run. Modern underline shape/color and overline use proved terminal capabilities; unsupported underline shapes become straight and unsupported color or overline is omitted.",
                Card(attributes, Glyphs.Light)),
            Doc.Example(
                "Responsive reading column",
                "A constrained document is useful for help panes, release notes, and inline documentation. Activate the button to append a differently styled run and watch the log.",
                Card(Doc.Column(wrapped, append, activity), Glyphs.Light)));
    }

    private static void AddAttributeLine(
        RichText document,
        string label,
        string sample,
        TerminalAttributes attributes,
        Color foreground)
    {
        if (document.Inlines.Count > 0)
        {
            document.Inlines.Add(new LineBreak());
        }

        document.Inlines.Add(new Run($"{label}: ") { Attributes = TerminalAttributes.Dim });
        document.Inlines.Add(new Run(sample)
        {
            Attributes = attributes,
            Foreground = foreground,
        });
    }

    private static Dock Card(Control child, Glyphs glyphs) => new()
    {
        BorderThickness = new Thickness(1),
        BorderGlyphs = glyphs,
        Padding = new Thickness(1, 0),
        Children = { child },
    };
}
