// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable navigation-divider presentation. This style declares
/// no theme section of its own: it falls back to <see cref="ControlStyle"/>'s "control" role
/// section for its passive chrome, resolves its own divider glyph from a code-owned default, and
/// is themeable only through that fallback and a locally assigned
/// <see cref="NavigationViewSeparator.Style"/>.</summary>
[PublicAPI]
public sealed record NavigationViewSeparatorStyle: ControlStyle
{
    /// <summary>Gets the primary navigation-separator style definition.</summary>
    internal static StyleDefinition<NavigationViewSeparatorStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        static (previous, _, current, _) =>
            previous != current ? InvalidationImpact.Render : InvalidationImpact.None);

    private static NavigationViewSeparatorStyle Complete(ControlStyle control, VisualState state, Theme theme) =>
        new(control.Face, control.Border, control.Shadow, ControlGlyphs.Navigation.Separator.Value);

    /// <summary>Initializes a complete navigation-divider presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="glyph">The printable one-cell divider glyph.</param>
    /// <exception cref="ArgumentException"><paramref name="glyph"/> is a control or is not one cell wide.</exception>
    [SetsRequiredMembers]
    public NavigationViewSeparatorStyle(Face face, Border border, Shadow shadow, Rune glyph)
        : base(face, border, shadow) =>
        Glyph = glyph;

    /// <summary>Gets the standard navigation-divider presentation.</summary>
    public static new NavigationViewSeparatorStyle Default => Complete(ControlStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the divider glyph.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune Glyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }
}
