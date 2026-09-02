// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable command-bar separator presentation.</summary>
/// <remarks>This leaf style falls back to <see cref="ControlStyle"/> and declares no theme section.</remarks>
[PublicAPI]
public sealed record CommandBarSeparatorStyle: ControlStyle
{
    /// <summary>Gets the separator style definition with a one-hop ControlStyle fallback.</summary>
    internal static StyleDefinition<CommandBarSeparatorStyle> Definition { get; } = StyleDefinitions.ControlWithThemeOwnedStateDefaults(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        static (previous, _, current, _) =>
            previous != current ? InvalidationImpact.Render : InvalidationImpact.None);

    private static CommandBarSeparatorStyle Complete(ControlStyle control, VisualState state, Theme theme)
    {
        var states = theme.GetStyleSet(ControlStyle.Default);
        return new CommandBarSeparatorStyle(
            BarAppearance.CompleteFace(control, state, states),
            control.Border,
            control.Shadow,
            ControlGlyphs.Separators.Vertical);
    }

    /// <summary>Initializes a complete command-bar separator presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="glyph">The printable one-cell divider glyph and fallback.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="glyph"/> contains a control or non-one-cell glyph.
    /// </exception>
    [SetsRequiredMembers]
    public CommandBarSeparatorStyle(Face face, Border border, Shadow shadow, ControlGlyph glyph)
        : base(face, border, shadow) => Glyph = new ControlGlyph(glyph.Value, glyph.Fallback);

    /// <summary>Gets the preferred and portable vertical divider glyph.</summary>
    /// <exception cref="ArgumentException">The replacement contains a control or non-one-cell glyph.</exception>
    public required ControlGlyph Glyph
    {
        get;
        init => field = new ControlGlyph(value.Value, value.Fallback);
    }

    /// <summary>Gets the standard passive vertical-divider presentation.</summary>
    public static new CommandBarSeparatorStyle Default =>
        Complete(ControlStyle.Default, VisualState.Normal, Theme.Unthemed);
}
