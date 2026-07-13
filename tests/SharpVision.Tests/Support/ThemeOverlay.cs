namespace SharpVision.Tests.Support;

using SharpVision.Layout;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

/// <summary>Represents optional visual fields for one themed overlay layer in tests.</summary>
internal readonly record struct ThemeOverlay
{
    /// <summary>Initializes one optional overlay field set.</summary>
    /// <param name="foreground">The optional foreground color.</param>
    /// <param name="background">The optional background color.</param>
    /// <param name="attributes">The optional rendition attributes.</param>
    /// <param name="underline">The optional typed underline variant.</param>
    /// <param name="underlineColor">The optional underline color.</param>
    /// <param name="padding">The optional internal padding.</param>
    /// <param name="borderColor">The optional border color.</param>
    internal ThemeOverlay(
        Color? foreground = null,
        Color? background = null,
        Attributes? attributes = null,
        Underline? underline = null,
        Color? underlineColor = null,
        Thickness? padding = null,
        Color? borderColor = null)
    {
        Foreground = foreground;
        Background = background;
        Attributes = attributes;
        Underline = underline;
        UnderlineColor = underlineColor;
        Padding = padding;
        BorderColor = borderColor;
    }

    /// <summary>Gets the optional foreground color.</summary>
    internal Color? Foreground { get; }

    /// <summary>Gets the optional background color.</summary>
    internal Color? Background { get; }

    /// <summary>Gets the optional rendition attributes.</summary>
    internal Attributes? Attributes { get; }

    /// <summary>Gets the optional typed underline variant.</summary>
    internal Underline? Underline { get; }

    /// <summary>Gets the optional underline color.</summary>
    internal Color? UnderlineColor { get; }

    /// <summary>Gets the optional internal padding.</summary>
    internal Thickness? Padding { get; }

    /// <summary>Gets the optional border color.</summary>
    internal Color? BorderColor { get; }
}
