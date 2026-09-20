// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable tree view presentation, including the synthetic
/// loading and failed status rows an unloaded item's asynchronous child request may present, and
/// the selected-row colors and keyboard-current underline every <see cref="TreeViewItem"/> row
/// resolves through <see cref="TreeView.ActualStyle"/>. This style declares no theme section of
/// its own: it falls back to <see cref="ContainerStyle"/>'s "container" role section for its
/// passive tree-surface chrome, resolves its own status and selection colors and glyphs from
/// semantic colors, and is themeable only through that fallback and a locally assigned
/// <see cref="TreeView.Style"/>. A row's own appearance is resolved separately, against the
/// theme's "item" row role rather than this style's "container" fallback - see
/// <see cref="TreeViewItem"/>.</summary>
[PublicAPI]
public sealed record TreeViewStyle: ContainerStyle
{
    /// <summary>Gets the primary tree view style definition. Falls back through
    /// <see cref="Theme.GetFocusableContainerStyleSet"/> rather than the bare "container" role
    /// section so a directly focused TreeView gets a visible border-color cue instead of none at
    /// all - TreeView is a focus target in its own right, unlike a merely passive panel. The
    /// status colors, glyphs, disclosure glyphs, selection colors, and current-row underline are
    /// all code-owned.</summary>
    internal static StyleDefinition<TreeViewStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetFocusableContainerStyleSet(),
        Complete,
        static (previous, previousTheme, current, currentTheme) =>
            previous != current ||
            ControlBase.ResolveColor(previous.LoadingColor, previousTheme) != ControlBase.ResolveColor(current.LoadingColor, currentTheme) ||
            ControlBase.ResolveColor(previous.FailedColor, previousTheme) != ControlBase.ResolveColor(current.FailedColor, currentTheme) ||
            ControlBase.ResolveColor(previous.SelectedTextColor, previousTheme) != ControlBase.ResolveColor(current.SelectedTextColor, currentTheme) ||
            ControlBase.ResolveColor(previous.SelectedBackground, previousTheme) != ControlBase.ResolveColor(current.SelectedBackground, currentTheme)
                ? InvalidationImpact.Render
                : InvalidationImpact.None);

    private static TreeViewStyle Complete(ContainerStyle container, VisualState state, Theme theme) =>
        new(
            container.Face,
            container.Border,
            container.Shadow,
            SemanticColor.Muted,
            SemanticColor.Error,
            ControlGlyphs.Status.Loading.Value,
            ControlGlyphs.Status.Failed.Value,
            ControlGlyphs.Disclosure.Collapsed.Value,
            ControlGlyphs.Disclosure.Expanded.Value,
            SemanticColor.SelectedText,
            SemanticColor.SelectedControl,
            Underline.Straight);

    /// <summary>Initializes a complete tree view presentation.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="loadingColor">The non-transparent loading-row foreground.</param>
    /// <param name="failedColor">The non-transparent failed-row foreground.</param>
    /// <param name="loadingGlyph">The printable one-cell loading-row indicator.</param>
    /// <param name="failedGlyph">The printable one-cell failed-row indicator.</param>
    /// <param name="collapsedGlyph">The printable one-cell collapsed-item disclosure indicator.</param>
    /// <param name="expandedGlyph">The printable one-cell expanded-item disclosure indicator.</param>
    /// <param name="selectedTextColor">The non-transparent selected-row foreground.</param>
    /// <param name="selectedBackground">The non-transparent selected-row background.</param>
    /// <param name="currentUnderline">The keyboard-current underline.</param>
    /// <exception cref="ArgumentException">A configured color is transparent, or a glyph is a control or is not one cell wide.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="currentUnderline"/> is unknown.</exception>
    [SetsRequiredMembers]
    public TreeViewStyle(
        Face face,
        Border border,
        Shadow shadow,
        ControlColor loadingColor,
        ControlColor failedColor,
        Rune loadingGlyph,
        Rune failedGlyph,
        Rune collapsedGlyph,
        Rune expandedGlyph,
        ControlColor selectedTextColor,
        ControlColor selectedBackground,
        Underline currentUnderline) : base(face, border, shadow)
    {
        LoadingColor = loadingColor;
        FailedColor = failedColor;
        LoadingGlyph = loadingGlyph;
        FailedGlyph = failedGlyph;
        CollapsedGlyph = collapsedGlyph;
        ExpandedGlyph = expandedGlyph;
        SelectedTextColor = selectedTextColor;
        SelectedBackground = selectedBackground;
        CurrentUnderline = currentUnderline;
    }

    /// <summary>Gets the standard tree view presentation.</summary>
    public static new TreeViewStyle Default => Complete(ContainerStyle.Default, VisualState.Normal, Theme.Unthemed);

    /// <summary>Gets the loading-row foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor LoadingColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the failed-row foreground.</summary>
    /// <exception cref="ArgumentException">The replacement value is transparent.</exception>
    public required ControlColor FailedColor
    {
        get;
        init
        {
            ControlColor.ValidatePaint(value, nameof(value));
            field = value;
        }
    }

    /// <summary>Gets the indicator drawn while a child request is in flight.</summary>
    /// <remarks>
    /// The glyph participates in layout: it is part of the measured status row, so replacing it
    /// with a different width moves the status text along with the drawn glyph rather than letting
    /// the two drift.
    /// </remarks>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune LoadingGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the indicator drawn after a child request fails.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune FailedGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the indicator drawn beside a collapsed item that has children.</summary>
    /// <remarks>
    /// Unlike <see cref="LoadingGlyph"/>, this glyph's reserved column is a fixed one-cell layout
    /// constant rather than measured text, so replacing it changes only the drawn glyph, never the
    /// row's width.
    /// </remarks>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune CollapsedGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the indicator drawn beside an expanded item that has children.</summary>
    /// <remarks>
    /// Unlike <see cref="LoadingGlyph"/>, this glyph's reserved column is a fixed one-cell layout
    /// constant rather than measured text, so replacing it changes only the drawn glyph, never the
    /// row's width.
    /// </remarks>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public required Rune ExpandedGlyph
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

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
