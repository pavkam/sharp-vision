using SharpVision.Terminal.Protocols;

using TerminalAttributes = SharpVision.Terminal.Rendering.Attributes;
using TerminalStyle = SharpVision.Terminal.Rendering.Style;

namespace SharpVision.Controls;

/// <summary>Validates one complete optional inline-decoration proposal before mutation.</summary>
internal static class Decoration
{
    /// <summary>Validates optional attributes, underline variant, and underline color together.</summary>
    /// <param name="attributes">The optional complete rendition flag set.</param>
    /// <param name="underline">The optional typed underline variant.</param>
    /// <param name="underlineColor">The optional semantic underline color.</param>
    /// <exception cref="ArgumentException">The decoration fields conflict.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum or flag value is unknown.</exception>
    internal static void Validate(
        TerminalAttributes? attributes,
        Underline? underline,
        Color? underlineColor) => _ = new TerminalStyle(
            attributes: attributes ?? TerminalAttributes.None,
            underline: underline ?? Underline.None,
            underlineColor: underlineColor ?? Color.Default);
}
