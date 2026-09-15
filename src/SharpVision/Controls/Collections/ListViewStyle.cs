// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines the complete ListView surface, selected-row colors, and keyboard-current cue.
/// The surface falls back to the passive projection of the theme's <c>item</c> role - the plane
/// its rows sit on - so a theme that gives rows their own plane paints the whole list with it,
/// while hover and focus stay with the rows.</summary>
[PublicAPI]
public sealed record ListViewStyle: ControlStyle
{
    /// <summary>Gets the primary ListView style definition. Falls back to the <c>item</c> role's
    /// Normal and Disabled states; the selection colors and current-row underline are code-owned.</summary>
    internal static StyleDefinition<ListViewStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetPassiveItemStyleSet(),
        Complete,
        static (previous, previousTheme, current, currentTheme) =>
            previous != current ||
            ControlBase.ResolveColor(previous.SelectedTextColor, previousTheme) !=
            ControlBase.ResolveColor(current.SelectedTextColor, currentTheme) ||
            ControlBase.ResolveColor(previous.SelectedBackground, previousTheme) !=
            ControlBase.ResolveColor(current.SelectedBackground, currentTheme)
                ? InvalidationImpact.Render
                : InvalidationImpact.None);

    private static ListViewStyle Complete(ItemStyle item, VisualState state, Theme theme) =>
        new(
            item.Face,
            item.Border,
            item.Shadow,
            SemanticColor.SelectedText,
            SemanticColor.SelectedControl,
            Underline.Straight);

    /// <summary>Initializes a complete list presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="selectedTextColor">The non-transparent selected-row foreground.</param>
    /// <param name="selectedBackground">The non-transparent selected-row background.</param>
    /// <param name="currentUnderline">The keyboard-current underline.</param>
    /// <exception cref="ArgumentException">A configured color is transparent.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="currentUnderline"/> is unknown.</exception>
    [SetsRequiredMembers]
    public ListViewStyle(
        Face face,
        Border border,
        Shadow shadow,
        ControlColor selectedTextColor,
        ControlColor selectedBackground,
        Underline currentUnderline) : base(face, border, shadow)
    {
        SelectedTextColor = selectedTextColor;
        SelectedBackground = selectedBackground;
        CurrentUnderline = currentUnderline;
    }

    /// <summary>Gets the standard list presentation.</summary>
    public static new ListViewStyle Default => Complete(ItemStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the selected-row foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor SelectedTextColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the selected-row background.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor SelectedBackground
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the underline used to distinguish the keyboard-current row from selection.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The replacement value is unknown.</exception>
    public required Underline CurrentUnderline
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The underline style is unknown.");
            field = value;
        }
    }
}
