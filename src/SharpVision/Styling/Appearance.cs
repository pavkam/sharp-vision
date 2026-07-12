using System.Text;

using SharpVision.Layout;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

using TerminalStyle = SharpVision.Terminal.Rendering.Style;

namespace SharpVision.Styling;

/// <summary>Represents optional visual fields contributed by one state overlay.</summary>
/// <remarks>
/// Null means unset. A non-null terminal default color, no attributes, or zero
/// thickness is an explicit value that overrides a lower-precedence field.
/// </remarks>
public readonly record struct Appearance
{
    /// <summary>Initializes optional appearance fields.</summary>
    /// <param name="foreground">The optional terminal foreground.</param>
    /// <param name="background">The optional terminal background.</param>
    /// <param name="attributes">The optional complete text-attribute set.</param>
    /// <param name="padding">The optional appearance padding.</param>
    /// <param name="border">The optional border Rune.</param>
    /// <param name="borderColor">The optional border color.</param>
    /// <param name="underline">The optional typed underline variant.</param>
    /// <param name="underlineColor">The optional underline color.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="attributes"/> contains unknown flags.
    /// </exception>
    public Appearance(
        Color? foreground = null,
        Color? background = null,
        Attributes? attributes = null,
        Thickness? padding = null,
        Rune? border = null,
        Color? borderColor = null,
        Underline? underline = null,
        Color? underlineColor = null)
    {
        if (attributes.HasValue)
        {
            _ = new TerminalStyle(attributes: attributes.Value);
        }

        Foreground = foreground;
        Background = background;
        Attributes = attributes;
        Padding = padding;
        Border = border;
        BorderColor = borderColor;
        Underline = underline;
        UnderlineColor = underlineColor;
    }

    /// <summary>Gets the optional terminal foreground.</summary>
    public Color? Foreground { get; }

    /// <summary>Gets the optional terminal background.</summary>
    public Color? Background { get; }

    /// <summary>Gets the optional complete text-attribute set.</summary>
    public Attributes? Attributes { get; }

    /// <summary>Gets the optional appearance padding.</summary>
    public Thickness? Padding { get; }

    /// <summary>Gets the optional border Rune.</summary>
    public Rune? Border { get; }

    /// <summary>Gets the optional border color.</summary>
    public Color? BorderColor { get; }

    /// <summary>Gets the optional typed underline variant.</summary>
    public Underline? Underline { get; }

    /// <summary>Gets the optional semantic underline color.</summary>
    public Color? UnderlineColor { get; }

    /// <summary>Overlays only explicitly set fields over this appearance.</summary>
    internal Appearance Overlay(Appearance value) => new(
        value.Foreground ?? Foreground,
        value.Background ?? Background,
        value.Attributes ?? Attributes,
        value.Padding ?? Padding,
        value.Border ?? Border,
        value.BorderColor ?? BorderColor,
        value.Underline ?? Underline,
        value.UnderlineColor ?? UnderlineColor);
}
