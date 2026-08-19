// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>Stores one active markup facet until a matching close removes it.</summary>
internal readonly record struct OpenTag
{
    private OpenTag(
        string name,
        TerminalAttributes attributes,
        Color? foreground,
        Color? background,
        Underline? underline,
        Color? underlineColor,
        string? link)
    {
        Name = name;
        Attributes = attributes;
        Foreground = foreground;
        Background = background;
        Underline = underline;
        UnderlineColor = underlineColor;
        Link = link;
    }

    /// <summary>Gets the case-preserving tag name used by named closes.</summary>
    public string Name { get; }

    /// <summary>Gets the attribute contribution.</summary>
    public TerminalAttributes Attributes { get; }

    /// <summary>Gets the foreground contribution.</summary>
    public Color? Foreground { get; }

    /// <summary>Gets the background contribution.</summary>
    public Color? Background { get; }

    /// <summary>Gets the underline contribution.</summary>
    public Underline? Underline { get; }

    /// <summary>Gets the underline color contribution.</summary>
    public Color? UnderlineColor { get; }

    /// <summary>Gets the semantic hyperlink contribution.</summary>
    public string? Link { get; }

    /// <summary>Creates one attribute facet.</summary>
    /// <param name="name">The source tag name.</param>
    /// <param name="value">The contributed flag.</param>
    /// <returns>The active facet.</returns>
    [Pure]
    public static OpenTag Attribute(ReadOnlySpan<char> name, TerminalAttributes value) =>
        new(name.ToString(), value, null, null, null, null, null);

    /// <summary>Creates one foreground facet.</summary>
    /// <param name="name">The source tag name.</param>
    /// <param name="value">The contributed color.</param>
    /// <returns>The active facet.</returns>
    [Pure]
    public static OpenTag ForegroundTag(ReadOnlySpan<char> name, Color value) =>
        new(name.ToString(), TerminalAttributes.None, value, null, null, null, null);

    /// <summary>Creates one background facet.</summary>
    /// <param name="name">The source tag name.</param>
    /// <param name="value">The contributed color.</param>
    /// <returns>The active facet.</returns>
    [Pure]
    public static OpenTag BackgroundTag(ReadOnlySpan<char> name, Color value) =>
        new(name.ToString(), TerminalAttributes.None, null, value, null, null, null);

    /// <summary>Creates one typed underline facet.</summary>
    /// <param name="name">The source tag name.</param>
    /// <param name="value">The contributed shape.</param>
    /// <returns>The active facet.</returns>
    [Pure]
    public static OpenTag UnderlineTag(ReadOnlySpan<char> name, Underline value) =>
        new(name.ToString(), TerminalAttributes.None, null, null, value, null, null);

    /// <summary>Creates one underline color facet.</summary>
    /// <param name="name">The source tag name.</param>
    /// <param name="value">The contributed color.</param>
    /// <returns>The active facet.</returns>
    [Pure]
    public static OpenTag UnderlineColorTag(ReadOnlySpan<char> name, Color value) =>
        new(name.ToString(), TerminalAttributes.None, null, null, null, value, null);

    /// <summary>Creates one semantic hyperlink facet.</summary>
    /// <param name="name">The source tag name.</param>
    /// <param name="value">The validated target.</param>
    /// <returns>The active facet.</returns>
    [Pure]
    public static OpenTag LinkTag(ReadOnlySpan<char> name, string value) =>
        new(name.ToString(), TerminalAttributes.None, null, null, null, null, value);
}
