// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>
/// Represents immutable semantic cell colors, attributes, and hyperlink.
/// </summary>
[DebuggerDisplay("{ToString()}")]
[PublicAPI]
public readonly record struct CellStyle
{
    private const TerminalAttributes _allAttributes =
        TerminalAttributes.Bold |
        TerminalAttributes.Dim |
        TerminalAttributes.Italic |
        TerminalAttributes.Underline |
        TerminalAttributes.Blink |
        TerminalAttributes.Reverse |
        TerminalAttributes.Hidden |
        TerminalAttributes.Strike |
        TerminalAttributes.RapidBlink |
        TerminalAttributes.Overline;

    /// <summary>Initializes a validated semantic style.</summary>
    /// <param name="foreground">The terminal foreground color.</param>
    /// <param name="background">The terminal background color.</param>
    /// <param name="attributes">The semantic rendition flags.</param>
    /// <param name="hyperlink">An optional immutable hyperlink target.</param>
    /// <param name="underline">An optional typed underline variant.</param>
    /// <param name="underlineColor">The semantic underline color.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="attributes"/> contains unknown flags.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Rendition fields conflict, underline color has no underline,
    /// <paramref name="hyperlink"/> is empty or contains a control code unit, or
    /// <paramref name="foreground"/>, <paramref name="background"/>, or <paramref name="underlineColor"/>
    /// uses transparency in a non-background channel.
    /// </exception>
    public CellStyle(
        Color foreground = default,
        Color background = default,
        TerminalAttributes attributes = TerminalAttributes.None,
        string? hyperlink = null,
        Underline underline = Underline.None,
        Color underlineColor = default)
    {
        ValidateColor(foreground, allowTransparent: false, nameof(foreground));
        ValidateColor(background, allowTransparent: true, nameof(background));
        ValidateColor(underlineColor, allowTransparent: false, nameof(underlineColor));

        // Deliberately NOT ArgumentOutOfRangeException.ThrowIfUndefinedFlags here: a CellStyle is
        // constructed for every rendered cell, and the generic helper's Convert.ToUInt64 boxes
        // both operands on every call. The direct bitwise check below costs nothing since
        // TerminalAttributes' operators never box.
        if ((attributes & ~_allAttributes) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attributes), attributes, "The style contains an unknown attribute flag.");
        }

        ArgumentOutOfRangeException.ThrowIfNotDefined(underline, nameof(underline), "The underline style is unknown.");

        if ((attributes & TerminalAttributes.Underline) != 0 && underline != Underline.None)
        {
            throw new ArgumentException(
                "Legacy straight underline and a typed underline variant cannot both be selected.",
                nameof(underline));
        }

        if ((attributes & (TerminalAttributes.Blink | TerminalAttributes.RapidBlink)) ==
            (TerminalAttributes.Blink | TerminalAttributes.RapidBlink))
        {
            throw new ArgumentException(
                "Slow and rapid blink cannot both be selected.",
                nameof(attributes));
        }

        if (underlineColor != Color.Default &&
            underline == Underline.None &&
            (attributes & TerminalAttributes.Underline) == 0)
        {
            throw new ArgumentException(
                "Underline color requires a straight or typed underline.",
                nameof(underlineColor));
        }

        if (hyperlink is not null)
        {
            if (hyperlink.Length == 0)
            {
                throw new ArgumentException("A hyperlink cannot be empty.", nameof(hyperlink));
            }

            foreach (var value in hyperlink)
            {
                if (char.IsControl(value))
                {
                    throw new ArgumentException(
                        "A hyperlink cannot contain control code units.",
                        nameof(hyperlink));
                }
            }
        }

        Foreground = foreground;
        Background = background;
        Attributes = attributes;
        Hyperlink = hyperlink;
        Underline = underline;
        UnderlineColor = underlineColor;
    }

    /// <summary>Gets the default terminal style.</summary>
    public static CellStyle Default => default;

    /// <summary>Gets the foreground color.</summary>
    public Color Foreground { get; }

    /// <summary>Gets the background color.</summary>
    public Color Background { get; }

    /// <summary>Gets the rendition attributes.</summary>
    public TerminalAttributes Attributes { get; }

    /// <summary>Gets the optional immutable hyperlink target.</summary>
    public string? Hyperlink { get; }

    /// <summary>Gets the typed underline variant, or none when legacy attributes own it.</summary>
    public Underline Underline { get; }

    /// <summary>Gets the semantic underline color.</summary>
    public Color UnderlineColor { get; }

    /// <summary>Returns a copy of this style with the specified foreground color.</summary>
    /// <param name="foreground">The replacement foreground color.</param>
    [Pure]
    public CellStyle WithForeground(Color foreground) => new(
        foreground,
        Background,
        Attributes,
        Hyperlink,
        Underline,
        UnderlineColor);

    /// <inheritdoc/>
    public override string ToString() => $"CellStyle {{ fg={Foreground}, bg={Background} }}";

    private static void ValidateColor(Color color, bool allowTransparent, string parameterName)
    {
        if (color.IsTransparent && !allowTransparent)
        {
            throw new ArgumentException(
                "A terminal cell style requires a resolved color; transparency is valid only for its background.",
                parameterName);
        }

    }
}
