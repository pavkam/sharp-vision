// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;


/// <summary>Represents the flattened active markup facets at one visible offset.</summary>
internal readonly record struct Style
{
    private const TerminalAttributes _blinkAttributes = TerminalAttributes.Blink | TerminalAttributes.RapidBlink;

    private Style(
        TerminalAttributes attributes,
        ThemeColor? foreground,
        ThemeColor? background,
        Underline underline,
        ThemeColor? underlineColor,
        string? link)
    {
        Attributes = attributes;
        Foreground = foreground;
        Background = background;
        Underline = underline;
        UnderlineColor = underlineColor;
        Link = link;
    }

    /// <summary>Gets the empty inherited style.</summary>
    internal static Style Inherit { get; } = new(
        TerminalAttributes.None,
        null,
        null,
        Underline.None,
        null,
        null);

    private TerminalAttributes Attributes { get; }

    private ThemeColor? Foreground { get; }

    private ThemeColor? Background { get; }

    private Underline Underline { get; }

    private ThemeColor? UnderlineColor { get; }

    private string? Link { get; }

    /// <summary>Flattens open facets using last-opened precedence for single-valued groups.</summary>
    /// <param name="open">The ordered active facet list.</param>
    /// <returns>The complete semantic delta at the current visible offset.</returns>
    internal static Style From(IReadOnlyList<OpenTag> open)
    {
        var attributes = TerminalAttributes.None;
        ThemeColor? foreground = null;
        ThemeColor? background = null;
        var underline = Underline.None;
        ThemeColor? underlineColor = null;
        string? link = null;

        foreach (var tag in open)
        {
            if ((tag.Attributes & _blinkAttributes) != 0)
            {
                attributes &= ~_blinkAttributes;
            }

            attributes |= tag.Attributes;
            foreground = tag.Foreground ?? foreground;
            background = tag.Background ?? background;
            underline = tag.Underline ?? underline;
            underlineColor = tag.UnderlineColor ?? underlineColor;
            link = tag.Link ?? link;
        }

        return new Style(attributes, foreground, background, underline, underlineColor, link);
    }

    /// <summary>Projects the active semantic delta onto one visible slice.</summary>
    /// <param name="offset">The non-negative visible UTF-16 offset.</param>
    /// <param name="length">The positive visible UTF-16 length.</param>
    /// <returns>The immutable style slice.</returns>
    internal StyleSpan ToSpan(int offset, int length) =>
        new(offset, length, Foreground, Background, Attributes, Underline, UnderlineColor, Link);
}
