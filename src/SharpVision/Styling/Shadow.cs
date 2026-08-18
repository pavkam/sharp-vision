// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Text.Json.Serialization;

/// <summary>Defines one complete intrinsic shadow appearance.</summary>
public readonly record struct Shadow: IAppearanceFragment
{
    /// <summary>Initializes a complete shadow appearance.</summary>
    /// <param name="isVisible">Whether shadow chrome is rendered.</param>
    /// <param name="mode">How shadow cells compose with their destination.</param>
    /// <param name="offset">The signed shadow translation in cells or half rows.</param>
    /// <param name="glyph">The block-glyph shadow Rune.</param>
    /// <param name="foreground">The shadow foreground.</param>
    /// <param name="background">The shadow background.</param>
    /// <param name="attributes">The shadow attributes.</param>
    /// <exception cref="ArgumentException">A paint channel is transparent or <paramref name="glyph"/> is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is unknown.</exception>
    public Shadow(
        bool isVisible,
        ShadowMode mode,
        Point offset,
        Rune glyph,
        ControlColor foreground,
        ControlColor background,
        ControlDecoration attributes)
    {
        // Validated by parameter name here as well as in each init accessor. The accessor guards
        // the doors a constructor cannot see - a `with` expression and the theme overlay's
        // reflective write - but it only ever knows the value as "value", which for a constructor
        // taking several channels of the same type identifies nothing.
        ArgumentOutOfRangeException.ThrowIfNotDefined(mode, nameof(mode), "The shadow mode is unknown.");

        ControlColor.ValidatePaint(foreground, nameof(foreground));

        if (glyph.Value != 0)
        {
            _ = glyph.ValidateSingleCell(nameof(glyph));
        }

        IsVisible = isVisible;
        Mode = mode;
        Offset = offset;
        Glyph = glyph;
        Foreground = foreground;
        Background = background;
        Attributes = attributes;
    }

    /// <summary>Gets whether shadow chrome is rendered.</summary>
    [JsonPropertyName("visible")]
    public bool IsVisible { get; init; }

    /// <summary>Gets how shadow cells compose with their destination.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is unknown.</exception>
    public ShadowMode Mode
    {
        get;
        init => field = Enum.IsDefined(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The shadow mode is unknown.");
    }

    /// <summary>Gets the signed shadow translation.</summary>
    public Point Offset { get; init; }

    /// <summary>Gets the block-glyph shadow Rune.</summary>
    /// <remarks>A zero Rune - the one <c>default(Rune)</c> produces - is normalized to the
    /// code-owned shadow glyph rather than rejected, so a partially-initialized value still draws.</remarks>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune Glyph
    {
        get;
        init => field = value.Value == 0
            ? ControlGlyphs.Chrome.Shadow.Value
            : value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the shadow foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public ControlColor Foreground
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the shadow background.</summary>
    public ControlColor Background { get; init; }

    /// <summary>Gets the shadow attributes.</summary>
    public ControlDecoration Attributes { get; init; }

    IAppearanceFragment IAppearanceFragment.Clone() => this with { };
}
