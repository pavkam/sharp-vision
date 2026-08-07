// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines one complete control-face appearance.</summary>
public readonly record struct Face: IAppearanceFragment
{
    /// <summary>Initializes a complete face appearance.</summary>
    /// <param name="foreground">The text and glyph foreground.</param>
    /// <param name="background">The body background.</param>
    /// <param name="attributes">The complete terminal attributes.</param>
    /// <param name="underline">The typed underline style.</param>
    /// <param name="underlineColor">The underline color.</param>
    /// <exception cref="ArgumentException">A paint channel is transparent or decorations conflict.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum or flag value is unknown.</exception>
    public Face(
        ControlColor foreground,
        ControlColor background,
        ControlDecoration attributes,
        Underline underline,
        ControlColor underlineColor)
    {
        // Validated by parameter name here as well as in each init accessor. The accessor guards
        // the doors a constructor cannot see - a `with` expression and the theme overlay's
        // reflective write - but it only ever knows the value as "value", which for a constructor
        // taking several channels of the same type identifies nothing.
        //
        // The cross-member decoration check stays below the assignments: it needs three members at
        // once, so no single accessor can see them all. Validate re-runs it for the other doors.
        ControlColor.ValidatePaint(foreground, nameof(foreground));
        ControlColor.ValidatePaint(underlineColor, nameof(underlineColor));

        if (!Enum.IsDefined(underline))
        {
            throw new ArgumentOutOfRangeException(nameof(underline), underline, "The underline style is unknown.");
        }

        Foreground = foreground;
        Background = background;
        Attributes = attributes;
        Underline = underline;
        UnderlineColor = underlineColor;
        ValidateDecorations(attributes, underline, underlineColor);
    }

    /// <summary>Gets the text and glyph foreground.</summary>
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

    /// <summary>Gets the body background.</summary>
    public ControlColor Background { get; init; }

    /// <summary>Gets the complete terminal attributes.</summary>
    public ControlDecoration Attributes { get; init; }

    /// <summary>Gets the typed underline style.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is unknown.</exception>
    public Underline Underline
    {
        get;
        init => field = Enum.IsDefined(value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The underline style is unknown.");
    }

    /// <summary>Gets the underline color.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public ControlColor UnderlineColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    void IAppearanceFragment.Validate() => ValidateDecorations(Attributes, Underline, UnderlineColor);

    // The attribute/underline conflict is a relation between three members rather than a property
    // of any one of them, so it cannot be enforced where the others may not have been written yet.
    private static void ValidateDecorations(
        ControlDecoration attributes,
        Underline underline,
        ControlColor underlineColor)
    {
        if (attributes.IsLiteral && underlineColor.IsLiteral)
        {
            ((TerminalAttributes?) attributes.Literal).Validate(underline, underlineColor.Literal);
        }
    }

    IAppearanceFragment IAppearanceFragment.Clone() => this with { };
}
