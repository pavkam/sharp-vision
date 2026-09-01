// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines the complete immutable appearance of a breadcrumb trail, including the
/// separator drawn between adjacent visible entries.</summary>
[PublicAPI]
public sealed record BreadcrumbStyle: ControlStyle
{
    /// <summary>Gets the breadcrumb style definition.</summary>
    internal static StyleDefinition<BreadcrumbStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        static (previous, previousTheme, current, currentTheme) =>
            previous != current ||
            ControlBase.ResolveColor(previous.SeparatorColor, previousTheme) !=
            ControlBase.ResolveColor(current.SeparatorColor, currentTheme)
                ? InvalidationImpact.Render
                : InvalidationImpact.None);

    private static BreadcrumbStyle Complete(ControlStyle control, VisualState state, Theme theme) =>
        new(
            control.Face,
            control.Border,
            control.Shadow,
            ControlGlyphs.Navigation.ItemCurrent,
            SemanticColor.ControlBorder);

    /// <summary>Initializes a complete breadcrumb presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="separatorGlyph">The preferred one-cell separator and its one-cell fallback.</param>
    /// <param name="separatorColor">The paintable separator foreground.</param>
    /// <exception cref="ArgumentException">A glyph is invalid or the separator color is not paintable.</exception>
    [SetsRequiredMembers]
    public BreadcrumbStyle(
        Face face,
        Border border,
        Shadow shadow,
        ControlGlyph separatorGlyph,
        ControlColor separatorColor) : base(face, border, shadow)
    {
        SeparatorGlyph = separatorGlyph;
        SeparatorColor = separatorColor;
    }

    /// <summary>Gets the standard breadcrumb presentation.</summary>
    public static new BreadcrumbStyle Default => Complete(ControlStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the preferred separator and the portable fallback used when the preferred
    /// glyph is not one terminal cell under the active width policy.</summary>
    /// <exception cref="ArgumentException">The replacement contains an invalid glyph.</exception>
    public required ControlGlyph SeparatorGlyph
    {
        get;
        init
        {
            _ = new ControlGlyph(value.Value, value.Fallback);
            field = value;
        }
    }

    /// <summary>Gets the separator foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is not paintable.</exception>
    public required ControlColor SeparatorColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }
}
