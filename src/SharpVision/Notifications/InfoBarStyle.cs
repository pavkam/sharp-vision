// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Notifications;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable in-flow notification presentation.</summary>
[PublicAPI]
public sealed record InfoBarStyle: ControlStyle
{
    /// <summary>Gets the primary InfoBar style definition with one-hop Control fallback.</summary>
    internal static StyleDefinition<InfoBarStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        static (previous, previousTheme, current, currentTheme) =>
            previous.Padding != current.Padding ||
            previous.ContentGap != current.ContentGap ||
            previous.AdornmentGap != current.AdornmentGap ||
            previous.DismissGlyph != current.DismissGlyph
                ? InvalidationImpact.Measure
                : previous != current ||
                  ControlBase.ResolveColor(previous.AdornmentColor, previousTheme) !=
                  ControlBase.ResolveColor(current.AdornmentColor, currentTheme) ||
                  ControlBase.ResolveColor(previous.DismissColor, previousTheme) !=
                  ControlBase.ResolveColor(current.DismissColor, currentTheme)
                    ? InvalidationImpact.Render
                    : InvalidationImpact.None);

    /// <summary>Initializes a complete InfoBar presentation.</summary>
    /// <param name="face">The body face.</param>
    /// <param name="border">The intrinsic border.</param>
    /// <param name="shadow">The intrinsic shadow.</param>
    /// <param name="titleFace">The optional title face.</param>
    /// <param name="adornmentColor">The paintable default adornment foreground.</param>
    /// <param name="dismissGlyph">The preferred and portable dismiss glyphs.</param>
    /// <param name="dismissColor">The paintable dismiss foreground.</param>
    /// <param name="padding">The non-negative internal content padding.</param>
    /// <param name="contentGap">The non-negative rows between header and body.</param>
    /// <param name="adornmentGap">The non-negative cells after an adornment.</param>
    /// <exception cref="ArgumentException">A foreground paint color is transparent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A gap is negative.</exception>
    [SetsRequiredMembers]
    public InfoBarStyle(
        Face face,
        Border border,
        Shadow shadow,
        Face titleFace,
        ControlColor adornmentColor,
        ControlGlyph dismissGlyph,
        ControlColor dismissColor,
        Thickness padding,
        int contentGap,
        int adornmentGap) : base(face, border, shadow)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(contentGap);
        ArgumentOutOfRangeException.ThrowIfNegative(adornmentGap);
        ControlColor.ValidatePaint(adornmentColor, nameof(adornmentColor));
        ControlColor.ValidatePaint(dismissColor, nameof(dismissColor));
        TitleFace = titleFace;
        AdornmentColor = adornmentColor;
        DismissGlyph = dismissGlyph;
        DismissColor = dismissColor;
        Padding = padding;
        ContentGap = contentGap;
        AdornmentGap = adornmentGap;
    }

    /// <summary>Gets the face used by the optional title.</summary>
    public required Face TitleFace { get; init; }

    /// <summary>Gets the default adornment foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor AdornmentColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the preferred and portable dismiss glyphs.</summary>
    public required ControlGlyph DismissGlyph { get; init; }

    /// <summary>Gets the dismiss foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor DismissColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the internal padding in terminal cells.</summary>
    public required Thickness Padding { get; init; }

    /// <summary>Gets the rows between a present header and visible body.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is negative.</exception>
    public required int ContentGap
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    }

    /// <summary>Gets the cells after a present adornment.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is negative.</exception>
    public required int AdornmentGap
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    }

    /// <summary>Gets the informational preset and default presentation.</summary>
    public static InfoBarStyle Info { get; } = CreatePreset(ControlStyle.Default, SemanticColor.Info);

    /// <summary>Gets the default presentation, aliasing <see cref="Info"/>.</summary>
    public static new InfoBarStyle Default => Info;

    /// <summary>Gets the success preset.</summary>
    public static InfoBarStyle Success { get; } = CreatePreset(ControlStyle.Default, SemanticColor.Success);

    /// <summary>Gets the warning preset.</summary>
    public static InfoBarStyle Warning { get; } = CreatePreset(ControlStyle.Default, SemanticColor.Warning);

    /// <summary>Gets the error preset.</summary>
    public static InfoBarStyle Error { get; } = CreatePreset(ControlStyle.Default, SemanticColor.Error);

    private static InfoBarStyle Complete(ControlStyle control, VisualState _, Theme __) =>
        CreatePreset(control, SemanticColor.Info);

    private static InfoBarStyle CreatePreset(ControlStyle control, SemanticColor accent)
    {
        var face = control.Face.Background.IsLiteral && control.Face.Background.Literal == Color.Transparent
            ? control.Face with { Background = SemanticColor.Window }
            : control.Face;
        var titleFace = face with
        {
            Foreground = accent,
            Attributes = SemanticDecoration.ActiveText
        };
        var border = control.Border with
        {
            Sides = BorderSide.All,
            Foreground = accent,
            Relief = BorderRelief.Flat,
            Background = face.Background
        };
        return new InfoBarStyle(
            face,
            border,
            control.Shadow with { IsVisible = false },
            titleFace,
            accent,
            ControlGlyphs.Chrome.WindowClose,
            accent,
            new Thickness(1),
            contentGap: 1,
            adornmentGap: 1);
    }
}
