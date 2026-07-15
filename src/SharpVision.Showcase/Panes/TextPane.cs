// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using SharpVision.Text;

using Text = SharpVision.Controls.Text;

/// <summary>Documents the Text control with Unicode geometry, pointer, wrapping, and alignment specimens.</summary>
internal sealed class TextPane: View
{
    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Text";

    /// <inheritdoc/>
    protected override Control Build()
    {
        var geometry = new Text("é vs é · orphan ́ · ambiguous · · 你好 · 👩‍💻 · 🇺🇸");

        var wrapped = new Text("Plain Unicode: café · 你好 · 👩‍💻\nA narrow reading column wraps words without splitting clusters.")
        {
            Width = Length.Cells(28),
            Wrapping = Wrapping.Word,
        };

        var centered = new Text("Centered status")
        {
            Width = Length.Cells(28),
            TextAlignment = Alignment.Center,
            Attributes = TerminalAttributes.Bold,
        };

        var trimmed = new Text("This deliberately long one-line label trims safely")
        {
            Width = Length.Cells(28),
            Trimming = Trimming.GraphemeEllipsis,
        };

        return Doc.Page(
            Title,
            "Formats Unicode text by grapheme cluster with wrapping, trimming, alignment, and cell-width policy.",
            Doc.Example(
                "Cell geometry specimen",
                "Composed and decomposed text share width. Orphan combining marks render as replacement cells without changing editable source text.",
                geometry),
            Doc.Example(
                "Uneven pixel pointer grid",
                "Pixel coordinates stay exact. Mapped cells appear only when exact grid metrics are available; unavailable cells are not shown as (0,0).",
                new PointerProbe()),
            Doc.Example(
                "Unicode-safe wrapping",
                "Word wrapping leaves complete grapheme clusters together, including combining marks and wide emoji.",
                wrapped),
            Doc.Example(
                "Centered label",
                "Centering is for compact labels and status messages; it is deliberately shown without trimming.",
                centered),
            Doc.Example(
                "Single-line truncation",
                "Ellipsis is for one-line labels where the remaining space matters more than wrapping.",
                trimmed));
    }
}
