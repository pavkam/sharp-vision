// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;


/// <summary>Describes one non-overlapping semantic style slice of parsed visible text.</summary>
internal readonly record struct StyleSpan
{
    /// <summary>Initializes one parser-produced style slice.</summary>
    /// <param name="offset">The non-negative UTF-16 offset into visible text.</param>
    /// <param name="length">The positive UTF-16 length of the slice.</param>
    /// <param name="foreground">The foreground override, or null to inherit.</param>
    /// <param name="background">The background override, or null to inherit.</param>
    /// <param name="attributes">The additive terminal attributes.</param>
    /// <param name="underline">The typed underline override, or none to inherit.</param>
    /// <param name="underlineColor">The underline color override, or null to inherit.</param>
    /// <param name="link">The semantic hyperlink target, or null.</param>
    internal StyleSpan(
        int offset,
        int length,
        Color? foreground,
        Color? background,
        TerminalAttributes attributes,
        Underline underline,
        Color? underlineColor,
        string? link)
    {
        Debug.Assert(offset >= 0, "Markup span offsets are non-negative.");
        Debug.Assert(length > 0, "Markup spans contain visible text.");
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
    internal int Offset { get; }

    /// <summary>Gets the positive UTF-16 length of the slice.</summary>
    internal int Length { get; }

    /// <summary>Gets the foreground override, or null to inherit.</summary>
    internal Color? Foreground { get; }

    /// <summary>Gets the background override, or null to inherit.</summary>
    internal Color? Background { get; }

    /// <summary>Gets the additive terminal attributes.</summary>
    internal TerminalAttributes Attributes { get; }

    /// <summary>Gets the typed underline override, or none to inherit.</summary>
    internal Underline Underline { get; }

    /// <summary>Gets the underline color override, or null to inherit.</summary>
    internal Color? UnderlineColor { get; }

    /// <summary>Gets the semantic hyperlink target, or null.</summary>
    internal string? Link { get; }
}
