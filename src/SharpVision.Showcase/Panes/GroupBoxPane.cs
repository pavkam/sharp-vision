// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents titled, empty, Unicode, styled, nested, and compact GroupBox specimens.</summary>
internal sealed class GroupBoxPane: CompositeControl
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "GroupBox";

    /// <summary>Initializes the retained GroupBox documentation page.</summary>
    internal GroupBoxPane() => InitializeContent(CreateContent());

    private static Dock CreateContent()
    {
        var empty = new GroupBox
        {
            Content = new Text("A continuous top edge groups content without a caption."),
            Width = Length.Cells(56),
            Height = Length.Cells(3),
        };
        var titled = new GroupBox
        {
            Header = "Settings",
            Content = new Stack
            {
                Children =
                {
                    new CheckBox { Content = new Text("Auto save") },
                    new CheckBox { Content = new Text("Line numbers") },
                },
            },
            Width = Length.Cells(32),
            Height = Length.Cells(6),
        };
        var unicode = new GroupBox
        {
            Header = "界 Tools",
            Content = new Text("Wide header cells interrupt the frame without splitting a continuation."),
            Width = Length.Cells(40),
            Height = Length.Cells(4),
        };
        var accentStyle = new ControlStyle<GroupBox>();
        accentStyle.Set(ForegroundProperty, State.Normal, ThemeColors.Accent);
        accentStyle.Set(BorderColorProperty, State.Normal, ThemeColors.Accent);
        var styled = new GroupBox
        {
            Header = "Scoped accent",
            Content = new Text("The GroupBox style scope supplies its accent to ordinary content."),
            Style = accentStyle,
            Width = Length.Cells(48),
            Height = Length.Cells(4),
        };
        var ascii = new GroupBox
        {
            Header = "ASCII",
            Glyphs = Glyphs.Ascii,
            Content = new Text("Portable frame"),
            Width = Length.Cells(22),
            Height = Length.Cells(3),
        };
        var tiny = new GroupBox
        {
            Header = "Long title",
            Content = new Text("Clipped"),
            Width = Length.Cells(5),
            Height = Length.Cells(2),
        };

        return Doc.Page(
            Title,
            "Frames one caller-owned content control with intrinsic border geometry and an optional cell-aware header.",
            Doc.Section(
                "▣",
                "Frame and header",
                "Empty headers preserve the top edge; titled frames reserve enough cells for the complete caption.",
                Doc.Example(
                    "Continuous frame",
                    "No header means no interruption in the top border.",
                    Doc.Card(empty)),
                Doc.Example(
                    "Titled settings",
                    "Use one Stack or Grid as content when the group owns several semantic fields.",
                    Doc.Card(titled),
                    "var group = new GroupBox { Header = \"Settings\", Content = fields };")),
            Doc.Section(
                "界",
                "Unicode and style scope",
                "Header measurement follows terminal cells, while the grouping boundary can cascade a style to descendants.",
                Doc.Example(
                    "Wide header",
                    "The wide glyph owns both physical cells in the interrupted top edge.",
                    Doc.Card(unicode)),
                Doc.Example(
                    "Scoped accent",
                    "A typed instance style applies to the frame and contributes to child style resolution.",
                    Doc.Card(styled))),
            Doc.Section(
                "+",
                "Glyphs and constrained space",
                "Validated border families and strict clipping preserve corners even when no content row fits.",
                Doc.Example(
                    "ASCII frame",
                    "Swap the physical glyph family without changing ownership or layout.",
                    Doc.Card(ascii),
                    "group.Glyphs = Glyphs.Ascii;"),
                Doc.Example(
                    "Tiny frame",
                    "The title clips between preserved corners and content disappears when the interior is empty.",
                    Doc.Card(tiny))));
    }
}
