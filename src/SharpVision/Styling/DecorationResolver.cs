// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Validates one complete optional inline-decoration proposal before mutation.</summary>
[PublicAPI]
public static class DecorationResolver
{
    private const TerminalAttributes _blinkAttributes =
        TerminalAttributes.Blink | TerminalAttributes.RapidBlink;

    /// <summary>Merges one inline-markup decoration span over a complete inherited semantic
    /// style.</summary>
    /// <param name="inherited">The complete inherited semantic style the span layers over.</param>
    /// <param name="attributes">The span's own additive terminal attributes.</param>
    /// <param name="underline">The span's typed underline override, or <see
    /// cref="Underline.None"/> to inherit the underline from <paramref name="inherited"/>.</param>
    /// <param name="foreground">The span's foreground override, already resolved to a literal
    /// color, or null to inherit.</param>
    /// <param name="background">The span's background override, already resolved to a literal
    /// color, or null to inherit.</param>
    /// <param name="underlineColor">The span's underline-color override, already resolved to a
    /// literal color, or null to inherit.</param>
    /// <param name="link">The span's hyperlink target override, or null to inherit.</param>
    /// <returns>The complete style produced by layering the span over <paramref
    /// name="inherited"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="underline"/> is undefined, or
    /// the merged attributes contain an unknown flag.</exception>
    /// <exception cref="ArgumentException">The merged decoration fields conflict, or <paramref
    /// name="link"/> is empty or contains a control code unit.</exception>
    [Pure]
    public static TerminalStyle Merge(
        TerminalStyle inherited,
        TerminalAttributes attributes,
        Underline underline,
        Color? foreground,
        Color? background,
        Color? underlineColor,
        string? link)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(underline, nameof(underline), "The underline style is unknown.");

        var mergedAttributes = inherited.Attributes;

        // Blink and rapid blink are mutually exclusive in a cell style, so a span asking for
        // either must clear both inherited bits before contributing its own: otherwise a span
        // that only wants rapid blink could leave a stale inherited slow-blink bit set, which the
        // final style construction below would then reject as a conflicting pair.
        if ((attributes & _blinkAttributes) != 0)
        {
            mergedAttributes &= ~_blinkAttributes;
        }

        mergedAttributes |= attributes;
        Underline? typedUnderline = null;

        // A typed underline variant always supersedes the legacy straight-underline attribute:
        // clear the legacy bit whenever the span carries a typed override, so the two never both
        // reach the cell style at once.
        if (underline != Underline.None)
        {
            mergedAttributes &= ~TerminalAttributes.Underline;
            typedUnderline = underline;
        }

        var (resolvedAttributes, resolvedUnderline, resolvedUnderlineColor) = Resolve(
            inherited,
            mergedAttributes,
            typedUnderline,
            underlineColor);

        return new TerminalStyle(
            foreground ?? inherited.Foreground,
            background ?? inherited.Background,
            resolvedAttributes,
            link ?? inherited.Hyperlink,
            resolvedUnderline,
            resolvedUnderlineColor);
    }

    /// <summary>Resolves optional decoration overlays against one inherited semantic style.</summary>
    /// <param name="inherited">The complete inherited semantic style.</param>
    /// <param name="attributes">The optional complete attribute override.</param>
    /// <param name="underline">The optional typed underline override.</param>
    /// <param name="underlineColor">The optional underline-color override.</param>
    /// <returns>The validated, conflict-free semantic decoration fields.</returns>
    [Pure]
    public static (TerminalAttributes Attributes, Underline Underline, Color UnderlineColor) Resolve(
        TerminalStyle inherited,
        TerminalAttributes? attributes = null,
        Underline? underline = null,
        Color? underlineColor = null)
    {
        var resolvedAttributes = attributes ?? inherited.Attributes;
        var resolvedUnderline = underline ?? inherited.Underline;

        if (underline.HasValue)
        {
            resolvedAttributes &= ~TerminalAttributes.Underline;
        }
        else if ((resolvedAttributes & TerminalAttributes.Underline) != 0)
        {
            resolvedUnderline = Underline.None;
        }

        var resolvedColor = underlineColor ?? inherited.UnderlineColor;

        if ((resolvedAttributes & TerminalAttributes.Underline) == 0 &&
            resolvedUnderline == Underline.None)
        {
            resolvedColor = Color.Default;
        }

        return (resolvedAttributes, resolvedUnderline, resolvedColor);
    }

    extension(TerminalAttributes? attributes)
    {
        /// <summary>Validates optional attributes, underline variant, and underline color together.</summary>
        /// <param name="underline">The optional typed underline variant.</param>
        /// <param name="underlineColor">The optional semantic underline color.</param>
        /// <exception cref="ArgumentException">The decoration fields conflict.</exception>
        /// <exception cref="ArgumentOutOfRangeException">An enum or flag value is unknown.</exception>
        public void Validate(
            Underline? underline,
            Color? underlineColor) => _ = new TerminalStyle(
            attributes: attributes ?? TerminalAttributes.None,
            underline: underline ?? Underline.None,
            underlineColor: underlineColor ?? Color.Default);
    }
}
