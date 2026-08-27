// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

using UnicodeWidth = Width;

/// <summary>Projects one directly rendered single-line caption into semantic text geometry.</summary>
internal static class SingleLineSelectableTextProjection
{
    /// <summary>Creates an authoritative caption projection constrained to its rendered aperture.</summary>
    internal static SelectableTextSnapshot Create(
        ControlBase source,
        string text,
        Point origin,
        Rect aperture,
        bool useMnemonic)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(text);
        var visible = text.ToVisibleText(useMnemonic);

        if (!source.EffectiveIsVisible)
        {
            return new SelectableTextSnapshot(visible, [], isAuthoritative: true);
        }

        var clip = aperture.Intersect(SelectableTextAggregation.GetEffectiveClip(source));
        var glyphs = new List<SelectableTextGlyph>();
        var x = origin.X;

        foreach (var grapheme in Graphemes.Enumerate(visible))
        {
            var cluster = visible.AsSpan(grapheme.Offset, grapheme.Length);
            var width = UnicodeWidth.Measure(cluster, source.CellPolicy.AmbiguousWidth).Cells;
            var absolute = new Rect(x, origin.Y, width, 1);

            if (width > 0 && SelectableTextAggregation.ContainsCompleteGlyph(clip, absolute))
            {
                glyphs.Add(new SelectableTextGlyph(
                    new Selection(grapheme.Offset, grapheme.Offset + grapheme.Length),
                    new Rect(
                        absolute.X - source.Bounds.X,
                        absolute.Y - source.Bounds.Y,
                        absolute.Width,
                        absolute.Height)));
            }

            x = x.Add(width);
        }

        return new SelectableTextSnapshot(visible, glyphs, isAuthoritative: true);
    }
}
