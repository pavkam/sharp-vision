// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Represents either a concrete terminal color or a global theme-color reference.</summary>
public readonly record struct ColorValue
{
    private Color LiteralValue { get; }

    private ThemeColor ThemeColorValue { get; }

    private bool ContainsThemeValue { get; }

    /// <summary>Initializes a concrete color value.</summary>
    /// <param name="literal">The concrete terminal color.</param>
    public ColorValue(Color literal)
    {
        LiteralValue = literal;
        ThemeColorValue = default;
        ContainsThemeValue = false;
    }

    /// <summary>Initializes a semantic theme-color reference.</summary>
    /// <param name="themeColor">The known global color role.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="themeColor"/> is unknown.</exception>
    public ColorValue(ThemeColor themeColor)
    {
        if (!Enum.IsDefined(themeColor))
        {
            throw new ArgumentOutOfRangeException(nameof(themeColor), themeColor, "The theme color is unknown.");
        }

        LiteralValue = default;
        ThemeColorValue = themeColor;
        ContainsThemeValue = true;
    }

    /// <summary>Gets whether this value contains a concrete terminal color.</summary>
    public bool IsLiteral => !ContainsThemeValue;

    /// <summary>Gets whether this value contains a global theme-color reference.</summary>
    public bool IsThemeValue => ContainsThemeValue;

    /// <summary>Gets the concrete terminal color.</summary>
    /// <exception cref="InvalidOperationException">This value contains a theme-color reference.</exception>
    public Color Literal => !ContainsThemeValue
        ? LiteralValue
        : throw new InvalidOperationException("The color value contains a theme-color reference.");

    /// <summary>Gets the global theme-color reference.</summary>
    /// <exception cref="InvalidOperationException">This value contains a concrete terminal color.</exception>
    public ThemeColor ThemeColor => ContainsThemeValue
        ? ThemeColorValue
        : throw new InvalidOperationException("The color value contains a concrete terminal color.");

    /// <summary>Converts a concrete terminal color to an authoring value.</summary>
    public static implicit operator ColorValue(Color value) => new(value);

    /// <summary>Converts a global theme-color reference to an authoring value.</summary>
    public static implicit operator ColorValue(ThemeColor value) => new(value);

    /// <summary>Formats the active literal or semantic branch without reading the inactive branch.</summary>
    /// <returns>A diagnostic representation of the contained value.</returns>
    public override string ToString() => ContainsThemeValue
        ? $"ThemeColor.{ThemeColorValue}"
        : LiteralValue.ToString();

    internal static void ValidatePaint(ColorValue value, string parameterName)
    {
        if (value.IsLiteral)
        {
            ((Color?) value.Literal).ValidatePaint(parameterName);
        }
    }
}
