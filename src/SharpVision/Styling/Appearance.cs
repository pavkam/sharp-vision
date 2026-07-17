// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Describes unresolved control appearance values before ThemeColor resolution.</summary>
public readonly struct Appearance
{
    /// <summary>Initializes one appearance value.</summary>
    public Appearance(
        ThemeColor? foreground,
        ThemeColor? background,
        TerminalAttributes? attributes,
        Underline? underline,
        ThemeColor? underlineColor,
        ThemeColor? borderColor,
        TerminalAttributes? borderAttributes,
        ThemeColor? shadowForeground,
        ThemeColor? shadowBackground,
        TerminalAttributes? shadowAttributes)
    {
        Foreground = foreground;
        Background = background;
        Attributes = attributes;
        Underline = underline;
        UnderlineColor = underlineColor;
        BorderColor = borderColor;
        BorderAttributes = borderAttributes;
        ShadowForeground = shadowForeground;
        ShadowBackground = shadowBackground;
        ShadowAttributes = shadowAttributes;
    }

    /// <summary>Gets the optional foreground.</summary>
    public ThemeColor? Foreground { get; }

    /// <summary>Gets the optional background.</summary>
    public ThemeColor? Background { get; }

    /// <summary>Gets the optional text attributes.</summary>
    public TerminalAttributes? Attributes { get; }

    /// <summary>Gets the optional underline.</summary>
    public Underline? Underline { get; }

    /// <summary>Gets the optional underline color.</summary>
    public ThemeColor? UnderlineColor { get; }

    /// <summary>Gets the optional border color.</summary>
    public ThemeColor? BorderColor { get; }

    /// <summary>Gets the optional border attributes.</summary>
    public TerminalAttributes? BorderAttributes { get; }

    /// <summary>Gets the optional shadow foreground.</summary>
    public ThemeColor? ShadowForeground { get; }

    /// <summary>Gets the optional shadow background.</summary>
    public ThemeColor? ShadowBackground { get; }

    /// <summary>Gets the optional shadow attributes.</summary>
    public TerminalAttributes? ShadowAttributes { get; }

    /// <summary>Gets an empty appearance.</summary>
    public static Appearance Empty { get; } = new(
        foreground: null,
        background: null,
        attributes: null,
        underline: null,
        underlineColor: null,
        borderColor: null,
        borderAttributes: null,
        shadowForeground: null,
        shadowBackground: null,
        shadowAttributes: null);

    /// <summary>Combines this value with a later appearance where non-null later values win.</summary>
    public Appearance Overlay(Appearance later) => new(
        later.Foreground ?? Foreground,
        later.Background ?? Background,
        later.Attributes ?? Attributes,
        later.Underline ?? Underline,
        later.UnderlineColor ?? UnderlineColor,
        later.BorderColor ?? BorderColor,
        later.BorderAttributes ?? BorderAttributes,
        later.ShadowForeground ?? ShadowForeground,
        later.ShadowBackground ?? ShadowBackground,
        later.ShadowAttributes ?? ShadowAttributes);
}
