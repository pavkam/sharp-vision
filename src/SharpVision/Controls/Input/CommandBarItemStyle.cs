// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable command-bar item presentation.</summary>
/// <remarks>
/// This leaf style resolves interactive states through <see cref="InputStyle"/> while forcing the
/// code-owned default to remain chromeless and compact. It declares no independent theme section.
/// </remarks>
[PublicAPI]
public sealed record CommandBarItemStyle: InputStyle
{
    /// <summary>Gets the command-item style definition with a one-hop InputStyle fallback.</summary>
    internal static StyleDefinition<CommandBarItemStyle> Definition { get; } =
        StyleDefinitions.ControlWithThemeOwnedStateDefaults(
            static theme => theme.GetStyleSet(InputStyle.Default),
            Complete,
            static (previous, _, current, _) =>
                previous.Padding != current.Padding || previous.AffixGap != current.AffixGap
                    ? InvalidationImpact.Measure
                    : InvalidationImpact.None);

    private static CommandBarItemStyle Complete(InputStyle input, VisualState state, Theme theme)
    {
        var states = theme.GetStyleSet(InputStyle.Default);
        return new CommandBarItemStyle(
            BarAppearance.CompleteFace(input, state, states),
            NoBorder,
            NoShadow,
            new Thickness(horizontal: 1, vertical: 0))
        {
            DropDownGlyph = input.DropDownGlyph,
            AffixGap = input.AffixGap
        };
    }

    /// <summary>Initializes a complete command-item presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="padding">The non-negative content padding in terminal cells.</param>
    [SetsRequiredMembers]
    public CommandBarItemStyle(Face face, Border border, Shadow shadow, Thickness padding)
        : base(face, border, shadow, InputStyle.Default.DropDownGlyph) => Padding = padding;

    /// <summary>Gets the padding around the caption and affixes in terminal cells.</summary>
    public required Thickness Padding { get; init; }

    /// <summary>Gets the standard chromeless command-item presentation.</summary>
    public static new CommandBarItemStyle Default =>
        Complete(InputStyle.Default, VisualState.Normal, Theme.Unthemed);
}
