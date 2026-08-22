// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Describes one non-overlapping semantic style slice of parsed visible text.</summary>
[PublicAPI]
public readonly record struct StyleSpan
{
    private const TerminalAttributes _knownAttributes =
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

    /// <summary>Initializes one parser-produced style slice.</summary>
    /// <param name="offset">The non-negative UTF-16 offset into visible text.</param>
    /// <param name="length">The positive UTF-16 length of the slice.</param>
    /// <param name="foreground">The foreground override, or null to inherit.</param>
    /// <param name="background">The background override, or null to inherit.</param>
    /// <param name="attributes">The additive terminal attributes.</param>
    /// <param name="underline">The typed underline override, or none to inherit.</param>
    /// <param name="underlineColor">The underline color override, or null to inherit.</param>
    /// <param name="link">The semantic hyperlink target, or null.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is negative,
    /// <paramref name="length"/> is not positive, <paramref name="attributes"/> contains an
    /// unknown bit, or <paramref name="underline"/> is undefined.</exception>
    public StyleSpan(
        int offset,
        int length,
        Color? foreground,
        Color? background,
        TerminalAttributes attributes,
        Underline underline,
        Color? underlineColor,
        string? link)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);

        if ((attributes & ~_knownAttributes) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attributes), attributes, "Unknown terminal attribute bits are not valid.");
        }

        if (!Enum.IsDefined(underline))
        {
            throw new ArgumentOutOfRangeException(nameof(underline), underline, "The underline style must be defined.");
        }

        Offset = offset;
        Length = length;
        Foreground = foreground;
        Background = background;
        Attributes = attributes;
        Underline = underline;
        UnderlineColor = underlineColor;
        Link = link;
    }

    /// <summary>Gets the zero-based UTF-16 offset into visible text.</summary>
    [NonNegativeValue]
    public int Offset { get; }

    /// <summary>Gets the positive UTF-16 length of the slice.</summary>
    [NonNegativeValue]
    public int Length { get; }

    /// <summary>Gets the foreground override, or null to inherit.</summary>
    public Color? Foreground { get; }

    /// <summary>Gets the background override, or null to inherit.</summary>
    public Color? Background { get; }

    /// <summary>Gets the additive terminal attributes.</summary>
    public TerminalAttributes Attributes { get; }

    /// <summary>Gets the typed underline override, or none to inherit.</summary>
    public Underline Underline { get; }

    /// <summary>Gets the underline color override, or null to inherit.</summary>
    public Color? UnderlineColor { get; }

    /// <summary>Gets the semantic hyperlink target, or null.</summary>
    public string? Link { get; }
}
