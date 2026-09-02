// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable command-bar presentation.</summary>
/// <remarks>
/// This leaf style declares no theme section. It resolves passive appearance through
/// <see cref="ControlStyle"/> and keeps padding, overflow glyph, and normal overflow color
/// code-owned unless a caller assigns a complete local <see cref="CommandBar.Style"/>.
/// </remarks>
[PublicAPI]
public sealed record CommandBarStyle: ControlStyle
{
    /// <summary>Gets the command-bar style definition with a one-hop ControlStyle fallback.</summary>
    internal static StyleDefinition<CommandBarStyle> Definition { get; } = StyleDefinitions.BarControlWithThemeOwnedStateDefaults(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        static (previous, _, current, _) =>
            previous.Padding != current.Padding
                ? InvalidationImpact.Measure
                : previous.OverflowGlyph != current.OverflowGlyph ||
                  previous.OverflowColor != current.OverflowColor
                    ? InvalidationImpact.Render
                    : InvalidationImpact.None);

    private static CommandBarStyle Complete(ControlStyle control, VisualState state, Theme theme)
    {
        var states = theme.GetStyleSet(ControlStyle.Default);
        return new CommandBarStyle(
            BarAppearance.CompleteFace(control, state, states),
            control.Border,
            control.Shadow,
            default,
            ControlGlyphs.Text.Ellipsis,
            SemanticColor.ControlText);
    }

    /// <summary>Initializes a complete command-bar presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="padding">The non-negative content padding in terminal cells.</param>
    /// <param name="overflowGlyph">The printable one-cell overflow glyph and fallback.</param>
    /// <param name="overflowColor">The non-transparent normal overflow-glyph foreground.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="overflowGlyph"/> contains a control or non-one-cell glyph, or
    /// <paramref name="overflowColor"/> is transparent.
    /// </exception>
    [SetsRequiredMembers]
    public CommandBarStyle(
        Face face,
        Border border,
        Shadow shadow,
        Thickness padding,
        ControlGlyph overflowGlyph,
        ControlColor overflowColor) : base(face, border, shadow)
    {
        var validatedGlyph = new ControlGlyph(overflowGlyph.Value, overflowGlyph.Fallback);
        ControlColor.ValidatePaint(overflowColor, nameof(overflowColor));
        Padding = padding;
        OverflowGlyph = validatedGlyph;
        OverflowColor = overflowColor;
    }

    /// <summary>Gets the padding between intrinsic chrome and command faces.</summary>
    public required Thickness Padding { get; init; }

    /// <summary>Gets the preferred and portable one-cell overflow trigger glyph.</summary>
    /// <exception cref="ArgumentException">The replacement contains a control or non-one-cell glyph.</exception>
    public required ControlGlyph OverflowGlyph
    {
        get;
        init => field = new ControlGlyph(value.Value, value.Fallback);
    }

    /// <summary>Gets the normal overflow-trigger foreground. Theme-owned non-normal states use
    /// their resolved foreground; a complete local style remains authoritative in every state.</summary>
    /// <exception cref="ArgumentException">The replacement color is transparent.</exception>
    public required ControlColor OverflowColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the standard borderless, unpadded command-bar presentation.</summary>
    public static new CommandBarStyle Default =>
        Complete(ControlStyle.Default, VisualState.Normal, Theme.Unthemed);
}
