// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

/// <summary>Verifies every core curated theme resolves <see cref="DocumentStyle"/> into visually
/// distinguishable, legible faces.</summary>
public sealed class CuratedThemeDocumentStyleTests
{
    /// <summary>Verifies every bundled theme keeps a Document's action-link chip visually distinct
    /// from an ordinary link and from its own focused state, so a call-to-action link can never
    /// blend into the surrounding text or fail to show which one currently has focus. Resolved
    /// colors are compared, not <see cref="Face"/> values, so a theme cannot pass by naming two
    /// different semantic colors that happen to share one resolved value.</summary>
    [Fact]
    public void EveryTheme_KeepsDocumentActionLinkFacesDistinguishable()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var style = DocumentStyle.Definition.Resolve(null, theme);

            var actionBackground = ControlBase.ResolveColor(style.ActionLinkFace.Background, theme);
            var linkBackground = ControlBase.ResolveColor(style.LinkFace.Background, theme);
            var activeActionBackground = ControlBase.ResolveColor(style.ActiveActionLinkFace.Background, theme);

            actionBackground.ShouldNotBe(
                linkBackground,
                $"{slug} a Document action-link chip must be visually distinct from an ordinary link");
            activeActionBackground.ShouldNotBe(
                actionBackground,
                $"{slug} a focused Document action link must be visually distinct from its resting chip");
        }
    }

    /// <summary>Verifies every bundled theme keeps a Document's quote foreground equal to its
    /// ambient body foreground. A quotation is set apart through the italic attribute rather than
    /// a lower-contrast tone that the theme has not validated against its surface.</summary>
    [Fact]
    public void EveryTheme_KeepsDocumentQuoteForegroundInheritingTheAmbientColor()
    {
        foreach (var slug in ThemeCatalog.Slugs)
        {
            var theme = ThemeCatalog.Load(slug);
            var style = DocumentStyle.Definition.Resolve(null, theme);

            var quoteForeground = ControlBase.ResolveColor(style.QuoteFace.Foreground, theme);
            var bodyForeground = ControlBase.ResolveColor(style.Face.Foreground, theme);

            quoteForeground.ShouldBe(
                bodyForeground,
                $"{slug} a Document quote must remain as legible as its ambient body text");
        }
    }
}
