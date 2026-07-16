// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents expanded, collapsed, nested, disabled, Unicode, and replaced-content Expander specimens.</summary>
internal sealed class ExpanderPane: CompositeControl
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Expander";

    /// <summary>Initializes the retained Expander documentation page.</summary>
    internal ExpanderPane() => InitializeContent(CreateContent());

    private static Dock CreateContent()
    {
        var expanded = Create("Details", "The content participates below the retained header.", isExpanded: true);
        var collapsed = Create("Advanced", "Activate the header to reveal this content.", isExpanded: false);
        var nested = new Expander
        {
            Header = "Outer",
            Content = Create("Inner", "Nested sections keep independent expansion state.", isExpanded: true),
            Width = Length.Cells(48),
            Height = Length.Cells(4),
        };
        var disabled = Create("Unavailable", "Disabled headers refuse pointer and keyboard activation.", isExpanded: false);
        disabled.IsEnabled = false;
        var unicode = Create("界 Tools", "Wide header graphemes preserve complete terminal cells.", isExpanded: true);
        var replaced = Create("Replacement", "First", isExpanded: false);
        replaced.Content = new Text("The replacement remains owned while collapsed and appears on activation.");

        return Doc.Page(
            Title,
            "Reveals one caller-owned content control beneath a retained, focusable header without rebuilding either child.",
            Doc.Section(
                "▼",
                "Expansion state",
                "The directional glyph, content geometry, and changed event commit as one observable state transition.",
                Doc.Example(
                    "Expanded",
                    "Content participates in measure, arrangement, rendering, hit testing, and navigation.",
                    Doc.Card(expanded),
                    "var details = new Expander { Header = \"Details\", Content = body };"),
                Doc.Example(
                    "Collapsed",
                    "The content remains owned but contributes no geometry until the header is activated.",
                    Doc.Card(collapsed))),
            Doc.Section(
                "↳",
                "Composition and availability",
                "Retained headers support nesting and inherited disabled state without virtual trees or rebuilt content.",
                Doc.Example(
                    "Nested sections",
                    "Each Expander owns an independent header and one caller-replaceable content child.",
                    Doc.Card(nested)),
                Doc.Example(
                    "Disabled",
                    "Unavailable headers remain visible but reject pointer, Space, and Enter activation.",
                    Doc.Card(disabled))),
            Doc.Section(
                "界",
                "Unicode and replacement",
                "Header measurement uses terminal cells, while replacing collapsed content transfers ownership immediately.",
                Doc.Example(
                    "Wide header",
                    "The wide glyph occupies one grapheme and two physical cells.",
                    Doc.Card(unicode)),
                Doc.Example(
                    "Replaced while collapsed",
                    "Activate the header to reveal the replacement rather than the released first child.",
                    Doc.Card(replaced),
                    "details.Content = replacement;")));
    }

    private static Expander Create(string header, string content, bool isExpanded) => new()
    {
        Header = header,
        Content = new Text(content),
        IsExpanded = isExpanded,
        Width = Length.Cells(64),
        Height = Length.Cells(isExpanded ? 3 : 1),
    };
}
