// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Terminal.Protocols;
using SharpVision.Text;

using TerminalAttributes = Terminal.Rendering.Attributes;

/// <summary>Documents and demonstrates the RichText control.</summary>
internal sealed class RichTextShowcasePane: ShowcasePane
{
    private static readonly Color _bright = Color.Indexed(15);
    private static readonly Color _muted = Color.Indexed(8);
    private static readonly Color _accent = Color.Indexed(14);
    private static readonly Color _success = Color.Indexed(10);
    private static readonly Color _warning = Color.Indexed(11);

    internal const string Title = "RichText";
    private const string _catalogSummary =
        "Displays an owned document of styled runs, explicit line breaks, and semantic hyperlinks.";

    private static readonly InteractionDescription[] _catalogInteractions =
    [
        new InteractionDescription("Inline mutation", "Add or edit a Run, Hyperlink, or LineBreak", "The document invalidates and remeasures its formatted content."),
        new InteractionDescription("Pointer", "Activate a Hyperlink", "The hyperlink event receives the clicked semantic target."),
        new InteractionDescription("Resize", "Change the available width", "Wrapping and line alignment recompute without splitting grapheme clusters."),
    ];

    private static readonly PropertyDescription[] _catalogProperties =
    [
        new PropertyDescription("Inlines", "Inlines", "empty", "Owns the ordered Run, Hyperlink, and LineBreak values that form the displayed document."),
        new PropertyDescription("Wrapping", "Wrapping", "Word", "Defaults to word-aware wrapping; applications may preserve logical lines or choose grapheme wrapping explicitly."),
        new PropertyDescription("TextAlignment", "Alignment", "Start", "Places every formatted document line at the start, center, or end of its content box."),
        new PropertyDescription("Padding", "Thickness", "0", "Adds internal terminal-cell space around the formatted inline document."),
    ];

    /// <summary>Initializes the RichText showcase page and composes its specimens.</summary>
    internal RichTextShowcasePane()
        : base(Title, _catalogSummary, _catalogInteractions, _catalogProperties)
    {
    }

    /// <inheritdoc/>
    protected override void BuildExamples(ControlStack examples)
    {
        ControlRichText introductory = new()
        {
            Wrapping = Wrapping.Word,
            TextAlignment = Alignment.Start,
        };
        introductory.Inlines.Add(new ControlRun("Rich ")
        {
            Attributes = TerminalAttributes.Bold,
        });
        introductory.Inlines.Add(new ControlRun("terminal text") { Attributes = TerminalAttributes.Italic });
        introductory.Inlines.Add(new LineBreak());
        introductory.Inlines.Add(new ControlRun("Unicode: café · 你好 · 👩‍💻 · "));
        introductory.Inlines.Add(new Hyperlink("project source", "https://github.com/pavkam")
        {
            Attributes = TerminalAttributes.Underline,
        });
        examples.Children.Add(PaneSupport.SampleSection(
            "Styled document and OSC 8 link",
            "Runs carry independent foreground, attributes, and hyperlink metadata. The link is explicitly underlined as well as semantic; compatible terminals expose it on hover or open it with their configured gesture.",
            PaneSupport.Card(introductory, Glyphs.Rounded)));

        ControlRichText attributes = new() { Wrapping = Wrapping.Word };
        PaneSupport.AddAttributeLine(attributes, "Bold", "increased intensity", TerminalAttributes.Bold, _bright);
        PaneSupport.AddAttributeLine(attributes, "Dim", "reduced intensity", TerminalAttributes.Dim, _muted);
        PaneSupport.AddAttributeLine(attributes, "Italic", "slanted presentation", TerminalAttributes.Italic, _accent);
        PaneSupport.AddAttributeLine(attributes, "Underline", "single underline", TerminalAttributes.Underline, _success);
        PaneSupport.AddAttributeLine(attributes, "Blink", "blink requested; terminal policy may suppress it", TerminalAttributes.Blink, _warning);
        PaneSupport.AddAttributeLine(attributes, "Rapid blink", "rapid blink requested", TerminalAttributes.RapidBlink, _warning);
        PaneSupport.AddAttributeLine(attributes, "Reverse", "foreground and background exchanged", TerminalAttributes.Reverse, _accent);
        PaneSupport.AddAttributeLine(attributes, "Strike", "strikethrough presentation", TerminalAttributes.Strike, _warning);
        PaneSupport.AddAttributeLine(attributes, "Overline", "line above the text", TerminalAttributes.Overline, _accent);
        attributes.Inlines.Add(new LineBreak());
        attributes.Inlines.Add(new ControlRun("Curly underline: ") { Attributes = TerminalAttributes.Dim });
        attributes.Inlines.Add(new ControlRun("diagnostic emphasis")
        {
            Foreground = _bright,
            Underline = Terminal.Protocols.Underline.Curly,
            UnderlineColor = _warning,
        });
        PaneSupport.AddAttributeLine(attributes, "Hidden", "concealed run follows", TerminalAttributes.Hidden, _muted);
        attributes.Inlines.Add(new ControlRun(" (the concealed sample is intentional)") { Attributes = TerminalAttributes.Dim });
        PaneSupport.AddAttributeLine(
            attributes,
            "Combined",
            "bold + underline + italic",
            TerminalAttributes.Bold | TerminalAttributes.Underline | TerminalAttributes.Italic,
            _success);
        examples.Children.Add(PaneSupport.SampleSection(
            "Terminal text attributes",
            "Every row below is a real RichText run. Modern underline shape/color and overline use proved terminal capabilities; unsupported underline shapes become straight and unsupported color or overline is omitted.",
            PaneSupport.Card(attributes, Glyphs.Light)));

        ControlRichText wrapped = new() { Width = Length.Cells(30), Wrapping = Wrapping.Word };
        wrapped.Inlines.Add(new ControlRun("Resize this narrow reading column. RichText wraps between words while keeping Unicode graphemes intact. "));
        wrapped.Inlines.Add(new Hyperlink("Read the protocol guide", "https://invisible-island.net/xterm/ctlseqs/ctlseqs.html"));

        ControlText activity = new("Activity log: waiting for an inline mutation.");
        ControlButton append = new()
        {
            Content = new ControlText("Append a Run"),
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
            wrapped.Inlines.Add(new ControlRun(
                $"Mutation {mutation}: {selectedStyle.Name} Run appended through the {eventArgs.Cause} path.")
            {
                Attributes = selectedStyle.Value,
                Foreground = selectedStyle.Color,
            });
            activity.Content = $"Activity log: {eventArgs.Cause} appended {selectedStyle.Name} Run {mutation}.";
        };

        ControlStack readingExample = PaneSupport.Vertical();
        readingExample.Children.Add(wrapped);
        readingExample.Children.Add(PaneSupport.ButtonSpecimen(append));
        readingExample.Children.Add(activity);
        examples.Children.Add(PaneSupport.SampleSection(
            "Responsive reading column",
            "A constrained document is useful for help panes, release notes, and inline documentation. Activate the button to append a differently styled run and watch the log.",
            PaneSupport.Card(readingExample, Glyphs.Light)));
    }
}
