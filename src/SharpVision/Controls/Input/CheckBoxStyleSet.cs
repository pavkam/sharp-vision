// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines optional member-wise contributions to a complete checkbox presentation.</summary>
[PublicAPI]
public readonly record struct CheckBoxStyleSet
{
    /// <summary>Initializes a partial checkbox presentation contribution.</summary>
    /// <param name="markStyle">The optional replacement mark-layout family.</param>
    /// <param name="glyphs">The optional replacement state glyph family.</param>
    /// <param name="appearance">The optional partial normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="markStyle"/> is undefined.</exception>
    /// <exception cref="ArgumentException">A supplied glyph is a control or is not one cell wide.</exception>
    public CheckBoxStyleSet(
        CheckBoxMarkStyle? markStyle = null,
        CheckBoxGlyphs? glyphs = null,
        AppearanceProfileSet? appearance = null)
    {
        if (markStyle is { } markStyleValue)
        {
            markStyleValue.ValidateDefined();
        }

        CheckBoxGlyphs? validatedGlyphs = null;
        if (glyphs is { } glyphValues)
        {
            validatedGlyphs = new CheckBoxGlyphs(
                glyphValues.Unchecked,
                glyphValues.Checked,
                glyphValues.Indeterminate);
        }

        MarkStyle = markStyle;
        Glyphs = validatedGlyphs;
        Appearance = appearance;
    }

    /// <summary>Gets the optional replacement mark-layout family.</summary>
    public CheckBoxMarkStyle? MarkStyle { get; }

    /// <summary>Gets the optional replacement state glyph family.</summary>
    public CheckBoxGlyphs? Glyphs { get; }

    /// <summary>Gets the optional partial normal and visual-state appearance profile.</summary>
    public AppearanceProfileSet? Appearance { get; }

    /// <summary>Applies this partial contribution to a complete checkbox presentation.</summary>
    /// <param name="baseline">The complete presentation that supplies omitted members.</param>
    /// <returns>The validated complete composed presentation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The composed mark style is undefined.</exception>
    /// <exception cref="ArgumentException">A composed glyph is a control or is not one cell wide.</exception>
    public CheckBoxStyle Apply(CheckBoxStyle baseline)
    {
        var appearance = Appearance is null
            ? baseline.Appearance
            : StyleResolution.Apply(baseline.Appearance, Appearance.Value);

        return new CheckBoxStyle(MarkStyle ?? baseline.MarkStyle, Glyphs ?? baseline.Glyphs, appearance);
    }
}

