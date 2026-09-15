// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines the well-known push-button appearance - the face, chrome, and padding of a
/// <c>Button</c> and of every dialog action button that forwards to it - one of the sibling styles
/// <see cref="ControlStyle"/> generalizes and the owner of the theme's <c>styles.button</c>
/// section.</summary>
/// <remarks>
/// A button cascades from <see cref="InputStyle"/>'s <c>input</c> section exactly the way
/// <c>input</c> cascades from <c>control</c>: an unauthored <c>button</c> section resolves to the
/// input chrome plus this type's own code-owned padding, and every input interaction state, so a
/// theme that never mentions it looks exactly as it did when a button was a leaf falling back to
/// <c>input</c>. Authoring it lets a theme separate a command from a field - Turbo Vision's
/// black-on-green buttons with a black block shadow beside its bordered blue input lines - and
/// author <see cref="Padding"/> under <c>normal</c> like any other structural member.
/// </remarks>
[PublicAPI]
public sealed record ButtonStyle: InputStyle
{
    /// <summary>Gets the primary Button-style definition: the <c>button</c> section itself, so a
    /// locally assigned <see cref="Controls.Input.Button.Style"/> still borrows that section's
    /// per-state deltas while a complete local style stays authoritative in every state.</summary>
    internal static StyleDefinition<ButtonStyle> Definition { get; } = StyleDefinitions.ControlWithThemeOwnedStateDefaults(
        static theme => theme.GetStyleSet(Default),
        static (button, _, _) => button,
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

    /// <summary>Gets the standard bordered Button presentation: the input family's own default
    /// chrome with one cell of horizontal padding, so an unauthored <c>button</c> section diffs to
    /// nothing beyond that padding and resolves exactly as <c>input</c> does.</summary>
    public static ButtonStyle Standard { get; } = new(
        InputStyle.Default.Face,
        InputStyle.Default.Border,
        InputStyle.Default.Shadow,
        new Thickness(horizontal: 1, vertical: 0));

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
        return ResolvePressedTranslation(previousProfile.Normal.Shadow) !=
               ResolvePressedTranslation(currentProfile.Normal.Shadow) ||
               ShadowGeometryChanged(previousProfile.IsPointerOver.Shadow, currentProfile.IsPointerOver.Shadow) ||
               ShadowGeometryChanged(previousProfile.FocusWithin.Shadow, currentProfile.FocusWithin.Shadow) ||
               ShadowGeometryChanged(previousProfile.Focused.Shadow, currentProfile.Focused.Shadow) ||
               ShadowGeometryChanged(previousProfile.Current.Shadow, currentProfile.Current.Shadow) ||
               ShadowGeometryChanged(previousProfile.Selected.Shadow, currentProfile.Selected.Shadow) ||
               ShadowGeometryChanged(previousProfile.Checked.Shadow, currentProfile.Checked.Shadow) ||
               ShadowGeometryChanged(previousProfile.Indeterminate.Shadow, currentProfile.Indeterminate.Shadow) ||
               ShadowGeometryChanged(previousProfile.Pressed.Shadow, currentProfile.Pressed.Shadow) ||
               ShadowGeometryChanged(previousProfile.Disabled.Shadow, currentProfile.Disabled.Shadow);
    }

    [Pure]
    private static bool ShadowGeometryChanged(ShadowOverlay? previous, ShadowOverlay? current) =>
        previous?.IsVisible != current?.IsVisible ||
        previous?.Mode != current?.Mode ||
        previous?.Offset != current?.Offset;

    [Pure]
    private static Point ResolvePressedTranslation(Shadow shadow) => !shadow.IsVisible
        ? default
        : shadow.Mode == ShadowMode.FractionalBlock
            ? new Point(shadow.Offset.X, 0)
            : shadow.Offset;
}
