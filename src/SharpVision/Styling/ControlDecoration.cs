// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Represents either concrete terminal attributes or a global theme-attribute reference.</summary>
public readonly record struct ControlDecoration
{
    private TerminalAttributes LiteralValue { get; }

    private SemanticDecoration ThemeDecorationValue { get; }

    private bool HasSemanticValue { get; }

    /// <summary>Initializes concrete terminal attributes.</summary>
    /// <param name="literal">The complete terminal attribute flags.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="literal"/> contains unknown flags.</exception>
    public ControlDecoration(TerminalAttributes literal)
    {
        ((TerminalAttributes?) literal).Validate(null, null);
        LiteralValue = literal;
        ThemeDecorationValue = default;
        HasSemanticValue = false;
    }

    /// <summary>Initializes a semantic theme-attribute reference.</summary>
    /// <param name="semanticDecoration">The known global semantic decoration.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="semanticDecoration"/> is unknown.</exception>
    public ControlDecoration(SemanticDecoration semanticDecoration)
    {
        semanticDecoration.ValidateDefined(nameof(semanticDecoration), "The theme attribute is unknown.");

        LiteralValue = default;
        ThemeDecorationValue = semanticDecoration;
        HasSemanticValue = true;
    }

    /// <summary>Gets whether this value contains concrete terminal attributes.</summary>
    public bool IsLiteral => !HasSemanticValue;

    /// <summary>Gets whether this value contains a global theme-attribute reference.</summary>
    public bool Semantic => HasSemanticValue;

    /// <summary>Gets the concrete terminal attributes.</summary>
    /// <exception cref="InvalidOperationException">This value contains a theme-attribute reference.</exception>
    public TerminalAttributes Literal => !HasSemanticValue
        ? LiteralValue
        : throw new InvalidOperationException("The attribute value contains a theme-attribute reference.");

    /// <summary>Gets the global theme-attribute reference.</summary>
    /// <exception cref="InvalidOperationException">This value contains concrete terminal attributes.</exception>
    public SemanticDecoration SemanticDecoration => HasSemanticValue
        ? ThemeDecorationValue
        : throw new InvalidOperationException("The attribute value contains concrete terminal attributes.");

    /// <summary>Converts concrete terminal attributes to an authoring value.</summary>
    public static implicit operator ControlDecoration(TerminalAttributes value) => new(value);

    /// <summary>Converts a global theme-attribute reference to an authoring value.</summary>
    public static implicit operator ControlDecoration(SemanticDecoration value) => new(value);

    /// <summary>Formats the active literal or semantic branch without reading the inactive branch.</summary>
    /// <returns>A diagnostic representation of the contained value.</returns>
    public override string ToString() => HasSemanticValue
        ? $"SemanticDecoration.{ThemeDecorationValue}"
        : LiteralValue.ToString();
}
