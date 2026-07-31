// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

/// <summary>Defines one complete immutable checkbox presentation.</summary>
[PublicAPI]
public readonly struct CheckBoxStyle: IEquatable<CheckBoxStyle>
{
    private static readonly ThemeProfile _standardAppearance = ControlStyleProfiles.Selection;
    private readonly CheckBoxMarkStyle? _markStyle;
    private readonly CheckBoxGlyphs? _glyphs;
    private readonly ThemeProfile? _appearance;

    /// <summary>Initializes a complete checkbox presentation.</summary>
    /// <param name="markStyle">The validated mark-layout family.</param>
    /// <param name="glyphs">The complete unchecked, checked, and indeterminate glyph family.</param>
    /// <param name="appearance">The complete normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="markStyle"/> is undefined.</exception>
    /// <exception cref="ArgumentException">A glyph is a control or is not one cell wide.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="appearance"/> is <see langword="null"/>.</exception>
    public CheckBoxStyle(CheckBoxMarkStyle markStyle, CheckBoxGlyphs glyphs, ThemeProfile appearance)
    {
        EnumValidation.ValidateDefined(markStyle);
        var validatedGlyphs = new CheckBoxGlyphs(glyphs.Unchecked, glyphs.Checked, glyphs.Indeterminate);
        ArgumentNullException.ThrowIfNull(appearance);

        _markStyle = markStyle;
        _glyphs = validatedGlyphs;
        _appearance = appearance;
    }

    /// <summary>Gets the standard three-cell bracket presentation.</summary>
    public static CheckBoxStyle Default => default;

    /// <summary>Gets the three-cell bracket presentation.</summary>
    public static CheckBoxStyle Brackets { get; } = new(
        CheckBoxMarkStyle.Brackets,
        new CheckBoxGlyphs(new Rune(' '), new Rune('✓'), new Rune('─')),
        _standardAppearance);

    /// <summary>Gets the one-cell circle, tick, and indeterminate presentation.</summary>
    public static CheckBoxStyle Tick { get; } = new(
        CheckBoxMarkStyle.Tick,
        new CheckBoxGlyphs(new Rune('○'), new Rune('✓'), new Rune('−')),
        _standardAppearance);

    /// <summary>Gets the one-cell square-state presentation.</summary>
    public static CheckBoxStyle Square { get; } = new(
        CheckBoxMarkStyle.Square,
        CheckBoxGlyphs.Default,
        _standardAppearance);

    /// <summary>Gets the mark-layout family.</summary>
    public CheckBoxMarkStyle MarkStyle => _markStyle ?? CheckBoxMarkStyle.Brackets;

    /// <summary>Gets the complete state glyph family.</summary>
    public CheckBoxGlyphs Glyphs => _glyphs ?? Brackets.Glyphs;

    /// <summary>Gets the horizontal terminal-cell reservation for the mark.</summary>
    public int MarkWidth => MarkStyle == CheckBoxMarkStyle.Brackets ? 3 : 1;

    /// <summary>Gets the complete normal and visual-state appearance profile.</summary>
    public ThemeProfile Appearance => ResolveAppearance();

    /// <summary>Determines whether this value and another style resolve to the same presentation.</summary>
    /// <param name="other">The other style to compare.</param>
    /// <returns><see langword="true"/> when all resolved presentation members are equal.</returns>
    public bool Equals(CheckBoxStyle other) =>
        MarkStyle == other.MarkStyle && Glyphs == other.Glyphs && Appearance.Equals(other.Appearance);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CheckBoxStyle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(MarkStyle, Glyphs, Appearance);

    /// <summary>Determines whether two checkbox styles resolve to the same presentation.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve equally.</returns>
    public static bool operator ==(CheckBoxStyle left, CheckBoxStyle right) => left.Equals(right);

    /// <summary>Determines whether two checkbox styles resolve to different presentations.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve differently.</returns>
    public static bool operator !=(CheckBoxStyle left, CheckBoxStyle right) => !left.Equals(right);

    private ThemeProfile ResolveAppearance() => _appearance ?? _standardAppearance;
}
