// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>Builds one presentation-fallback selectable-text snapshot from a retained subtree.</summary>
/// <remarks>
/// Collapsed, hidden, effectively invisible, and disposed children contribute neither semantic text
/// nor geometry because they are absent from the current visual page. Aggregate nodes are traversed
/// directly through their internal ordered-child seam; only leaf sources materialize snapshots, so
/// validation and owned copies occur once at the requested root instead of once per retained depth.
/// </remarks>
internal static class SelectableTextAggregation
{
    /// <summary>Aggregates the owner's semantic retained descendants in stable presentation order.</summary>
    /// <param name="owner">The aggregate owner whose origin defines returned glyph coordinates.</param>
    /// <returns>An independently owned non-authoritative aggregate snapshot.</returns>
    internal static SelectableTextSnapshot Create(ControlBase owner)
    {
        var map = CreateMap(owner);
        var glyphs = new SelectableTextGlyph[map.Glyphs.Count];

        for (var index = 0; index < glyphs.Length; index++)
        {
            var glyph = map.Glyphs[index];
            glyphs[index] = new SelectableTextGlyph(glyph.Range, glyph.Bounds);
        }

        return new SelectableTextSnapshot(map.Text, glyphs, isAuthoritative: false);
    }

    /// <summary>Aggregates semantic text, glyphs, and source identity into the common indexed map.</summary>
    /// <param name="owner">The aggregate owner whose origin defines returned glyph coordinates.</param>
    /// <returns>An immutable common selection map.</returns>
    internal static TextSelectionMap CreateMap(ControlBase owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (owner.IsDisposed || !owner.EffectiveIsVisible)
        {
            return TextSelectionMap.Empty;
        }

        var text = new StringBuilder();
        var glyphs = new List<TextSelectionGlyph>();
        var sources = new List<TextSelectionSource>();
        var inheritedClip = GetEffectiveClip(owner);
        var descendantClip = owner.ResolveSelectableTextDescendantClip(inheritedClip);
        CollectAggregate(owner, owner, descendantClip, text, glyphs, sources);
        var lineCount = Math.Max(0, owner.Bounds.Height);

        foreach (var glyph in glyphs)
        {
            lineCount = Math.Max(lineCount, glyph.Bounds.Bottom);
        }

        return new TextSelectionMap(text.ToString(), [.. glyphs], [.. sources], lineCount);
    }

    /// <summary>Visits one aggregate node without constructing an intermediate snapshot.</summary>
    private static void CollectAggregate(
        ControlBase owner,
        ControlBase aggregate,
        Rect clip,
        StringBuilder text,
        List<TextSelectionGlyph> glyphs,
        List<TextSelectionSource> sources)
    {
        var children = new List<ControlBase>();

        if (!aggregate.AddSelectableTextChildren(children))
        {
            CollectLeaf(owner, aggregate, clip, text, glyphs, sources);
            return;
        }

        CollectChildren(owner, children, clip, text, glyphs, sources);
    }

    /// <summary>Visits ordered aggregate children through one carried clipping aperture.</summary>
    private static void CollectChildren(
        ControlBase owner,
        List<ControlBase> children,
        Rect clip,
        StringBuilder text,
        List<TextSelectionGlyph> glyphs,
        List<TextSelectionSource> sources)
    {
        foreach (var child in children)
        {
            if (child.IsDisposed || child.Visibility != Visibility.Visible || !child.EffectiveIsVisible)
            {
                continue;
            }

            var nested = new List<ControlBase>();

            if (child.AddSelectableTextChildren(nested))
            {
                var childClip = child.ResolveSelectableTextDescendantClip(clip);
                CollectChildren(owner, nested, childClip, text, glyphs, sources);
            }
            else
            {
                CollectLeaf(owner, child, clip, text, glyphs, sources);
            }
        }
    }

    /// <summary>Appends one leaf projection and filters geometry through the effective aperture.</summary>
    private static void CollectLeaf(
        ControlBase owner,
        ControlBase leaf,
        Rect clip,
        StringBuilder text,
        List<TextSelectionGlyph> glyphs,
        List<TextSelectionSource> sources)
    {
        if (leaf is not ISelectableTextSource source)
        {
            return;
        }

        var snapshot = source.GetSelectableTextSnapshot();
        var offset = text.Length;
        _ = text.Append(snapshot.Text);
        var selectionSource = new TextSelectionSource(
            source,
            leaf as ISelectableTextViewport,
            new Selection(offset, text.Length),
            snapshot.Text,
            source.SelectableTextVersion);
        sources.Add(selectionSource);

        foreach (var glyph in snapshot.Glyphs)
        {
            var absolute = new Rect(
                leaf.Bounds.X.SaturatingAdd(glyph.Bounds.X),
                leaf.Bounds.Y.SaturatingAdd(glyph.Bounds.Y),
                glyph.Bounds.Width,
                glyph.Bounds.Height);

            if (!ContainsCompleteGlyph(clip, absolute))
            {
                continue;
            }

            glyphs.Add(new TextSelectionGlyph(
                new Selection(
                    offset.SaturatingAdd(glyph.Range.Start),
                    offset.SaturatingAdd(glyph.Range.End)),
                new Rect(
                    absolute.X.SaturatingSubtract(owner.Bounds.X),
                    absolute.Y.SaturatingSubtract(owner.Bounds.Y),
                    absolute.Width,
                    absolute.Height),
                selectionSource));
        }
    }

    /// <summary>Gets whether a complete glyph rectangle lies inside the effective aperture.</summary>
    internal static bool ContainsCompleteGlyph(Rect clip, Rect candidate) =>
        candidate.X >= clip.X && candidate.Y >= clip.Y &&
        (long) candidate.X + candidate.Width <= (long) clip.X + clip.Width &&
        (long) candidate.Y + candidate.Height <= (long) clip.Y + clip.Height;

    /// <summary>Gets the effective absolute render aperture inherited by one source control.</summary>
    /// <param name="source">The attached source whose retained ancestors define the aperture.</param>
    /// <returns>The finite absolute cell rectangle inherited by the source.</returns>
    internal static Rect GetEffectiveClip(ControlBase source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GetSelectableTextInheritedClip();
    }

}
