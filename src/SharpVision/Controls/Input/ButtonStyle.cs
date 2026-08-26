// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable Button presentation. This style declares no theme
/// section of its own: it falls back to <see cref="InputStyle"/>'s "input" role section for its
/// passive chrome, its own Padding is code-owned, and it is themeable only through that fallback
/// and a locally assigned <see cref="Button.Style"/>.</summary>
[PublicAPI]
public sealed record ButtonStyle: InputStyle
{
    /// <summary>Gets the primary Button-style definition. Falls back to <see cref="InputStyle"/>'s
    /// "input" role section for its passive chrome; Padding is code-owned.</summary>
    internal static StyleDefinition<ButtonStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(InputStyle.Default),
        Complete,
        static (previous, previousTheme, current, currentTheme) =>
            previous.Padding != current.Padding || previous.AffixGap != current.AffixGap
                ? InvalidationImpact.Measure
                : PressedTranslationChanged(previous, previousTheme, current, currentTheme)
                    ? InvalidationImpact.Arrange
                    : InvalidationImpact.None);

    /// <summary>Gets the non-invalidating definition used by library and external pure forwarding hosts.</summary>
    public static StyleDefinition<ButtonStyle> ForwardingDefinition { get; } = StyleDefinitions.Part(
        static theme => Definition.Resolve(null, theme),
        static (_, _, _, _) => InvalidationImpact.None);

    private static ButtonStyle Complete(InputStyle input, VisualState state, Theme theme) =>
        new(input.Face, input.Border, input.Shadow, new Thickness(horizontal: 1, vertical: 0))
        {
            // Forwarded from the fallback rather than left at the code-owned value the base
            // constructor supplies. Without this, DropDownGlyph would stay stuck at
            // InputStyle.Default's literal regardless of how a theme customizes "input"'s own
            // dropDownGlyph - a divergence Button would never surface visually, since it never
            // draws a dropdown glyph itself.
            DropDownGlyph = input.DropDownGlyph,
            AffixGap = input.AffixGap
        };

    /// <summary>Initializes a complete Button presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="padding">The non-negative internal content padding in cells.</param>
    [SetsRequiredMembers]
    public ButtonStyle(Face face, Border border, Shadow shadow, Thickness padding) : base(face, border, shadow, InputStyle.Default.DropDownGlyph) =>
        Padding = padding;

    /// <summary>Gets the internal content padding in terminal cells.</summary>
    public required Thickness Padding { get; init; }

    /// <summary>Gets the standard bordered Button presentation.</summary>
    public static ButtonStyle Standard { get; } = Complete(InputStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the standard Button presentation, aliasing <see cref="Standard"/>.</summary>
    /// <remarks>
    /// Every other style type declares its own <c>Default</c>, either as a distinct value or as an
    /// alias to its first named preset. Without one here, <c>ButtonStyle.Default</c> was still a
    /// legal expression - it resolved to the inherited <see cref="InputStyle"/> member, so it
    /// compiled and returned the base type without <c>Padding</c>, and
    /// <c>button.Style = ButtonStyle.Default</c> failed to convert while the identical line worked
    /// for every sibling control. Nothing at the use site signalled the difference.
    /// </remarks>
    public static new ButtonStyle Default => Standard;

    /// <summary>Gets the compact filled Button presentation with a fractional lower-right shadow.</summary>
    public static ButtonStyle Filled { get; } = new(
        Standard.Face,
        new Border(
            BorderSide.None,
            BorderGlyphStyle.Default,
            SemanticColor.ControlBorder,
            Color.Transparent,
            SemanticDecoration.Border),
        new Shadow(
            true,
            ShadowMode.FractionalBlock,
            new Point(1, 1),
            ControlGlyphs.Chrome.Shadow.Value,
            SemanticColor.ControlShadow,
            Color.Transparent,
            SemanticDecoration.Shadow),
        new Thickness(horizontal: 2, vertical: 0));

    [Pure]
    private static bool PressedTranslationChanged(
        ButtonStyle previous,
        Theme? previousTheme,
        ButtonStyle current,
        Theme? currentTheme)
    {
        var previousProfile = Definition.Appearance!(previous, previousTheme);
        var currentProfile = Definition.Appearance!(current, currentTheme);
        var combinations = 1 << VisualStateOrder.OrderedOverlays.Length;

        for (var flags = 0; flags < combinations; flags++)
        {
            var state = VisualState.Normal;
            for (var index = 0; index < VisualStateOrder.OrderedOverlays.Length; index++)
            {
                if ((flags & (1 << index)) != 0)
                {
                    state |= VisualStateOrder.OrderedOverlays[index];
                }
            }

            var previousShadow = previousProfile.Resolve(state).Shadow;
            var currentShadow = currentProfile.Resolve(state).Shadow;
            if (ResolvePressedTranslation(previousShadow) != ResolvePressedTranslation(currentShadow))
            {
                return true;
            }
        }

        return false;
    }

    [Pure]
    private static Point ResolvePressedTranslation(Shadow shadow) => !shadow.IsVisible
        ? default
        : shadow.Mode == ShadowMode.FractionalBlock
            ? new Point(shadow.Offset.X, 0)
            : shadow.Offset;
}
