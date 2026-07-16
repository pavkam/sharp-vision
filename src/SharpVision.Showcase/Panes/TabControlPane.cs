// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents selection, availability, Unicode, overflow, and replacement TabControl specimens.</summary>
internal sealed class TabControlPane: CompositeControl
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "TabControl";

    /// <summary>Initializes the retained TabControl documentation page.</summary>
    internal TabControlPane() => InitializeContent(CreateContent());

    private static Dock CreateContent()
    {
        var basic = CreateTabs(48, ("General", "General settings"), ("Advanced", "Advanced settings"));
        var disabled = CreateTabs(48, ("Available", "Selected page"), ("Unavailable", "Cannot select"));
        disabled.Items[1].IsEnabled = false;
        var unicode = CreateTabs(32, ("界 Tools", "Wide headers preserve complete cells."), ("Emoji", "Ordinary text follows."));
        var overflow = CreateTabs(
            14,
            ("Overview", "First"),
            ("Long settings", "Middle"),
            ("界", "Selected overflow page"));
        overflow.SelectedIndex = 2;
        var overflowStage = new Stack
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                overflow,
                new Text { Width = Length.Star(1) },
            },
        };
        var replacement = CreateTabs(48, ("Replacement", "First"), ("Other", "Other page"));
        replacement.Items[0].Content = new Text("Caller content was replaced without rebuilding the header.");

        return Doc.Page(
            Title,
            "Coordinates typed retained pages through one focusable header strip and one selected content region.",
            Doc.Section(
                "▤",
                "Selection and content",
                "Pointer and keyboard selection commit header state before swapping the participating page content.",
                Doc.Example(
                    "Basic pages",
                    "Use typed TabItem pages; the first eligible page selects automatically.",
                    Doc.Card(basic),
                    "tabs.Items.Add(new TabItem { Header = \"General\", Content = body });"),
                Doc.Example(
                    "Disabled page",
                    "Navigation skips unavailable headers without stealing the current selection.",
                    Doc.Card(disabled))),
            Doc.Section(
                "界",
                "Unicode and overflow",
                "Header measurement uses terminal cells and keeps the selected label visible in constrained strips.",
                Doc.Example(
                    "Wide header",
                    "The CJK grapheme owns its continuation cell inside the retained button.",
                    Doc.Card(unicode)),
                Doc.Example(
                    "Overflow reveal",
                    "Selecting the final page scrolls only the clipped header origin, not page content.",
                    Doc.Card(overflowStage))),
            Doc.Section(
                "↻",
                "Ownership and repair",
                "Content replacement preserves header identity, while page removal chooses the nearest eligible peer.",
                Doc.Example(
                    "Replaced content",
                    "Caller content transfers through ContentControl ownership without reconstructing the page.",
                    Doc.Card(replacement),
                    "page.Content = replacement;")));
    }

    private static TabControl CreateTabs(int width, params (string Header, string Content)[] pages)
    {
        var tabs = new TabControl
        {
            Width = Length.Cells(width),
            Height = Length.Cells(4),
        };

        foreach (var page in pages)
        {
            tabs.Items.Add(new TabItem
            {
                Header = page.Header,
                Content = new Text(page.Content),
            });
        }

        return tabs;
    }
}
