// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Notifications;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable Toast presentation with open-ended semantic coloring.</summary>
[PublicAPI]
public sealed record ToastStyle: PopupStyle
{
    /// <summary>Gets the primary Toast style definition, falling back to the theme's Popup role.</summary>
    internal static StyleDefinition<ToastStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(PopupStyle.Default),
        Complete,
        static (previous, _, current, _) =>
            previous.Padding != current.Padding ||
            previous.ContentGap != current.ContentGap ||
            previous.AdornmentGap != current.AdornmentGap ||
            previous.CloseGlyph != current.CloseGlyph
                ? InvalidationImpact.Measure
                : InvalidationImpact.None);

    /// <summary>Initializes a complete Toast appearance.</summary>
    /// <param name="face">The body face.</param>
    /// <param name="border">The intrinsic border.</param>
    /// <param name="shadow">The intrinsic shadow.</param>
    /// <param name="titleFace">The optional title face.</param>
    /// <param name="adornmentColor">The default adornment foreground.</param>
    /// <param name="closeGlyph">The dismiss affordance glyph and fallback.</param>
    /// <param name="closeColor">The dismiss affordance foreground.</param>
    /// <param name="padding">The non-negative content padding in cells.</param>
    /// <param name="contentGap">The non-negative rows between title and content.</param>
    /// <param name="adornmentGap">The non-negative columns after an adornment.</param>
    /// <exception cref="ArgumentOutOfRangeException">A gap is negative.</exception>
    [SetsRequiredMembers]
    public ToastStyle(
        Face face,
        Border border,
        Shadow shadow,
        Face titleFace,
        ControlColor adornmentColor,
        ControlGlyph closeGlyph,
        ControlColor closeColor,
        Thickness padding,
        int contentGap,
        int adornmentGap) : base(face, border, shadow)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(contentGap);
        ArgumentOutOfRangeException.ThrowIfNegative(adornmentGap);
        TitleFace = titleFace;
        AdornmentColor = adornmentColor;
        CloseGlyph = closeGlyph;
        CloseColor = closeColor;
        Padding = padding;
        ContentGap = contentGap;
        AdornmentGap = adornmentGap;
    }

    /// <summary>Gets the face used by the optional title.</summary>
    public required Face TitleFace { get; init; }

    /// <summary>Gets the default foreground used by an adornment without its own color.</summary>
    public required ControlColor AdornmentColor { get; init; }

    /// <summary>Gets the preferred and fallback glyph used by the dismiss affordance.</summary>
    public required ControlGlyph CloseGlyph { get; init; }

    /// <summary>Gets the dismiss affordance foreground.</summary>
    public required ControlColor CloseColor { get; init; }

    /// <summary>Gets the internal padding in terminal cells.</summary>
    public required Thickness Padding { get; init; }

    /// <summary>Gets the rows reserved between a present title and content.</summary>
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

    /// <summary>Gets the columns reserved after a present adornment.</summary>
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

    /// <summary>Gets the informational Toast preset and default presentation.</summary>
    public static ToastStyle Info { get; } = CreatePreset(PopupStyle.Default, SemanticColor.Info);

    /// <summary>Gets the default Toast presentation, aliasing <see cref="Info"/>.</summary>
    public static new ToastStyle Default => Info;

    /// <summary>Gets the error Toast preset.</summary>
    public static ToastStyle Error { get; } = CreatePreset(PopupStyle.Default, SemanticColor.Error);

    /// <summary>Gets the warning Toast preset.</summary>
    public static ToastStyle Warning { get; } = CreatePreset(PopupStyle.Default, SemanticColor.Warning);

    /// <summary>Gets the success Toast preset.</summary>
    public static ToastStyle Success { get; } = CreatePreset(PopupStyle.Default, SemanticColor.Success);

    /// <summary>Gets the muted trace Toast preset.</summary>
    public static ToastStyle Trace { get; } = CreatePreset(PopupStyle.Default, SemanticColor.Muted);

    private static ToastStyle Complete(PopupStyle popup, VisualState _, Theme __) =>
        CreatePreset(popup, SemanticColor.Info);

    private static ToastStyle CreatePreset(PopupStyle popup, SemanticColor accent)
    {
        var face = popup.Face.Background.IsLiteral && popup.Face.Background.Literal == Color.Transparent
            ? popup.Face with { Background = SemanticColor.Window }
            : popup.Face;
        var titleFace = face with
        {
            Foreground = accent,
            Attributes = SemanticDecoration.ActiveText
        };
        var border = popup.Border with { Foreground = accent };
        return new ToastStyle(
            face,
            border,
            popup.Shadow,
            titleFace,
            accent,
            ControlGlyphs.Chrome.WindowClose,
            accent,
            new Thickness(1),
            contentGap: 1,
            adornmentGap: 1);
    }
}
