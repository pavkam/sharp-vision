// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Text;

/// <summary>Documents horizontal, vertical, styled, and compact Separator specimens.</summary>
internal sealed class SeparatorPane: CompositeControl
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Separator";

    /// <summary>Initializes the retained Separator documentation page.</summary>
    internal SeparatorPane() => InitializeContent(CreateContent());

    private static Dock CreateContent()
    {
        var horizontal = new Separator
        {
            Width = Length.Cells(28),
            Height = Length.Cells(1),
        };
        var vertical = new Separator
        {
            Width = Length.Cells(1),
            Height = Length.Cells(6),
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        var verticalStage = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Children =
            {
                vertical,
                new Text("Six rows share one vertical semantic divider."),
            },
        };
        var custom = new Separator
        {
            Width = Length.Cells(28),
            Height = Length.Cells(1),
            HorizontalGlyph = new Rune('='),
            Foreground = ThemeColors.Accent,
        };
        var compact = new Separator
        {
            Width = Length.Cells(1),
            Height = Length.Cells(1),
            HorizontalGlyph = new Rune('·'),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        return Doc.Page(
            Title,
            "Separates adjacent content with one non-interactive semantic line and no wrapper control.",
            Doc.Section(
                "─",
                "Orientation",
                "Parent layout supplies the final axis length while Separator keeps one-cell intrinsic size.",
                Doc.Example(
                    "Horizontal line",
                    "The default glyph fills the committed content row.",
                    Doc.Card(horizontal),
                    "var divider = new Separator { Width = Length.Cells(28) };"),
                Doc.Example(
                    "Vertical line",
                    "Vertical orientation fills the first content column from top to bottom.",
                    Doc.Card(verticalStage),
                    "divider.Orientation = Orientation.Vertical;")),
            Doc.Section(
                "=",
                "Glyph and style",
                "Validated one-cell glyphs and ordinary visual-state styling change presentation without adding behavior.",
                Doc.Example(
                    "Custom accent divider",
                    "This caller-defined glyph uses the semantic Accent role.",
                    Doc.Card(custom),
                    "divider.HorizontalGlyph = new Rune('=');"),
                Doc.Example(
                    "Tiny bound",
                    "A one-cell slot draws one complete cell and never overflows.",
                    Doc.Card(compact))));
    }
}
