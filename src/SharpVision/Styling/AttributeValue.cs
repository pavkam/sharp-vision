// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Represents either concrete terminal attributes or a global theme-attribute reference.</summary>
public readonly record struct AttributeValue
{
    private TerminalAttributes LiteralValue { get; }

    private ThemeDecoration ThemeDecorationValue { get; }

    private bool ContainsThemeValue { get; }

    /// <summary>Initializes concrete terminal attributes.</summary>
    /// <param name="literal">The complete terminal attribute flags.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="literal"/> contains unknown flags.</exception>
    public AttributeValue(TerminalAttributes literal)
    {
        DecorationResolver.Validate(literal, null, null);
        LiteralValue = literal;
        ThemeDecorationValue = default;
        ContainsThemeValue = false;
    }

    /// <summary>Initializes a semantic theme-attribute reference.</summary>
    /// <param name="themeAttribute">The known global attribute role.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="themeAttribute"/> is unknown.</exception>
    public AttributeValue(ThemeDecoration themeAttribute)
    {
        if (!Enum.IsDefined(themeAttribute))
        {
            throw new ArgumentOutOfRangeException(
                nameof(themeAttribute),
                themeAttribute,
                "The theme attribute is unknown.");
        }

        LiteralValue = default;
        ThemeDecorationValue = themeAttribute;
        ContainsThemeValue = true;
    }

    /// <summary>Gets whether this value contains concrete terminal attributes.</summary>
    public bool IsLiteral => !ContainsThemeValue;

    /// <summary>Gets whether this value contains a global theme-attribute reference.</summary>
    public bool IsThemeValue => ContainsThemeValue;

    /// <summary>Gets the concrete terminal attributes.</summary>
    /// <exception cref="InvalidOperationException">This value contains a theme-attribute reference.</exception>
    public TerminalAttributes Literal => !ContainsThemeValue
        ? LiteralValue
        : throw new InvalidOperationException("The attribute value contains a theme-attribute reference.");

    /// <summary>Gets the global theme-attribute reference.</summary>
    /// <exception cref="InvalidOperationException">This value contains concrete terminal attributes.</exception>
    public ThemeDecoration ThemeDecoration => ContainsThemeValue
        ? ThemeDecorationValue
        : throw new InvalidOperationException("The attribute value contains concrete terminal attributes.");

    /// <summary>Converts concrete terminal attributes to an authoring value.</summary>
    public static implicit operator AttributeValue(TerminalAttributes value) => new(value);

    /// <summary>Converts a global theme-attribute reference to an authoring value.</summary>
    public static implicit operator AttributeValue(ThemeDecoration value) => new(value);

    /// <summary>Formats the active literal or semantic branch without reading the inactive branch.</summary>
    /// <returns>A diagnostic representation of the contained value.</returns>
    public override string ToString() => ContainsThemeValue
        ? $"ThemeDecoration.{ThemeDecorationValue}"
        : LiteralValue.ToString();
}
