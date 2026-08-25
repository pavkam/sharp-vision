// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Represents either a concrete terminal color or a global theme-color reference.</summary>
public readonly record struct ControlColor
{
    private Color LiteralValue { get; }

    private SemanticColor SemanticValue { get; }

    private bool HasSemanticValue { get; }

    /// <summary>Initializes a concrete color value.</summary>
    /// <param name="literal">The concrete terminal color.</param>
    public ControlColor(Color literal)
    {
        LiteralValue = literal;
        SemanticValue = default;
        HasSemanticValue = false;
    }

    /// <summary>Initializes a semantic theme-color reference.</summary>
    /// <param name="semanticColor">The known global semantic color.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="semanticColor"/> is unknown.</exception>
    public ControlColor(SemanticColor semanticColor)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(semanticColor, nameof(semanticColor), "The theme color is unknown.");

        LiteralValue = default;
        SemanticValue = semanticColor;
        HasSemanticValue = true;
    }

    /// <summary>Gets whether this value contains a concrete terminal color.</summary>
    public bool IsLiteral => !HasSemanticValue;

    /// <summary>Gets whether this value contains a global theme-color reference.</summary>
    public bool IsSemantic => HasSemanticValue;

    /// <summary>Gets the concrete terminal color.</summary>
    /// <exception cref="InvalidOperationException">This value contains a theme-color reference.</exception>
    public Color Literal => !HasSemanticValue
        ? LiteralValue
        : throw new InvalidOperationException("The color value contains a theme-color reference.");

    /// <summary>Gets the global theme-color reference.</summary>
    /// <exception cref="InvalidOperationException">This value contains a concrete terminal color.</exception>
    public SemanticColor SemanticColor => HasSemanticValue
        ? SemanticValue
        : throw new InvalidOperationException("The color value contains a concrete terminal color.");

    /// <summary>Resolves this authoring value to the color a control paints under one Theme.</summary>
    /// <param name="theme">The active Theme, or null when no semantic color table is available.</param>
    /// <returns>The literal color, the Theme-resolved semantic color, or <see cref="Color.Default"/>.</returns>
    [Pure]
    public Color Resolve(Theme? theme) => IsLiteral
        ? Literal
        : theme?.ResolveColor(SemanticColor) ?? Color.Default;

    /// <summary>Converts a concrete terminal color to an authoring value.</summary>
    public static implicit operator ControlColor(Color value) => new(value);

    /// <summary>Converts a global theme-color reference to an authoring value.</summary>
    public static implicit operator ControlColor(SemanticColor value) => new(value);

    /// <summary>Formats the active literal or semantic branch without reading the inactive branch.</summary>
    /// <returns>A diagnostic representation of the contained value.</returns>
    public override string ToString() => HasSemanticValue
        ? $"SemanticColor.{SemanticValue}"
        : LiteralValue.ToString();

    internal static void ValidatePaint(ControlColor value, string paramName)
    {
        if (value.IsLiteral)
        {
            ArgumentException.ThrowIfTransparent(value.Literal, paramName);
        }
    }
}
