namespace SharpVision.Controls;

using SharpVision.Terminal.Protocols;

using TerminalAttributes = Terminal.Rendering.Attributes;
using TerminalStyle = Terminal.Rendering.Style;

/// <summary>Validates one complete optional inline-decoration proposal before mutation.</summary>
internal static class Decoration
{
    /// <summary>Resolves optional decoration overlays against one inherited semantic style.</summary>
    /// <param name="inherited">The complete inherited semantic style.</param>
    /// <param name="attributes">The optional complete attribute override.</param>
    /// <param name="underline">The optional typed underline override.</param>
    /// <param name="underlineColor">The optional underline-color override.</param>
    /// <returns>The validated, conflict-free semantic decoration fields.</returns>
    internal static (TerminalAttributes Attributes, Underline Underline, Color UnderlineColor) Resolve(
        TerminalStyle inherited,
        TerminalAttributes? attributes = null,
        Underline? underline = null,
        Color? underlineColor = null)
    {
        var resolvedAttributes = attributes ?? inherited.Attributes;
        var resolvedUnderline = underline ?? inherited.Underline;

        if ((resolvedAttributes & TerminalAttributes.Underline) != 0)
        {
            resolvedUnderline = Underline.None;
        }
        else if (underline.HasValue)
        {
            resolvedAttributes &= ~TerminalAttributes.Underline;
        }

        var resolvedColor = underlineColor ?? inherited.UnderlineColor;

        if ((resolvedAttributes & TerminalAttributes.Underline) == 0 &&
            resolvedUnderline == Underline.None)
        {
            resolvedColor = Color.Default;
        }

        return (resolvedAttributes, resolvedUnderline, resolvedColor);
    }

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
