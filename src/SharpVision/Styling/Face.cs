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
    /// <remarks>The access-key channel defaults to <see cref="SemanticColor.Hotkey"/>, the theme-wide
    /// mnemonic color every face carried before the channel existed.</remarks>
    public Face(
        ControlColor foreground,
        ControlColor background,
        ControlDecoration attributes,
        Underline underline,
        ControlColor underlineColor)
        : this(foreground, background, attributes, underline, underlineColor, SemanticColor.Hotkey)
    {
    }

    /// <summary>Initializes a complete face appearance with an explicit access-key color.</summary>
    /// <param name="foreground">The text and glyph foreground.</param>
    /// <param name="background">The body background.</param>
    /// <param name="attributes">The complete terminal attributes.</param>
    /// <param name="underline">The typed underline style.</param>
    /// <param name="underlineColor">The underline color.</param>
    /// <param name="accessKeyColor">The foreground of a caption's marked access-key grapheme.</param>
    /// <exception cref="ArgumentException">A paint channel is transparent or decorations conflict.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum or flag value is unknown.</exception>
    public Face(
        ControlColor foreground,
        ControlColor background,
        ControlDecoration attributes,
        Underline underline,
        ControlColor underlineColor,
        ControlColor accessKeyColor)
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
        ControlColor.ValidatePaint(accessKeyColor, nameof(accessKeyColor));

        ArgumentOutOfRangeException.ThrowIfNotDefined(underline, nameof(underline), "The underline style is unknown.");

        Foreground = foreground;
        Background = background;
        Attributes = attributes;
        Underline = underline;
        UnderlineColor = underlineColor;
        AccessKeyColor = accessKeyColor;
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
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The underline style is unknown.");
            field = value;
        }
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

    /// <summary>Gets the foreground of a caption's marked access-key grapheme - the one letter a
    /// mnemonic caption colors apart from the rest of its text.</summary>
    /// <remarks>
    /// A channel of the face rather than one theme-wide color because the letter has to read on
    /// whatever plane the caption sits on: a theme whose menu bar is light and whose buttons are
    /// dark cannot pick one color that survives both. Every state of every role section may author
    /// it (<c>face.accessKeyColor</c>), a caption inherits it from its owner's face the same way it
    /// inherits the foreground, and the default is <see cref="SemanticColor.Hotkey"/>, so a face
    /// that never mentions it behaves exactly as before the channel existed. The grapheme's
    /// attributes come from <see cref="SemanticDecoration.Hotkey"/>, not from this face.
    /// </remarks>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public ControlColor AccessKeyColor
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
