// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable Pager presentation.</summary>
/// <remarks>The passive chrome falls back to the standard interactive-control role. Navigation
/// glyphs and current-page emphasis are code-owned unless a local style replaces them.</remarks>
[PublicAPI]
public sealed record PagerStyle: ControlStyle
{
    /// <summary>Gets the primary Pager style definition.</summary>
    internal static StyleDefinition<PagerStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetInteractiveControlStyleSet(),
        Complete,
        static (previous, previousTheme, current, currentTheme) =>
            previous.FirstPageGlyph != current.FirstPageGlyph ||
            previous.PreviousPageGlyph != current.PreviousPageGlyph ||
            previous.NextPageGlyph != current.NextPageGlyph ||
            previous.LastPageGlyph != current.LastPageGlyph ||
            previous.OmittedPagesGlyph != current.OmittedPagesGlyph
                ? InvalidationImpact.Measure
                : previous != current ||
                  ControlBase.ResolveColor(previous.CurrentPageColor, previousTheme) !=
                  ControlBase.ResolveColor(current.CurrentPageColor, currentTheme)
                    ? InvalidationImpact.Render
                    : InvalidationImpact.None);

    /// <summary>Initializes a complete Pager presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete intrinsic border.</param>
    /// <param name="shadow">The complete intrinsic shadow.</param>
    /// <param name="firstPageGlyph">The preferred and portable first-page glyphs.</param>
    /// <param name="previousPageGlyph">The preferred and portable previous-page glyphs.</param>
    /// <param name="nextPageGlyph">The preferred and portable next-page glyphs.</param>
    /// <param name="lastPageGlyph">The preferred and portable last-page glyphs.</param>
    /// <param name="omittedPagesGlyph">The preferred and portable omitted-pages glyphs.</param>
    /// <param name="currentPageColor">The paintable foreground for the current page number.</param>
    /// <exception cref="ArgumentException"><paramref name="currentPageColor"/> is transparent.</exception>
    [SetsRequiredMembers]
    public PagerStyle(
        Face face,
        Border border,
        Shadow shadow,
        ControlGlyph firstPageGlyph,
        ControlGlyph previousPageGlyph,
        ControlGlyph nextPageGlyph,
        ControlGlyph lastPageGlyph,
        ControlGlyph omittedPagesGlyph,
        ControlColor currentPageColor) : base(face, border, shadow)
    {
        FirstPageGlyph = firstPageGlyph;
        PreviousPageGlyph = previousPageGlyph;
        NextPageGlyph = nextPageGlyph;
        LastPageGlyph = lastPageGlyph;
        OmittedPagesGlyph = omittedPagesGlyph;
        CurrentPageColor = currentPageColor;
    }

    /// <summary>Gets the standard Pager presentation.</summary>
    public static new PagerStyle Default { get; } = Complete(
        ControlStyle.Default,
        VisualState.Normal,
        Theme.Unthemed);

    /// <summary>Gets the preferred and portable first-page glyphs.</summary>
    public required ControlGlyph FirstPageGlyph { get; init; }

    /// <summary>Gets the preferred and portable previous-page glyphs.</summary>
    public required ControlGlyph PreviousPageGlyph { get; init; }

    /// <summary>Gets the preferred and portable next-page glyphs.</summary>
    public required ControlGlyph NextPageGlyph { get; init; }

    /// <summary>Gets the preferred and portable last-page glyphs.</summary>
    public required ControlGlyph LastPageGlyph { get; init; }

    /// <summary>Gets the preferred and portable omitted-pages glyphs.</summary>
    public required ControlGlyph OmittedPagesGlyph { get; init; }

    /// <summary>Gets the foreground used for the current page number.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor CurrentPageColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    private static PagerStyle Complete(ControlStyle control, VisualState _, Theme __) => new(
        control.Face,
        control.Border,
        control.Shadow,
        new ControlGlyph(new Rune('«'), new Rune('<')),
        new ControlGlyph(new Rune('‹'), new Rune('<')),
        new ControlGlyph(new Rune('›'), new Rune('>')),
        new ControlGlyph(new Rune('»'), new Rune('>')),
        new ControlGlyph(new Rune('…'), new Rune('.')),
        SemanticColor.Accent);
}
